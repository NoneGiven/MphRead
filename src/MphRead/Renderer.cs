using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public void AddRoom(int id, int layerMask = 0)
        {
            _window.AddRoom(id, layerMask);
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
        public int UseFog { get; set; }
        public int FogColor { get; set; }
        public int FogOffset { get; set; }
        public int UseOverride { get; set; }
        public int OverrideColor { get; set; }
    }

    public class RenderWindow : GameWindow
    {
        private enum SelectionMode
        {
            None,
            Model,
            Mesh,
            Node
        }

        private bool _roomLoaded = false;
        private readonly List<Model> _models = new List<Model>();
        private readonly Dictionary<uint, Model> _modelMap = new Dictionary<uint, Model>();
        private readonly ConcurrentQueue<Model> _loadQueue = new ConcurrentQueue<Model>();
        private readonly ConcurrentQueue<Model> _unloadQueue = new ConcurrentQueue<Model>();
        private readonly Dictionary<Model, List<int>> _textureMap = new Dictionary<Model, List<int>>();
        private SelectionMode _selectionMode = SelectionMode.None;
        private uint _selectedModelId = 0;
        private int _selectedMeshId = 0;
        private bool _showSelection = true;

        private Model SelectedModel => _modelMap[_selectedModelId];

        private CameraMode _cameraMode = CameraMode.Pivot;
        private float _angleX = 0.0f;
        private float _angleY = 0.0f;
        private float _distance = 5.0f;
        private Vector3 _cameraPosition = new Vector3(0, 0, 0);
        private bool _leftMouse = false;
        public int _textureCount = 0;

        private bool _showTextures = true;
        private bool _showColors = true;
        private bool _wireframe = false;
        private bool _faceCulling = true;
        private bool _textureFiltering = true;
        private bool _lighting = true;
        private bool _showInvisible = false;

        private static readonly Color4 _clearColor = new Color4(0, 0, 0, 1);
        private static readonly float _frameTime = 1.0f / 30.0f;

        private Vector4 _light1Vector = default;
        private Vector4 _light1Color = default;
        private Vector4 _light2Vector = default;
        private Vector4 _light2Color = default;
        private bool _hasFog = false;
        private bool _showFog = false;
        private Vector4 _fogColor = default;
        private int _fogOffset = default;

        private int _shaderProgramId = 0;
        private readonly ShaderLocations _shaderLocations = new ShaderLocations();

        private readonly List<string> _logs = new List<string>();

        public RenderWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
            : base(gameWindowSettings, nativeWindowSettings)
        {
        }

        public void AddRoom(int id, int layerMask)
        {
            RoomMetadata? meta = Metadata.GetRoomById(id);
            if (meta != null)
            {
                AddRoom(meta.Name, layerMask);
            }
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
            _modelMap.Add(room.SceneId, room);
            foreach (Model entity in entities)
            {
                _modelMap.Add(entity.SceneId, entity);
            }
            float roomScale = room.Header.ScaleBase.FloatValue * (1 << (int)room.Header.ScaleFactor);
            room.Scale = new Vector3(roomScale, roomScale, roomScale);
            _light1Vector = new Vector4(roomMeta.Light1Vector);
            _light1Color = new Vector4(
                roomMeta.Light1Color.Red / 31.0f,
                roomMeta.Light1Color.Green / 31.0f,
                roomMeta.Light1Color.Blue / 31.0f,
                roomMeta.Light1Color.Alpha / 31.0f
            );
            _light2Vector = new Vector4(roomMeta.Light2Vector);
            _light2Color = new Vector4(
                roomMeta.Light2Color.Red / 31.0f,
                roomMeta.Light2Color.Green / 31.0f,
                roomMeta.Light2Color.Blue / 31.0f,
                roomMeta.Light2Color.Alpha / 31.0f
            );
            _hasFog = roomMeta.FogEnabled != 0;
            _fogColor = new Vector4(
                ((roomMeta.FogColor) & 0x1F) / (float)0x1F,
                (((roomMeta.FogColor) >> 5) & 0x1F) / (float)0x1F,
                (((roomMeta.FogColor) >> 10) & 0x1F) / (float)0x1F,
                1.0f
            );
            _fogOffset = (int)roomMeta.FogOffset;
            _cameraMode = CameraMode.Roam;
        }

        public void AddModel(string name, int recolor)
        {
            Model model = Read.GetModelByName(name, recolor);
            SceneSetup.ComputeNodeMatrices(model, index: 0);
            _models.Add(model);
            _modelMap.Add(model.SceneId, model);
        }

        protected override async void OnLoad()
        {
            await Output.Begin();
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

            await PrintOutput();

            base.OnLoad();
        }

        private void InitShaders()
        {
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, Shaders.VertexShader);
            GL.CompileShader(vertexShader);
            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, Shaders.FragmentShader);
            GL.CompileShader(fragmentShader);
            _shaderProgramId = GL.CreateProgram();
            GL.AttachShader(_shaderProgramId, vertexShader);
            GL.AttachShader(_shaderProgramId, fragmentShader);
            GL.LinkProgram(_shaderProgramId);
            GL.DetachShader(_shaderProgramId, vertexShader);
            GL.DetachShader(_shaderProgramId, fragmentShader);

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
            _shaderLocations.UseFog = GL.GetUniformLocation(_shaderProgramId, "fog_enable");
            _shaderLocations.FogColor = GL.GetUniformLocation(_shaderProgramId, "fog_color");
            _shaderLocations.FogOffset = GL.GetUniformLocation(_shaderProgramId, "fog_offset");

            _shaderLocations.UseOverride = GL.GetUniformLocation(_shaderProgramId, "use_override");
            _shaderLocations.OverrideColor = GL.GetUniformLocation(_shaderProgramId, "override_color");
        }

        private string FormatOnOff(bool setting)
        {
            return setting ? "on" : "off";
        }

        private void InitTextures(Model model)
        {
            _textureMap[model] = new List<int>();
            int minParameter = _textureFiltering ? (int)TextureMinFilter.Linear : (int)TextureMinFilter.Nearest;
            int magParameter = _textureFiltering ? (int)TextureMagFilter.Linear : (int)TextureMagFilter.Nearest;
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
                    _logs.Add($"mat {material.Name} of model {model.Name} has render mode {material.RenderMode}");
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
                BindAndMakeTexture(material, _textureCount, width, height, minParameter, magParameter, pixels);
                GL.BindTexture(TextureTarget.Texture2D, 0);
            }
        }

        private void BindAndMakeTexture(Material material, int id, int width, int height, int minParameter, int magParameter, List<uint> pixels)
        {
            GL.BindTexture(TextureTarget.Texture2D, id);
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
        }

        private async Task UpdateModelStates(float time)
        {
            while (_loadQueue.TryDequeue(out Model? model))
            {
                InitTextures(model);
                _models.Add(model);
                _modelMap.Add(model.SceneId, model);
                await PrintOutput();
            }

            while (_unloadQueue.TryDequeue(out Model? model))
            {
                Deselect();
                _selectedModelId = 0;
                _selectedMeshId = 0;
                _selectionMode = SelectionMode.None;
                foreach (int index in _textureMap[model].Where(i => i != -1).Distinct())
                {
                    GL.DeleteTexture(index);
                }
                _textureMap.Remove(model);
                _models.Remove(model);
                _modelMap.Remove(model.SceneId);
                await PrintOutput();
            }

            // sktodo:
            // - allow selecting nodes
            // - allow entering scene ID/mesh ID to select
            // - make a "go to/look at" function for the selected model/mesh
            if (_selectionMode != SelectionMode.None)
            {
                if (_selectionMode == SelectionMode.Mesh)
                {
                    UpdateSelected(SelectedModel.Meshes[_selectedMeshId], time);
                }
                else if (_selectionMode == SelectionMode.Model)
                {
                    foreach (Mesh mesh in SelectedModel.Meshes)
                    {
                        UpdateSelected(mesh, time);
                    }
                }
            }
        }

        protected override async void OnRenderFrame(FrameEventArgs args)
        {
            // extra non-rendering updates
            await UpdateModelStates((float)args.Time);
            OnKeyHeld();

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.GetFloat(GetPName.Viewport, out Vector4 viewport);
            float aspect = (viewport.Z - viewport.X) / (viewport.W - viewport.Y);

            GL.MatrixMode(MatrixMode.Projection);
            float fov = MathHelper.DegreesToRadians(80.0f);
            var perspectiveMatrix = Matrix4.CreatePerspectiveFieldOfView(fov, aspect, 0.001f, 10000.0f);
            GL.LoadMatrix(ref perspectiveMatrix);

            TransformCamera();
            UpdateCameraPosition();

            RenderScene(args.Time);
            SwapBuffers();
            base.OnRenderFrame(args);
        }

        private void SetSelected(uint sceneId)
        {
            Deselect();
            _selectedModelId = sceneId;
            foreach (Mesh mesh in SelectedModel.Meshes)
            {
                mesh.OverrideColor = new Vector4(1f, 1f, 1f, 1f);
            }
        }

        private void SetSelected(uint sceneId, int meshId)
        {
            Deselect();
            _selectedModelId = sceneId;
            _selectedMeshId = meshId;
            SelectedModel.Meshes[meshId].OverrideColor = new Vector4(1f, 1f, 1f, 1f);
        }

        private bool _flashUp = false;

        private void UpdateSelected(Mesh mesh, float time)
        {
            // todo: meshes can get out sync (e.g. Crate01)
            Vector4 color = mesh.OverrideColor.GetValueOrDefault();
            float value = color.X;
            value -= time * 1.5f * (_flashUp ? -1 : 1);
            if (value < 0)
            {
                value = 0;
                _flashUp = true;
            }
            else if (value > 1)
            {
                value = 1;
                _flashUp = false;
            }
            mesh.OverrideColor = new Vector4(value, value, value, color.W);
        }

        private void Deselect()
        {
            foreach (Mesh mesh in SelectedModel.Meshes)
            {
                mesh.OverrideColor = SelectedModel.Type == ModelType.Placeholder ? mesh.PlaceholderColor : null;
            }
            _flashUp = false;
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
                if (model.Type == ModelType.Placeholder && !_showInvisible)
                {
                    continue;
                }
                ProcessAnimations(model, elapsedTime);
                GL.MatrixMode(MatrixMode.Modelview);
                GL.PushMatrix();
                Matrix4 transform = model.Transform;
                if (model.Type == ModelType.Item)
                {
                    float rotation = (float)(model.Rotation.Y + elapsedTime * 360 * 0.35) % 360;
                    model.Rotation = new Vector3(model.Rotation.X, rotation, model.Rotation.Z);
                    transform.M42 += (MathF.Sin(model.Rotation.Y / 180 * MathF.PI) + 1) / 8f;
                }
                GL.MultMatrix(ref transform);
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

        private void UpdateUniforms()
        {
            GL.Uniform4(_shaderLocations.Light1Vector, _light1Vector);
            GL.Uniform4(_shaderLocations.Light1Color, _light1Color);
            GL.Uniform4(_shaderLocations.Light2Vector, _light2Vector);
            GL.Uniform4(_shaderLocations.Light2Color, _light2Color);
            GL.Uniform1(_shaderLocations.UseFog, _hasFog && _showFog ? 1 : 0);
            GL.Uniform4(_shaderLocations.FogColor, _fogColor);
            GL.Uniform1(_shaderLocations.FogOffset, _fogOffset);
        }

        private void RenderRoom(Model model)
        {
            GL.UseProgram(_shaderProgramId);
            UpdateUniforms();
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
            GL.AlphaFunc(AlphaFunction.Gequal, 0.25f);
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
            GL.DepthMask(true);
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.Normal);
            }
            // pass 2: opaque pixels on translucent surfaces
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Enable(EnableCap.AlphaTest);
            GL.AlphaFunc(AlphaFunction.Gequal, 0.95f);
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.Normal, invertFilter: true);
            }
            // pass 3: translucent
            GL.AlphaFunc(AlphaFunction.Less, 0.95f);
            //GL.Enable(EnableCap.Blend);
            GL.DepthMask(false);
            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.Normal, invertFilter: true);
            }
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.AlphaTest);
            GL.UseProgram(0);
        }

        private void RenderNode(Model model, Node node, RenderMode modeFilter, bool invertFilter = false)
        {
            if (node.MeshCount > 0 && node.Enabled)
            {
                // temporary -- applying transforms on models which have node animation breaks them,
                // presumably because information needs to come from the animation which we aren't loading yet
                if (model.NodeAnimationGroups.Count == 0 || model.ForceApplyTransform)
                {
                    GL.MatrixMode(MatrixMode.Modelview);
                    GL.PushMatrix();
                    Matrix4 transform = node.Transform;
                    // node transforms applied to room meshes only seem to break things (positions are wrong)
                    if (model.Type == ModelType.Room)
                    {
                        transform = transform.ClearTranslation();
                    }
                    GL.MultMatrix(ref transform);
                }
                int meshStart = node.MeshId / 2;
                for (int i = 0; i < node.MeshCount; i++)
                {
                    int meshId = meshStart + i;
                    Mesh mesh = model.Meshes[meshId];
                    Material material = model.Materials[mesh.MaterialId];
                    RenderMode renderMode = _selectionMode == SelectionMode.None || !_showSelection
                        ? material.RenderMode
                        : material.GetEffectiveRenderMode(mesh);
                    if ((!invertFilter && renderMode != modeFilter)
                        || (invertFilter && renderMode == modeFilter)
                        || (_selectionMode == SelectionMode.None && (!model.Visible || !mesh.Visible)))
                    {
                        continue;
                    }
                    GL.Uniform1(_shaderLocations.IsBillboard, node.Type == 1 ? 1 : 0);
                    RenderMesh(model, mesh, material);
                }
                if (model.NodeAnimationGroups.Count == 0 || model.ForceApplyTransform)
                {
                    GL.MatrixMode(MatrixMode.Modelview);
                    GL.PopMatrix();
                }
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
            DoTexture(model, mesh, material);
            DoLighting(mesh, material);
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
            DoDlist(model, mesh);
            if (_lighting)
            {
                GL.Disable(EnableCap.Lighting);
            }
        }

        private void DoTexture(Model model, Mesh mesh, Material material)
        {
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
            // _showSelection affects the placeholder colors too
            if (mesh.OverrideColor != null && _showSelection)
            {
                GL.Uniform1(_shaderLocations.UseOverride, 1);
                var overrideColor = new Vector4(
                    mesh.OverrideColor.Value.X,
                    mesh.OverrideColor.Value.Y,
                    mesh.OverrideColor.Value.Z,
                    mesh.OverrideColor.Value.W);
                GL.Uniform4(_shaderLocations.OverrideColor, ref overrideColor);
            }
            else
            {
                GL.Uniform1(_shaderLocations.UseOverride, 0);
            }
            GL.MatrixMode(MatrixMode.Texture);
            GL.LoadIdentity();
            if (model.Animate && textureId != UInt16.MaxValue && material.TexcoordAnimationId != -1)
            {
                GL.Scale(1.0f / width, 1.0f / height, 1.0f);
                AnimateTexcoords(model, material, width, height);
            }
            else if (material.TexgenMode != TexgenMode.None && model.TextureMatrices.Count > 0)
            {
                Matrix4 matrix = model.TextureMatrices[material.MatrixId];
                GL.LoadMatrix(ref matrix);
            }
            else if (material.TexgenMode != 0)
            {
                GL.Translate(material.TranslateS, material.TranslateT, 0.0f);
                GL.Scale(material.ScaleS, material.ScaleT, 1.0f);
                GL.Scale(1.0f / width, 1.0f / height, 1.0f);
            }
            else
            {
                GL.Scale(1.0f / width, 1.0f / height, 1.0f);
            }
            GL.Uniform1(_shaderLocations.UseTexture, GL.IsEnabled(EnableCap.Texture2D) ? 1 : 0);
        }

        private void DoLighting(Mesh mesh, Material material)
        {
            if (_lighting && material.Lighting != 0 && (mesh.OverrideColor == null || !_showSelection))
            {
                // todo: would be nice if the approaches for this and the room lights were the same
                var ambient = new Vector4(
                    material.Ambient.Red / 31.0f,
                    material.Ambient.Green / 31.0f,
                    material.Ambient.Blue / 31.0f,
                    1.0f
                );
                var diffuse = new Vector4(
                    material.Diffuse.Red / 31.0f,
                    material.Diffuse.Green / 31.0f,
                    material.Diffuse.Blue / 31.0f,
                    1.0f
                );
                var specular = new Vector4(
                    material.Specular.Red / 31.0f,
                    material.Specular.Green / 31.0f,
                    material.Specular.Blue / 31.0f,
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
        }

        private void DoDlist(Model model, Mesh mesh)
        {
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
                    if (_showColors && (mesh.OverrideColor == null || !_showSelection))
                    {
                        uint rgb = instruction.Arguments[0];
                        uint r = (rgb >> 0) & 0x1F;
                        uint g = (rgb >> 5) & 0x1F;
                        uint b = (rgb >> 10) & 0x1F;
                        GL.Color3(r / 31.0f, g / 31.0f, b / 31.0f);
                    }
                    break;
                case InstructionCode.DIF_AMB:
                    // Actual usage of this is to prepare both the diffuse and ambient colors to be applied when NORMAL is called,
                    // but with bit 15 acting as a flag to directly set the diffuse color as the vertex color immediately.
                    // However, bit 15 and bits 16-30 (ambient color) are never used by MPH. Still, because of the way we're using
                    // the shader program, the easiest hack to apply the diffuse color is to just set it as the vertex color.
                    if (_lighting && (mesh.OverrideColor == null || !_showSelection))
                    {
                        uint rgb = instruction.Arguments[0];
                        uint r = (rgb >> 0) & 0x1F;
                        uint g = (rgb >> 5) & 0x1F;
                        uint b = (rgb >> 10) & 0x1F;
                        GL.Color3(r / 31.0f, g / 31.0f, b / 31.0f);
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
                case InstructionCode.NOP:
                    break;
                default:
                    throw new ProgramException("Unknown opcode");
                }
            }
        }

        private async Task UnloadModel()
        {
            await PrintOutput();
            _ = Output.Read("Unload model: ").ContinueWith(async (task) =>
            {
                await PrintOutput();
                if (UInt32.TryParse(task.Result.Trim(), out uint sceneId) && _modelMap.ContainsKey(sceneId))
                {
                    _unloadQueue.Enqueue(_modelMap[sceneId]);
                }
            });
        }

        private async Task LoadModel()
        {
            await PrintOutput();
            _ = Output.Read("Open model: ").ContinueWith(async (task) =>
            {
                await PrintOutput();
                string modelName = task.Result.Trim();
                if (modelName != "")
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
                        _loadQueue.Enqueue(Read.GetModelByName(modelName, recolor));
                    }
                    catch (ProgramException ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            });
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

        protected override async void OnKeyDown(KeyboardKeyEventArgs e)
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
                await PrintOutput();
            }
            else if (e.Key == Key.C)
            {
                _showColors = !_showColors;
                await PrintOutput();
            }
            else if (e.Key == Key.Q)
            {
                _wireframe = !_wireframe;
                GL.PolygonMode(MaterialFace.FrontAndBack,
                    _wireframe
                        ? OpenToolkit.Graphics.OpenGL.PolygonMode.Line
                        : OpenToolkit.Graphics.OpenGL.PolygonMode.Fill);
                await PrintOutput();
            }
            else if (e.Key == Key.B)
            {
                _faceCulling = !_faceCulling;
                if (!_faceCulling)
                {
                    GL.Disable(EnableCap.CullFace);
                }
                await PrintOutput();
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
                await PrintOutput();
            }
            else if (e.Key == Key.L)
            {
                _lighting = !_lighting;
                await PrintOutput();
            }
            else if (e.Key == Key.G)
            {
                _showFog = !_showFog;
                await PrintOutput();
            }
            else if (e.Key == Key.H)
            {
                _showSelection = !_showSelection;
            }
            else if (e.Key == Key.I)
            {
                _showInvisible = !_showInvisible;
                await PrintOutput();
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
                await PrintOutput();
            }
            else if (e.Control && e.Key == Key.O)
            {
                await LoadModel();
            }
            else if (e.Control && e.Key == Key.U)
            {
                await UnloadModel();
            }
            else if (e.Key == Key.M)
            {
                if (_models.Any(m => m.Meshes.Count > 0))
                {
                    Deselect();
                    if (_selectionMode == SelectionMode.None)
                    {
                        _selectionMode = SelectionMode.Model;
                        SetSelected(_selectedModelId);
                    }
                    else if (_selectionMode == SelectionMode.Model)
                    {
                        if (SelectedModel.Meshes.Count == 0)
                        {
                            _selectionMode = SelectionMode.None;
                        }
                        else
                        {
                            _selectionMode = SelectionMode.Mesh;
                            SetSelected(_selectedModelId, _selectedMeshId);
                        }
                    }
                    else
                    {
                        _selectionMode = SelectionMode.None;
                    }
                    await PrintOutput();
                }
            }
            else if (e.Key == Key.Plus || e.Key == Key.KeypadPlus)
            {
                if (_modelMap.TryGetValue(_selectedModelId, out Model? model) && model.Meshes.Count > 0)
                {
                    if (_selectionMode == SelectionMode.Mesh)
                    {
                        int index = _selectedMeshId + 1;
                        if (index > model.Meshes.Count - 1)
                        {
                            index = 0;
                        }
                        SetSelected(_selectedModelId, index);
                    }
                    else if (_selectionMode == SelectionMode.Model)
                    {
                        Model? nextModel = _models.Where(m => m.SceneId > model.SceneId &&
                            (_showInvisible || m.Type != ModelType.Placeholder)).OrderBy(m => m.SceneId).FirstOrDefault();
                        if (nextModel == null)
                        {
                            nextModel = _models.OrderBy(m => m.SceneId).First(m => m.Meshes.Count > 0);
                        }
                        if (nextModel == null)
                        {
                            nextModel = model;
                        }
                        _selectedMeshId = 0;
                        SetSelected(nextModel.SceneId);
                    }
                    await PrintOutput();
                }
            }
            else if (e.Key == Key.Minus || e.Key == Key.Minus)
            {
                if (_modelMap.TryGetValue(_selectedModelId, out Model? model) && model.Meshes.Count > 0)
                {
                    if (_selectionMode == SelectionMode.Mesh)
                    {
                        int index = _selectedMeshId - 1;
                        if (index < 0)
                        {
                            index = model.Meshes.Count - 1;
                        }
                        SetSelected(_selectedModelId, index);
                    }
                    else if (_selectionMode == SelectionMode.Model)
                    {
                        Model? nextModel = _models.Where(m => m.SceneId < model.SceneId &&
                            (_showInvisible || m.Type != ModelType.Placeholder)).OrderBy(m => m.SceneId).LastOrDefault();
                        if (nextModel == null)
                        {
                            nextModel = _models.OrderBy(m => m.SceneId).Last(m => m.Meshes.Count > 0);
                        }
                        if (nextModel == null)
                        {
                            nextModel = model;
                        }
                        _selectedMeshId = 0;
                        SetSelected(nextModel.SceneId);
                    }
                    await PrintOutput();
                }
            }
            else if (e.Key == Key.Number0 || e.Key == Key.Keypad0)
            {
                if (_selectionMode == SelectionMode.Model)
                {
                    SelectedModel.Visible = !SelectedModel.Visible;
                    await PrintOutput();
                }
                else if (_selectionMode == SelectionMode.Mesh)
                {
                    SelectedModel.Meshes[_selectedMeshId].Visible = !SelectedModel.Meshes[_selectedMeshId].Visible;
                    await PrintOutput();
                }
            }
            else if (e.Key == Key.Number1 || e.Key == Key.Keypad1)
            {
                if (_selectionMode == SelectionMode.Model)
                {
                    int recolor = SelectedModel.CurrentRecolor - 1;
                    if (recolor < 0)
                    {
                        recolor = SelectedModel.Recolors.Count - 1;
                    }
                    SelectedModel.CurrentRecolor = recolor;
                    foreach (int index in _textureMap[SelectedModel].Where(i => i != -1).Distinct())
                    {
                        GL.DeleteTexture(index);
                    }
                    InitTextures(SelectedModel);
                    await PrintOutput();
                }
            }
            else if (e.Key == Key.Number2 || e.Key == Key.Keypad2)
            {
                if (_selectionMode == SelectionMode.Model)
                {
                    int recolor = SelectedModel.CurrentRecolor + 1;
                    if (recolor > SelectedModel.Recolors.Count - 1)
                    {
                        recolor = 0;
                    }
                    SelectedModel.CurrentRecolor = recolor;
                    foreach (int index in _textureMap[SelectedModel].Where(i => i != -1).Distinct())
                    {
                        GL.DeleteTexture(index);
                    }
                    InitTextures(SelectedModel);
                    await PrintOutput();
                }
            }
            else if (e.Key == Key.Escape)
            {
                await Output.End();
                Close();
            }
            base.OnKeyDown(e);
        }

        private void OnKeyHeld()
        {
            if ((KeyboardState.IsKeyDown(Key.AltLeft) || KeyboardState.IsKeyDown(Key.AltRight))
                && _selectionMode == SelectionMode.Model)
            {
                MoveModel();
                return;
            }
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

        private void MoveModel()
        {
            float step = 0.3f;
            if (KeyboardState.IsKeyDown(Key.W)) // move Z-
            {
                SelectedModel.Position = SelectedModel.Position.WithZ(SelectedModel.Position.Z - step);
            }
            else if (KeyboardState.IsKeyDown(Key.S)) // move Z+
            {
                SelectedModel.Position = SelectedModel.Position.WithZ(SelectedModel.Position.Z + step);
            }
            if (KeyboardState.IsKeyDown(Key.Space)) // move Y+
            {
                SelectedModel.Position = SelectedModel.Position.WithY(SelectedModel.Position.Y + step);
            }
            else if (KeyboardState.IsKeyDown(Key.V)) // move Y-
            {
                SelectedModel.Position = SelectedModel.Position.WithY(SelectedModel.Position.Y - step);
            }
            if (KeyboardState.IsKeyDown(Key.A)) // move X-
            {
                SelectedModel.Position = SelectedModel.Position.WithX(SelectedModel.Position.X - step);
            }
            else if (KeyboardState.IsKeyDown(Key.D)) // move X+
            {
                SelectedModel.Position = SelectedModel.Position.WithX(SelectedModel.Position.X + step);
            }
        }

        private enum CameraMode
        {
            Pivot,
            Roam
        }

        private async Task PrintOutput()
        {
            Guid guid = await Output.StartBatch();
            await Output.Clear(guid);
            await Output.Write($"MphRead Version {Program.Version}", guid);
            if (_selectionMode == SelectionMode.Model)
            {
                await PrintModelInfo(guid);
            }
            else if (_selectionMode == SelectionMode.Mesh)
            {
                await PrintMeshInfo(guid);
            }
            else
            {
                await PrintMenu(guid);
            }
            await Output.EndBatch();
        }

        private async Task PrintModelInfo(Guid guid)
        {
            Model model = SelectedModel;
            await Output.Write(guid);
            await Output.Write($"Camera ({_cameraPosition.X}, {_cameraPosition.Y}, {_cameraPosition.Z})", guid);
            await Output.Write(guid);
            await Output.Write($"Model: {model.Name} [{model.SceneId}] {(model.Visible ? "On " : "Off")} - " +
                $"Color {model.CurrentRecolor} / {model.Recolors.Count - 1}", guid);
            await Output.Write($"{model.Type}{(model.Type == ModelType.Placeholder ? $" - {model.EntityType}" : "")}", guid);
            // todo: pickup rotation shows up, but the floating height change does not, would be nice to be consistent
            await Output.Write($"Position ({model.Position.X}, {model.Position.Y}, {model.Position.Z})", guid);
            await Output.Write($"Rotation ({model.Rotation.X}, {model.Rotation.Y}, {model.Rotation.Z})", guid);
            await Output.Write($"   Scale ({model.Scale.X}, {model.Scale.Y}, {model.Scale.Z})", guid);
            await Output.Write($"Nodes {model.Nodes.Count}, Meshes {model.Meshes.Count}, Materials {model.Materials.Count}, " +
                $"Textures {model.Textures.Count}, Palettes {model.Palettes.Count}", guid);
            await Output.Write(guid);
        }
        
        private async Task PrintMeshInfo(Guid guid)
        {
            await PrintModelInfo(guid);
            for (int i = 0; i < SelectedModel.Nodes.Count; i++)
            {
                Node node = SelectedModel.Nodes[i];
                if (node.MeshId / 2 <= _selectedMeshId && node.MeshId / 2 + node.MeshCount - 1 >= _selectedMeshId)
                {
                    await Output.Write($"Node: {node.Name} [{i}] {(node.Enabled ? "On ": "Off")} - Meshes {node.MeshCount}", guid);
                    await Output.Write($"{node.Type} - Parent {node.ParentIndex}, Child {node.ChildIndex}, Next {node.NextIndex}", guid);
                    await Output.Write($"Position ({node.Position.X}, {node.Position.Y}, {node.Position.Z})", guid);
                    await Output.Write($"Rotation ({node.Angle.X}, {node.Angle.Y}, {node.Angle.Z})", guid);
                    await Output.Write($"   Scale ({node.Scale.X}, {node.Scale.Y}, {node.Scale.Z})", guid);
                    await Output.Write($"   ??? 1 ({node.Vector1.X}, {node.Vector1.Y}, {node.Vector1.Z})", guid);
                    await Output.Write($"   ??? 2 ({node.Vector2.X}, {node.Vector2.Y}, {node.Vector2.Z})", guid);
                    await Output.Write(guid);
                    break;
                }
            }
            Mesh mesh = SelectedModel.Meshes[_selectedMeshId];
            await Output.Write($"Mesh: [{_selectedMeshId}] {(mesh.Visible ? "On " : "Off")}", guid);
            await Output.Write($"Material ID {mesh.MaterialId}, DList ID {mesh.DlistId}", guid);
            await Output.Write(guid);
            Material material = SelectedModel.Materials[mesh.MaterialId];
            await Output.Write($"Material: {material.Name} [{mesh.MaterialId}] - {material.RenderMode}, {material.PolygonMode}", guid);
            await Output.Write($"Lighting {material.Lighting}, Alpha {material.Alpha}", guid);
            await Output.Write($"Texture ID {material.TextureId}, Palette ID {material.PaletteId}", guid);
            await Output.Write($" Diffuse ({material.Diffuse.Red}, {material.Diffuse.Green}, {material.Diffuse.Blue})", guid);
            await Output.Write($" Ambient ({material.Ambient.Red}, {material.Ambient.Green}, {material.Ambient.Blue})", guid);
            await Output.Write($"Specular ({material.Specular.Red}, {material.Specular.Green}, {material.Specular.Blue})", guid);
            await Output.Write(guid);
        }

        private async Task PrintMenu(Guid guid)
        {
            if (_cameraMode == CameraMode.Pivot)
            {
                await Output.Write(" - Scroll mouse wheel to zoom", guid);
            }
            else if (_cameraMode == CameraMode.Roam)
            {
                await Output.Write(" - Use WASD, Space, and V to move", guid);
            }
            await Output.Write(" - Hold left mouse button or use arrow keys to rotate", guid);
            await Output.Write(" - Hold Shift to move the camera faster", guid);
            await Output.Write($" - T toggles texturing ({FormatOnOff(_showTextures)})", guid);
            await Output.Write($" - C toggles vertex colours ({FormatOnOff(_showColors)})", guid);
            await Output.Write($" - Q toggles wireframe ({FormatOnOff(_wireframe)})", guid);
            await Output.Write($" - B toggles face culling ({FormatOnOff(_faceCulling)})", guid);
            await Output.Write($" - F toggles texture filtering ({FormatOnOff(_textureFiltering)})", guid);
            await Output.Write($" - L toggles lighting ({FormatOnOff(_lighting)})", guid);
            await Output.Write($" - G toggles fog ({FormatOnOff(_showFog)})", guid);
            await Output.Write($" - I toggles invisible entities ({FormatOnOff(_showInvisible)})", guid);
            await Output.Write($" - P switches camera mode ({(_cameraMode == CameraMode.Pivot ? "pivot" : "roam")})", guid);
            await Output.Write(" - R resets the camera", guid);
            await Output.Write(" - Ctrl+O then enter \"model_name [recolor]\" to load", guid);
            await Output.Write(" - Ctrl+U then enter \"model_id\" to unload", guid);
            await Output.Write(" - Esc closes the viewer", guid);
            await Output.Write(guid);
            if (_logs.Count > 0)
            {
                foreach (string log in _logs)
                {
                    await Output.Write(log, guid);
                }
                await Output.Write(guid);
            }
        }
    }
}
