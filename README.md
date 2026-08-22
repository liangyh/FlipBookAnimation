# KingdomTD Flipbook

`KingdomTD Flipbook` 是一个 Unity UPM 包，用同一张序列帧网格纹理驱动场景中的 Quad 和 UGUI 动画。它负责 UV 切换、播放控制、帧事件与完成事件，适用于角色特效、UI 动效和轻量级序列帧表现。

## 环境要求

- Unity `6000.0` 或更高版本
- `com.unity.ugui` 2.0.0
- Universal Render Pipeline 17.3.0

依赖会由 Unity Package Manager 自动解析。

## 安装

在目标 Unity 项目中打开 **Window > Package Manager**，点击左上角 **+**：

1. 本地开发选择 **Add package from disk...**，选中本仓库的 `package.json`；或通过 **Add package from git URL...** 填入此包的 Git 地址。
2. 安装完成后，确认 Package Manager 中出现 **KingdomTD Flipbook**。

## 创建动画资源

1. 准备网格序列帧纹理：帧从左到右、从上到下排列。
2. 在 Project 面板右键，选择 **Create > KingdomTD > Flipbook Animation**。
3. 设置以下字段：
   - **Main Texture**：序列帧纹理。
   - **Columns / Rows**：纹理的列数和行数。
   - **Default Animation Name**：未指定名称时播放的动画。
   - **Clips**：为每个动作设置名称、起始帧、帧数、帧率、速度和可选帧事件。
4. 用 Shader `KingdomTD/Flipbook/World` 新建一个材质，并赋给 **World Material**；将主纹理设置为 `_MainTex`。资源在 Inspector 中会自动同步列数、行数与纹理。

> 例如：4 列 × 2 行的纹理共有 8 帧；`Run` 动画可配置为 `Start Frame = 0`、`Frame Count = 8`、`Frame Rate = 12`。

## 在场景中使用

在 GameObject 上添加 **KingdomTD > Flipbook Renderer**，并在 **Animation Asset** 中引用创建的资源。组件会自动管理 `MeshFilter`、`MeshRenderer` 和 Quad，无需手动创建网格。

也可以把一个 `FlipbookAnimationAsset` 从 Project 面板拖到 Hierarchy 或 Scene View，在菜单中选择 **Flipbook Renderer** 自动创建对象。场景组件默认使用缩放时间，并可在 Inspector 设置循环、随机起始帧、面向相机和事件回调。

```csharp
using UnityEngine;
using KingdomTD.Flipbook;

public class PlayEffect : MonoBehaviour
{
    [SerializeField] private FlipbookRenderer flipbook;

    private void Start()
    {
        flipbook.Play("Run", loop: true, speed: 1f);
        flipbook.SetChangeColor(Color.red);
        flipbook.SetChangeRate(0.5f);
    }
}
```

使用 `Pause()`、`Resume()`、`Stop()` 控制播放；`SetTimeMode(FlipbookTimeMode.Unscaled)` 可改用非缩放时间。`SetEffectChangeRate()` 可混合资源材质中的可选 **Effect Texture**。

## 在 UGUI 中使用

在 Canvas 下添加 **UI (Canvas) > Flipbook Graphic**，将动画资源赋给 **Animation Asset**。它继承 `MaskableGraphic`，可直接使用颜色、Mask、RectMask2D 与 Raycast，不需要 `Image` 或 `RawImage`。

将资源拖到 Canvas 或 Hierarchy 时，也可在弹出菜单中选择 **Flipbook Graphic (UI)** 自动创建。UI 默认使用非缩放时间；调用 `SetNativeSize()` 可将 `RectTransform` 设为单帧像素尺寸。

```csharp
[SerializeField] private FlipbookGraphic uiFlipbook;

void OnEnable()
{
    uiFlipbook.Play("Idle");
}
```

## 注意事项

- 每个 Clip 名称必须唯一，帧范围不能超出 `Columns × Rows`。
- 场景渲染要求 **World Material**、`_MainTex` 与动画资源的主纹理一致。
- 单独运行时切换资源时，先由上层加载 `FlipbookAnimationAsset`，再调用 `SetAsset()`；本包不绑定 Addressables、Resources 或特定加载方案。
- 修改运行时接口、资源字段或工作流后，请同步更新 [详细使用说明](Documentation~/README.md)。
