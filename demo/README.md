# FairyGUI for Godot: demo and smoke test

The demo contains Godot C# versions of the FairyGUI-unity examples (`_ref/FairyGUI-unity/Assets/Examples`). They use the same packages, fonts and icons:

| Path | Contents |
|---|---|
| `demo/UI/` | Packages exported by the FairyGUI editor (`Xxx_fui.bytes`, atlases, sounds) |
| `demo/Fonts/afont.ttf` | Default font. Its alias `afont` is registered with a CJK system-font fallback |
| `demo/Icons/` | Icons loaded from outside the packages (Bag, Cooldown) |
| `demo/Scripts/<Name>/` | One folder per example. `XxxMain` is the Godot node that replaces the Unity MonoBehaviour |
| `demo/Scripts/DemoMain.cs`, `demo/Scenes/Main.tscn` | The project's main scene: a menu that lists every example |
| `tests/SmokeTest.cs`, `tests/SmokeTest.tscn` | The headless integration test |

## Running the demo

Use the Godot 4.7 .NET build. On Windows, use the `_console.exe` variant so that output and exit codes reach the terminal.

```sh
dotnet build FairyGUIForGodot.csproj        # build the C# assembly (the editor also does this when you press Play)
godot --headless --path . --import          # one-time import of textures, sounds and fonts
godot --path .                              # runs res://demo/Scenes/Main.tscn
```

Click an example in the menu to start it. **Back** (top right) or **Esc** returns to the menu. Pressing **Esc** while the menu is shown quits.

Each `XxxMain` node also works by itself. Attach it to any node, or create it with `new XxxMain()` and add it to the tree. Its `_Ready` builds the UI, and its `_ExitTree` disposes the UI and restores any `UIConfig` values it changed.

Differences from the Unity examples:

- Unity's screen-space `UIPanel` (FitScreen = FitSize) is replaced by `DemoUtil.CreateMainView`, which calls `UIPackage.CreateObject`, then `MakeFullScreen`, then `GRoot.inst.AddChild`.
- HeadBar and EmitNumbers used 3D NPC models. Here the NPCs are 2D placeholders, and their head bars are world-space `UIPanel` nodes.
- Curve rendered its UI onto a curved 3D mesh. Here the UI is shown flat.
- TurnCard's perspective is an affine approximation.
- HitTest's 3D cube is replaced by a 2D square.
- Not ported, because they need 3D, RenderTexture or TextMeshPro, or their packages are not shipped: Model, Particles, RenderTexture, TextMeshPro, Gesture, BundleUsage, Perspective, TurnPage.

## Running the smoke test

```sh
dotnet build FairyGUIForGodot.csproj
godot --headless --path . --import
godot --headless --path . res://tests/SmokeTest.tscn
godot --headless --path . res://tests/SmokeTest.tscn -- --skip-demos   # skip the phase that runs every example
```

The smoke test runs these phases:

1. Loads every package in `res://demo/UI`.
2. Instantiates every exported component and adds it to GRoot. Then it switches every controller page, plays every transition, and disposes the component.
3. Tests GList virtual and loop scrolling, Transition (play, hook, stop, repeat), Controller, GTween, Window, modal wait and popups.
4. Tests InputTextField focus and typing (Latin and CJK), Backspace and Enter. The keys go through `Viewport.PushInput`, then `StageEngine`, then `Stage`.
5. Runs every example for a few frames and clicks its buttons. Then it clicks through the `DemoMain` menu with real mouse events.

Each check, and each engine error that occurs while it runs, is logged with the package or component name. Engine errors include `GD.PushError` calls and unhandled exceptions in callbacks. At the end the test prints a `[SMOKE] PASS` or `[SMOKE] FAIL` summary.

Exit codes:

| Code | Meaning |
|---|---|
| `0` | All checks passed |
| `1` | At least one check failed or an engine error occurred |
| `3` | The watchdog killed the run: the main loop stalled for 45 s, or quitting hung |

The test cannot hang. Each phase has a 90 s / 9000 frame budget, and the whole run has a 240 s / 30000 frame budget.

The headless display server reports a 0x0 window. `DemoUtil.EnsureInit` therefore sets the root viewport to the 1136x640 design size before the Stage is created.

## Using FairyGUI from your own scripts

FairyGUI and Godot both define some type names: `Window`, `PopupMenu`, `Image`, `Container` and `InputEvent`. If a file has both `using Godot;` and `using FairyGUI;`, add aliases for the names it uses:

```csharp
using Godot;
using FairyGUI;
using Window = FairyGUI.Window;
using PopupMenu = FairyGUI.PopupMenu;
using InputEvent = FairyGUI.InputEvent;
```

Paths are Godot resource paths: `UIPackage.AddPackage("res://demo/UI/Basics")`. A GLoader with an external URL also needs the full resource path, for example `res://demo/Icons/k0.png`.

## Exporting

`*.bytes` and `*.fui` files are not Godot resources, so by default the exporter leaves them out. In the export preset, open **Resources → Filters to export non-resource files/folders** and add:

```
*.bytes, *.fui
```

Textures, sounds and fonts are imported resources and are exported automatically.
