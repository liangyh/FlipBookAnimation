# KingdomTD Flipbook

`KingdomTD Flipbook` 是一个独立 UPM Package。它用同一份网格纹理和动作数据驱动场景 Quad 与 UGUI，项目业务代码不需要关心 UV、帧推进和帧事件。

项目级详细交付文档见 `UnityProject/CCMd/KingdomTDFlipbook使用手册.md`，包含资源规范、完整 API、业务接入、性能建议、故障排查和验收清单。

## 资产

通过 `Assets/Create/KingdomTD/Flipbook Animation` 创建 `FlipbookAnimationAsset`：

- `Main Texture`：原始序列帧网格纹理，不生成低清副本。
- `Columns`、`Rows`：纹理网格的列数和行数，帧顺序为从左到右、从上到下。
- `Default Animation Name`：调用 `Play()` 时没有传动作名所使用的动作。
- `Clips`：动作名、起始帧、帧数、帧率、动作速度和帧事件。
- `World Material`：场景渲染共享材质，使用 `KingdomTD/Flipbook/World` Shader。
- `Effect Texture`：场景表现的可选扩展纹理；默认 UGUI 渲染不采样它。

Inspector 会校验动作重名、越界帧、无效帧率和事件帧，并提供动作预览。

## 拖放生成对象

从 Project 将单个 `FlipbookAnimationAsset` 拖到 Hierarchy 或 Scene View，会弹出与 Spine 类似的生成菜单：

- `Flipbook Renderer`：生成场景对象并自动添加 `FlipbookRenderer`，底层 `MeshFilter` 和 `MeshRenderer` 由组件管理。
- `Flipbook Graphic (UI)`：生成带有 `RectTransform`、`CanvasRenderer` 和 `FlipbookGraphic` 的 UI 对象，并使用单帧像素尺寸初始化大小。

拖到 Hierarchy 时会保留目标父节点和插入顺序；拖到 Scene View 时场景对象生成在鼠标位置，UI 对象优先放到当前选中的 `RectTransform` 下。没有可用 Canvas 父节点时仍会创建 UI 对象，但需要之后手工将它放入 Canvas。创建操作支持 Undo，并会自动选中新对象。

## 场景渲染

在 GameObject 上只需添加 `FlipbookRenderer`。它会自动添加并管理底层 `MeshFilter` 和 `MeshRenderer`，使用共享 Quad、共享材质和 `MaterialPropertyBlock` 切换帧，因此不需要手工配置底层渲染组件，也不会为每个实例创建 Mesh 或 Material。

主要运行时接口：

```csharp
renderer.SetAsset(asset);
renderer.Play("Run", loop: true, speed: 1f);
renderer.Play("Run", loop: true, speed: 1f, forceRestart: false);
renderer.Pause();
renderer.Resume();
renderer.Stop();
renderer.SetTimeMode(FlipbookTimeMode.Scaled);
```

场景组件支持 Billboard、随机循环起始帧、颜色替换参数、帧事件和播放完成事件。
`forceRestart` 默认为 `true`，保持每次调用 `Play` 都重新播放的兼容行为；传入 `false` 时，若同一动画仍在播放，则保留当前进度，仅更新循环状态和速度并解除暂停。动画已经停止、播放完成或发生切换时仍会重新播放。
组件 Inspector 的 `Animation` 下拉框会列出资产中的全部动画；编辑态切换时保存默认动画并显示首帧。Play Mode 下拉框只更新当前播放状态，不修改序列化配置或触发资源重建；打开和关闭 Unity 原生下拉菜单时产生的 EditorLoop 停顿不属于运行时开销。

## UGUI 渲染

在 Canvas 下直接添加 `FlipbookGraphic`，不需要同时添加 `Image` 或 `RawImage`。它继承 `MaskableGraphic`，自己生成四顶点网格和当前帧 UV，可直接使用 `Mask`、`RectMask2D`、颜色、Raycast 和 `SetNativeSize`。

通常直接在 Inspector 的 `Animation Asset` 字段引用 `FlipbookAnimationAsset`。下面的 `SetAsset` 只用于确实需要在运行时切换资产的场景：

```csharp
graphic.SetAsset(asset);
graphic.Play("Idle");
graphic.SetTimeMode(FlipbookTimeMode.Unscaled);
```

UI 默认使用 `Unscaled`，场景默认使用 `Scaled`；两个组件都可在 Inspector 或运行时独立切换。相同纹理、材质和裁剪状态的 Graphic 可以由 Canvas 合批。
`FlipbookGraphic` Inspector 同样提供 `Animation` 下拉框，并与场景组件使用一致的切换规则。

## 资源加载边界

Package 不依赖 Addressables、Resources 或项目业务程序集，也不提供专用 Loader。场景或 Prefab 可以直接序列化引用 `FlipbookAnimationAsset`；Addressable Prefab 会自动携带并加载该依赖。

只有运行时单独切换资产时，上层应用才需要加载 `FlipbookAnimationAsset`、调用 `SetAsset`，并自行管理对应加载句柄。

## 材质制作

1. 创建使用 `KingdomTD/Flipbook/World` Shader 的 Material。
2. 将资产的 `Main Texture` 同时设置到 Material 的 `_MainTex`。
3. 需要效果纹理时设置 `_EffectTex`，并通过 `SetEffectChangeRate` 控制混合。
4. 需要角色染色时通过 `SetChangeColor` 和 `SetChangeRate` 设置实例参数。
