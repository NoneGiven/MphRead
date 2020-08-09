namespace MphRead
{
    public static class Shaders
    {
        public static string VertexShader => _vertexShader;
        public static string FragmentShader => _fragmentShader;

        private static readonly string _vertexShader = @"
#version 120
uniform bool is_billboard;
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
    if (is_billboard) {
        gl_Position = proj_mtx * (view_mtx * model_mtx * vec4(0.0, 0.0, 0.0, 1.0) + vec4(gl_Vertex.xyz, 0.0));
    }
    else {
        gl_Position = proj_mtx * view_mtx * model_mtx * gl_Vertex;
    }
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
                texcoord = vec4(normal, 1.0) * texgen_mtx;
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

        private static readonly string _fragmentShader = @"
#version 120
uniform bool use_texture;
uniform bool fog_enable;
uniform vec4 fog_color;
uniform int fog_offset;
uniform sampler2D tex;
uniform bool use_override;
uniform vec4 override_color;
uniform float mat_alpha;
uniform int mat_mode;

varying vec2 texcoord;
varying vec4 color;

vec4 toon_color(vec4 vtx_color)
{
    vec3 toon_table[32] = vec3[](
        vec3(0, 0, 0.2580645),
        vec3(0, 0, 0.2580645),
        vec3(0, 0.032258064, 0.2580645),
        vec3(0.032258064, 0.032258064, 0.2580645),
        vec3(0.032258064, 0.032258064, 0.2580645),
        vec3(0.032258064, 0.06451613, 0.2580645),
        vec3(0.032258064, 0.06451613, 0.29032257),
        vec3(0.032258064, 0.09677419, 0.29032257),
        vec3(0.032258064, 0.09677419, 0.29032257),
        vec3(0.06451613, 0.09677419, 0.29032257),
        vec3(0.06451613, 0.12903225, 0.29032257),
        vec3(0.06451613, 0.12903225, 0.29032257),
        vec3(0.09677419, 0.19354838, 0.32258064),
        vec3(0.12903225, 0.22580644, 0.3548387),
        vec3(0.16129032, 0.2580645, 0.38709676),
        vec3(0.19354838, 0.32258064, 0.41935483),
        vec3(0.22580644, 0.3548387, 0.4516129),
        vec3(0.2580645, 0.38709676, 0.48387095),
        vec3(0.29032257, 0.4516129, 0.516129),
        vec3(0.32258064, 0.48387095, 0.5483871),
        vec3(0.3548387, 0.516129, 0.58064514),
        vec3(0.3548387, 0.58064514, 0.61290324),
        vec3(0.38709676, 0.61290324, 0.6451613),
        vec3(0.41935483, 0.6451613, 0.67741936),
        vec3(0.4516129, 0.7096774, 0.7096774),
        vec3(0.48387095, 0.7419355, 0.7419355),
        vec3(0.516129, 0.7741935, 0.7741935),
        vec3(0.5483871, 0.83870965, 0.8064516),
        vec3(0.58064514, 0.87096775, 0.83870965),
        vec3(0.61290324, 0.9032258, 0.87096775),
        vec3(0.6451613, 0.9677419, 0.9032258),
        vec3(0.67741936, 1, 0.9354839)
    );
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
        else {
            col = (mat_mode == 2 ? toon_color(color) : color) * vec4(texcolor.rgb, mat_alpha * texcolor.a);
        }
        if (use_override) {
            col.r = override_color.r;
            col.g = override_color.g;
            col.b = override_color.b;
            col.a *= override_color.a;
        }
    }
    else {
        col = use_override ? override_color : (mat_mode == 2 ? toon_color(color) : color);
    }
    if (fog_enable) {
        float ndcDepth = (2.0 * gl_FragCoord.z - gl_DepthRange.near - gl_DepthRange.far) / (gl_DepthRange.far - gl_DepthRange.near);
        float clipDepth = ndcDepth / gl_FragCoord.w;
        float depth = clamp(clipDepth / 128.0, 0.0, 1.0);
        float density = depth - (1.0 - float(fog_offset) / 65536.0);
        if (density < 0.0) {
            density = 0.0;
        }
        // adjust fog slope
        density = sqrt(density / 32.0);
        gl_FragColor = vec4((col * (1.0 - density) + fog_color * density).xyz, col.a);
        // float i = density;
        // gl_FragColor = vec4(i, i, i, 1);
    }
    else {
        gl_FragColor = col;
    }
}
";
    }

    public class ShaderLocations
    {
        public int IsBillboard { get; set; }
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
        public int FogOffset { get; set; }
        public int UseOverride { get; set; }
        public int OverrideColor { get; set; }
        public int MaterialAlpha { get; set; }
        public int MaterialMode { get; set; }
        public int ModelMatrix { get; set; }
        public int ViewMatrix { get; set; }
        public int ProjectionMatrix { get; set; }
        public int TextureMatrix { get; set; }
        public int TexgenMode { get; set; }
    }
}
