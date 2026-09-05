shader_type spatial;
render_mode blend_mix, depth_prepass_alpha, cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec4 base_color : source_color = vec4(0.4, 0.42, 0.38, 1.0);
uniform sampler2D albedo_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D normal_texture : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D orm_texture : filter_linear_mipmap_anisotropic, repeat_enable;
uniform bool use_albedo_texture = false;
uniform bool use_normal_texture = false;
uniform bool use_orm_texture = false;
uniform vec2 uv_scale = vec2(1.0);
uniform vec2 uv_offset = vec2(0.0);
uniform float roughness_value : hint_range(0.0, 1.0) = 0.9;
uniform float metallic_value : hint_range(0.0, 1.0) = 0.0;
uniform float normal_scale : hint_range(-16.0, 16.0) = 1.0;
uniform float cutaway_alpha : hint_range(0.0, 1.0) = 1.0;

void fragment() {
	vec2 surface_uv = UV * uv_scale + uv_offset;
	vec4 albedo_sample = use_albedo_texture
		? texture(albedo_texture, surface_uv)
		: vec4(1.0);
	vec3 orm_sample = use_orm_texture
		? texture(orm_texture, surface_uv).rgb
		: vec3(1.0, 1.0, 0.0);
	ALBEDO = base_color.rgb * albedo_sample.rgb;
	AO = use_orm_texture ? orm_sample.r : 1.0;
	ROUGHNESS = use_orm_texture ? orm_sample.g * roughness_value : roughness_value;
	METALLIC = use_orm_texture ? orm_sample.b * metallic_value : metallic_value;
	if (use_normal_texture) {
		NORMAL_MAP = texture(normal_texture, surface_uv).rgb;
		NORMAL_MAP_DEPTH = normal_scale;
	}
	ALPHA = base_color.a * albedo_sample.a * cutaway_alpha;
}
