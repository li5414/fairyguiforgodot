// FairyGUI for Godot - port of Unity "FairyGUI/BlurFilter" (Resources/Shaders/AddOn/FairyGUI-BlurFilter.shader).
// Unity: down sample to 1/8 size, then two four-tap cone passes with offsets (blurSize * i + 0.5) texels (i = 0, 1).
// Godot: used as the CanvasGroup material of the blurred object (BlurFilter enables back buffer mipmaps). The 1/8 size
// image is mipmap level 3 of the back buffer; the two cone passes are combined into one 16-tap pass.
shader_type canvas_item;
render_mode unshaded, blend_mix;

#include "fairygui_common.gdshaderinc"

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform float blur_size = 1.0;

void fragment() {
	vec4 c;
	if (blur_size < 0.01) {
		c = textureLod(screen_texture, SCREEN_UV, 0.0);
	} else {
		float lod = 3.0 * min(blur_size, 1.0);
		vec2 texel = SCREEN_PIXEL_SIZE * exp2(lod);
		float off0 = 0.5;
		float off1 = blur_size + 0.5;
		vec2 dirs[4] = vec2[](vec2(-1.0, -1.0), vec2(-1.0, 1.0), vec2(1.0, 1.0), vec2(1.0, -1.0));
		c = vec4(0.0);
		for (int i = 0; i < 4; i++) {
			for (int j = 0; j < 4; j++) {
				vec2 o = (dirs[i] * off0 + dirs[j] * off1) * texel;
				c += textureLod(screen_texture, SCREEN_UV + o, lod);
			}
		}
		c *= 1.0 / 16.0;
	}
	// the back buffer holds premultiplied color, the FairyGUI effects expect straight alpha
	if (c.a > 0.0001) {
		c.rgb /= c.a;
	}
	// Not `COLOR * c`: the back buffer already contains the ancestor alpha (see fairygui_painting.gdshader).
	COLOR = fgui_process(c);
}
