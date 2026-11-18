# KingdomTD Flipbook

`KingdomTD Flipbook` 是一个独立 UPM Package。它用同一份网格纹理和动作数据驱动场景 Quad 与 UGUI，项目业务代码不需要关心 UV、帧推进和帧事件。

项目级详细交付文档见 `UnityProject/CCMd/KingdomTDFlipbook使用手册.md`，包含资源规范、完整 API、业务接入、性能建议、故障排查和验收清单。

## 资产

通过 `Assets/Create/KingdomTD/Flipbook Animation` 创建 `FlipbookAnimationAsset`：

- `Main Texture`：原始序列帧网格纹理，不生成低清副本。
- `Columns`、`Rows`：纹理网格的列数和行数，帧顺序为从左到右、从上到下。
- `Default Animation Name`：调用 `Play()` 时没有传动作名所使用的动作。
- `Clips`：动作名、起始帧、帧数、帧率、动作速度、分方向起始帧和帧事件。
- `Directions`：可选的方向表；为空表示单方向（旧资产保持原行为）。
- `World Material`：场景渲染共享材质，使用 `KingdomTD/Flipbook/World` Shader。
- `Effect Texture`：场景表现的可选扩展纹理；默认 UGUI 渲染不采样它。

资产 Inspector 分为 **纹理与网格**、**朝向**、**动画** 三个区域，所有问题汇总显示在顶部；底部预览区可切换动画与方向。Inspector 会校验动作重名、越界帧、无效帧率和事件帧。

## 多方向动画

一个动作（如 `Walk`）可以有多个朝向。业务代码只需告诉组件朝向，再调用 `Play`，组件会选出角度最接近的方向。

### 方向表

`Directions` 中每一项包含：

| 字段 | 说明 |
|---|---|
| `Name` | 显示名，例如 `E`、`NE`。 |
| `Angle` | 屏幕空间角度：0 = 右，90 = 上（远离相机），180 = 左，270 = 下，逆时针递增。 |
| `SourceDirection` | `-1` 表示使用本方向自己的帧；`>= 0` 表示复用该索引方向的帧（目标必须使用自己的帧，不支持链式借用）。 |
| `FlipX` | 采样时水平翻转。 |

在资产 Inspector 的 **朝向** 区域配置：

1. **方向模板**：选择 `单方向 / 左右 / 4 方向 / 8 方向`。8 方向模板只需美术绘制 `E、NE、N、S、SE` 五个方向，`NW、W、SW` 分别镜像 `NE、E、SE`。切换模板时同名方向已配置的起始帧会保留；修改后的方向表显示为“自定义”。
2. **罗盘**：直观显示各方向的位置。实心圆点为自己绘制的方向，空心圆点为镜像/复用方向，虚线指向它使用的帧来源；点击圆点可在预览中查看该方向。
3. **方向表**：每行编辑名称、角度和 **帧来源**（`自己绘制`、`镜像 X`、`复用 X（不翻转）`）。若美术画的是左朝向，把左侧方向改为“自己绘制”、右侧改为“镜像”即可。增删方向或修改帧来源时，各动画的起始帧会自动跟随调整。

### 分方向帧

同一动作的所有方向共享帧数、帧率、速度与帧事件。多方向资产在 **动画** 区域中按方向名称列出每个“自己绘制”方向的起始帧，并显示该方向首帧缩略图和帧范围（越界时标红），以及哪些方向镜像/复用了它。

- **紧密排列**：每个方向紧接上一个方向的最后一帧。
- **每方向一行**：每个方向从纹理的新一行开始。

底层数据存放在 Clip 的 `DirectionStartFrames`（按自己绘制的方向依次排列，为空或缺项时使用 `StartFrame`），由 Inspector 自动维护。

### 运行时接口

