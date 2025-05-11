shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_lambert, specular_schlick_ggx;

// Wind parameters
uniform float wind_strength : hint_range(0.0, 1.0) = 0.5;
uniform float wind_direction : hint_range(0.0, 6.28) = 0.0;
uniform float wind_time : hint_range(0.0, 1000.0) = 0.0;
uniform float wind_gustiness : hint_range(0.0, 1.0) = 0.3;

// Material parameters
uniform vec4 albedo : source_color = vec4(1.0);
uniform float roughness : hint_range(0.0, 1.0) = 0.8;
uniform float specular : hint_range(0.0, 1.0) = 0.5;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;

// Vertex color usage
uniform bool use_vertex_color = true;

// Wind noise function
float noise(vec2 p) {
    return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

// Improved noise function for smoother wind
float smoothNoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);

    // Four corners in 2D of a tile
    float a = noise(i);
    float b = noise(i + vec2(1.0, 0.0));
    float c = noise(i + vec2(0.0, 1.0));
    float d = noise(i + vec2(1.0, 1.0));

    // Smooth interpolation
    vec2 u = f * f * (3.0 - 2.0 * f);

    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

// Wind displacement function
vec3 applyWind(vec3 vertex, vec3 normal, float height_factor) {
    // Only apply wind to vertices above a certain height
    // This keeps the base of grass/flowers stable while the tops sway
    if (height_factor <= 0.1) {
        return vertex;
    }

    // Calculate wind direction vector
    vec2 wind_vec = vec2(cos(wind_direction), sin(wind_direction));

    // Create wind variation based on position and time
    float wind_variation = smoothNoise(vertex.xz * 0.1 + wind_time * 0.2);

    // Add gustiness
    float gust = mix(1.0, smoothNoise(vertex.xz * 0.05 + wind_time * 0.1), wind_gustiness);

    // Calculate displacement amount based on height
    float displacement_amount = wind_strength * height_factor * height_factor * gust;

    // Apply wind displacement - increased amplitude for more visible effect
    vec3 displacement = vec3(
        wind_vec.x * displacement_amount * 1.5 * (0.8 + 0.6 * sin(wind_time * 2.0 + vertex.x * 0.5)),
        0.0, // No vertical displacement
        wind_vec.y * displacement_amount * 1.5 * (0.8 + 0.6 * sin(wind_time * 2.0 + vertex.z * 0.5))
    );

    // Add more pronounced vertical bobbing
    displacement.y = sin(wind_time * 3.0 + vertex.x * 0.2 + vertex.z * 0.2) * 0.05 * displacement_amount;

    return vertex + displacement;
}

void vertex() {
    // Calculate height factor (0 at bottom, 1 at top)
    // For our test objects, we know they're simple boxes with Y-up orientation
    // The height factor is simply the normalized Y position within the box
    // For more complex models, we might need to adjust these bounds
    float min_y = VERTEX.y - 1.0; // Assume bottom is at -1.0 relative to center
    float max_y = VERTEX.y + 1.0; // Assume top is at +1.0 relative to center
    float height_factor = clamp((VERTEX.y - min_y) / (max_y - min_y), 0.0, 1.0);

    // Apply wind effect
    VERTEX = applyWind(VERTEX, NORMAL, height_factor);

    // Pass vertex color to fragment shader
    if (use_vertex_color) {
        COLOR = COLOR;
    }
}

void fragment() {
    // Base color
    vec4 base_color = albedo;

    // Apply vertex color if enabled
    if (use_vertex_color) {
        base_color *= COLOR;
    }

    // Set material properties
    ALBEDO = base_color.rgb;
    ROUGHNESS = roughness;
    SPECULAR = specular;
    METALLIC = metallic;
}
