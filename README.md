# KingdomTD Flipbook

`KingdomTD Flipbook` 是一个 Unity UPM 包，用同一张序列帧网格纹理驱动场景中的 Quad 和 UGUI 动画。它负责 UV 切换、播放控制、帧事件与完成事件，适用于角色特效、UI 动效和轻量级序列帧表现。

[下载中文功能说明书（PDF）](Documentation~/KingdomTD-Flipbook-Manual.pdf) · [在线阅读与编辑源稿](Documentation~/功能说明书.md)

## 环境要求

- Unity `6000.0` 或更高版本
- `com.unity.ugui` 2.0.0
- Universal Render Pipeline 17.3.0

依赖会由 Unity Package Manager 自动解析。

## 安装

### 通过 Git 标签安装（推荐）

在目标 Unity 项目中打开 **Window > Package Manager**，点击左上角 **+**，选择 **Add package from git URL...**，填入：

```text
https://github.com/liangyh/FlipBookAnimation.git#v1.1.0
```

也可以直接编辑项目的 `Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.kingdomtd.flipbook": "https://github.com/liangyh/FlipBookAnimation.git#v1.1.0"
  }
}
```

安装后，确认 Package Manager 中出现 **KingdomTD Flipbook**。升级时把 URL 末尾的标签改成目标版本（例如从 `#v1.0.4` 改成 `#v1.1.0`）。Git 依赖不会自动发现新标签。

## 创建动画资源

1. 准备网格序列帧纹理：帧从左到右、从上到下排列。
2. 在 Project 面板右键，选择 **Create > KingdomTD > Flipbook Animation**。
3. 设置以下字段：
   - **Main Texture**：序列帧纹理。
   - **Columns / Rows**：纹理的列数和行数。
   - **Default Animation Name**：未指定名称时播放的动画。
   - **Clips**：为每个动作设置名称、起始帧、帧数、帧率、速度和可选帧事件。
   - **Directions**（可选）：多方向动画的方向表，在 Inspector 的 **朝向** 区域选择 4/8 方向模板，部分方向可镜像其他方向的帧；**动画** 区域按方向名称设置各方向的起始帧并显示首帧缩略图。
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

配置了方向表的资源，只需设置朝向再播放，组件会按角度选择最接近的方向，播放中切换方向不会重播动画：

```csharp
flipbook.SetFacingWorld(velocity);      // 地面上的世界方向，按相机朝向换算
flipbook.Play("Walk", forceRestart: false);
```

使用 `Pause()`、`Resume()`、`Stop()` 控制播放；`SetTimeMode(FlipbookTimeMode.Unscaled)` 可改用非缩放时间。`SetEffectChangeRate()` 可混合资源材质中的可选 **Effect Texture**。

先在 URP Renderer 资产中添加 **Flipbook Ground Shadow Renderer Feature**，再在角色预制体的 **Flipbook Renderer** 上勾选 **Ground Shadow Enabled**，当前帧的轮廓就会以可调浓淡的纯色投到水平地面。默认地面为世界 `Y = 0`，影子朝屏幕右上方。给场景对象添加 **KingdomTD > Flipbook Ground Shadow Settings** 可统一调整颜色、浓淡（Alpha）、方向、长度和地面高度。

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

## 导入示例

在 Package Manager 中选择 **KingdomTD Flipbook**，展开 **Samples** 后导入 **Flipbook Basic**。打开 `FlipbookBasic.unity` 进入 Play 模式，即可同时查看 `FlipbookRenderer` 和 `FlipbookGraphic`；样例资源独立于本地开发沙盒，可安全复制到业务项目。

同一个示例还包含 `FlipbookGroundShadowTest.unity`。Play 模式中可以定格查看单角色的阴影轮廓，也可以调整同屏数量并对照阴影开关前后的帧耗时。[阴影测试场景用法](Samples~/01-Flipbook-Basic/README.md#地面阴影测试)

## 注意事项

- 每个 Clip 名称必须唯一，帧范围不能超出 `Columns × Rows`。
- 场景渲染要求 **World Material**、`_MainTex` 与动画资源的主纹理一致。
- 单独运行时切换资源时，先由上层加载 `FlipbookAnimationAsset`，再调用 `SetAsset()`；本包不绑定 Addressables、Resources 或特定加载方案。
- 修改运行时接口、资源字段或工作流后，请同步更新 [详细使用说明](Documentation~/README.md)。
