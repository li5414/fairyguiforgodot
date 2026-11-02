// FairyGUI for Godot - painting mode material (no Unity counterpart file).
// Unity draws the captured RenderTexture with FairyGUI/Image. Godot draws the object as a CanvasGroup: this shader is
// the CanvasGroup material and reads the composited group from the back buffer (like Godot's default CanvasGroup
// shader), then applies GRAYED / COLOR_FILTER / blend_op exactly like fairygui_image.gdshader.
shader_type canvas_item;
render_mode unshaded, blend_mix;

#include "fairygui_common.gdshaderinc"

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

void fragment() {
	vec4 c = textureLod(screen_texture, SCREEN_UV, 0.0);
	// the back buffer holds premultiplied color, the FairyGUI effects expect straight alpha
	if (c.a > 0.0001) {
		c.rgb /= c.a;
	}
	// Not `COLOR * c` (Godot's default CanvasGroup shader): the group's children were drawn into the back buffer with
	// every ancestor alpha already applied, and COLOR carries that same modulate again. Unity applies it once.
	COLOR = fgui_process(c);
}
