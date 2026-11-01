# FairyGUI for Godot — 移植规则

配合 [ARCHITECTURE.md](ARCHITECTURE.md) 使用。参考源码：`_ref/FairyGUI-unity/Assets/Scripts/`（下文简称“Unity 源”）。

## 1. 目录与文件映射

- Unity 源 `Assets/Scripts/<rel>.cs` → `addons/fairygui/Scripts/<rel>.cs`（相对路径不变）。
- 新增文件放在同一目录树下（如 `Core/DisplayObjectNode.cs`）。shader 放在 `addons/fairygui/Shaders/`。
- `addons/fairygui/Scripts/Compat/` 是已写好的兼容层（`Rect`、`UMath`、`UVec`、`UColor`、`FrameTime`），**不要修改**。缺少的辅助方法写进报告。
- 构建：`dotnet build FairyGUIForGodot.csproj`。Godot：`G:\software\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`。
- 查 Godot C# API 签名：`G:\software\Godot_v4.7.2-stable_mono_win64\GodotSharp\Api\Release\GodotSharp.xml`（grep `M:Godot.类名.方法名`）。引擎源码：`G:\git\godot`。
- 辅助参考（另一个 FairyGUI SDK 的 C++ 实现）：`G:\git\axmol\extensions\fairygui\src\fairygui`。

## 2. 类型与 API 映射

| Unity | Godot 版 |
|---|---|
| `using UnityEngine;` | `using Godot;`（命名空间仍为 `FairyGUI` / `FairyGUI.Utils`） |
| `Vector2/3/4` 字段 `.x .y .z .w` | 同名类型，字段 `.X .Y .Z .W` |
| `Vector2.zero/one`、`Vector3.zero/one` | `Vector2.Zero/One`、`Vector3.Zero/One` |
| `Vector3.forward/back`、`Vector2.up/down` | `UVec.forward/back`、`UVec.up2/down2`（**Godot 的 Up/Back 方向相反**） |
| `Vector2/3.Lerp` | `UVec.Lerp`（Unity 会 clamp t） |
| `Vector2/3.Distance/Dot/Cross` | `UVec.Distance/Dot/Cross` 或 `a.DistanceTo(b)` 等 |
| `.magnitude` `.sqrMagnitude` `.normalized` `v.Normalize()` | `.Length()` `.LengthSquared()` `.Normalized()` `v = v.Normalized()` |
| Vector2↔Vector3 隐式转换 | `v.ToV2()` / `v.ToV3()` |
| `Rect`（含 xMin/xMax/…） | `FairyGUI.Rect`（Compat，语义与 Unity 相同，可隐式转换为 `Rect2`） |
| `Mathf.*` | **一律 `UMath.*`**（Unity 语义：Lerp clamp、`Sign(0)=1` 等）。不要用 `Godot.Mathf` |
| `Color` 字段 `.r .g .b .a` | `Godot.Color`，字段 `.R .G .B .A` |
| `Color.white/black/clear/red/gray` | `UColor.white/black/clear/red/gray`（**Godot 的 Transparent、Gray 值不同**） |
| `Color32`、`new Color32(r,g,b,a)` | `Color`、`UColor.FromBytes(r,g,b,a)` 或 `Color.Color8(...)` |
| `Color.Lerp` | `UColor.Lerp` |
| `Matrix4x4` / `Quaternion` | 仅 Core 内部使用，按需换成 `Transform2D` / 自行计算 |
| `Time.deltaTime/unscaledDeltaTime/time/unscaledTime/frameCount/realtimeSinceStartup/timeScale` | `FrameTime.*`（同名字段） |
| `Application.targetFrameRate` | `FrameTime.targetFrameRate` |
| `Application.isPlaying` | `!Engine.IsEditorHint()` |
| `Application.isEditor` | `OS.HasFeature("editor")` |
| `Application.isMobilePlatform` / `platform` | `OS.HasFeature("mobile")` / `OS.GetName()` |
| `Screen.width/height` | `Stage.inst.stageWidth/stageHeight` |
| `Screen.dpi` | `DisplayServer.ScreenGetDpi()` |
| `Debug.Log/LogWarning/LogError` | `GD.Print` / `GD.PushWarning` / `GD.PushError` |
| `Resources.Load<T>(path)` | `ResourceLoader.Load<T>(path)`（path 为完整 `res://` 路径，含扩展名） |
| `Texture` / `Texture2D` | `Godot.Texture2D` |
| `Material` / `Shader` | `Godot.Material` / `Godot.Shader` |
| `AudioClip` | `Godot.AudioStream`（FairyGUI API 中通过 `NAudioClip` 包装） |
| `GameObject` / `Transform`（显示对象相关） | `DisplayObject.node`（见 ARCHITECTURE §2） |
| `KeyCode.X` | `Godot.Key.X`：`LeftArrow→Left`、`RightArrow→Right`、`UpArrow→Up`、`DownArrow→Down`、`PageUp→Pageup`、`PageDown→Pagedown`、`Return→Enter`、`KeypadEnter→KpEnter`、`LeftShift/RightShift→Shift`、`LeftControl/RightControl→Ctrl`、`LeftCommand/RightCommand→Meta`、`Backspace/Delete/Home/End/Tab/Escape/Insert/Space` 同名 |
| `Input.GetKey(k)` | `Input.IsKeyPressed(k)` |
| `Input.mousePosition` | Stage 记录的指针位置（Core 内部） |
| `GUIUtility.systemCopyBuffer` | `DisplayServer.ClipboardGet()` / `ClipboardSet()` |
| `Object.Destroy(x)` / `DestroyImmediate(x)`（节点） | `node.QueueFree()` / `node.Free()` |
| `x == null`（判断 Unity 对象已销毁） | `!GodotObject.IsInstanceValid(x)` |
| `[SerializeField]` / `[HideInInspector]` / `[ExecuteInEditMode]` / `[AddComponentMenu]` / `[RequireComponent]` | 删除；需要在检查器中显示的字段用 `[Export]` |
| `Profiler.BeginSample/EndSample` | 删除 |
| `#if UNITY_xxx` 条件编译 | 保留适用于 Godot 的分支，删掉其余分支 |

