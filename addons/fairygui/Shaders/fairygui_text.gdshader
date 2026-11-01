// FairyGUI for Godot - port of Unity "FairyGUI/Text" (Resources/Shaders/FairyGUI-Text.shader).
// Unity uses only the texture alpha (col = vertex color, col.a *= tex.a). Godot glyph textures are white with alpha
// (or full color for color glyphs), and Godot does the texture fetch (plus MSDF/LCD handling) before fragment(),
// so COLOR is already vertex color * glyph; keeping it unchanged also keeps color emoji correct.
shader_type canvas_item;
render_mode blend_mix;

#include "fairygui_common.gdshaderinc"

void fragment() {
	COLOR = fgui_process(COLOR);
}
