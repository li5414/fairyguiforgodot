# 从 Unity 版迁移

FairyGUI for Godot 的 API 与 FairyGUI-unity 一致：类名、成员名、方法签名都相同，只是 Unity 类型换成了 Godot 类型。本文列出写业务代码时会遇到的差异。移植时使用的完整对照规则见 [PORTING.md](PORTING.md)。

## 类型

| Unity | Godot 版 |
|---|---|
| `Vector2/3` 的 `.x .y` | `Godot.Vector2/3` 的 `.X .Y` |
| `Color` / `Color32` | `Godot.Color`，用 `Color.Color8(r, g, b, a)` 代替 `new Color32(...)` |
| `Rect` | `FairyGUI.Rect`：语义与 Unity 相同，可以和 `Rect2` 隐式互转 |
| `Mathf` | 一般直接用 `Godot.Mathf`；需要 Unity 的语义（Lerp 会夹紧到 [0,1]、`Sign(0)` 返回 1）时用 `FairyGUI.UMath` |
| `KeyCode.LeftArrow` | `Godot.Key.Left`，`InputEvent.keyCode` 的类型是 `Godot.Key` |
| `Texture2D` / `Material` / `Shader` | `Godot.Texture2D` / `Godot.Material` / `Godot.Shader` |
| `AudioClip` | `Godot.AudioStream`，包装在 `NAudioClip.nativeClip` 里 |
| `Font` | `Godot.Font`，写法为 `new DynamicFont(name, godotFont)` |
| `GameObject` / `Transform` | `Node` / `Node2D`，通过 `DisplayObject.node` 访问 |
| LineMesh 的 `AnimationCurve` / `Gradient` | `Godot.Curve` / `Godot.Gradient` |

## 资源加载

- `UIPackage.AddPackage("res://UI/Bag")`：参数是不带后缀的完整路径（Unity 版用的是 Resources 下的相对路径 `"UI/Bag"`），会依次查找 `Bag_fui.bytes` 和 `Bag.fui`。
- 自定义加载器 `AddPackage(path, LoadResource)` 的签名不变。请求的类型分别是：描述文件 `typeof(byte[])`，图集 `typeof(Texture2D)`，声音 `typeof(AudioStream)`。`DestroyMethod` 保留了，但不起作用，因为 Godot 资源是引用计数的。
- AssetBundle 相关的重载已全部删除。改用 `ProjectSettings.LoadResourcePack()` 加载 PCK，加载后资源同样通过 `res://` 路径访问。
- 自定义加载器也可以返回 `AtlasTexture`（对应 Unity 的 Sprite）。
- `GLoader` 加载外部图片时，url 是完整的 Godot 路径（`res://...`、`user://...`），不再是 Resources 路径。
- `GLoader3D` 不支持 Spine / DragonBones。url 指向 `.tscn` / `.scn` 时会实例化该场景并显示，场景里第一个 `AnimationPlayer` / `AnimatedSprite2D` 受 animationName、playing、frame、loop 控制。也可以用 `SetWrapTarget(Node, ...)` 显示任意节点。

## 组件（替代原来的 MonoBehaviour）

- `UIPanel`：一个 `Node2D`（`[GlobalClass]`），在检查器里设置 packagePath / packageName / componentName。界面跟随节点和 Camera2D 移动，点击、焦点、拖动照常工作。
- `UIConfig`：一个 `Node`。`Items` 是字典（ConfigKey → 值），在 `_Ready` 中应用。静态字段（如 `UIConfig.defaultFont`）的用法不变。
- `UIContentScaler`：普通类，通过 `Stage.inst.contentScaler` 访问。通常直接调用 `GRoot.inst.SetContentScaleFactor(...)` 即可。
- `StageCamera`、`CaptureCamera`：改为静态类，只保留少量成员，`StageCamera.UnitsPerPixel` 恒为 1。
- `UIPainter`：一个 `Node`，把界面渲染到 `ViewportTexture` 供 3D 材质使用。不支持在 3D 表面上输入。
- `GoWrapper`：包装任意 Godot 节点（Unity 版包装的是 GameObject）。

## 坐标与显示

- 坐标系与 FairyGUI 编辑器一致：y 轴向下，旋转以顺时针为正，全局坐标就是视口坐标。
- `GObject.WorldToLocal` 没有 Camera 重载，这里的“World”指 canvas 全局坐标。
- `DisplayObject.node` 是 Godot 节点，相当于 Unity 的 gameObject。FairyGUI 的显示树在 Godot 的远程场景树里可以看到。这些节点的 Transform、Modulate、Material 由 FairyGUI 管理，不要直接修改。
- `DisplayObject.GetScreenShot` 返回的是 `Godot.Texture2D`。

## 输入

- 鼠标、触摸和键盘事件由 `StageEngine._Input` 交给 Stage 处理。点中 FairyGUI 中可触摸的对象时会调用 `SetInputAsHandled()`，事件不会再传给游戏和 Godot 原生 GUI（对应 Unity 版的 `Stage.isTouchOnUI`）。
- macOS 和 Wayland 上的触控板滚动按鼠标滚轮处理。
- 输入法：输入框获得焦点时会启用窗口的 IME，正在组合的文字通过 `DisplayServer.ImeGetText()` 读取。

## 类型名冲突

`Window`、`Image`、`Container`、`InputEvent`、`PopupMenu`、`PlaneMesh` 在 Godot 和 FairyGUI 中都有定义；`ZipReader` 与 `FairyGUI.Utils.ZipReader` 同名。如果同一个文件同时 `using Godot;` 和 `using FairyGUI;`，需要加别名：

```csharp
using Window = FairyGUI.Window;
using Image = FairyGUI.Image;
```

## 其他

- 可以使用 `Timers.inst.StartCoroutine(IEnumerator)`，等待时用 `new FairyGUI.WaitForSeconds(t)`。
- 播放音效：`Stage.inst.PlayOneShotSound(AudioStream)` 或 `GRoot.inst.PlayOneShotSound(AudioStream)`。
- 退出时 `StageEngine` 会依次释放所有包、GRoot 和对象池。
