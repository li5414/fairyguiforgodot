// FairyGUI for Godot - export plugin (not a port of a Unity file).
// FairyGUI package descriptions (xxx_fui.bytes, xxx.fui) are not Godot resources, so a default export skips them and
// UIPackage.AddPackage fails in the exported game. This plugin packs them as extra files unless the preset's
// "Filters to export non-resource files" already includes them (Godot does not deduplicate the two).
// Atlases, sounds and fonts of a package are imported resources and are exported normally with "Export all resources";
// with the other export modes they are only exported if they are selected, because nothing references them statically.
#if TOOLS
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    [Tool]
    public partial class FairyGUIExportPlugin : EditorExportPlugin
    {
        public override string _GetName()
        {
            return "FairyGUI";
        }

        public override void _ExportBegin(string[] features, bool isDebug, string path, uint flags)
        {
            EditorExportPreset preset = GetExportPreset();
            string[] includeFilters = preset != null ? preset.GetIncludeFilter().Split(',', System.StringSplitOptions.RemoveEmptyEntries) : new string[0];

            var files = new List<string>();
            CollectPackages("res://", files);

            int added = 0;
            foreach (string file in files)
            {
                if (MatchesFilter(file, includeFilters))
                    continue; // the preset already exports it
                byte[] data = FileAccess.GetFileAsBytes(file);
                if (data == null || data.Length == 0)
                {
                    GD.PushWarning("FairyGUI export: cannot read " + file);
                    continue;
                }
                AddFile(file, data, false);
                added++;
            }

            if (files.Count > 0 && preset != null && (int)preset.GetExportFilter() != 0)
                GD.PushWarning("FairyGUI export: the preset does not export all resources. Make sure the atlases, sounds and fonts of the "
                    + files.Count + " FairyGUI package(s) are selected, UIPackage loads them by path at run time.");

            if (added > 0)
                GD.Print("FairyGUI export: packed " + added + " package description file(s)");
        }

        static bool MatchesFilter(string file, string[] filters)
        {
            string relative = file.TrimPrefix("res://");
            foreach (string f in filters)
            {
                string filter = f.StripEdges();
                if (filter.Length == 0)
                    continue;
                if (file.MatchN(filter) || relative.MatchN(filter))
                    return true;
            }
            return false;
        }

        static void CollectPackages(string dir, List<string> result)
        {
            using DirAccess da = DirAccess.Open(dir);
            if (da == null)
                return;
            if (da.FileExists(".gdignore"))
                return;

            da.IncludeHidden = false;
            da.ListDirBegin();
            for (string name = da.GetNext(); name != ""; name = da.GetNext())
            {
                if (name == "." || name == "..")
                    continue;
                string full = dir.PathJoin(name);
                if (da.CurrentIsDir())
                {
                    if (name != ".godot")
                        CollectPackages(full, result);
                }
                else if (name.EndsWith("_fui.bytes") || name.EndsWith(".fui"))
                    result.Add(full);
            }
            da.ListDirEnd();
        }
    }
}
#endif
