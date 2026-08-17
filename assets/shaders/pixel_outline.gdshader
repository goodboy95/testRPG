shader_type canvas_item;

uniform vec4 outline_color : source_color = vec4(0.95, 0.72, 0.28, 1.0);
uniform bool high_contrast = false;

void fragment() {
    vec4 base = texture(TEXTURE, UV);
    float neighbor_alpha = 0.0;
    neighbor_alpha = max(neighbor_alpha, texture(TEXTURE, UV + vec2(TEXTURE_PIXEL_SIZE.x, 0.0)).a);
    neighbor_alpha = max(neighbor_alpha, texture(TEXTURE, UV - vec2(TEXTURE_PIXEL_SIZE.x, 0.0)).a);
    neighbor_alpha = max(neighbor_alpha, texture(TEXTURE, UV + vec2(0.0, TEXTURE_PIXEL_SIZE.y)).a);
    neighbor_alpha = max(neighbor_alpha, texture(TEXTURE, UV - vec2(0.0, TEXTURE_PIXEL_SIZE.y)).a);
    float outer_alpha = 0.0;
    outer_alpha = max(outer_alpha, texture(TEXTURE, UV + vec2(TEXTURE_PIXEL_SIZE.x * 2.0, 0.0)).a);
    outer_alpha = max(outer_alpha, texture(TEXTURE, UV - vec2(TEXTURE_PIXEL_SIZE.x * 2.0, 0.0)).a);
    outer_alpha = max(outer_alpha, texture(TEXTURE, UV + vec2(0.0, TEXTURE_PIXEL_SIZE.y * 2.0)).a);
    outer_alpha = max(outer_alpha, texture(TEXTURE, UV - vec2(0.0, TEXTURE_PIXEL_SIZE.y * 2.0)).a);
    vec4 outline = vec4(outline_color.rgb, outline_color.a * neighbor_alpha);
    vec4 outer = vec4(0.0, 0.0, 0.0, outer_alpha);
    vec4 stroke = high_contrast && neighbor_alpha <= 0.0 ? outer : outline;
    COLOR = mix(stroke, base, base.a);
}
