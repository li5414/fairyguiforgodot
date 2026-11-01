// Ported from FairyGUI-unity: Assets/Scripts/Utils/Html/HtmlParseOptions.cs
using System;
using System.Collections.Generic;
using Godot;

namespace FairyGUI.Utils
{
    /// <summary>
    ///
    /// </summary>
    public class HtmlParseOptions
    {
        /// <summary>
        ///
        /// </summary>
        public bool linkUnderline;

        /// <summary>
        ///
        /// </summary>
        public Color linkColor;

        /// <summary>
        ///
        /// </summary>
        public Color linkBgColor;

        /// <summary>
        ///
        /// </summary>
        public Color linkHoverBgColor;

        /// <summary>
        ///
        /// </summary>
        public bool ignoreWhiteSpace;

        /// <summary>
        ///
        /// </summary>
        public static bool DefaultLinkUnderline = true;

        /// <summary>
        ///
        /// </summary>
        public static Color DefaultLinkColor = UColor.FromBytes(0x3A, 0x67, 0xCC, 0xFF);

        /// <summary>
        ///
        /// </summary>
        public static Color DefaultLinkBgColor = UColor.clear;

        /// <summary>
        ///
        /// </summary>
        public static Color DefaultLinkHoverBgColor = UColor.clear;

        public HtmlParseOptions()
        {
            linkUnderline = DefaultLinkUnderline;
            linkColor = DefaultLinkColor;
            linkBgColor = DefaultLinkBgColor;
            linkHoverBgColor = DefaultLinkHoverBgColor;
        }
    }
}
