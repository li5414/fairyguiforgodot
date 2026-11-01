// FairyGUI for Godot - port of Unity "FairyGUI/BMFont" (Resources/Shaders/FairyGUI-BMFont.shader).
// Bitmap fonts whose glyphs are packed into texture channels: col = vertex color, col.a *= tex[channel].
// Godot triangle arrays have a single uv channel, so NGraphics encodes the channel into u: u' = u + 2 * channel
// (channel 0-3 = r,g,b,a; any other value, e.g. 15 "all channels", uses alpha).
shader_type canvas_item;
render_mode blend_mix;

#include "fairygui_common.gdshaderinc"

varying vec4 fgui_vertex_color;

void vertex() {
	// vertex color * modulate, before the texture is applied
	fgui_vertex_color = COLOR;
}

void fragment() {
	float channel = floor(UV.x * 0.5);
	vec2 uv = vec2(UV.x - channel * 2.0, UV.y);
	vec4 tcol = texture(TEXTURE, uv);
	int c = int(channel + 0.5);
	float a;
	if (c == 0)
		a = tcol.r;
	else if (c == 1)
		a = tcol.g;
	else if (c == 2)
		a = tcol.b;
	else
		a = tcol.a;

	vec4 col = fgui_vertex_color;
	col.a *= a;
	COLOR = fgui_process(col);
}
