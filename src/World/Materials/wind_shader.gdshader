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

// Root position - this is the base position of the entire decoration
// We'll use this to calculate the distance from the center for each vertex
uniform vec3 root_position = vec3(0.0, 0.0, 0.0);

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
vec3 applyWind(vec3 vertex, vec3 normal, float height_factor, float blade_id) {
    // Only apply wind to vertices above a certain height
    // This keeps the base of grass/flowers stable while the tops sway
    if (height_factor <= 0.1) {
        return vertex;
    }

    // Gradually increase effect from bottom to top
    // Use a strong power curve to concentrate effect at the very top
    float adjusted_factor = pow(height_factor, 2.0);

    // Calculate wind direction vector
    vec2 wind_vec = vec2(cos(wind_direction), sin(wind_direction));

    // Create a unique phase offset for each blade based on its ID
    // This ensures different blades move with different timing
    float phase_offset = blade_id * 1.5;

    // Create a time-based oscillation with phase offset
    // This creates a simple back-and-forth swaying motion that varies per blade
    float wind_time_factor = sin(wind_time * 1.5 + phase_offset);

    // Add gustiness that varies per blade
    float gust = mix(1.0, smoothNoise(vec2(wind_time * 0.1, blade_id * 10.0)), wind_gustiness);

    // Calculate displacement amount
    float displacement_amount = wind_strength * adjusted_factor * gust;

    // Create a simple directional displacement
    // This will make each grass blade sway back and forth independently
    vec3 displacement = vec3(
        wind_vec.x * displacement_amount * wind_time_factor,
        0.0, // No vertical displacement initially
        wind_vec.y * displacement_amount * wind_time_factor
    );

    // Add a small amount of vertical movement
    // This creates a slight bobbing effect as the grass sways
    displacement.y = cos(wind_time * 2.0 + phase_offset) * 0.02 * displacement_amount;

    return vertex + displacement;
}

void vertex() {
    // For grass decorations, we need to identify individual blades
    // and apply wind to each blade independently

    // Get the vertex position
    vec3 pos = VERTEX;

    // Calculate height factor based on absolute Y position
    float y_pos = pos.y;
    float max_height = 3.0; // Covers most decoration heights
    float height_factor = clamp(y_pos / max_height, 0.0, 1.0);

    // Identify which blade this vertex belongs to based on its XZ position
    // We'll use a grid-based approach to group vertices into blades
    // Looking at the grass model, we can see distinct blades at different XZ positions

    // Round XZ position to the nearest 0.1 to group vertices into blades
    // This creates a unique ID for each blade based on its position
    vec2 blade_pos = floor(pos.xz * 10.0) / 10.0;

    // Create a unique ID for each blade
    // We'll use a simple hash of the XZ position
    float blade_id = fract(sin(dot(blade_pos, vec2(12.9898, 78.233))) * 43758.5453);

    // Apply wind effect with height factor and blade ID
    // This ensures each blade moves independently
    VERTEX = applyWind(VERTEX, NORMAL, height_factor, blade_id);

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
