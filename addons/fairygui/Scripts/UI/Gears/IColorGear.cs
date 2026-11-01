// Ported from FairyGUI-unity: Assets/Scripts/UI/Gears/IColorGear.cs
using Godot;

namespace FairyGUI
{
    /// <summary>
    /// 
    /// </summary>
    public interface IColorGear
    {
        /// <summary>
        /// 
        /// </summary>
        Color color { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public interface ITextColorGear : IColorGear
    {
        /// <summary>
        /// 
        /// </summary>
        Color strokeColor { get; set; }
    }
}
