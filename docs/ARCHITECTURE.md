# FairyGUI for Godot — 架构设计

本文是移植的设计契约。所有移植单元（见 [PORTING.md](PORTING.md)）都必须遵守这里的决策。

## 1. 目标与分层

- 目标：把 FairyGUI-unity 运行时完整移植到 Godot 4.7（C# / .NET 8），直接加载 FairyGUI 编辑器“Unity 发布格式”导出的包（`xxx_fui.bytes` + `xxx_atlas0.png` 等）。
- 分层沿用 FairyGUI 各语言 SDK（Unity / Cocos / Laya / Egret / axmol）的共同结构：
  - **UI 层**（`GObject` 及以上：GComponent、GList、ScrollPane、Transition、Controller、Gears、Relations、UIPackage……）与引擎基本无关，**逐行移植**。
  - **Core 层**（`DisplayObject` / `Container` / `NGraphics` / `NTexture` / `Stage` / Text）是显示后端，**按 Godot 重新实现**，但对 UI 层暴露与 Unity 版相同的 API。
  - **Event / Tween / Utils / Mesh** 基本是纯 C# 算法，逐行移植。
- 公共 API：命名空间、类名、成员名、方法签名与 Unity 版一致，仅把 Unity 类型替换为 Godot 类型（映射表见 PORTING.md）。业务代码与官方文档可近乎直接迁移。
- 不移植：Unity Editor 扩展、`Extensions/`（Spine、DragonBones、TextMeshPro、WebGLTextInput）、LuaSupport、AssetBundle 相关 API。

## 2. 显示节点模型

- 每个 `DisplayObject` 持有一个 `DisplayObjectNode : Node2D`（`node` 属性），节点的 `_Draw()` 回调转发到 `owner.OnNodeDraw()`。
- `DisplayObject.node` 取代 Unity 版的 `gameObject` 与 `cachedTransform`。`gameObject.name` → `node.Name`（写入前把 `. : @ / " %` 替换为 `_`）。
- `Container` 的子对象顺序 = Godot 子节点顺序（`AddChild` / `MoveChild`），不使用 `ZIndex`。
- `DisplayObject.home` 类型改为 `Node`。Godot 允许孤儿节点，所以从显示列表移除只需 `RemoveChild`。UI 层里 Unity 风格的 `x.displayObject.cachedTransform.SetParent(y.cachedTransform, false)`：
  - 目标是父容器时（如 `GComponent.ConstructFromResource` 末尾）直接删除，`Container.AddChild` 已负责挂接；
  - 用于对象池回收时（`GObjectPool`、`HtmlPageContext`）改为把节点从当前父节点摘下（`DisplayObject.RemoveFromParent()` 或 `node.GetParent()?.RemoveChild(node)`）。
- `Dispose()`：在树内用 `QueueFree()`，不在树内用 `Free()`。
- 变换：由 x / y / pivot / rotation / scale / skew 计算出一个 `Transform2D`，整体赋给 `node.Transform`，不分别设置 Node2D 的 Position/Rotation/Scale/Skew。FairyGUI 坐标约定（y 向下、rotation 顺时针为正、单位为度）与 Godot 2D 一致，**去掉 Unity 版中所有 y 取反与 `-rotation`**。
- `rotationX/rotationY/perspective/focalLength`：Godot 2D 没有透视，用绕 pivot 的仿射近似（x 方向乘 `cos(rotationY)`、y 方向乘 `cos(rotationX)`），标注 `// [Godot] approximation`。
- `visible` → `node.Visible`；`alpha` → `node.Modulate.A`。Godot 的 Modulate 在子树中自动累乘，等价于 Unity 版 UpdateContext 里累乘 alpha 的逻辑。
- 坐标转换以 `node.GetGlobalTransformWithCanvas()` 为准（见 §5）。节点不在场景树中时它不可用，要回退到 `GetGlobalTransform()`，不能报错。

## 3. 渲染：NGraphics → CanvasItem

