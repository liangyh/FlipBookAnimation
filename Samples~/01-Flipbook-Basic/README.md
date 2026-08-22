# Flipbook Basic

打开 `Scenes/FlipbookBasic.unity` 并进入 Play 模式。场景同时包含 `World Flipbook`（`FlipbookRenderer`）和 Canvas 下的 `Flipbook Graphic`（`FlipbookGraphic`）；两者共用 `Animations/Huofa_Flipbook.asset`，World 组件额外使用 `Materials/Huofa_Flipbook.mat`。

在 Inspector 中切换默认动画名称，或调用 `Play("Run")`、`Pause()`、`Resume()` 与 `SetNativeSize()` 验证两种渲染工作流。
