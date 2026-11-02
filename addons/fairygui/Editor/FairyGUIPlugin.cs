// FairyGUI for Godot - editor plugin (not a port of a Unity file).
// Enabling the plugin registers the "FairyGUI" autoload (StageEngine, the frame loop and input driver) and adds an
// export plugin that packs FairyGUI package files (*_fui.bytes / *.fui) into exports.
// Without the autoload the runtime still works: Stage.Instantiate() creates the engine on first use.
#if TOOLS
using Godot;

namespace FairyGUI
{
    [Tool]
    public partial class FairyGUIPlugin : EditorPlugin
    {
        const string AutoloadName = "FairyGUI";

        FairyGUIExportPlugin _exportPlugin;

        public override void _EnablePlugin()
        {
            if (!ProjectSettings.HasSetting("autoload/" + AutoloadName))
                AddAutoloadSingleton(AutoloadName, GetStageEnginePath());
        }

        public override void _DisablePlugin()
        {
            if (ProjectSettings.HasSetting("autoload/" + AutoloadName))
                RemoveAutoloadSingleton(AutoloadName);
        }

        public override void _EnterTree()
        {
            _exportPlugin = new FairyGUIExportPlugin();
            AddExportPlugin(_exportPlugin);
        }

        public override void _ExitTree()
        {
            if (_exportPlugin != null)
            {
                RemoveExportPlugin(_exportPlugin);
                _exportPlugin = null;
            }
        }

        // addons/<folder>/Editor/FairyGUIPlugin.cs -> addons/<folder>/Scripts/Core/StageEngine.cs, so the addon folder
        // can be renamed.
        string GetStageEnginePath()
        {
            Script script = GetScript().As<Script>();
            string editorDir = script != null ? script.ResourcePath.GetBaseDir() : "res://addons/fairygui/Editor";
            return editorDir.GetBaseDir().PathJoin("Scripts/Core/StageEngine.cs");
        }
    }
}
#endif
