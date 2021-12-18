shader_type canvas_item;

uniform vec4 shadow_color = vec4(0., 0., 0., 0.5);
uniform float shadow_angle = 1.7;
uniform float shadow_offset = 0.312;
uniform float widen = 0.586;
uniform float distance_from_ground = 0.0;
void fragment() {
        vec4 original = texture(TEXTURE, UV);
        vec2 shadow_uv = vec2( widen * (UV.x * (shadow_angle + UV.y) - shadow_offset), UV.y + distance_from_ground);
        vec4 shadow = texture(TEXTURE, shadow_uv);
        shadow.rgba = shadow_color * shadow.a;
        COLOR = (original + shadow);
}
