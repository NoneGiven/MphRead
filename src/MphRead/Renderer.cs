using System;
using System.Collections.Generic;
using System.Linq;
using OpenToolkit.Graphics.OpenGL;
using OpenToolkit.Mathematics;
using OpenToolkit.Windowing.Common;
using OpenToolkit.Windowing.Common.Input;
using OpenToolkit.Windowing.Desktop;

namespace MphRead
{
    public class Renderer : IDisposable
    {
        private readonly RenderWindow _window;

        public Renderer(string? title = null)
        {
            GameWindowSettings settings = GameWindowSettings.Default;
            settings.RenderFrequency = 60;
            settings.UpdateFrequency = 60;
            NativeWindowSettings native = NativeWindowSettings.Default;
            native.Size = new Vector2i(800, 600);
            native.Title = title ?? "Render";
            native.Profile = ContextProfile.Compatability;
            native.APIVersion = new Version(3, 2);
            _window = new RenderWindow(settings, native);
        }

        public void AddRoom(string name, int layerMask = 0)
        {
            _window.AddRoom(name, layerMask);
        }

        public void AddModel(string name, int recolor = 0)
        {
            _window.AddModel(name, recolor);
        }

        public void Run()
        {
            _window.Run();
        }

        public void Dispose()
        {
            _window.Dispose();
        }
    }

    public class ShaderLocations
    {
        public int IsBillboard { get; set; }
        public int UseLight { get; set; }
        public int UseTexture { get; set; }
        public int Light1Color { get; set; }
        public int Light1Vector { get; set; }
        public int Light2Color { get; set; }
        public int Light2Vector { get; set; }
        public int Diffuse { get; set; }
        public int Ambient { get; set; }
        public int Specular { get; set; }
    }

    public class RenderWindow : GameWindow
    {
        private bool _roomLoaded = false;
        private readonly List<Model> _models = new List<Model>();
        private readonly Dictionary<Model, List<int>> _textureMap = new Dictionary<Model, List<int>>();

        private CameraMode _cameraMode = CameraMode.Pivot;
        private float _angle = 0.0f;
        private float _elevation = 0.0f;
        private float _distance = 5.0f;
        private Vector3 _cameraPosition = default;
        private bool _leftMouse = false;
        public int _textureCount = 0;
        public float _roomScale = 1;

        private bool _showTextures = true;
        private bool _showColors = true;
        private bool _wireframe = false;
        private bool _faceCulling = true;
        private bool _textureFiltering = true;

        private static readonly Color4 _clearColor = new Color4(0.4f, 0.4f, 0.4f, 1.0f);

        private int _shaderProgramId = 0;
        private readonly ShaderLocations _shaderLocations = new ShaderLocations();

        public RenderWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
            : base(gameWindowSettings, nativeWindowSettings)
        {
        }

        public void AddRoom(string name, int layerMask)
        {
            if (_roomLoaded)
            {
                throw new InvalidOperationException();
            }
            _roomLoaded = true;
            (Model room, IReadOnlyList<Model> entities) = SceneSetup.LoadRoom(name, layerMask);
            _models.Insert(0, room);
            _models.AddRange(entities);
            _roomScale = room.Header.ScaleBase.FloatValue * (1 << (int)room.Header.ScaleFactor);
        }

        public void AddModel(string name, int recolor)
        {
            Model model = Read.GetModelByName(name, recolor);
            model.Position = new Vector3(0, 0, -2);
            SceneSetup.ComputeMatrices(model, index: 0);
            _models.Add(model);
        }

        protected override void OnLoad()
        {
            GL.ClearColor(_clearColor);

            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Texture2D);
            GL.PolygonMode(MaterialFace.FrontAndBack,
                    _wireframe
                    ? OpenToolkit.Graphics.OpenGL.PolygonMode.Line
                    : OpenToolkit.Graphics.OpenGL.PolygonMode.Fill);

            GL.DepthFunc(DepthFunction.Lequal);

