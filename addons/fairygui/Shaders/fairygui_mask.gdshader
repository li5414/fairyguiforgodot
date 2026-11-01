// FairyGUI for Godot - alpha mask material (Unity: FairyGUI/Image with the ALPHA_MASK keyword).
// Unity writes the mask into the stencil buffer where alpha > 0.001 (hard edge). In Godot the mask is drawn by a
// CanvasItem with clip_children = ClipChildrenMode.Only; with this material the clipped children (in the back buffer)
// are shown where the mask alpha > 0.001, like the stencil test. `reversed` inverts the test inside the mask geometry
// (a reversed mask also needs a covering shape around the mask, see Container).
shader_type canvas_item;
render_mode unshaded, blend_mix;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;
uniform bool reversed = false;
// unused, declared so the keyword uniforms set by MaterialManager exist
uniform bool ALPHA_MASK = true;
uniform bool GRAYED = false;

void fragment() {
	float m = step(0.001, COLOR.a);
	if (reversed) {
		m = 1.0 - m;
	}
	vec4 c = textureLod(screen_texture, SCREEN_UV, 0.0);
	COLOR = vec4(c.rgb, m);
}