- `VertexBuffer`、`IMeshFactory` 和 `Mesh/` 下所有类逐行移植，**内部约定与 Unity 版完全一致**：顶点 y 取反存储，纹理 uv 原点在左下（沿用 Unity 版 uvRect 公式）。
- **唯一的转换点**在 NGraphics 提交时：顶点 `(x, y)` → `(x, -y)`，uv `(u, v)` → `(u, 1 - v)`，三角形索引原样提交。Mesh 代码、FillMesh、`NTexture.uvRect`、MovieClip 帧、BitmapFont 字形都不需要改坐标约定。
- 构造：`new NGraphics(Node2D node)`，替代 `new NGraphics(gameObject)`。网格变化时调用 `node.QueueRedraw()`。
- 提交：`RenderingServer.CanvasItemAddTriangleArray(node.GetCanvasItem(), indices, points, colors, uvs, null, null, textureRid)`（优先用 ReadOnlySpan 重载）。**只能在该节点的 `_Draw()` 期间调用**：Godot 在重绘前会清空 CanvasItem 的命令。
- 绘制入口：`DisplayObject` 上的 `internal virtual void OnNodeDraw()`，默认实现为 `graphics?.Draw()`。TextField 重写它，用来绘制动态字体字形（§4）。
- 网格重建时机与 Unity 版相同：每帧 Update 遍历时按脏标记重建顶点缓冲并缓存数组，`_Draw` 只提交缓存。
- 顶点色 = `graphics.color` × 顶点色。对象 alpha 走 `node.Modulate`，**不烘进顶点**。纯色图形传空纹理 RID，不需要白色占位纹理。
- `NTexture` 包装 `Godot.Texture2D`，保留 uvRect / region / offset / originalSize / rotated 的语义。`alphaTexture` 字段保留，恒为 null（Godot 纹理自带 alpha）。像素级点击测试使用包内的 PixelHitTestData，与引擎无关。

### 3.1 裁剪与遮罩

- 矩形裁剪（`Container.clipRect`，overflow hidden/scroll）：`RenderingServer.CanvasItemSetCustomRect(rid, true, rect)` + `CanvasItemSetClip(rid, true)`，作用于该节点及其全部子孙。Godot 按屏幕轴对齐裁剪（旋转后取 AABB），与 Unity 版基于 shader 的裁剪效果一致。`clipSoftness` 不支持，标注 `// [Godot]`。
- 遮罩（`Container.mask`）：使用 `CanvasItem.ClipChildren = ClipChildrenMode.Only`。Godot 要求遮罩节点是被遮罩内容的父节点，而 FairyGUI 的 mask 是容器的一个子对象，所以 Container 内部需要调整节点结构（例如加一个内部节点，承载遮罩绘制并作为其余子节点的父节点）。对外 API 不变，具体结构由 core-display 决定并写注释说明。`reversedMask` 尽量实现（例如反向 alpha 的 shader），实现不了就打警告，退化为普通遮罩。
- CanvasGroup 与 ClipChildren 都依赖 backbuffer，二者嵌套时可能失效，在代码注释中写明。

### 3.2 材质、置灰、混合模式、滤镜