## 3. 类型名冲突

FairyGUI 与 Godot 同名的类型：`Container`、`Image`、`InputEvent`、`PlaneMesh`、`PopupMenu`、`Window`、`ZipReader`。

- 保留 FairyGUI 原名（API 一致性优先）。
- 在 `namespace FairyGUI { ... }` 内，简单名优先解析为 FairyGUI 自己的类型，所以 `Image`、`Container` 等指 FairyGUI 版。要用 Godot 版时写全名：`Godot.Image`、`Godot.InputEvent`、`Godot.Container`、`Godot.Window`。
- `using Godot;` 写在文件顶部的 namespace 外面。不要 `using Godot.Collections;`（与 `System.Collections.Generic` 冲突），确实要用时写全名。
- 用户代码同时 `using Godot; using FairyGUI;` 时需要别名，例如 `using Image = FairyGUI.Image;`。这一点写进 README。

## 4. Godot C# 规则

- 继承 `GodotObject` 的类（Node、Resource、RefCounted……）必须是 `partial class`，**一个文件一个，文件名 = 类名**（Godot 源生成器和脚本绑定依赖它）。普通 C# 类不受此限制。
- 这类类必须有**无参构造函数**（Godot 实例化脚本和热重载时需要），额外参数用属性在 `new` 之后赋值（如 `new DisplayObjectNode { owner = this }`）。
- 节点的构造函数里不要访问场景树。初始化放在 `_EnterTree` / `_Ready`，或由 FairyGUI 自己的构造流程完成。
- 引擎回调：`_Process(double)`、`_Input(InputEvent)`、`_Notification(int)`、`_Draw()`、`_Ready()`、`_EnterTree()`、`_ExitTree()`。在 FairyGUI 命名空间内，参数类型要写 `Godot.InputEvent`。
- 编辑器专用代码放在 `#if TOOLS` 中。

