shader_type spatial;
render_mode blend_mix, depth_prepass_alpha, cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec4 base_color : source_color = vec4(0.4, 0.42, 0.38, 1.0);
uniform float roughness_value : hint_range(0.0, 1.0) = 0.9;
uniform float metallic_value : hint_range(0.0, 1.0) = 0.0;
uniform float cutaway_alpha : hint_range(0.0, 1.0) = 1.0;

void fragment() {
	ALBEDO = base_color.rgb;
	ROUGHNESS = roughness_value;
	METALLIC = metallic_value;
	ALPHA = base_color.a * cutaway_alpha;
}
