# Flipbook Basic

打开 `Scenes/FlipbookBasic.unity` 并进入 Play 模式。场景同时包含 `World Flipbook`（`FlipbookRenderer`）和 Canvas 下的 `Flipbook Graphic`（`FlipbookGraphic`）。两者共用 `Character/OldMan/Huofa_Flipbook.asset`，World 组件额外使用同目录的 `Huofa_Flipbook.mat`。

在 Inspector 中切换默认动画名称，或调用 `Play("Run")`、`Pause()`、`Resume()` 与 `SetNativeSize()` 验证两种渲染工作流。

## 地面阴影测试

先确认项目的 URP Renderer 资产已添加 **Flipbook Ground Shadow Renderer Feature**，否则看不到影子。然后打开 `Scenes/FlipbookGroundShadowTest.unity` 并进入 Play 模式。默认用近景显示一个正在播放 `Skill01` 的角色。

- 点 **Pause** 定格当前帧，查看阴影轮廓和脚底贴合。点 **1** 从压测返回近景。
- 在 **Characters** 输入数量并点 **Apply**。也可以直接点 **1,000**、**2,000**，或在 Inspector 编辑 **Ground Shadow Test → Character Count**。相机会调整取景，把角色网格纳入画面。
- 拖动 **Shadow Opacity** 调整影子浓淡（默认 50%），对照网格地面的透出效果；多个角色叠站时，重叠处不会加深。
- 用 **Ground shadows** 在相同数量和相机位置下对照阴影开关。面板每半秒更新 FPS 和平均帧耗时，CPU 主线程、GPU 与绘制次数使用 Unity 提供的最近一次数据。缺少计时或计数器时显示 `—`。