- `MaterialManager` 按（grayed, blendMode, 自定义 shader, 颜色矩阵）组合缓存 `ShaderMaterial` / `CanvasItemMaterial`，同一节点上多种效果合成一个材质。内置 shader 放在 `addons/fairygui/Shaders/*.gdshader`。
- 置灰：沿用 Unity 版的递归下发，Container 把 grayed 传给子孙的 NGraphics，由 NGraphics 切换材质。
- BlendMode：Normal → mix，Add → add，Multiply → mul，None → disabled（通过 shader `render_mode` 或 `CanvasItemMaterial.BlendMode`）。Screen / Erase / Mask / Off 等用自定义 shader 近似，标注 `// [Godot]`。
- `GImage/GLoader/GMovieClip.material` 直接映射到 `Godot.Material`；`shader`（string）经 `ShaderConfig` 解析为 `Godot.Shader`。
- `ColorFilter`：叶子对象用颜色矩阵 ShaderMaterial；容器使用 CanvasGroup 语义（`RenderingServer.CanvasItemSetCanvasGroupMode(rid, CanvasGroupMode.Transparent, ...)`），配合采样 `screen_texture` 的 shader。
- `BlurFilter`：CanvasGroup + `textureLod` 近似模糊。
- `EnterPaintingMode/LeavePaintingMode`、`cacheAsBitmap`：用 CanvasGroup 实现，**不移植** Unity 版的 RenderTexture + CaptureCamera 方案。
- 滤镜与材质的契约（core-display ↔ core-graphics）：
  - `paintingGraphics` 仍是 `NGraphics`。进入绘画模式时，core-display 对承载节点设置 CanvasGroup 模式，`paintingGraphics` 的材质即作用于整个组。
  - `NGraphics.ToggleKeyword(string, bool)` 保留，映射为 shader uniform 开关或变体。Unity 的 `materialPropertyBlock` 替换为 `NGraphics.SetShaderParameter(string name, Variant value)`：首次调用时把共享材质复制成本实例独有的 ShaderMaterial。
  - 共享材质（普通 / 置灰 / 各 BlendMode）由 MaterialManager 缓存，与 Unity 版 NMaterial 池对应。
  - Unity 版 `onPaint` + `Graphics.Blit`（BlurFilter）无法对应，改为在 `paintingGraphics` 上用模糊 shader。
- alpha 只走 `node.Modulate`：`NGraphics.Update(context, alpha, grayed)` 签名保留，但 alpha **不再乘进顶点色**（否则与 Modulate 叠加，变成重复相乘），grayed 只用于选材质。

## 4. 文字

原则：**保留 FairyGUI 自己的排版引擎**（TextField 逐字排版、UBB/HTML、RichTextField 内嵌对象、对齐、autoSize、shrink、单/多行、超链接区域、打字效果），只把字体光栅化和绘制换成 Godot。这样排版与 FairyGUI 编辑器和 Unity 版一致，HTML 里内嵌的图片、按钮、输入框、链接都能工作。

- `BaseFont` 抽象保留。`BitmapFont` 逐行移植：字形是图集里的四边形，走 VertexBuffer / NGraphics，与 Unity 版相同。
- `DynamicFont` 重写，包装 `Godot.Font`（FontFile / SystemFont / FontVariation）：
  - 度量：`font.GetCharSize(ch, size)`、`GetAscent(size)`、`GetDescent(size)`、`GetHeight(size)`。
  - 绘制：在 TextField 的 `OnNodeDraw()` 里逐字调用 `font.DrawChar(canvasItem, pos, ch, size, color)`。字形缓存、多页纹理、MSDF、后备字体都由 Godot 处理。
  - 描边：先 `DrawCharOutline(canvasItem, pos, ch, size, outlineSize, color)` 再 `DrawChar`。阴影：按 shadowOffset 偏移，用阴影色多画一遍。
  - 粗体 / 斜体：FontManager 缓存 `FontVariation`（`VariationEmbolden`、`VariationTransform` 斜切）。下划线 / 删除线：`RenderingServer.CanvasItemAddRect`。
- 因此 TextField 有两条绘制路径：位图字体 → VertexBuffer；动态字体 → 记录字形列表，在 `OnNodeDraw()` 中逐字绘制。两条路径都由 TextField 内部管理。
- `FontManager`：API 同 Unity 版（`RegisterFont` / `GetFont` / `UnregisterFont`）。字体名解析顺序：已注册别名 → `ui://` 包内字体 → `res://` 路径 → `SystemFont`（按名）→ `ThemeDB.FallbackFont`。`UIConfig.defaultFont` 同样适用。
- 打字效果（TypingEffect）：通过 TextField 的可见字符数控制只绘制前 N 个字形，不再修改顶点 alpha。
- `InputTextField`：保留 FairyGUI 的编辑逻辑（光标、选区、剪贴板、restrict、maxLength、密码、键盘导航）。
  - 键盘：Stage 把 `InputEventKey` 转发给焦点对象。`Unicode` 用于插入字符，`Keycode` 用于命令键。
  - IME：获得焦点时调用 `DisplayServer.WindowSetImeActive(true)`，`WindowSetImePosition` 设到光标位置。收到 `NotificationOsImeUpdate` 时用 `DisplayServer.ImeGetText()` / `ImeGetSelection()` 读取组合串。提交的文本以 InputEventKey(Unicode) 形式到达。
  - 剪贴板：`DisplayServer.ClipboardGet/ClipboardSet`。移动端软键盘：`IKeyboard` 接口，默认实现 `TouchScreenKeyboard` 调用 `DisplayServer.VirtualKeyboardShow/Hide`。
