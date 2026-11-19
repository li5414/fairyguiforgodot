# FairyGUI for Godot

把 [FairyGUI](https://www.fairygui.com/) 的 Unity 运行时（[FairyGUI-unity](https://github.com/fairygui/FairyGUI-unity)）完整移植到 Godot 4.7（C#）。直接加载 FairyGUI 编辑器按 Unity 格式发布的 UI 包；API 与 Unity 版一致，Unity 版的业务代码和官方文档基本可以沿用。

- 已移植全部运行时：GObject、GComponent、GList（虚拟列表、循环列表）、ScrollPane、Transition、Controller、Gears、Relations、GTree、Window、PopupMenu、富文本、输入框（含输入法）、拖放、手势、Tween 等。
- 渲染基于 Godot 的 CanvasItem：每个显示对象对应一个 Node2D，网格通过 RenderingServer 提交。文字使用 FairyGUI 自己的排版引擎，字形由 Godot 字体绘制。
- 设计说明见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)，与 Unity 版的 API 差异见 [docs/MIGRATION.md](docs/MIGRATION.md)。

## 环境

- Godot 4.7 **.NET 版**，.NET 8 SDK 或更高。
- 项目需要是 C# 项目（有 `.csproj` 和 `.sln`）。如果是纯 GDScript 项目，先在编辑器里执行“项目 > 工具 > C# > Create C# solution”。

## 安装

1. 将版本化插件 ZIP 解压到包含 `project.godot` 的目录，得到 `addons/fairygui/`。
2. 构建 C#：编辑器里点 Build，或运行 `dotnet build`。
3. 在“项目设置 > 插件”里启用 **FairyGUI**。插件会做两件事：
   - 注册名为 `FairyGUI` 的 autoload（`StageEngine`，负责驱动帧循环和输入）；
   - 导出时自动把 FairyGUI 包描述文件（`*_fui.bytes`、`*.fui`）打进 pck。

不启用插件也能运行，首次使用时会自动创建 StageEngine。但导出前必须手动在导出预设的“导出非资源文件的过滤器”里加入 `*.bytes, *.fui`，否则导出后的游戏找不到 UI 包。

如果插件目录改了名，需要同时修改 `ShaderConfig.shaderPath`，它的默认值是 `res://addons/fairygui/Shaders/`。

宿主可在 `project.godot` 中配置自己的 `FairyGUI` autoload。启停插件会保留这个自定义入口。使用独立运行时程序集时，在宿主 `.csproj` 中从 `Compile` 移除 `addons/fairygui/**`、重新包含 `addons/fairygui/Editor/*.cs`，并用 `ProjectReference` 引用 `addons/fairygui/FairyGUI.csproj`。

## 发布 UI 包

在 FairyGUI 编辑器中按 **Unity** 格式发布（项目类型选 Unity，描述文件扩展名用 `bytes`），发布目录放在 Godot 项目里，例如 `res://UI/`。每个包会生成 `Xxx_fui.bytes`、`Xxx_atlas0.png` 等文件，Godot 会自动导入其中的图集和声音。

## 快速上手

```csharp
using FairyGUI;
using Godot;

public partial class Main : Node
{
    GComponent _view;

    public override void _Ready()
    {
        // 设计分辨率，相当于 Unity 版 UIContentScaler 的 ScaleWithScreenSize
        GRoot.inst.SetContentScaleFactor(1136, 640, UIContentScaler.ScreenMatchMode.MatchWidthOrHeight);
        UIConfig.defaultFont = "Microsoft YaHei"; // 系统字体名、res:// 字体路径或注册过的别名

        UIPackage.AddPackage("res://UI/Bag"); // 读取 res://UI/Bag_fui.bytes
        _view = UIPackage.CreateObject("Bag", "Main").asCom;
        _view.MakeFullScreen();
        GRoot.inst.AddChild(_view);

        _view.GetChild("bagBtn").onClick.Add(() => GD.Print("clicked"));
    }

    public override void _ExitTree()
    {
        _view?.Dispose();
    }
}
```

- **字体**：Godot 的默认后备字体没有中文字形，需要设置 `UIConfig.defaultFont`。也可以注册自己的字体：`FontManager.RegisterFont(new DynamicFont("main", GD.Load<Godot.Font>("res://fonts/main.ttf")), "main");`。
- **放进 2D 场景**：添加一个 `UIPanel` 节点，在检查器里设置 packagePath（如 `res://UI/Bag`）、packageName、componentName。界面会随节点和 Camera2D 移动，输入照常工作。
- **类型名冲突**：同一个文件同时 `using Godot;` 和 `using FairyGUI;` 时，`Window`、`Image`、`Container`、`InputEvent`、`PopupMenu`、`PlaneMesh` 会有歧义，需要用别名，例如 `using Window = FairyGUI.Window;`。

## 导出

- 启用插件后，导出预设使用默认的“导出所有资源”模式时，不需要任何额外设置。
- 图集、声音、字体是普通的 Godot 资源，但没有场景静态引用它们（UIPackage 在运行时按路径加载）。如果导出预设用的是“导出选中的场景/资源”模式，需要把它们也选上，插件会在导出时给出警告。
- headless 运行时（服务器、自动化测试）根视口只有 64×64。在使用 FairyGUI 之前先执行 `GetTree().Root.Size = new Vector2I(设计宽, 设计高);`，否则内容缩放和文字度量都会不对。

## 示例和测试

- `demo/`：22 个官方示例的 Godot 版。主场景 `res://demo/Scenes/Main.tscn` 是示例菜单。
- `tests/SmokeTest.tscn`：headless 冒烟测试，共 326 项检查，说明见 [demo/README.md](demo/README.md)。

```sh
dotnet build FairyGUIForGodot.csproj
godot --headless --path . --import
godot --headless --path . res://tests/SmokeTest.tscn
# 导出版（官方导出模板不接受命令行指定场景，由 demo 菜单根据参数切换）：
FairyGUIDemo.console.exe --headless -- --scene=res://tests/SmokeTest.tscn
```

## 已知限制

与 Unity 版的差异详见 [docs/ARCHITECTURE.md §8](docs/ARCHITECTURE.md)：

- 不支持：clipSoftness（按硬裁剪处理）、Spine / DragonBones（GLoader3D 改为加载 PackedScene）、AssetBundle（改用 PCK）、编辑器内预览、UIPainter 3D 表面上的输入。
- 近似实现：rotationX/Y 透视（仿射近似）、BlurFilter、部分 BlendMode、动态字体行高（使用字体的真实度量）。
- 只在 Windows 桌面上验证过（编辑器运行和导出版）。移动端触摸与软键盘、macOS 触控板、Linux 输入法、Web 都还没有实测。

## 许可

FairyGUI-unity 以 MIT 许可发布，原许可证见 [addons/fairygui/LICENSE-FairyGUI.txt](addons/fairygui/LICENSE-FairyGUI.txt)。