## 5. 编码规则（所有移植单元）

1. 开始前完整阅读 ARCHITECTURE.md、本文件，以及本单元负责的所有 Unity 源文件。
2. **保持结构**：同样的类、成员顺序、方法划分、原有注释（包括中文注释），方便与 Unity 版逐段对照。每个文件头部加一行 `// Ported from FairyGUI-unity: Assets/Scripts/<rel>.cs`。
3. **完整移植**：不能留 `TODO`、空方法桩，或 `throw new NotImplementedException()`。某个特性在 Godot 下确实无法支持时，写出最合理的退化实现，并标注 `// [Godot] <原因>`。
4. 与 Unity 行为有差异的地方都标注 `// [Godot]`。不顺手重构，不改算法，不改命名。
5. **只写本单元的文件**。需要其他单元提供、但 Unity 版里没有的符号，写进报告的 `crossUnitNeeds`，不要去改别人的文件。
6. 跨单元调用一律按 Unity 版的成员名和签名书写（经过 §2 映射后）。Core 单元必须保留 Unity 版 Core 的 public/internal 成员（同样经过映射），UI 层才能直接编译。改动了对外签名的，写进报告的 `apiChanges`。
7. 移植阶段**不要运行 `dotnet build`**：其他单元在并行写文件，整体暂时无法编译。集成阶段统一编译修错。
8. 删除的 Unity 专有成员（如 `StageCamera` 的相机逻辑、AssetBundle 重载）写进报告的 `removed`。

## 6. 移植单元

单元划分与文件归属见 [units.json](units.json)（覆盖 Unity 源全部 153 个运行时文件，每个文件只属于一个单元）。集成阶段的编译错误按文件归属分派回对应单元。

各单元的跨单元契约要点（其余一律以 Unity 版为准）：

- **core-display**：`DisplayObject.node`、`DisplayObjectNode`、`OnNodeDraw()`、`RemoveFromParent()`、`home : Node`、Container 的裁剪 / 遮罩 / 子节点顺序、命中测试、PaintingMode（CanvasGroup）。
- **core-graphics**：`NGraphics(Node2D)`、`NGraphics.Draw()`（只在 `_Draw` 内调用）、`NTexture(Texture2D)`、MaterialManager、ShaderConfig、滤镜、GoWrapper（包装 Godot 节点）。
- **core-stage**：`StageEngine`（autoload、帧循环、`_Input`、`_Notification` 转发 IME）、`Stage.HandleInputEvent`、`Stage.inst.contentScaler`、`PlayOneShotSound`、`RegisterPanel/UnregisterPanel`、`Stage.canvasLayer`、`Timers`、`UIPanel`、`UIPainter`。
- **core-text-a/b**：TextField 双绘制路径、DynamicFont 包装 `Godot.Font`、FontManager 解析规则、InputTextField 的键盘 / IME / 剪贴板。
- **ui-package**：`UIPackage.AddPackage(res 路径)`、默认加载器、`PackageItem` 中的 `NTexture/NAudioClip/BitmapFont`。

## 7. 单元报告格式

每个移植单元结束时返回（结构化）：

- `filesWritten`：写出的文件（相对仓库根目录）。
- `removed`：删除的 Unity 专有成员 / 文件，以及原因。
- `apiChanges`：与 Unity 版对外签名不同的地方（其他单元集成时要据此调整）。
- `crossUnitNeeds`：需要其他单元提供、但 Unity 版里没有的符号。
- `approximations`：退化或近似实现的特性。
- `notes`：其他需要集成者知道的事。
