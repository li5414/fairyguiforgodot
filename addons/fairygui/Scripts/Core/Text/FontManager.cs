// Ported from FairyGUI-unity: Assets/Scripts/Core/Text/FontManager.cs
using System;
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    /// <summary>
    ///
    /// </summary>
    /// <remarks>
    /// [Godot] Font name resolution (also used for UIConfig.defaultFont):
    /// 1. ui://... : a font of a loaded package (BitmapFont), checked before the registry as in Unity, so a fallback
    ///    cached for a package that was not loaded yet does not hide the package font later;
    /// 2. a font registered with RegisterFont (name or alias), or resolved before;
    /// 3. a resource path (res://, user://, uid://...) loaded as Godot.Font (FontFile, SystemFont, FontVariation...);
    /// 4. a system font name, or several names separated by commas: SystemFont { FontNames = names };
    /// 5. otherwise the font of UIConfig.defaultFont if it was resolved before, else ThemeDB.FallbackFont.
    /// Unity loads Resources/name and Resources/Fonts/name, then Font.CreateDynamicFontFromOSFont, then the built-in Arial.
    /// </remarks>
    public class FontManager
    {
        public static Dictionary<string, BaseFont> sFontFactory = new Dictionary<string, BaseFont>();

        /// <summary>
        ///
        /// </summary>
        /// <param name="font"></param>
        /// <param name="alias"></param>
        static public void RegisterFont(BaseFont font, string alias = null)
        {
            sFontFactory[font.name] = font;
            if (alias != null)
                sFontFactory[alias] = font;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="font"></param>
        static public void UnregisterFont(BaseFont font)
        {
            List<string> toDelete = new List<string>();
            foreach (KeyValuePair<string, BaseFont> kv in sFontFactory)
            {
                if (kv.Value == font)
                    toDelete.Add(kv.Key);
            }

            foreach (string key in toDelete)
                sFontFactory.Remove(key);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        static public BaseFont GetFont(string name)
        {
            BaseFont font;
            if (name.StartsWith(UIPackage.URL_PREFIX))
            {
                font = UIPackage.GetItemAssetByURL(name) as BaseFont;
                if (font != null)
                    return font;
            }

            if (sFontFactory.TryGetValue(name, out font))
                return font;

            // [Godot] Unity tries Resources.Load/OS fonts with the ui:// name, which cannot succeed.
            if (name.StartsWith(UIPackage.URL_PREFIX))
                return Fallback(name);

            Godot.Font asset = LoadFontAsset(name);

            if (asset == null)
                return Fallback(name);

            font = new DynamicFont();
            font.name = name;
            sFontFactory.Add(name, font);

            ((DynamicFont)font).nativeFont = asset;

            return font;
        }

        /// <summary>
        /// [Godot] Replaces Resources.Load(name), Resources.Load("Fonts/" + name) and Font.CreateDynamicFontFromOSFont.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>null if the font cannot be found.</returns>
        static Godot.Font LoadFontAsset(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (name.IndexOf("://") != -1)
            {
                if (!ResourceLoader.Exists(name))
                    return null;

                Godot.Font font = ResourceLoader.Load(name) as Godot.Font;
                if (font == null)
                    GD.PushWarning("FairyGUI: '" + name + "' is not a font resource.");
                return font;
            }

            string[] arr;
            if (name.IndexOf(",") != -1)
            {
                arr = name.Split(',');
                int cnt = arr.Length;
                for (int i = 0; i < cnt; i++)
                    arr[i] = arr[i].Trim();
            }
            else
                arr = new string[] { name };

            // [Godot] Unity's CreateDynamicFontFromOSFont always succeeds (unknown names render with the OS default font).
            // SystemFont would silently use the default theme font for unknown names, so the names are checked first and
            // an unknown font goes to Fallback (UIConfig.defaultFont). OS.GetSystemFontPath is not implemented on Web:
            // there every system font name falls back.
            bool found = false;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].Length > 0 && !string.IsNullOrEmpty(OS.GetSystemFontPath(arr[i], 400, 100, false)))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
                return null;

            SystemFont systemFont = new SystemFont();
            systemFont.FontNames = arr;
            return systemFont;
        }

        static BaseFont Fallback(string name)
        {
            if (name != UIConfig.defaultFont)
            {
                BaseFont ff;
                if (sFontFactory.TryGetValue(UIConfig.defaultFont, out ff))
                {
                    sFontFactory[name] = ff;
                    return ff;
                }
            }

            // [Godot] Unity: the built-in Arial.ttf. Godot: the project/engine fallback font
            // (gui/theme/custom_font, or the engine default font).
            Godot.Font asset = ThemeDB.FallbackFont;
            if (asset == null)
                throw new Exception("Failed to load font '" + name + "'");

            BaseFont font = new DynamicFont();
            font.name = name;
            ((DynamicFont)font).nativeFont = asset;

            sFontFactory.Add(name, font);
            return font;
        }

        /// <summary>
        ///
        /// </summary>
        static public void Clear()
        {
            foreach (KeyValuePair<string, BaseFont> kv in sFontFactory)
                kv.Value.Dispose();

            sFontFactory.Clear();
        }

        // [Godot] Unity's InitializeOnLoad (RuntimeInitializeOnLoadMethod, domain reload off) is not needed: Godot reloads
        // the C# assembly, which resets the static state. The TextMeshPro check is removed (TMPFont is not ported).
    }
}
