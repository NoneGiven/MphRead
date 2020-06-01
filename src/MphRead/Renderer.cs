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
        private float _angleX = 0.0f;
        private float _angleY = 0.0f;
        private float _distance = 5.0f;
        private Vector3 _cameraPosition = new Vector3(0, 0, 0);
        private bool _leftMouse = false;
        public int _textureCount = 0;
        public float _roomScale = 1;

        private bool _showTextures = true;
        private bool _showColors = true;
        private bool _wireframe = false;
        private bool _faceCulling = true;
        private bool _textureFiltering = true;
        private bool _lighting = true;

        private static readonly Color4 _clearColor = new Color4(0, 0, 0, 1);
        private static readonly float _frameTime = 1.0f / 30.0f;

        private Vector4 _light1Vector = default;
        private Vector4 _light1Color = default;
        private Vector4 _light2Vector = default;
        private Vector4 _light2Color = default;

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
            (Model room, RoomMetadata roomMeta, IReadOnlyList<Model> entities) = SceneSetup.LoadRoom(name, layerMask);
            _models.Insert(0, room);
            _models.AddRange(entities);
            _roomScale = room.Header.ScaleBase.FloatValue * (1 << (int)room.Header.ScaleFactor);
            _light1Vector = new Vector4(roomMeta.Light1Vector);
            _light1Color = new Vector4(
                roomMeta.Light1Color.Red / 255.0f,
                roomMeta.Light1Color.Green / 255.0f,
                roomMeta.Light1Color.Blue / 255.0f,
                roomMeta.Light1Color.Alpha / 255.0f
            );
            _light2Vector = new Vector4(roomMeta.Light2Vector);
            _light2Color = new Vector4(
                roomMeta.Light2Color.Red / 255.0f,
                roomMeta.Light2Color.Green / 255.0f,
                roomMeta.Light2Color.Blue / 255.0f,
                roomMeta.Light2Color.Alpha / 255.0f
            );
            _cameraMode = CameraMode.Roam;
        }

        public void AddModel(string name, int recolor)
        {
            Model model = Read.GetModelByName(name, recolor);
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
            if (_cameraMode == CameraMode.Pivot)
            {
                Console.WriteLine(" - Scroll mouse wheel to zoom");
            }
            else if (_cameraMode == CameraMode.Roam)
            {
                Console.WriteLine(" - Use WASD, Space, and V to move");
            }
            Console.WriteLine(" - Hold left mouse button or use arrow keys to rotate");
            Console.WriteLine(" - Hold Shift to move the camera faster");
            Console.WriteLine($" - T toggles texturing ({FormatOnOff(_showTextures)})");
            Console.WriteLine($" - C toggles vertex colours ({FormatOnOff(_showColors)})");
            Console.WriteLine($" - Q toggles wireframe ({FormatOnOff(_wireframe)})");
            Console.WriteLine($" - B toggles face culling ({FormatOnOff(_faceCulling)})");
            Console.WriteLine($" - F toggles texture filtering ({FormatOnOff(_textureFiltering)})");
            Console.WriteLine($" - L toggles lighting ({FormatOnOff(_lighting)})");
            Console.WriteLine($" - P switches camera mode ({(_cameraMode == CameraMode.Pivot ? "pivot" : "roam")})");
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
                if (material.TextureId == UInt16.MaxValue)
                {
                    _textureMap[model].Add(-1);
                    continue;
                }
                _textureCount++;
                var pixels = new List<uint>();
                Texture texture = model.Textures[material.TextureId];
                ushort width = texture.Width;
                ushort height = texture.Height;
                TextureFormat textureFormat = texture.Format;
                bool decal = material.PolygonMode == PolygonMode.Decal;
                bool onlyOpaque = true;
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

                _textureMap[model].Add(_textureCount);

                if (material.RenderMode == RenderMode.Unknown3 || material.RenderMode == RenderMode.Unknown4)
                {
                    // todo: logging (this will get overwritten by the menu)
                    Console.WriteLine($"mat {material.Name} of model {model.Name} has render mode {material.RenderMode}");
                    material.RenderMode = RenderMode.Normal;
                }
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
            OnKeyHeld();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.GetFloat(GetPName.Viewport, out Vector4 viewport);
            float aspect = (viewport.Z - viewport.X) / (viewport.W - viewport.Y);

            GL.MatrixMode(MatrixMode.Projection);
            float fov = MathHelper.DegreesToRadians(80.0f);
            var perspectiveMatrix = Matrix4.CreatePerspectiveFieldOfView(fov, aspect, 0.02f, 100.0f);
            GL.LoadMatrix(ref perspectiveMatrix);

            TransformCamera();
            UpdateCameraPosition();

            RenderScene(args.Time);
            SwapBuffers();
            base.OnRenderFrame(args);
        }

        private void ResetCamera()
        {
            _angleY = 0;
            _angleX = 0;
            _distance = 5.0f;
            if (_cameraMode == CameraMode.Roam)
            {
                _cameraPosition = new Vector3(0, 0, 0);
            }
        }

        private void TransformCamera()
        {
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            if (_cameraMode == CameraMode.Pivot)
            {
                GL.Translate(0, 0, _distance * -1);
                GL.Rotate(_angleY, 1, 0, 0);
                GL.Rotate(_angleX, 0, 1, 0);
            }
            else if (_cameraMode == CameraMode.Roam)
            {
                GL.Rotate(_angleY, 1, 0, 0);
                GL.Rotate(_angleX, 0, 1, 0);
                GL.Translate(_cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z);
            }
        }

        private void UpdateCameraPosition()
        {
            if (_cameraMode == CameraMode.Pivot)
            {
                float angleX = _angleX + 90;
                if (angleX > 360)
                {
                    angleX -= 360;
                }
                float angleY = _angleY + 90;
                if (angleY > 360)
                {
                    angleY -= 360;
                }
                float theta = MathHelper.DegreesToRadians(angleX);
                float phi = MathHelper.DegreesToRadians(angleY);
                float x = MathF.Round(_distance * MathF.Cos(theta), 4);
                float y = MathF.Round(_distance * MathF.Sin(theta) * MathF.Cos(phi), 4) * -1;
                float z = MathF.Round(_distance * MathF.Sin(theta) * MathF.Sin(phi), 4);
                _cameraPosition = new Vector3(x, y, z);
            }
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
            float distanceOne = Vector3.Distance(_cameraPosition, one.Position);
            float distanceTwo = Vector3.Distance(_cameraPosition, two.Position);
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

        private void ProcessAnimations(Model model, double elapsedTime)
        {
            foreach (TexcoordAnimationGroup group in model.TexcoordAnimationGroups)
            {
                group.Time += elapsedTime;
                int increment = (int)(group.Time / _frameTime);
                if (increment != 0)
                {
                    group.CurrentFrame += increment;
                    group.Time -= increment * _frameTime;
                }
                group.CurrentFrame %= group.FrameCount;
            }
        }

        private void RenderScene(double elapsedTime)
        {
            _models.Sort(CompareModels);
            foreach (Model model in _models)
            {
                ProcessAnimations(model, elapsedTime);
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
                    GL.Scale(_roomScale, _roomScale, _roomScale);
                }
                GL.Rotate(model.Rotation.X, 1, 0, 0);
                GL.Rotate(model.Rotation.Y, 0, 1, 0);
                GL.Rotate(model.Rotation.Z, 0, 0, 1);
                if (model.Type == ModelType.Room)
                {
                    RenderRoom(model);
                }
                else
                {
                    RenderModel(model);
                }
                GL.MatrixMode(MatrixMode.Modelview);
                GL.PopMatrix();
            }
        }

        private void SetLights()
        {
            GL.Uniform4(_shaderLocations.Light1Vector, _light1Vector);
            GL.Uniform4(_shaderLocations.Light1Color, _light1Color);
            GL.Uniform4(_shaderLocations.Light2Vector, _light2Vector);
            GL.Uniform4(_shaderLocations.Light2Color, _light2Color);
        }

        private void RenderRoom(Model model)
        {
            GL.UseProgram(_shaderProgramId);
            SetLights();
            // pass 1: opaque
            GL.DepthMask(true);
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.Normal);
            }
            // pass 2: decal
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.PolygonOffset(-1, -1);
            GL.DepthMask(false);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.Decal);
            }
            // pass 3: translucent with alpha test
            GL.DepthMask(true);
            GL.Enable(EnableCap.AlphaTest);
            GL.AlphaFunc(AlphaFunction.Gequal, 0.5f);
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.AlphaTest);
            }
            GL.Disable(EnableCap.AlphaTest);
            // pass 4: translucent
            GL.DepthMask(false);
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.Translucent);
            }
            GL.PolygonOffset(0, 0);
            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.UseProgram(0);
        }

        private void RenderModel(Model model)
        {
            GL.UseProgram(_shaderProgramId);
            // pass 1: opaque
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.Normal);
            }
            // pass 2: translucent
            GL.DepthMask(false);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.Normal, invertFilter: true);
            }
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.UseProgram(0);
        }

        private void RenderNode(Model model, Node node, RenderMode modeFilter, bool invertFilter = false)
        {
            if (node.MeshCount > 0 && node.Enabled)
            {
                GL.MatrixMode(MatrixMode.Modelview);
                GL.PushMatrix();
                Matrix4 transform = node.Transform;
                GL.MultTransposeMatrix(ref transform);
                int meshStart = node.MeshId / 2;
                for (int i = 0; i < node.MeshCount; i++)
                {
                    int meshId = meshStart + i;
                    Mesh mesh = model.Meshes[meshId];
                    Material material = model.Materials[mesh.MaterialId];
                    if ((!invertFilter && material.RenderMode != modeFilter)
                        || (invertFilter && material.RenderMode == modeFilter))
                    {
                        continue;
                    }
                    GL.Uniform1(_shaderLocations.IsBillboard, node.Type == 1 ? 1 : 0);
                    RenderMesh(model, mesh, material);
                }
                GL.MatrixMode(MatrixMode.Modelview);
                GL.PopMatrix();
            }
        }

        private float InterpolateAnimation(IReadOnlyList<float> values, int start, int frame, int speed, int length)
        {
            if (length == 1)
            {
                return values[start];
            }
            if (speed == 1)
            {
                return values[start + frame];
            }
            int v7 = (frame - 1) >> (speed / 2) << (speed / 2);
            if (frame >= v7)
            {
                return values[start + frame - v7 + (frame >> (speed / 2))];
            }
            int index1 = frame >> (speed / 2);
            int index2 = frame >> (speed / 2) + 1;
            float div = 1 << (speed / 2);
            int t = frame & ((speed / 2) | 1);
            if (t != 0)
            {
                return values[start + index1] * (1 - t / div) + values[start + index2] * (t / div);
            }
            return values[start + index1];
        }

        private void AnimateTexcoords(Model model, Material material, int width, int height)
        {
            if (model.TexcoordAnimationGroups.Count > 0)
            {
                // todo: Get Model is currently overwriting things so the last group's information is always used
                TexcoordAnimationGroup group = model.TexcoordAnimationGroups.Last();
                TexcoordAnimation animation = group.Animations[material.TexcoordAnimationId];
                float scaleS = InterpolateAnimation(group.Scales, animation.ScaleLutIndexS, group.CurrentFrame,
                    animation.ScaleBlendS, animation.ScaleLutLengthS);
                float scaleT = InterpolateAnimation(group.Scales, animation.ScaleLutIndexT, group.CurrentFrame,
                    animation.ScaleBlendT, animation.ScaleLutLengthT);
                float rotate = InterpolateAnimation(group.Rotations, animation.RotateLutIndexZ, group.CurrentFrame,
                    animation.RotateBlendZ, animation.RotateLutLengthZ);
                float translateS = InterpolateAnimation(group.Translations, animation.TranslateLutIndexS, group.CurrentFrame,
                    animation.TranslateBlendS, animation.TranslateLutLengthS);
                float translateT = InterpolateAnimation(group.Translations, animation.TranslateLutIndexT, group.CurrentFrame,
                    animation.TranslateBlendT, animation.TranslateLutLengthT);
                GL.MatrixMode(MatrixMode.Texture);
                GL.Translate(translateS * width, translateT * height, 0);
                if (rotate != 0)
                {
                    GL.Translate(width / 2, height / 2, 0);
                    GL.Rotate(-rotate / MathF.PI * 180, 0, 0, 1);
                    GL.Translate(-width / 2, -height / 2, 0);
                }
                GL.Scale(scaleS, scaleT, 1);
            }
        }

        private void RenderMesh(Model model, Mesh mesh, Material material)
        {
            GL.Color3(1f, 1f, 1f);
            ushort width = 1;
            ushort height = 1;
            int textureId = material.TextureId;
            if (textureId == UInt16.MaxValue)
            {
                GL.Disable(EnableCap.Texture2D);
            }
            else if (_showTextures)
            {
                GL.Enable(EnableCap.Texture2D);
                Texture texture = model.Textures[textureId];
                width = texture.Width;
                height = texture.Height;
                GL.BindTexture(TextureTarget.Texture2D, _textureMap[model][mesh.MaterialId]);
            }
            GL.MatrixMode(MatrixMode.Texture);
            GL.LoadIdentity();
            if (model.Animate && textureId != UInt16.MaxValue && material.TexcoordAnimationId != -1)
            {
                GL.Scale(1.0f / width, 1.0f / height, 1.0f);
                AnimateTexcoords(model, material, width, height);
            }
            else
            {
                GL.Translate(material.TranslateS, material.TranslateT, 0.0f);
                GL.Scale(material.ScaleS, material.ScaleT, 1.0f);
                GL.Scale(1.0f / width, 1.0f / height, 1.0f);
            }
            GL.Uniform1(_shaderLocations.UseTexture, GL.IsEnabled(EnableCap.Texture2D) ? 1 : 0);
            if (_lighting && material.Lighting != 0)
            {
                // todo: would be nice if the approaches for this and the room lights were the same
                var ambient = new Vector4(
                    material.Ambient.Red / 255.0f,
                    material.Ambient.Green / 255.0f,
                    material.Ambient.Blue / 255.0f,
                    1.0f
                );
                var diffuse = new Vector4(
                    material.Diffuse.Red / 255.0f,
                    material.Diffuse.Green / 255.0f,
                    material.Diffuse.Blue / 255.0f,
                    1.0f
                );
                var specular = new Vector4(
                    material.Specular.Red / 255.0f,
                    material.Specular.Green / 255.0f,
                    material.Specular.Blue / 255.0f,
                    1.0f
                );
                GL.Enable(EnableCap.Lighting);
                GL.Material(MaterialFace.Front, MaterialParameter.Ambient, ambient);
                GL.Material(MaterialFace.Front, MaterialParameter.Diffuse, diffuse);
                GL.Uniform4(_shaderLocations.Ambient, ambient);
                GL.Uniform4(_shaderLocations.Diffuse, diffuse);
                GL.Uniform4(_shaderLocations.Specular, specular);
                GL.Uniform1(_shaderLocations.UseLight, 1);
            }
            else
            {
                GL.Uniform1(_shaderLocations.UseLight, 0);
            }
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
            IReadOnlyList<RenderInstruction> list = model.RenderInstructionLists[mesh.DlistId];
            float vtxX = 0;
            float vtxY = 0;
            float vtxZ = 0;
            // note: calling this every frame will have some overhead,
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
            if (_lighting)
            {
                GL.Disable(EnableCap.Lighting);
            }
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
                _angleY -= e.DeltaY / 1.5f;
                _angleY = Math.Clamp(_angleY, -90.0f, 90.0f);
                _angleX -= e.DeltaX / 1.5f;
                _angleX %= 360f;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if (_cameraMode == CameraMode.Pivot)
            {
                _distance -= e.OffsetY / 1.5f;
            }
            base.OnMouseWheel(e);
        }

        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            if (e.Key == Key.T)
            {
                _showTextures = !_showTextures;
                if (_showTextures)
                {
                    GL.Enable(EnableCap.Texture2D);
                }
                else
                {
                    GL.Disable(EnableCap.Texture2D);
                }
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
            else if (e.Key == Key.L)
            {
                _lighting = !_lighting;
                PrintMenu();
            }
            else if (e.Key == Key.R)
            {
                ResetCamera();
            }
            else if (e.Key == Key.P)
            {
                if (_cameraMode == CameraMode.Pivot)
                {
                    _cameraMode = CameraMode.Roam;
                }
                else
                {
                    _cameraMode = CameraMode.Pivot;
                }
                ResetCamera();
                PrintMenu();
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

        private void OnKeyHeld()
        {
            // sprint
            float step = KeyboardState.IsKeyDown(Key.ShiftLeft) || KeyboardState.IsKeyDown(Key.ShiftRight) ? 5 : 1;
            if (_cameraMode == CameraMode.Roam)
            {
                if (KeyboardState.IsKeyDown(Key.W)) // move forward
                {
                    _cameraPosition = new Vector3(
                        _cameraPosition.X +
                            step * MathF.Sin(MathHelper.DegreesToRadians(-1 * _angleX))
                            * MathF.Cos(MathHelper.DegreesToRadians(_angleY)) * 0.1f,
                        _cameraPosition.Y +
                            step * MathF.Sin(MathHelper.DegreesToRadians(_angleY)) * 0.1f,
                        _cameraPosition.Z +
                            step * MathF.Cos(MathHelper.DegreesToRadians(_angleX))
                            * MathF.Cos(MathHelper.DegreesToRadians(_angleY)) * 0.1f
                    );
                }
                else if (KeyboardState.IsKeyDown(Key.S)) // move backward
                {
                    _cameraPosition = new Vector3(
                        _cameraPosition.X -
                            step * MathF.Sin(MathHelper.DegreesToRadians(-1 * _angleX))
                            * MathF.Cos(MathHelper.DegreesToRadians(_angleY)) * 0.1f,
                        _cameraPosition.Y -
                            step * MathF.Sin(MathHelper.DegreesToRadians(_angleY)) * 0.1f,
                        _cameraPosition.Z -
                            step * MathF.Cos(MathHelper.DegreesToRadians(_angleX))
                            * MathF.Cos(MathHelper.DegreesToRadians(_angleY)) * 0.1f
                    );
                }
                if (KeyboardState.IsKeyDown(Key.Space)) // move up
                {
                    _cameraPosition = new Vector3(_cameraPosition.X, _cameraPosition.Y - step * 0.1f, _cameraPosition.Z);
                }
                else if (KeyboardState.IsKeyDown(Key.V)) // move down
                {
                    _cameraPosition = new Vector3(_cameraPosition.X, _cameraPosition.Y + step * 0.1f, _cameraPosition.Z);
                }
                if (KeyboardState.IsKeyDown(Key.A)) // move left
                {
                    float angleX = _angleX - 90;
                    if (angleX < 0)
                    {
                        angleX += 360;
                    }
                    _cameraPosition = new Vector3(
                        _cameraPosition.X +
                            step * MathF.Sin(MathHelper.DegreesToRadians(-1 * angleX))
                            * 0.1f,
                        _cameraPosition.Y,
                        _cameraPosition.Z +
                            step * MathF.Cos(MathHelper.DegreesToRadians(angleX))
                            * 0.1f
                    );
                }
                else if (KeyboardState.IsKeyDown(Key.D)) // move right
                {
                    float angleX = _angleX + 90;
                    if (angleX > 360)
                    {
                        angleX -= 360;
                    }
                    _cameraPosition = new Vector3(
                        _cameraPosition.X +
                            step * MathF.Sin(MathHelper.DegreesToRadians(-1 * angleX))
                            * 0.1f,
                        _cameraPosition.Y,
                        _cameraPosition.Z +
                            step * MathF.Cos(MathHelper.DegreesToRadians(angleX))
                            * 0.1f
                    );
                }
                step = KeyboardState.IsKeyDown(Key.ShiftLeft) ? -3 : -1.5f;
            }
            if (KeyboardState.IsKeyDown(Key.Up)) // rotate up
            {
                _angleY += step;
                _angleY = Math.Clamp(_angleY, -90.0f, 90.0f);
            }
            else if (KeyboardState.IsKeyDown(Key.Down)) // rotate down
            {
                _angleY -= step;
                _angleY = Math.Clamp(_angleY, -90.0f, 90.0f);
            }
            if (KeyboardState.IsKeyDown(Key.Left)) // rotate left
            {
                _angleX += step;
                _angleX %= 360f;
            }
            else if (KeyboardState.IsKeyDown(Key.Right)) // rotate right
            {
                _angleX -= step;
                _angleX %= 360f;
            }
        }

        private enum CameraMode
        {
            Pivot,
            Roam
        }
    }
}
