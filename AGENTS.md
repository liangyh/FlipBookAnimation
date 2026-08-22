# Repository Guidelines

## Project Structure & Module Organization

This repository is a Unity Package Manager (UPM) package: `com.kingdomtd.flipbook`.

- `Runtime/Core/` contains animation data and playback logic.
- `Runtime/World/` provides scene rendering components; `Runtime/UI/` contains UGUI components.
- `Runtime/Shaders/` holds the shared world shader.
- `Editor/` contains inspectors, asset-creation menus, drag-and-drop support, and package icons.
- `Documentation~/README.md` documents package behavior and public usage. Keep it aligned with API changes.

Runtime code must not reference `UnityEditor`. Put editor-only code in `Editor/` so the asmdefs remain platform-safe. Commit Unity `.meta` files with every added, moved, or deleted Unity asset.

## Build, Test, and Development Commands

The package has no standalone build script or committed test suite. Validate it from a Unity 6000.0+ host project that references this package:

```sh
Unity -batchmode -quit -projectPath /path/to/host-project -logFile -
```

Use Unity’s Package Manager to link/install the package during local development, then check compilation in the Console. Run automated tests, when present, with the Unity Test Framework:

```sh
Unity -batchmode -quit -projectPath /path/to/host-project -runTests -testPlatform EditMode
```

## Coding Style & Naming Conventions

Use four-space indentation, braces on a new line, and one type per file. Follow existing C# naming: PascalCase for public types, methods, properties, and events; camelCase for parameters and locals; `_camelCase` for private serialized or instance fields. Keep namespaces `KingdomTD.Flipbook` and `KingdomTD.Flipbook.Editor` consistent with their assemblies. Prefer explicit Unity types and guard invalid assets or inspector input early.

## Testing Guidelines

Add Unity Test Framework tests in a dedicated `Tests/Editor/` or `Tests/Runtime/` assembly when changing playback timing, frame events, asset validation, or editor workflows. Name test methods by outcome, for example `Play_WhenClipEndsWithoutLoop_RaisesCompleted`. Test both normal and invalid asset data; manually verify inspectors, drag-and-drop creation, world rendering, and UGUI rendering in a sample host project.

## Commit & Pull Request Guidelines

The repository history uses concise conventional-style commits, e.g. `feat: 初始化Flipbook动画包`. Use prefixes such as `feat:`, `fix:`, `docs:`, and `refactor:` followed by a focused description. Keep commits scoped to one change. Pull requests should explain user-visible behavior, list validation performed, link relevant issues, and include screenshots or a short recording for inspector, shader, or UI changes.