- `RTLSupport` 是纯算法，逐行移植。
- 已知限制：与 Unity 版一样逐字排版，不做复杂文字整形（阿拉伯连写、泰文组合、彩色 emoji 序列）和 kerning。

## 5. Stage、输入与帧循环

- `StageEngine : Node`（`addons/fairygui/Scripts/Core/StageEngine.cs`）是唯一的驱动节点。
  - 推荐注册为 autoload（`project.godot` 中名为 `FairyGUI`）。没有 autoload 时，`Stage.Instantiate()` 自动创建它，并 `CallDeferred` 加到 `SceneTree.Root`。
  - `ProcessMode = Always`（暂停时 UI 仍可用）。`ProcessPriority = 1000`，使其 `_Process` 在用户脚本之后执行，对应 Unity 的 LateUpdate。
  - `_Process` 固定顺序：`FrameTime.Advance(delta)` → `Timers.inst.Update()` → `TweenManager.Update()` → `Stage.inst.InternalUpdate()`。
  - Unity 版中 Timers / TweenManager 各自创建隐藏 GameObject 驱动自己；Godot 版改为由 StageEngine 驱动，它们首次使用时调用 `Stage.Instantiate()`（幂等）确保引擎存在。
- Stage 的根节点放在 StageEngine 下的 `CanvasLayer` 中（层号 `Stage.canvasLayer`，默认 100），不受 Camera2D 影响。
- `stageWidth/stageHeight` = `GetViewport().GetVisibleRect().Size`，尺寸变化时派发 `onStageResized`（对应 Unity 版的屏幕尺寸监听）。
- 坐标转换：视口坐标 → 本地坐标 = `node.GetGlobalTransformWithCanvas().AffineInverse() * p`，反向用正变换。`GlobalToLocal / LocalToGlobal / TransformPoint / TransformRect` 都基于它，对 Stage 下的对象和 UIPanel 内的对象（世界空间）同样成立。`GObject.WorldToLocal` 删除 Camera 重载，“World”在 Godot 版里即 canvas 全局坐标。
- 输入：`StageEngine._Input(InputEvent e)` 交给 `Stage.HandleInputEvent(e)`。Stage 自己做命中测试（逆序遍历显示树，遵守 touchable / touchChildren / hitArea / opaque / clipRect / mask / PixelHitTest / ShapeHitTest），派发与 Unity 版语义一致的 touchBegin / touchMove / touchEnd / click / rollOver / rollOut / mouseWheel / keyDown 等事件。
  - 鼠标：`InputEventMouseButton`（左 / 右 / 中键 → button 0/1/2；`WheelUp/WheelDown` → `mouseWheelDelta`，符号与 Unity 版一致：向下滚为正）、`InputEventMouseMotion`（驱动 rollOver / rollOut）。
  - 触摸：`InputEventScreenTouch/ScreenDrag`，`Index` 作为 touchId。收到过真实触摸后设置 `Stage.touchScreen = true`，并忽略 `Device == InputEvent.DeviceIdEmulation` 的模拟事件，避免与鼠标模拟重复派发。
  - 键盘：`InputEventKey` 生成 InputEvent 的 keyCode / character / modifiers，发给焦点对象（InputTextField 处理编辑）。
  - 命中可触摸对象时调用 `GetViewport().SetInputAsHandled()`，事件不再穿透到游戏层和 Godot 原生 GUI（对应 Unity 版的 `Stage.isTouchOnUI`）。
