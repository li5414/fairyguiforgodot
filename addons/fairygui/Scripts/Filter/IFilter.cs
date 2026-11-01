// Ported from FairyGUI-unity: Assets/Scripts/Filter/IFilter.cs
using System;
using System.Collections.Generic;
using Godot;

namespace FairyGUI
{
    public interface IFilter
    {
        /// <summary>
        ///
        /// </summary>
        DisplayObject target { get; set; }

        /// <summary>
        ///
        /// </summary>
        void Update();

        /// <summary>
        ///
        /// </summary>
        void Dispose();
    }
}
