// FairyGUI for Godot - port of Unity "FairyGUI/Image" (Resources/Shaders/FairyGUI-Image.shader).
// COLOR arrives as texture * vertex color * modulate (Godot does the texture fetch before fragment()).
// Unity's COMBINED (separate alpha texture) is not needed: Godot textures carry their own alpha.
shader_type canvas_item;
render_mode blend_mix;

#include "fairygui_common.gdshaderinc"

void fragment() {
	COLOR = fgui_process(COLOR);
}