- `UIContentScaler` 从 MonoBehaviour 改为普通类，由 Stage 持有：`Stage.inst.contentScaler`。字段（scaleMode / screenMatchMode / designResolutionX/Y / constantScaleFactor / ignoreOrientation …）与静态 `scaleFactor / scaleLevel` 保留。GRoot 中的 `Stage.inst.gameObject.GetComponent<UIContentScaler>()` 改为 `Stage.inst.contentScaler`。
- 声音：`NAudioClip` 包装 `AudioStream`（`nativeClip`）。`Stage.inst.PlayOneShotSound(clip, volumeScale)` 使用 Stage 下的 `AudioStreamPlayer` 池。
- 额外根：UIPanel 等不在 Stage 下的容器通过 `Stage.inst.RegisterPanel(Container)` / `UnregisterPanel` 参与每帧 Update 与命中测试（对应 Unity 版 world-space panel 的处理）。

## 6. 资源加载

- `UIPackage.AddPackage(string assetPath)`：`assetPath` 形如 `res://demo/UI/Basics`。依次尝试 `assetPath + "_fui.bytes"`、`assetPath + ".fui"`，用 `FileAccess.GetFileAsBytes` 读取。
- 默认资源加载器：`ResourceLoader.Load<T>(assetNamePrefix + fileName + ext)`，与 Unity 版命名规则一致（如 `Basics_atlas0.png`、`Basics_gojg7u.wav`）。纹理、声音、字体由 Godot 导入系统处理。
- `LoadResource` 委托签名保留，`DestroyMethod` 枚举保留（Godot 资源是引用计数，无实际作用）。`LoadResourceAsync` 用 `ResourceLoader.LoadThreadedRequest` 实现。
- 删除所有 AssetBundle 重载（Godot 用 `ProjectSettings.LoadResourcePack` 加载 PCK 后，资源同样以 `res://` 路径访问）。
- 导出提醒：`.bytes/.fui` 不是 Godot 资源，导出预设的“非资源文件过滤”需加入 `*.bytes, *.fui`。

## 7. 节点组件（替代 MonoBehaviour）

- `UIPanel : Node2D`，`[GlobalClass]`。导出字段：packagePath、packageName、componentName、fitScreen、sortingOrder、touchDisabled、hitTestMode。`_Ready` 中加载包、创建组件，把它的 `displayObject.node` 挂到一个内部 Container 下并 `RegisterPanel`。用于把 FairyGUI 界面嵌入普通 2D 场景（随 Camera2D 移动）。
- `UIPainter`：用 `SubViewport` 渲染到 `ViewportTexture`，供 3D 材质使用。3D 表面上的输入不支持。
- `UIConfig`：静态字段全部保留。组件部分改为可选的 `UIConfig : Node`，导出 Items，在 `_Ready` 中应用。
- `StageCamera`、`CaptureCamera`：Godot 2D 渲染不需要专用相机。只保留被其他代码引用、且在 Godot 下仍有意义的静态成员（如 `StageCamera.UnitsPerPixel` 恒为 1），其余删除并在报告中列出。
- `GoWrapper`：保留类名，用于把任意 Godot `Node2D/Control` 包进 FairyGUI 显示树。`GLoader3D` 用它显示 `PackedScene` 实例。Spine / DragonBones 相关 API 删除。

## 8. 不支持 / 近似实现

| 特性 | Godot 版处理 |
|---|---|
| rotationX/Y、perspective | 仿射近似（§2） |
| clipSoftness | 不支持，按硬裁剪 |
| reversedMask | 尽力实现，否则警告 |
| 部分 BlendMode | 自定义 shader 近似 |
| BlurFilter | CanvasGroup + shader 近似 |
| Spine / DragonBones | 不内置；可用 GoWrapper / GLoader3D 包装 Godot 节点或 PackedScene |
| 复杂文字整形、kerning | 不支持（与 Unity 版相同） |
| AssetBundle | 删除，用 PCK |
| 编辑器内预览（Unity 的 EM 渲染） | 不支持，只做运行时 |