```csharp
// 地面（XZ 平面）上的世界方向，例如移动速度；会按主相机的水平朝向换算为屏幕角度
renderer.SetFacingWorld(agent.velocity);
renderer.Play("Walk", forceRestart: false);

// 直接给屏幕空间方向或角度（2D XY 平面游戏可直接传速度）
renderer.SetFacing(new Vector2(velocity.x, velocity.y));
renderer.SetFacingAngle(135f);
renderer.Play("Attack", facingAngle: 135f, loop: false);

int direction = renderer.CurrentDirectionIndex;
bool flipped = renderer.IsFlippedX;
```

- 播放中切换方向只替换起始帧和翻转状态，保留当前帧进度，不会重复触发帧事件或完成事件。
- 零向量会被忽略并保持当前方向。
- 组件的 `Initial Facing Angle`（默认 270°，朝下）用于尚未设置朝向时；`Direction Hysteresis`（默认 5°）避免在方向边界来回抖动。
- 翻转由 Shader 的实例属性 `_FlipX`（场景）或顶点 UV（UGUI）完成，不修改 Transform，不影响合批。
- `FlipbookGraphic` 提供同样的 `SetFacingAngle`、`SetFacing`、`Play(name, facingAngle, ...)` 与 `SetDirection` 接口。

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

### 纯色地面阴影

在角色预制体的 `FlipbookRenderer` 上勾选 **Ground Shadow Enabled**。内置 World 材质会将当前帧的主纹理 Alpha 按材质的 **Alpha Cutoff** 裁成不透明轮廓，动作、朝向和水平镜像都随角色变化。适用于连续的水平地面。角色离地时影子留在设定的世界高度，地面前方的不透明物体通过深度测试遮挡影子。

**Ground Shadow Foot Height** 是脚底距离帧底的比例，默认值为 0。例如 200 像素高的帧在脚下留了 14 像素透明边，可填 `0.07`。在正交或透视的俯视镜头中调整脚底位置最直观。

场景可添加一个 **KingdomTD > Flipbook Ground Shadow Settings** 统一调整投影。缺省使用不透明深灰色、屏幕角度 45°、长度 0.7、世界地面高度 0。角度从屏幕右方向上逆时针计算，阴影的宽度沿地面垂直于投影方向，长度表示一整帧的角色高度对应多少地面距离。普通场景共用最后启用的设置，禁用后恢复上一份或缺省设置。Prefab Mode 的预览使用所在 Prefab Stage 的设置，缺省时使用默认值。

```csharp
renderer.GroundShadowFootHeight = 0.07f;
renderer.GroundShadowEnabled = true;
shadowSettings.ScreenAngle = 120f;
shadowSettings.Length = 0.85f;
shadowSettings.GroundHeight = 2f;
shadowSettings.ShadowColor = new Color(0.18f, 0.17f, 0.2f);
```

使用阴影的角色共享一个八顶点网格，角色与影子在同一子网格、同一材质、同一 Pass 中绘制。关闭时使用四顶点网格。内置 World Shader 保留每个对象的变换矩阵，可在共享材质上启用 **Enable GPU Instancing** 合并同类角色的绘制。切换阴影沿用当前材质，使用不支持阴影的旧材质时显示原四顶点角色。

阴影每个片元只采一次原图集，透明留白与近景大面积重叠仍有像素成本。测量时使用相同分辨率、镜头、动作与角色布局，对照开关前后的 GPU 帧耗时，并用 Frame Debugger 检查同材质实例的批次。

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
3. 需要效果纹理时设置 **Effect Texture**，用 **Effect Rate** 调整混合比例。
4. 需要角色染色时设置 **Change Color** 和 **Change Rate**。

`FlipbookRenderer` 默认沿用材质上的这三个参数。在 Play Mode 修改材质时，共用它的角色会实时更新。脚本调用 `SetEffectChangeRate`、`SetChangeColor` 或 `SetChangeRate` 时，只覆盖对应角色的那一个参数。
## 示例

通过 Package Manager 导入 **Flipbook Basic** 示例。打开其场景即可同时验证 `FlipbookRenderer` 与 `FlipbookGraphic`；两种组件共享同一动画资源，World 渲染额外使用样例材质。
