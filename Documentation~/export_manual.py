"""Export or verify the versioned Chinese user manual."""

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import tempfile
from html import escape
from pathlib import Path
from urllib.parse import urljoin

import markdown
from markdown.treeprocessors import Treeprocessor
from pypdf import PdfReader, PdfWriter


DOCS = Path(__file__).resolve().parent
ROOT = DOCS.parent
SOURCE = DOCS / "功能说明书.md"
OUTPUT = DOCS / "KingdomTD-Flipbook-Manual.pdf"
STYLES = DOCS / "manual-print.css"
DIGEST_KEY = "/FlipbookManualSourceSHA256"


def source_digest():
    files = [ROOT / "package.json", SOURCE, STYLES, Path(__file__).resolve()]
    files.extend(sorted(path for path in (DOCS / "images").rglob("*")
                        if path.is_file() and not path.name.startswith(".")))
    digest = hashlib.sha256()
    for path in files:
        # Treat equivalent text checkouts with LF and CRLF as the same source.
        if path.suffix in {".md", ".json", ".css", ".py"}:
            data = path.read_text(encoding="utf-8").encode("utf-8")
        else:
            data = path.read_bytes()
        digest.update(path.relative_to(ROOT).as_posix().encode("utf-8") + b"\0")
        digest.update(hashlib.sha256(data).digest())
    return digest.hexdigest()


def find_chrome():
    windows = Path(os.environ.get("PROGRAMFILES", "C:/Program Files"))
    candidates = [
        os.environ.get("CHROME_BIN"),
        "google-chrome", "google-chrome-stable", "chromium", "chromium-browser",
        "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
        "/Applications/Chromium.app/Contents/MacOS/Chromium",
        str(windows / "Google/Chrome/Application/chrome.exe"),
    ]
    for candidate in candidates:
        if candidate and (executable := shutil.which(candidate)):
            return executable
    raise SystemExit("找不到 Chrome 或 Chromium。请安装浏览器，或用 CHROME_BIN 指定可执行文件。")


def heading_id(title, _separator):
    # Keep Chinese headings distinct in HTML, PDF bookmarks and the contents links.
    return "s-" + title.encode("utf-8").hex()


class ImageSources(Treeprocessor):
    def run(self, root):
        # Only images need filesystem URLs. Fragment links must stay inside the PDF.
        for image in root.iter("img"):
            image.set("src", urljoin(DOCS.as_uri() + "/", image.get("src")))


def render_html(package, page_numbers=None):
    renderer = markdown.Markdown(
        extensions=["tables", "fenced_code", "toc"],
        extension_configs={"toc": {"slugify": heading_id}},
        output_format="html",
    )
    renderer.treeprocessors.register(ImageSources(renderer), "manual-images", 4)
    fragment = renderer.convert(SOURCE.read_text(encoding="utf-8"))
    cover, first_section, chapters = fragment.partition("<h2 ")
    title_end = cover.index("</h1>") + len("</h1>")
    title, introduction = cover[:title_end], cover[title_end:]
    links = []
    for entry in renderer.toc_tokens[0]["children"]:
        if entry["level"] == 2:
            anchor = entry["id"]
            number = "" if page_numbers is None else page_numbers[anchor]
            links.append(f'<a href="#{anchor}">{escape(entry["name"])}'
                         f'<b aria-hidden="true"></b><span>{number}</span></a>')
    dependencies = package["dependencies"]
    version = escape(package["version"])
    project_name = escape(package["displayName"])
    package_id = escape(package["name"])
    repo_url = package["repository"]["url"].removeprefix("git+")
    install_url = escape(f'{repo_url}#v{package["version"]}')
    css = STYLES.read_text(encoding="utf-8")
    css = css.replace("{{product}}", json.dumps(package["displayName"], ensure_ascii=False))
    css = css.replace("{{release}}", json.dumps(f"功能说明书   /   v{version}", ensure_ascii=False))
    return f"""<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8">
<title>{project_name} 功能说明书 · v{version}</title><style>{css}</style></head>
<body><header class="cover">
  <div class="identity"><span>{package_id}</span><b>v{version}</b></div>
  {title}
  <p class="environment">Unity {escape(package["unity"])}+　 /　 UGUI {escape(dependencies["com.unity.ugui"])}　 /　 URP {escape(dependencies["com.unity.render-pipelines.universal"])}</p>
  {introduction}
  <p class="install">本版 Git 安装地址<br><code>{install_url}</code></p>
  <nav aria-label="目录"><h3>目录</h3><div>{"".join(links)}</div></nav>
</header><main>{first_section}{chapters}</main></body></html>"""


def print_html(chrome, work, html, filename):
    source = work / "manual.html"
    target = work / filename
    source.write_text(html, encoding="utf-8")
    command = [chrome, "--headless", "--disable-gpu", "--disable-extensions",
               "--disable-background-networking", "--no-first-run", "--no-default-browser-check",
               f"--user-data-dir={work / 'profile'}", "--no-pdf-header-footer",
               "--export-tagged-pdf", "--generate-pdf-document-outline",
               f"--print-to-pdf={target}", source.as_uri()]
    result = subprocess.run(command, capture_output=True, text=True, timeout=90)
    if result.returncode or not target.is_file():
        raise SystemExit(result.stderr.strip() or "浏览器未能生成 PDF。")
    return PdfReader(target)


def chapter_pages(reader):
    return {str(name).lstrip("/"): reader.get_destination_page_number(destination) + 1
            for name, destination in reader.named_destinations.items()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=OUTPUT, help="PDF path (default: beside the source)")
    parser.add_argument("--check", action="store_true", help="verify the existing PDF without starting Chrome")
    args = parser.parse_args()
    output = args.output.resolve()
    if output.suffix.lower() != ".pdf":
        parser.error("--output 应指向 .pdf 文件")
    digest = source_digest()
    if args.check:
        if not output.is_file() or (PdfReader(output).metadata or {}).get(DIGEST_KEY) != digest:
            raise SystemExit("PDF 缺失或与当前源稿、包清单或排版不一致。请重新运行 export_manual.py。")
        print(f"说明书与当前版本一致  {output}")
        return

    package = json.loads((ROOT / "package.json").read_text(encoding="utf-8"))
    chrome = find_chrome()
    # The PDF and final destination share a filesystem, so replacement is atomic.
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".flipbook-manual-", dir=output.parent) as folder:
        work = Path(folder)
        layout = print_html(chrome, work, render_html(package), "layout.pdf")
        pages = chapter_pages(layout)
        document = print_html(chrome, work, render_html(package, pages), "numbered.pdf")
        if chapter_pages(document) != pages or package["version"] not in document.pages[0].extract_text():
            raise SystemExit("PDF 的目录页码或包版本不匹配。")
        writer = PdfWriter(clone_from=document)
        writer.add_metadata({DIGEST_KEY: digest})
        final = work / "final.pdf"
        writer.write(final)
        if digest != source_digest():
            raise SystemExit("导出期间输入文件有修改，请重新导出。")
        final.replace(output)
    print(f"{output}  (v{package['version']}, {len(document.pages)} 页)")


if __name__ == "__main__":
    main()
