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
uniform vec4 light1vec;
uniform vec4 light1col;
uniform vec4 light2vec;
uniform vec4 light2col;
uniform vec4 diffuse;
uniform vec4 ambient;
uniform vec4 specular;
uniform vec4 fog_color;

varying vec2 texcoord;
varying vec4 color;
varying float depth;

void main()
{
    if(is_billboard) {
        gl_Position = gl_ProjectionMatrix * (gl_ModelViewMatrix * vec4(0.0, 0.0, 0.0, 1.0) + vec4(gl_Vertex.x, gl_Vertex.y, gl_Vertex.z, 0.0));
    } else {
        gl_Position = gl_ModelViewProjectionMatrix * gl_Vertex;
    }
    if(use_light) {
        // light 1
        float fixed_diffuse1 = dot(light1vec.xyz, gl_Normal);
        vec3 neghalf1 = -(light1vec.xyz / 2.0);
        float d1 = dot(neghalf1, gl_Normal);
        float fixed_shininess1 = d1 > 0.0 ? 2.0 * d1 * d1 : 0.0;
        vec4 spec1 = specular * light1col * fixed_shininess1;
        vec4 diff1 = diffuse * light1col * fixed_diffuse1;
        vec4 amb1 = ambient * light1col;
        vec4 col1 = spec1 + diff1 + amb1;
        // light 2
        float fixed_diffuse2 = dot(light2vec.xyz, gl_Normal);
        vec3 neghalf2 = -(light2vec.xyz / 2.0);
        float d2 = dot(neghalf2, gl_Normal);
        float fixed_shininess2 = d2 > 0.0 ? 2.0 * d2 * d2 : 0.0;
        vec4 spec2 = specular * light2col * fixed_shininess2;
        vec4 diff2 = diffuse * light2col * fixed_diffuse2;
        vec4 amb2 = ambient * light2col;
        vec4 col2 = spec2 + diff2 + amb2;
        color = gl_Color * col1 + col2;
    } else {
        color = gl_Color;
    }
    texcoord = vec2(gl_TextureMatrix[0] * gl_MultiTexCoord0);
    depth = clamp(length(gl_Position) / 256.0, 0, 1);
}
";

        private static readonly string _fragmentShader = @"
uniform bool is_billboard;
uniform bool use_texture;
uniform bool fog_enable;
uniform vec4 fog_color;
uniform vec4 ambient;
uniform int fog_offset;
uniform sampler2D tex;
varying vec2 texcoord;
varying vec4 color;
varying float depth;

void main()
{
    vec4 col;
    if(use_texture) {
        vec4 texcolor = texture2D(tex, texcoord);
        col = color * texcolor;
    } else  {
        col = color;
    }
    if(fog_enable) {
        float density = depth - (1 - float(fog_offset) / 65536.0);
        if(density < 0)
            density = 0;
        gl_FragColor = vec4((col * (1 - density) + fog_color * density).xyz, col.a);
        // float i = density;
        // gl_FragColor = vec4(i, i, i, 1);
    } else {
        gl_FragColor = col;
    }
}
";
    }
}
