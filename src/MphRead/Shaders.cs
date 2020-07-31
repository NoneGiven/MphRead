namespace MphRead
{
    public static class Shaders
    {
        public static string VertexShader => _vertexShader;
        public static string FragmentShader => _fragmentShader;

        private static readonly string _vertexShader = @"
uniform bool is_billboard;
uniform bool use_light;
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
uniform mat4 model_mtx;

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
        gl_Position = gl_ProjectionMatrix * (gl_ModelViewMatrix * vec4(0.0, 0.0, 0.0, 1.0) + vec4(gl_Vertex.x, gl_Vertex.y, gl_Vertex.z, 0.0));
    }
    else {
        gl_Position = gl_ModelViewProjectionMatrix * gl_Vertex;
    }
    if (use_light) {
        vec3 normal = normalize(mat3(model_mtx) * gl_Normal);
        vec3 dif_current = diffuse;
        vec3 amb_current = ambient;
        if (gl_Color.a == 0) {
            // see comment on DIF_AMB
            dif_current = gl_Color.rgb;
            amb_current = vec3(0.0, 0.0, 0.0);
        }
        vec3 col1 = light_calc(light1vec, light1col, normal, dif_current, amb_current, specular);
        vec3 col2 = light_calc(light2vec, light2col, normal, dif_current, amb_current, specular);
        color = vec4(min((col1 + col2), vec3(1.0, 1.0, 1.0)), 1.0);
    }
    else {
        color = gl_Color;
    }
    texcoord = vec2(gl_TextureMatrix[0] * gl_MultiTexCoord0);
}
";

        private static readonly string _fragmentShader = @"
uniform bool is_billboard;
uniform bool use_texture;
uniform bool fog_enable;
uniform vec4 fog_color;
uniform int fog_offset;
uniform float alpha_scale;
uniform sampler2D tex;
varying vec2 texcoord;
varying vec4 color;
uniform bool use_override;
uniform vec4 override_color;
uniform float mat_alpha;
uniform int mat_mode;

void main()
{
    // mat_mode: 0 - modulate, 1 - decal, 2 - toon
    vec4 col;
    if (use_texture) {
        vec4 texcolor = texture2D(tex, texcoord);
        texcolor = vec4(texcolor.x, texcolor.y, texcolor.z, mat_alpha * (mat_mode == 1 ? 1.0 : texcolor.w));
        col = color * texcolor;
        if (use_override) {
            col.r = override_color.r;
            col.g = override_color.g;
            col.b = override_color.b;
            col.a *= override_color.a;
        }
    }
    else {
        col = use_override ? override_color : color;
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
        gl_FragColor = vec4((col * (1.0 - density) + fog_color * density).xyz, col.a * alpha_scale);
        // float i = density;
        // gl_FragColor = vec4(i, i, i, 1);
    }
    else {
        gl_FragColor = col * vec4(1.0, 1.0, 1.0, alpha_scale);
    }
}
";
    }
}
