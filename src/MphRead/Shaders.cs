namespace MphRead
{
    public static class Shaders
    {
        public static string VertexShader { get; } = @"
#version 120
uniform bool use_light;
uniform bool use_texture;
uniform bool show_colors;
uniform bool fog_enable;
uniform vec3 light1vec;
uniform vec3 light1col;
uniform vec3 light2vec;
uniform vec3 light2col;
uniform vec3 diffuse;
uniform vec3 ambient;
uniform vec3 specular;
uniform vec4 fog_color;
uniform float far_plane;
uniform mat4 proj_mtx;
uniform mat4 view_mtx;
uniform mat4 model_mtx;
uniform mat4 tex_mtx;
uniform int texgen_mode;

varying vec2 texcoord;
varying vec4 color;

vec3 light_calc(vec3 light_vec, vec3 light_col, vec3 normal_vec, vec3 dif_col, vec3 amb_col, vec3 spe_col)
{
    vec3 sight_vec = vec3(0.0, 0.0, -1.0);
    float dif_factor = max(0.0, -dot(light_vec, normal_vec));
    vec3 half_vec = (light_vec + sight_vec) / 2.0;
    float spe_factor = max(0.0, dot(-half_vec, normal_vec));
    spe_factor = spe_factor * spe_factor;
    vec3 spe_out = spe_col * light_col * spe_factor;
    vec3 dif_out = dif_col * light_col * dif_factor;
    vec3 amb_out = amb_col * light_col;
    return spe_out + dif_out + amb_out;
}

void main()
{
    gl_Position = proj_mtx * view_mtx * model_mtx * gl_Vertex;
    vec4 vtx_color = show_colors ? gl_Color : vec4(1.0);
    vec3 normal = normalize(mat3(model_mtx) * gl_Normal);
    if (use_light) {
        vec3 dif_current = diffuse;
        vec3 amb_current = ambient;
        if (gl_Color.a == 0.0) {
            // see comment on DIF_AMB
            dif_current = vtx_color.rgb;
            amb_current = vec3(0.0, 0.0, 0.0);
        }
        vec3 col1 = light_calc(light1vec, light1col, normal, dif_current, amb_current, specular);
        vec3 col2 = light_calc(light2vec, light2col, normal, dif_current, amb_current, specular);
        color = vec4(min((col1 + col2), vec3(1.0, 1.0, 1.0)), 1.0);
    }
    else {
        // alpha will only be less than 1.0 here if DIF_AMB is used but lighting is disabled
        color = vec4(vtx_color.rgb, 1.0);
    }
    if (use_texture) {
        // texgen mode: 0 - none, 1 - texcoord, 2 - normal, 3 - vertex
        if (texgen_mode == 0 || texgen_mode == 1) {
            texcoord = vec2(tex_mtx * gl_MultiTexCoord0);
        }
        else if (texgen_mode == 2 || texgen_mode == 3) {
            mat2x4 texgen_mtx = mat2x4(
                vec4(tex_mtx[0][0], tex_mtx[0][1], tex_mtx[0][2], gl_MultiTexCoord0.x),
                vec4(tex_mtx[1][0], tex_mtx[1][1], tex_mtx[1][2], gl_MultiTexCoord0.y)
            );
            if (texgen_mode == 2) {
                texcoord = vec4(gl_Normal, 1.0) * texgen_mtx;
            }
            else {
                texcoord = vec4(gl_Vertex.xyz, 1.0) * texgen_mtx;
            }
        }
    }
    else {
        texcoord = vec2(0.0, 0.0);
    }
}
";

        public static string FragmentShader { get; } = @"
#version 120
uniform bool use_texture;
uniform bool fog_enable;
uniform vec4 fog_color;
uniform float fog_min;
uniform float fog_max;
uniform float far_dist;
uniform sampler2D tex;
uniform bool use_override;
uniform vec4 override_color;
uniform float mat_alpha;
uniform int mat_mode;
uniform vec3[32] toon_table;

varying vec2 texcoord;
varying vec4 color;

vec4 toon_color(vec4 vtx_color)
{
    return vec4(toon_table[int(vtx_color.r * 31)], vtx_color.a);
}

void main()
{
    // mat_mode: 0 - modulate, 1 - decal, 2 - toon
    vec4 col;
    if (use_texture) {
        vec4 texcolor = texture2D(tex, texcoord);
        if (mat_mode == 1) {
            col = vec4(
                (texcolor.r * texcolor.a + color.r * (1 - texcolor.a)),
                (texcolor.g * texcolor.a + color.g * (1 - texcolor.a)),
                (texcolor.b * texcolor.a + color.b * (1 - texcolor.a)),
                mat_alpha * color.a
            );
        }
        else if (mat_mode == 2) {
            vec4 toon = toon_color(color);
            col = vec4(texcolor.rgb * toon.rgb + toon.rgb, mat_alpha * texcolor.a * color.a);
        }
        else {
            col = color * vec4(texcolor.rgb, mat_alpha * texcolor.a);
        }
        if (use_override) {
            col.r = override_color.r;
            col.g = override_color.g;
            col.b = override_color.b;
            col.a *= override_color.a;
        }
    }
    else if (use_override) {
        col = override_color;
    }
    else {
        col = mat_mode == 2 ? toon_color(color) : color;
        col.a *= mat_alpha;
    }
    if (fog_enable) {
        float depth = gl_FragCoord.z;
        float density = 0.0;
        if (depth >= fog_max) {
            density = 1.0;
        }
        else if (depth > fog_min) {
            // MPH fog table has min 0 and max 124
            density = (depth - fog_min) / (fog_max - fog_min) * 124.0 / 128.0;
        }
        gl_FragColor = (col * (1.0 - density) + fog_color * density);
    }
    else {
        gl_FragColor = col;
    }
}
";
    }

    public class ShaderLocations
    {
        public int UseLight { get; set; }
        public int ShowColors { get; set; }
        public int UseTexture { get; set; }
        public int Light1Color { get; set; }
        public int Light1Vector { get; set; }
        public int Light2Color { get; set; }
        public int Light2Vector { get; set; }
        public int Diffuse { get; set; }
        public int Ambient { get; set; }
        public int Specular { get; set; }
        public int UseFog { get; set; }
        public int FogColor { get; set; }
        public int FogMinDistance { get; set; }
        public int FogMaxDistance { get; set; }
        public int FarDistance { get; set; }
        public int UseOverride { get; set; }
        public int OverrideColor { get; set; }
        public int MaterialAlpha { get; set; }
        public int MaterialMode { get; set; }
        public int ModelMatrix { get; set; }
        public int ViewMatrix { get; set; }
        public int ProjectionMatrix { get; set; }
        public int TextureMatrix { get; set; }
        public int TexgenMode { get; set; }
        public int ToonTable { get; set; }
    }
}