            InitShaders();

            foreach (Model model in _models)
            {
                InitTextures(model);
            }

            PrintMenu();

            base.OnLoad();
        }

        private void InitShaders()
        {
            int vtxS = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vtxS, Shaders.VertexShader);
            GL.CompileShader(vtxS);
            int frgS = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(frgS, Shaders.FragmentShader);
            GL.CompileShader(frgS);
            _shaderProgramId = GL.CreateProgram();
            GL.AttachShader(_shaderProgramId, vtxS);
            GL.AttachShader(_shaderProgramId, frgS);
            GL.LinkProgram(_shaderProgramId);
            GL.DetachShader(_shaderProgramId, vtxS);
            GL.DetachShader(_shaderProgramId, frgS);

            _shaderLocations.IsBillboard = GL.GetUniformLocation(_shaderProgramId, "is_billboard");
            _shaderLocations.UseLight = GL.GetUniformLocation(_shaderProgramId, "use_light");
            _shaderLocations.UseTexture = GL.GetUniformLocation(_shaderProgramId, "use_texture");
            _shaderLocations.Light1Color = GL.GetUniformLocation(_shaderProgramId, "light1col");
            _shaderLocations.Light1Vector = GL.GetUniformLocation(_shaderProgramId, "light1vec");
            _shaderLocations.Light2Color = GL.GetUniformLocation(_shaderProgramId, "light2col");
            _shaderLocations.Light2Vector = GL.GetUniformLocation(_shaderProgramId, "light2vec");
            _shaderLocations.Diffuse = GL.GetUniformLocation(_shaderProgramId, "diffuse");
            _shaderLocations.Ambient = GL.GetUniformLocation(_shaderProgramId, "ambient");
            _shaderLocations.Specular = GL.GetUniformLocation(_shaderProgramId, "specular");
        }

        private void PrintMenu()
        {
            Console.Clear();
            Console.WriteLine($"MphRead Version {Program.Version}");
            Console.WriteLine(" - Scroll mouse wheel to zoom");
            Console.WriteLine(" - Hold left mouse button to rotate");
            Console.WriteLine($" - T toggles texturing ({FormatOnOff(_showTextures)})");
            Console.WriteLine($" - C toggles vertex colours ({FormatOnOff(_showColors)})");
            Console.WriteLine($" - Q toggles wireframe ({FormatOnOff(_wireframe)})");
            Console.WriteLine($" - B toggles face culling ({FormatOnOff(_faceCulling)})");
            Console.WriteLine($" - F toggles texture filtering ({FormatOnOff(_textureFiltering)})");
            Console.WriteLine(" - R resets the camera");
            Console.WriteLine(" - Ctrl+O then enter \"model_name [recolor]\" to load");
            Console.WriteLine(" - Esc closes the viewer");
            Console.WriteLine();
        }

        private string FormatOnOff(bool setting)
        {
            return setting ? "on" : "off";
        }

        private void InitTextures(Model model)
        {
            _textureMap.Add(model, new List<int>());
            int minParameter = _textureFiltering ? (int)TextureMinFilter.Linear : (int)TextureMagFilter.Nearest;
            int magParameter = _textureFiltering ? (int)TextureMinFilter.Linear : (int)TextureMagFilter.Nearest;
            foreach (Material material in model.Materials)
            {
                _textureCount++;
                ushort width = 1;
                ushort height = 1;
                var pixels = new List<uint>();
                bool onlyOpaque = true;
                TextureFormat textureFormat = TextureFormat.Palette2Bit;
                if (material.TextureId != UInt16.MaxValue)
                {
                    Texture texture = model.Textures[material.TextureId];
                    width = texture.Width;
                    height = texture.Height;
                    textureFormat = texture.Format;
                    bool decal = material.RenderMode == RenderMode.Decal;
                    foreach (ColorRgba pixel in model.GetPixels(material.TextureId, material.PaletteId))
                    {
                        uint red = pixel.Red;
                        uint green = pixel.Green;
                        uint blue = pixel.Blue;
                        uint alpha = (uint)((decal ? 255 : pixel.Alpha) * material.Alpha / 31.0f);
                        pixels.Add((red << 0) | (green << 8) | (blue << 16) | (alpha << 24));
                        if (alpha < 255)
                        {
                            onlyOpaque = false;
                        }
                    }
                }
                else
                {
                    pixels.Add(((uint)255 << 0) | ((uint)255 << 8) | ((uint)255 << 16) | ((uint)255 << 24));
                }

                _textureMap[model].Add(_textureCount);

                // - if material alpha is less than 31, and render mode is not Translucent, set to Translucent
                // - if render mode is not Normal, but there are no non-opaque pixels, set to Normal
                // - if render mode is Normal, but there are non-opaque pixels, set to AlphaTest
                // - if render mode is Translucent, material alpha is 31, and texture format is DirectRgba, set to AlphaTest
                if (material.Alpha < 31)
                {
                    material.RenderMode = RenderMode.Translucent;
                }
                if (material.RenderMode != RenderMode.Normal && onlyOpaque)
                {
                    material.RenderMode = RenderMode.Normal;
                }
                else if (material.RenderMode == RenderMode.Normal && !onlyOpaque)
                {
                    material.RenderMode = RenderMode.AlphaTest;
                }
                if (material.RenderMode == RenderMode.Translucent && material.Alpha == 31
                    && (textureFormat == TextureFormat.DirectRgb || textureFormat == TextureFormat.DirectRgba))
                {
                    material.RenderMode = RenderMode.AlphaTest;
                }

                GL.BindTexture(TextureTarget.Texture2D, _textureCount);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, pixels.ToArray());
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, minParameter);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, magParameter);
                switch (material.XRepeat)
                {
                case RepeatMode.Clamp:
                    GL.TexParameter(TextureTarget.Texture2D,
                        TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    break;
                case RepeatMode.Repeat:
                    GL.TexParameter(TextureTarget.Texture2D,
                        TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                    break;
                case RepeatMode.Mirror:
                    GL.TexParameter(TextureTarget.Texture2D,
                        TextureParameterName.TextureWrapS, (int)TextureWrapMode.MirroredRepeat);
                    break;
                }
                switch (material.YRepeat)
                {
                case RepeatMode.Clamp:
                    GL.TexParameter(TextureTarget.Texture2D,
                        TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                    break;
                case RepeatMode.Repeat:
                    GL.TexParameter(TextureTarget.Texture2D,
                        TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                    break;
                case RepeatMode.Mirror:
                    GL.TexParameter(TextureTarget.Texture2D,
                        TextureParameterName.TextureWrapT, (int)TextureWrapMode.MirroredRepeat);
                    break;
                }
                GL.BindTexture(TextureTarget.Texture2D, 0);
            }
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            float[] vp = new float[4];
            GL.GetFloat(GetPName.Viewport, vp);
            float aspect = (vp[2] - vp[0]) / (vp[3] - vp[1]);

            GL.MatrixMode(MatrixMode.Projection);
            float fov = MathHelper.DegreesToRadians(80.0f);
            var perspectiveMatrix = Matrix4.CreatePerspectiveFieldOfView(fov, aspect, 0.02f, 100.0f);
            GL.LoadMatrix(ref perspectiveMatrix);

            TransformCamera();
            _cameraPosition = GetCameraPosition();

            RenderScene(args.Time);
            SwapBuffers();
            base.OnRenderFrame(args);
        }

        private void TransformCamera()
        {
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            if (_cameraMode == CameraMode.Pivot)
            {
                GL.Translate(0, 0, _distance * -1);
                GL.Rotate(_elevation, 1, 0, 0);
                GL.Rotate(_angle, 0, 1, 0);
            }
        }

        private float ConvertToRadians(float angle)
        {
            return MathF.PI / 180 * angle;
        }

        private Vector3 GetCameraPosition()
        {
            if (_cameraMode == CameraMode.Pivot)
            {
                float angle = _angle + 90;
                if (angle > 360)
                {
                    angle -= 360;
                }
                float elevation = _elevation + 90;
                if (elevation > 360)
                {
                    elevation -= 360;
                }
                float theta = ConvertToRadians(angle);
                float phi = ConvertToRadians(elevation);
                float x = MathF.Round(_distance * MathF.Cos(theta), 4);
                float y = MathF.Round(_distance * MathF.Sin(theta) * MathF.Cos(phi), 4) * -1;
                float z = MathF.Round(_distance * MathF.Sin(theta) * MathF.Sin(phi), 4);
                return new Vector3(x, y, z);
            }
            return default;
        }

        private int CompareModels(Model one, Model two)
        {
            if (one.Type == ModelType.Room)
            {
                if (two.Type != ModelType.Room)
                {
                    return -1;
                }
                return 0;
            }
            else if (two.Type == ModelType.Room)
            {
                return 1;
            }
            // todo: use built-in types the whole way through
            var camera = new System.Numerics.Vector3(_cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z);
            var vectorOne = new System.Numerics.Vector3(one.Position.X, one.Position.Y, one.Position.Z);
            var vectorTwo = new System.Numerics.Vector3(two.Position.X, two.Position.Y, two.Position.Z);
            float distanceOne = System.Numerics.Vector3.Distance(camera, vectorOne);
            float distanceTwo = System.Numerics.Vector3.Distance(camera, vectorTwo);
            if (distanceOne == distanceTwo)
            {
                return 0;
            }
            if (distanceOne < distanceTwo)
            {
                return 1;
            }
            return -1;
        }

        private void RenderScene(double elapsedTime)
        {
            // todo: process animations
            _models.Sort(CompareModels);
            foreach (Model model in _models)
            {
                GL.MatrixMode(MatrixMode.Modelview);
                GL.PushMatrix();
                float height = 0;
                if (model.Type == ModelType.Item)
                {
                    float rotation = (float)(model.Rotation.Y + elapsedTime * 360 * 0.35) % 360;
                    model.Rotation = new Vector3(model.Rotation.X, rotation, model.Rotation.Z);
                    height = (MathF.Sin(model.Rotation.Y / 180 * MathF.PI) + 1) / 8f;
                }
                GL.Translate(model.Position.X, model.Position.Y + height, model.Position.Z);
                if (model.Type == ModelType.Room)
                {
                    // todo: lights
                    GL.Scale(_roomScale, _roomScale, _roomScale);
                }
                GL.Rotate(model.Rotation.X, 1, 0, 0);
                GL.Rotate(model.Rotation.Y, 0, 1, 0);
                GL.Rotate(model.Rotation.Z, 0, 0, 1);
                RenderModel(model);
                GL.MatrixMode(MatrixMode.Modelview);
                GL.PopMatrix();
            }
        }

        private void RenderModel(Model model)
        {
            foreach (Mesh mesh in model.Meshes)
            {
                Material material = model.Materials[mesh.MaterialId];
                if (_faceCulling)
                {
                    GL.Enable(EnableCap.CullFace);
                    if (material.Culling == CullingMode.Neither)
                    {
                        GL.Disable(EnableCap.CullFace);
                    }
                    else if (material.Culling == CullingMode.Back)
                    {
                        GL.CullFace(CullFaceMode.Back);
                    }
                    else if (material.Culling == CullingMode.Front)
                    {
                        GL.CullFace(CullFaceMode.Front);
                    }
                }
                if (_showTextures && !_wireframe)
                {
                    ushort width = 1;
                    ushort height = 1;
                    int textureId = material.TextureId;
                    if (textureId != UInt16.MaxValue)
                    {
                        Texture texture = model.Textures[textureId];
                        width = texture.Width;
                        height = texture.Height;
                    }
                    GL.BindTexture(TextureTarget.Texture2D, _textureMap[model][mesh.MaterialId]);
                    GL.MatrixMode(MatrixMode.Texture);
                    GL.LoadIdentity();
                    GL.Scale(1.0f / width, 1.0f / height, 1.0f);
                }
                else
                {
                    GL.BindTexture(TextureTarget.Texture2D, 0);
                }
                // always call this regardless of the _showColors setting,
                // otherwise one object's vertex colors will affect the others
                GL.Color3(1f, 1f, 1f);
                IReadOnlyList<RenderInstruction> list = model.RenderInstructionLists[mesh.DlistId];
                float vtxX = 0;
                float vtxY = 0;
                float vtxZ = 0;
                // note: calling this every frame will have some overhead (iterating and ifs),
                // but baking it in on load would prevent e.g. vertex color toggle
                foreach (RenderInstruction instruction in list)
                {
                    switch (instruction.Code)
                    {
                    case InstructionCode.BEGIN_VTXS:
                        if (instruction.Arguments[0] == 0)
                        {
                            GL.Begin(PrimitiveType.Triangles);
                        }
                        else if (instruction.Arguments[0] == 1)
                        {
                            GL.Begin(PrimitiveType.Quads);
                        }
                        else if (instruction.Arguments[0] == 2)
                        {
                            GL.Begin(PrimitiveType.TriangleStrip);
                        }
                        else if (instruction.Arguments[0] == 3)
                        {
                            GL.Begin(PrimitiveType.QuadStrip);
                        }
                        else
                        {
                            throw new ProgramException("Invalid geometry type");
                        }
                        break;
                    case InstructionCode.COLOR:
                        {
                            if (_showColors)
                            {
                                uint rgb = instruction.Arguments[0];
                                uint r = (rgb >> 0) & 0x1F;
                                uint g = (rgb >> 5) & 0x1F;
                                uint b = (rgb >> 10) & 0x1F;
                                GL.Color3(r / 31.0f, g / 31.0f, b / 31.0f);
                            }
                        }
                        break;
                    case InstructionCode.NORMAL:
                        {
                            uint xyz = instruction.Arguments[0];
                            int x = (int)((xyz >> 0) & 0x3FF);
                            if ((x & 0x200) > 0)
                            {
                                x = (int)(x | 0xFFFFFC00);
                            }
                            int y = (int)((xyz >> 10) & 0x3FF);
                            if ((y & 0x200) > 0)
                            {
                                y = (int)(y | 0xFFFFFC00);
                            }
                            int z = (int)((xyz >> 20) & 0x3FF);
                            if ((z & 0x200) > 0)
                            {
                                z = (int)(z | 0xFFFFFC00);
                            }
                            GL.Normal3(x / 512.0f, y / 512.0f, z / 512.0f);
                        }
                        break;
                    case InstructionCode.TEXCOORD:
                        {
                            uint st = instruction.Arguments[0];
                            int s = (int)((st >> 0) & 0xFFFF);
                            if ((s & 0x8000) > 0)
                            {
                                s = (int)(s | 0xFFFF0000);
                            }
                            int t = (int)((st >> 16) & 0xFFFF);
                            if ((t & 0x8000) > 0)
                            {
                                t = (int)(t | 0xFFFF0000);
                            }
                            GL.TexCoord2(s / 16.0f, t / 16.0f);
                        }
                        break;
                    case InstructionCode.VTX_16:
                        {
                            uint xy = instruction.Arguments[0];
                            int x = (int)((xy >> 0) & 0xFFFF);
                            if ((x & 0x8000) > 0)
                            {
                                x = (int)(x | 0xFFFF0000);
                            }
                            int y = (int)((xy >> 16) & 0xFFFF);
                            if ((y & 0x8000) > 0)
                            {
                                y = (int)(y | 0xFFFF0000);
                            }
                            int z = (int)(instruction.Arguments[1] & 0xFFFF);
                            if ((z & 0x8000) > 0)
                            {
                                z = (int)(z | 0xFFFF0000);
                            }
                            vtxX = Fixed.ToFloat(x);
                            vtxY = Fixed.ToFloat(y);
                            vtxZ = Fixed.ToFloat(z);
                            GL.Vertex3(vtxX, vtxY, vtxZ);
                        }
                        break;
                    case InstructionCode.VTX_10:
                        {
                            uint xyz = instruction.Arguments[0];
                            int x = (int)((xyz >> 0) & 0x3FF);
                            if ((x & 0x200) > 0)
                            {
                                x = (int)(x | 0xFFFFFC00);
                            }
                            int y = (int)((xyz >> 10) & 0x3FF);
                            if ((y & 0x200) > 0)
                            {
                                y = (int)(y | 0xFFFFFC00);
                            }
                            int z = (int)((xyz >> 20) & 0x3FF);
                            if ((z & 0x200) > 0)
                            {
                                z = (int)(z | 0xFFFFFC00);
                            }
                            vtxX = x / 64.0f;
                            vtxY = y / 64.0f;
                            vtxZ = z / 64.0f;
                            GL.Vertex3(vtxX, vtxY, vtxZ);
                        }
                        break;
                    case InstructionCode.VTX_XY:
                        {
                            uint xy = instruction.Arguments[0];
                            int x = (int)((xy >> 0) & 0xFFFF);
                            if ((x & 0x8000) > 0)
                            {
                                x = (int)(x | 0xFFFF0000);
                            }
                            int y = (int)((xy >> 16) & 0xFFFF);
                            if ((y & 0x8000) > 0)
                            {
                                y = (int)(y | 0xFFFF0000);
                            }
                            vtxX = Fixed.ToFloat(x);
                            vtxY = Fixed.ToFloat(y);
                            GL.Vertex3(vtxX, vtxY, vtxZ);
                        }
                        break;
                    case InstructionCode.VTX_XZ:
                        {
                            uint xz = instruction.Arguments[0];
                            int x = (int)((xz >> 0) & 0xFFFF);
                            if ((x & 0x8000) > 0)
                            {
                                x = (int)(x | 0xFFFF0000);
                            }
                            int z = (int)((xz >> 16) & 0xFFFF);
                            if ((z & 0x8000) > 0)
                            {
                                z = (int)(z | 0xFFFF0000);
                            }
                            vtxX = Fixed.ToFloat(x);
                            vtxZ = Fixed.ToFloat(z);
                            GL.Vertex3(vtxX, vtxY, vtxZ);
                        }
                        break;
                    case InstructionCode.VTX_YZ:
                        {
                            uint yz = instruction.Arguments[0];
                            int y = (int)((yz >> 0) & 0xFFFF);
                            if ((y & 0x8000) > 0)
                            {
                                y = (int)(y | 0xFFFF0000);
                            }
                            int z = (int)((yz >> 16) & 0xFFFF);
                            if ((z & 0x8000) > 0)
                            {
                                z = (int)(z | 0xFFFF0000);
                            }
                            vtxY = Fixed.ToFloat(y);
                            vtxZ = Fixed.ToFloat(z);
                            GL.Vertex3(vtxX, vtxY, vtxZ);
                        }
                        break;
                    case InstructionCode.VTX_DIFF:
                        {
                            uint xyz = instruction.Arguments[0];
                            int x = (int)((xyz >> 0) & 0x3FF);
                            if ((x & 0x200) > 0)
                            {
                                x = (int)(x | 0xFFFFFC00);
                            }
                            int y = (int)((xyz >> 10) & 0x3FF);
                            if ((y & 0x200) > 0)
                            {
                                y = (int)(y | 0xFFFFFC00);
                            }
                            int z = (int)((xyz >> 20) & 0x3FF);
                            if ((z & 0x200) > 0)
                            {
                                z = (int)(z | 0xFFFFFC00);
                            }
                            vtxX += Fixed.ToFloat(x);
                            vtxY += Fixed.ToFloat(y);
                            vtxZ += Fixed.ToFloat(z);
                            GL.Vertex3(vtxX, vtxY, vtxZ);
                        }
                        break;
                    case InstructionCode.END_VTXS:
                        GL.End();
                        break;
                    case InstructionCode.MTX_RESTORE:
                    case InstructionCode.DIF_AMB:
                    case InstructionCode.NOP:
                        break;
                    default:
                        throw new ProgramException("Unknown opcode");
                    }
                }
            }
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            GL.Viewport(0, 0, Size.X, Size.Y);
            base.OnResize(e);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.Button == MouseButton.Button1)
            {
                _leftMouse = true;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (e.Button == MouseButton.Button1)
            {
                _leftMouse = false;
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            if (_leftMouse)
            {
                _elevation -= e.DeltaY / 1.5f;
                _elevation = Math.Clamp(_elevation, -90.0f, 90.0f);
                _angle -= e.DeltaX / 1.5f;
                _angle %= 360f;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            _distance -= e.OffsetY / 1.5f;
            base.OnMouseWheel(e);
        }

        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            if (e.Key == Key.T)
            {
                _showTextures = !_showTextures;
                PrintMenu();
            }
            else if (e.Key == Key.C)
            {
                _showColors = !_showColors;
                PrintMenu();
            }
            else if (e.Key == Key.Q)
            {
                _wireframe = !_wireframe;
                GL.PolygonMode(MaterialFace.FrontAndBack,
                    _wireframe
                    ? OpenToolkit.Graphics.OpenGL.PolygonMode.Line
                    : OpenToolkit.Graphics.OpenGL.PolygonMode.Fill);
                PrintMenu();
            }
            else if (e.Key == Key.B)
            {
                _faceCulling = !_faceCulling;
                if (!_faceCulling)
                {
                    GL.Disable(EnableCap.CullFace);
                }
                PrintMenu();
            }
            else if (e.Key == Key.F)
            {
                _textureFiltering = !_textureFiltering;
                int minParameter = _textureFiltering ? (int)TextureMinFilter.Linear : (int)TextureMagFilter.Nearest;
                int magParameter = _textureFiltering ? (int)TextureMinFilter.Linear : (int)TextureMagFilter.Nearest;
                for (int i = 1; i <= _textureCount; i++)
                {
                    GL.BindTexture(TextureTarget.Texture2D, i);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, minParameter);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, magParameter);
                }
                PrintMenu();
            }
            else if (e.Key == Key.R)
            {
                _elevation = 0;
                _angle = 0;
                _distance = 5.0f;
            }
            else if (e.Key == Key.P)
            {
                // todo: alternate camera mode
                if (_cameraMode == CameraMode.Pivot)
                {
                    _cameraMode = CameraMode.Roam;
                }
                else
                {
                    _cameraMode = CameraMode.Pivot;
                }
            }
            else if (e.Control && e.Key == Key.O)
            {
                LoadModel();
            }
            else if (e.Key == Key.Escape)
            {
                Close();
            }
            base.OnKeyDown(e);
        }

        private void LoadModel()
        {
            PrintMenu();
            Console.Write("Open model: ");
            string modelName = Console.ReadLine().Trim();
            if (modelName == "")
            {
                PrintMenu();
            }
            else
            {
                int recolor = 0;
                if (modelName.Count(c => c == ' ') == 1)
                {
                    string[] split = modelName.Split(' ');
                    if (Int32.TryParse(split[1], out recolor))
                    {
                        modelName = split[0];
                    }
                }
                try
                {
                    Model model = Read.GetModelByName(modelName, recolor);
                    InitTextures(model);
                    PrintMenu();
                    _models.Add(model);
                }
                catch (ProgramException ex)
                {
                    PrintMenu();
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private enum CameraMode
        {
            Pivot,
            Roam
        }
    }
}
