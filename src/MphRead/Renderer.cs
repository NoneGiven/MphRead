using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MphRead.Export;
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

        public void AddModel(string name, int recolor = 0, bool firstHunt = false)
        {
            _window.AddModel(name, recolor, firstHunt);
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
        public int AlphaScale { get; set; }
        public int UseOverride { get; set; }
        public int OverrideColor { get; set; }
        public int MaterialAlpha { get; set; }
        public int MaterialDecal { get; set; }
        public int ModelMatrix { get; set; }
    }

    public class TextureMap : Dictionary<(int TextureId, int PaletteId), (int BindingId, bool OnlyOpaque)>
    {
        public (int, bool) Get(int textureId, int paletteId)
        {
            return this[(textureId, paletteId)];
        }

        public void Add(int textureId, int paletteId, int bindingId, bool onlyOpaque)
        {
            this[(textureId, paletteId)] = (bindingId, onlyOpaque);
        }
    }

    public class RenderWindow : GameWindow
    {
        private enum SelectionMode
        {
            None,
            Model,
            Node,
            Mesh
        }

        private long _frameCount = -1;
        private bool _roomLoaded = false;
        private readonly List<Model> _models = new List<Model>();
        private readonly Dictionary<int, Model> _modelMap = new Dictionary<int, Model>();
        private readonly ConcurrentQueue<Model> _loadQueue = new ConcurrentQueue<Model>();
        private readonly ConcurrentQueue<Model> _unloadQueue = new ConcurrentQueue<Model>();

        // map each model's texture ID/palette ID combinations to the bound OpenGL texture ID and "onlyOpaque" boolean
        private int _textureCount = 0;
        private readonly Dictionary<int, TextureMap> _texPalMap = new Dictionary<int, TextureMap>();

        private SelectionMode _selectionMode = SelectionMode.None;
        private int _selectedModelId = -1;
        private int _selectedMeshId = 0;
        private int _selectedNodeId = 0;
        private bool _showSelection = true;

        private Model SelectedModel => _modelMap[_selectedModelId];

        private CameraMode _cameraMode = CameraMode.Pivot;
        private float _angleY = 0.0f;
        private float _angleX = 0.0f;
        private float _distance = 5.0f;
        // todo: somehow the axes are reversed from the model coordinates
        private Vector3 _cameraPosition = new Vector3(0, 0, 0);
        private Matrix4 _modelMatrix = Matrix4.Identity;
        private bool _leftMouse = false;

        private bool _showTextures = true;
        private bool _showColors = true;
        private bool _wireframe = false;
        private bool _faceCulling = true;
        private bool _textureFiltering = false;
        private bool _lighting = false;
        private bool _scanVisor = false;
        private bool _showInvisible = false;
        private bool _transformRoomNodes = false; // undocumented

        private static readonly Color4 _clearColor = new Color4(0, 0, 0, 1);

        private Vector4 _light1Vector = default;
        private Vector4 _light1Color = default;
        private Vector4 _light2Vector = default;
        private Vector4 _light2Color = default;
        private bool _hasFog = false;
        private bool _showFog = true;
        private Vector4 _fogColor = default;
        private int _fogOffset = default;
        private bool _frameAdvanceOn = false;
        private bool _advanceOneFrame = false;

        private int _shaderProgramId = 0;
        private readonly ShaderLocations _shaderLocations = new ShaderLocations();

        private readonly List<string> _logs = new List<string>();
        private bool _recording = false;
        private int _framesRecorded = 0;

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
            if (roomMeta.InGameName != null)
            {
                Title = roomMeta.InGameName;
            }
            _models.Insert(0, room);
            _models.AddRange(entities);
            _modelMap.Add(room.SceneId, room);
            foreach (Model entity in entities)
            {
                _modelMap.Add(entity.SceneId, entity);
            }
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

        public void AddModel(string name, int recolor, bool firstHunt)
        {
            Model model = Read.GetModelByName(name, recolor, firstHunt);
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
                UpdateMaterials(model);
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
            _shaderLocations.AlphaScale = GL.GetUniformLocation(_shaderProgramId, "alpha_scale");

            _shaderLocations.UseOverride = GL.GetUniformLocation(_shaderProgramId, "use_override");
            _shaderLocations.OverrideColor = GL.GetUniformLocation(_shaderProgramId, "override_color");
            _shaderLocations.MaterialAlpha = GL.GetUniformLocation(_shaderProgramId, "mat_alpha");
            _shaderLocations.MaterialDecal = GL.GetUniformLocation(_shaderProgramId, "mat_decal");
            _shaderLocations.ModelMatrix = GL.GetUniformLocation(_shaderProgramId, "model_mtx");
        }

        private string FormatOnOff(bool setting)
        {
            return setting ? "on" : "off";
        }

        private void InitTextures(Model model)
        {
            var combos = new HashSet<(int, int)>();
            foreach (Material material in model.Materials)
            {
                if (material.TextureId == UInt16.MaxValue)
                {
                    continue;
                }
                if (material.PaletteId == UInt16.MaxValue)
                {
                    combos.Add((material.TextureId, -1));
                }
                else
                {
                    combos.Add((material.TextureId, material.PaletteId));
                }
                if (material.RenderMode == RenderMode.Unknown3 || material.RenderMode == RenderMode.Unknown4)
                {
                    _logs.Add($"mat {material.Name} of model {model.Name} has render mode {material.RenderMode}");
                    material.RenderMode = RenderMode.Normal;
                }
            }
            foreach (TextureAnimationGroup group in model.TextureAnimationGroups)
            {
                foreach (TextureAnimation animation in group.Animations.Values)
                {
                    for (int i = animation.StartIndex; i < animation.StartIndex + animation.Count; i++)
                    {
                        combos.Add((group.TextureIds[i], group.PaletteIds[i]));
                    }
                }
            }
            if (combos.Count > 0)
            {
                var map = new TextureMap();
                foreach ((int textureId, int paletteId) in combos)
                {
                    _textureCount++;
                    bool onlyOpaque = true;
                    var pixels = new List<uint>();
                    foreach (ColorRgba pixel in model.GetPixels(textureId, paletteId))
                    {
                        uint red = pixel.Red;
                        uint green = pixel.Green;
                        uint blue = pixel.Blue;
                        uint alpha = pixel.Alpha;
                        pixels.Add((red << 0) | (green << 8) | (blue << 16) | (alpha << 24));
                        if (alpha < 255)
                        {
                            onlyOpaque = false;
                        }
                    }
                    Texture texture = model.Textures[textureId];
                    GL.BindTexture(TextureTarget.Texture2D, _textureCount);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, texture.Width, texture.Height, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte, pixels.ToArray());
                    GL.BindTexture(TextureTarget.Texture2D, 0);
                    map.Add(textureId, paletteId, _textureCount, onlyOpaque);
                }
                _texPalMap.Add(model.SceneId, map);
            }
        }

        private void DeleteTextures(int sceneId)
        {
            if (_texPalMap.TryGetValue(sceneId, out TextureMap? map))
            {
                foreach (int id in map.Values.Select(v => v.BindingId).Distinct())
                {
                    GL.DeleteTexture(id);
                }
                _texPalMap.Remove(sceneId);
            }
        }

        private async Task UpdateModelStates(float time)
        {
            while (_loadQueue.TryDequeue(out Model? model))
            {
                SceneSetup.ComputeNodeMatrices(model, index: 0);
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
                DeleteTextures(model.SceneId);
                _models.Remove(model);
                _modelMap.Remove(model.SceneId);
                await PrintOutput();
            }

            if (_selectionMode != SelectionMode.None)
            {
                if (_selectionMode == SelectionMode.Mesh)
                {
                    UpdateSelected(SelectedModel.Meshes[_selectedMeshId], time);
                }
                else if (_selectionMode == SelectionMode.Node)
                {
                    foreach (Mesh mesh in SelectedModel.GetNodeMeshes(_selectedNodeId))
                    {
                        UpdateSelected(mesh, time);
                    }
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
            _frameCount++;
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
            if (_recording)
            {
                Images.Screenshot(Size.X, Size.Y, $"frame{_framesRecorded:0000}");
                _framesRecorded++;
            }
            if (_advanceOneFrame)
            {
                await PrintOutput();
                _advanceOneFrame = false;
            }
            base.OnRenderFrame(args);
        }

        private void SetSelectedModel(int sceneId)
        {
            Deselect();
            _selectedModelId = sceneId;
            foreach (Mesh mesh in SelectedModel.Meshes)
            {
                mesh.OverrideColor = new Vector4(1f, 1f, 1f, 1f);
            }
        }

        private void SetSelectedNode(int sceneId, int nodeId)
        {
            Deselect();
            _selectedModelId = sceneId;
            _selectedNodeId = nodeId;
            foreach (Mesh mesh in SelectedModel.GetNodeMeshes(_selectedNodeId))
            {
                mesh.OverrideColor = new Vector4(1f, 1f, 1f, 1f);
            }
        }

        private void SetSelectedMesh(int sceneId, int meshId)
        {
            Deselect();
            _selectedModelId = sceneId;
            _selectedMeshId = meshId;
            SelectedModel.Meshes[meshId].OverrideColor = new Vector4(1f, 1f, 1f, 1f);
        }

        private bool _flashUp = false;

        private void UpdateSelected(Mesh mesh, float time)
        {
            // todo: meshes can get out sync due to existing alpha values
            // --> also tends to break when tabbing out/resizing/etc.
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
            if (_selectedModelId > -1)
            {
                foreach (Mesh mesh in SelectedModel.Meshes)
                {
                    mesh.OverrideColor = SelectedModel.Type == ModelType.Placeholder ? mesh.PlaceholderColor : null;
                }
            }
            _flashUp = false;
        }

        private void ResetCamera()
        {
            _angleX = 0;
            _angleY = 0;
            _distance = 5.0f;
            if (_cameraMode == CameraMode.Roam)
            {
                _cameraPosition = new Vector3(0, 0, 0);
            }
        }

        private bool FloatEqual(float one, float two)
        {
            return MathF.Abs(one - two) < 0.001f;
        }

        private void LookAt(Vector3 target, bool skipGoTo)
        {
            if (_cameraMode == CameraMode.Roam)
            {
                if (!skipGoTo)
                {
                    _cameraPosition = -1 * target.WithZ(target.Z + 5);
                }
                Vector3 position = -1 * _cameraPosition;
                Vector3 unit = FloatEqual(position.Z, target.Z) && FloatEqual(position.X, target.X)
                    ? Vector3.UnitZ
                    : Vector3.UnitY;
                Matrix4.LookAt(position, target, unit).ExtractRotation().ToEulerAngles(out Vector3 angles);
                _angleX = MathHelper.RadiansToDegrees(angles.X + angles.Z);
                if (_angleX < -90)
                {
                    _angleX += 360;
                }
                else if (_angleX > 90)
                {
                    _angleX -= 360;
                }
                _angleY = MathHelper.RadiansToDegrees(angles.Y);
                if (FloatEqual(MathF.Abs(angles.Z), MathF.PI))
                {
                    _angleY = 180 - _angleY;
                }
            }
        }

        private void TransformCamera()
        {
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            if (_cameraMode == CameraMode.Pivot)
            {
                GL.Translate(0, 0, _distance * -1);
                GL.Rotate(_angleX, 1, 0, 0);
                GL.Rotate(_angleY, 0, 1, 0);
            }
            else if (_cameraMode == CameraMode.Roam)
            {
                GL.Rotate(_angleX, 1, 0, 0);
                GL.Rotate(_angleY, 0, 1, 0);
                GL.Translate(_cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z);
            }
        }

        private void UpdateCameraPosition()
        {
            if (_cameraMode == CameraMode.Pivot)
            {
                float angleX = _angleY + 90;
                if (angleX > 360)
                {
                    angleX -= 360;
                }
                float angleY = _angleX + 90;
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
            if (two.Type == ModelType.Room)
            {
                return 1;
            }
            if (one.Type == ModelType.JumpPad && two.Type == ModelType.JumpPadBeam)
            {
                return -1;
            }
            if (one.Type == ModelType.JumpPadBeam && two.Type == ModelType.JumpPad)
            {
                return 1;
            }
            float distanceOne = Vector3.Distance(-1 * _cameraPosition, one.Position);
            float distanceTwo = Vector3.Distance(-1 * _cameraPosition, two.Position);
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
            _models.Sort(CompareModels);
            foreach (Model model in _models)
            {
                if ((model.Type == ModelType.Placeholder && !_showInvisible) || (model.ScanVisorOnly && !_scanVisor))
                {
                    continue;
                }
                if (_frameCount != 0 &&
                    ((!_frameAdvanceOn && _frameCount % 2 == 0)
                    || (_frameAdvanceOn && _advanceOneFrame)))
                {
                    ProcessAnimations(model);
                }
                GL.MatrixMode(MatrixMode.Modelview);
                GL.PushMatrix();
                Matrix4 transform = model.Transform;
                GL.MultMatrix(ref transform);
                _modelMatrix = Matrix4.Identity;
                _modelMatrix *= transform;
                if (model.Rotating)
                {
                    model.Spin = (float)(model.Spin + elapsedTime * 360 * 0.35) % 360;
                    transform = SceneSetup.ComputeNodeTransforms(Vector3.One, new Vector3(
                        MathHelper.DegreesToRadians(model.SpinAxis.X * model.Spin),
                        MathHelper.DegreesToRadians(model.SpinAxis.Y * model.Spin),
                        MathHelper.DegreesToRadians(model.SpinAxis.Z * model.Spin)),
                        Vector3.Zero);
                    if (model.Floating)
                    {
                        transform.M42 += (MathF.Sin(model.Spin / 180 * MathF.PI) + 1) / 8f;
                    }
                    GL.MultMatrix(ref transform);
                    _modelMatrix *= transform;
                }
                UpdateMaterials(model);
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

        private void UpdateMaterials(Model model)
        {
            foreach (Material material in model.Materials.Where(m => m.TextureId != UInt16.MaxValue))
            {
                int textureId = material.CurrentTextureId;
                int paletteId = material.CurrentPaletteId;
                // todo: group indexing
                if (model.TextureAnimationGroups.Count > 0)
                {
                    TextureAnimationGroup group = model.TextureAnimationGroups[0];
                    if (group.Animations.TryGetValue(material.Name, out TextureAnimation animation))
                    {
                        for (int i = animation.StartIndex; i < animation.StartIndex + animation.Count; i++)
                        {
                            if (group.FrameIndices[i] == group.CurrentFrame)
                            {
                                textureId = group.TextureIds[i];
                                paletteId = group.PaletteIds[i];
                                break;
                            }
                        }
                    }
                }
                (int bindingId, bool onlyOpaque) = _texPalMap[model.SceneId].Get(textureId, paletteId);
                material.TextureBindingId = bindingId;
                material.CurrentTextureId = textureId;
                material.CurrentPaletteId = paletteId;
                UpdateMaterial(material, onlyOpaque, model.Textures[textureId].Format);
            }
        }

        private void UpdateMaterial(Material material, bool onlyOpaque, TextureFormat textureFormat)
        {
            // - if material alpha is less than 31, and render mode is not Translucent, set to Translucent
            // - else if render mode is not Normal, but there are no non-opaque pixels, set to Normal
            // - else if render mode is Normal, but there are non-opaque pixels, set to AlphaTest
            // - if render mode is Translucent, material alpha is 31, and texture format is DirectRgba, set to AlphaTest
            if (material.Alpha < 31)
            {
                material.RenderMode = RenderMode.Translucent;
            }
            else if (material.RenderMode != RenderMode.Normal && onlyOpaque)
            {
                material.RenderMode = RenderMode.Normal;
            }
            else if (material.RenderMode == RenderMode.Normal && !onlyOpaque)
            {
                material.RenderMode = RenderMode.AlphaTest;
            }
            if (material.RenderMode == RenderMode.Translucent && material.Alpha == 31 && textureFormat == TextureFormat.DirectRgb)
            {
                material.RenderMode = RenderMode.AlphaTest;
            }
        }

        private void RenderRoom(Model model)
        {
            // todo: should use room nodes only as roots; need to handle things like force fields separately
            GL.UseProgram(_shaderProgramId);
            UpdateUniforms();
            GL.Uniform1(_shaderLocations.AlphaScale, 1.0f);
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
            GL.AlphaFunc(AlphaFunction.Gequal, 0.01f);
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
            GL.Uniform1(_shaderLocations.AlphaScale, 1.0f);
            // pass 1: opaque
            GL.DepthMask(true);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.Normal);
            }
            // pass 2: opaque pixels on translucent surfaces
            GL.Enable(EnableCap.AlphaTest);
            GL.AlphaFunc(AlphaFunction.Gequal, 0.999f);
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.Decal);
            }
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.Translucent);
            }
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.AlphaTest);
            }
            // pass 3: translucent
            GL.AlphaFunc(AlphaFunction.Less, 0.999f);
            GL.DepthMask(false);
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.Decal);
            }
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.Translucent);
            }
            foreach (Node node in model.Nodes)
            {
                RenderNode(model, node, RenderMode.AlphaTest);
            }
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.AlphaTest);
            GL.UseProgram(0);
        }

        private void RenderNode(Model model, Node node, RenderMode modeFilter, bool invertFilter = false)
        {
            if (node.MeshCount > 0 && node.Enabled && model.NodeParentsEnabled(node))
            {
                GL.MatrixMode(MatrixMode.Modelview);
                GL.PushMatrix();
                Matrix4 transform = node.Transform;
                if (model.Type == ModelType.Room && !_transformRoomNodes)
                {
                    transform = Matrix4.Identity;
                }
                GL.MultMatrix(ref transform);
                _modelMatrix *= transform;
                GL.UniformMatrix4(_shaderLocations.ModelMatrix, transpose: false, ref _modelMatrix);
                foreach (Mesh mesh in model.GetNodeMeshes(node))
                {
                    Material material = model.Materials[mesh.MaterialId];
                    RenderMode renderMode = _selectionMode == SelectionMode.None || !_showSelection
                        ? material.RenderMode
                        : material.GetEffectiveRenderMode(mesh);
                    if ((!invertFilter && renderMode != modeFilter)
                        || (invertFilter && renderMode == modeFilter)
                        || !model.Visible || !mesh.Visible)
                    {
                        continue;
                    }
                    GL.Uniform1(_shaderLocations.IsBillboard, node.Billboard ? 1 : 0);
                    RenderMesh(model, mesh, material);
                }
                GL.MatrixMode(MatrixMode.Modelview);
                GL.PopMatrix();
            }
        }

        private void ProcessAnimations(Model model)
        {
            foreach (TexcoordAnimationGroup group in model.TexcoordAnimationGroups)
            {
                group.CurrentFrame++;
                group.CurrentFrame %= group.FrameCount;
            }
            foreach (TextureAnimationGroup group in model.TextureAnimationGroups)
            {
                group.CurrentFrame++;
                group.CurrentFrame %= group.FrameCount;
            }
        }

        private float InterpolateAnimation(IReadOnlyList<float> values, int start, int frame, int blend, int lutLength, int frameCount,
            bool isRotation = false)
        {
            if (lutLength == 1)
            {
                return values[start];
            }
            if (blend == 1)
            {
                return values[start + frame];
            }
            int limit = (frameCount - 1) >> (blend >> 1) << (blend >> 1);
            if (frame >= limit)
            {
                return values[start + lutLength - (frameCount - limit - (frame - limit))];
            }
            int index = Math.DivRem(frame, blend, out int remainder);
            if (remainder == 0)
            {
                return values[start + index];
            }
            float first = values[start + index];
            float second = values[start + index + 1];
            if (isRotation)
            {
                if (first - second > MathF.PI)
                {
                    second += MathF.PI * 2f;
                }
                else if (first - second < -MathF.PI)
                {
                    first += MathF.PI * 2f;
                }
            }
            float factor = 1.0f / blend * remainder;
            return first + (second - first) * factor;
        }

        private void AnimateTexcoords(TexcoordAnimationGroup group, TexcoordAnimation animation, int width, int height)
        {
            float scaleS = InterpolateAnimation(group.Scales, animation.ScaleLutIndexS, group.CurrentFrame,
                animation.ScaleBlendS, animation.ScaleLutLengthS, group.FrameCount);
            float scaleT = InterpolateAnimation(group.Scales, animation.ScaleLutIndexT, group.CurrentFrame,
                animation.ScaleBlendT, animation.ScaleLutLengthT, group.FrameCount);
            float rotate = InterpolateAnimation(group.Rotations, animation.RotateLutIndexZ, group.CurrentFrame,
                animation.RotateBlendZ, animation.RotateLutLengthZ, group.FrameCount, isRotation: true);
            float translateS = InterpolateAnimation(group.Translations, animation.TranslateLutIndexS, group.CurrentFrame,
                animation.TranslateBlendS, animation.TranslateLutLengthS, group.FrameCount);
            float translateT = InterpolateAnimation(group.Translations, animation.TranslateLutIndexT, group.CurrentFrame,
                animation.TranslateBlendT, animation.TranslateLutLengthT, group.FrameCount);
            GL.MatrixMode(MatrixMode.Texture);
            GL.Translate(translateS * width, translateT * height, 0);
            if (rotate != 0)
            {
                GL.Translate(width / 2, height / 2, 0);
                GL.Rotate(MathHelper.RadiansToDegrees(rotate), Vector3.UnitZ);
                GL.Translate(-width / 2, -height / 2, 0);
            }
            GL.Scale(scaleS, scaleT, 1);
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
            int textureId = material.CurrentTextureId;
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
                GL.BindTexture(TextureTarget.Texture2D, material.TextureBindingId);
                int minParameter = _textureFiltering ? (int)TextureMinFilter.Linear : (int)TextureMinFilter.Nearest;
                int magParameter = _textureFiltering ? (int)TextureMagFilter.Linear : (int)TextureMagFilter.Nearest;
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
                GL.Uniform1(_shaderLocations.MaterialAlpha, material.Alpha / 31.0f);
                GL.Uniform1(_shaderLocations.MaterialDecal, material.PolygonMode == PolygonMode.Decal ? 1 : 0);
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

            TexcoordAnimationGroup? group = null;
            TexcoordAnimation? animation = null;
            if (model.TexcoordAnimationGroups.Count > 0 && textureId != UInt16.MaxValue)
            {
                // todo: this is essentially just always using the first group now
                group = model.TexcoordAnimationGroups[material.TexcoordAnimationId];
                if (group.Animations.TryGetValue(material.Name, out TexcoordAnimation result))
                {
                    animation = result;
                }
            }
            if (group != null && animation != null)
            {
                GL.Scale(1.0f / width, 1.0f / height, 1.0f);
                AnimateTexcoords(group, animation.Value, width, height);
            }
            else if (material.TexgenMode != TexgenMode.None)
            {
                if (model.TextureMatrices.Count > 0)
                {
                    Matrix4 matrix = model.TextureMatrices[material.MatrixId];
                    GL.LoadMatrix(ref matrix);
                }
                else
                {
                    GL.Translate(material.ScaleS * width * material.TranslateS, material.ScaleT * height * material.TranslateT, 0.0f);
                    GL.Scale(material.ScaleS, material.ScaleT, 1.0f);
                    GL.Scale(1.0f / width, 1.0f / height, 1.0f);
                    GL.Rotate(material.RotateZ, Vector3.UnitZ);
                }
            }
            else
            {
                GL.Scale(1.0f / width, 1.0f / height, 1.0f);
                GL.Rotate(material.RotateZ, Vector3.UnitZ);
            }
            GL.Uniform1(_shaderLocations.UseTexture, GL.IsEnabled(EnableCap.Texture2D) ? 1 : 0);
        }

        private void DoLighting(Mesh mesh, Material material)
        {
            if (_lighting && material.Lighting != 0 && (mesh.OverrideColor == null || !_showSelection))
            {
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
                GL.Uniform1(_shaderLocations.UseLight, 1);
                GL.Uniform4(_shaderLocations.Ambient, ambient);
                GL.Uniform4(_shaderLocations.Diffuse, diffuse);
                GL.Uniform4(_shaderLocations.Specular, specular);
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
                if (Int32.TryParse(task.Result.Trim(), out int sceneId) && _modelMap.ContainsKey(sceneId))
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

        private async Task InputSelectModel()
        {
            await PrintOutput();
            _ = Output.Read("Enter model's scene ID: ").ContinueWith(async (task) =>
            {
                await PrintOutput();
                if (_selectionMode == SelectionMode.Model)
                {
                    string value = task.Result.Trim();
                    if (Int32.TryParse(value, out int sceneId) && _modelMap.ContainsKey(sceneId))
                    {
                        SetSelectedModel(sceneId);
                        await PrintOutput();
                    }
                }
            });
        }

        private async Task InputSelectNode()
        {
            await PrintOutput();
            _ = Output.Read("Enter node ID: ").ContinueWith(async (task) =>
            {
                await PrintOutput();
                if (_selectionMode == SelectionMode.Node)
                {
                    string value = task.Result.Trim();
                    if (Int32.TryParse(value, out int nodeId) && nodeId >= 0 && nodeId < SelectedModel.Nodes.Count)
                    {
                        Node node = SelectedModel.Nodes[nodeId];
                        if (node.MeshCount > 0)
                        {
                            _selectedMeshId = node.GetMeshIds().First();
                            SetSelectedNode(_selectedModelId, nodeId);
                            await PrintOutput();
                        }
                    }
                }
            });
        }

        private async Task InputSelectMesh()
        {
            await PrintOutput();
            _ = Output.Read("Enter mesh ID: ").ContinueWith(async (task) =>
            {
                await PrintOutput();
                if (_selectionMode == SelectionMode.Mesh)
                {
                    string value = task.Result.Trim();
                    if (Int32.TryParse(value, out int meshId) && meshId >= 0 && meshId < SelectedModel.Meshes.Count)
                    {
                        for (int i = 0; i < SelectedModel.Nodes.Count; i++)
                        {
                            if (SelectedModel.Nodes[i].GetMeshIds().Contains(meshId))
                            {
                                SetSelectedNode(_selectedModelId, i);
                            }
                        }
                        SetSelectedMesh(_selectedModelId, meshId);
                        await PrintOutput();
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
                _angleX -= e.DeltaY / 1.5f;
                _angleX = Math.Clamp(_angleX, -90.0f, 90.0f);
                _angleY -= e.DeltaX / 1.5f;
                _angleY %= 360f;
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
            if (e.Key == Key.Number5)
            {
                if (!_recording)
                {
                    Images.Screenshot(Size.X, Size.Y);
                }
            }
            else if (e.Key == Key.T)
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
            else if (e.Key == Key.N)
            {
                _transformRoomNodes = !_transformRoomNodes;
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
            else if (e.Key == Key.E)
            {
                _scanVisor = !_scanVisor;
                await PrintOutput();
            }
            else if (e.Key == Key.R)
            {
                if (e.Control && e.Shift)
                {
                    _recording = !_recording;
                    _framesRecorded = 0;
                }
                else
                {
                    ResetCamera();
                }
                await PrintOutput();
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
            else if (e.Key == Key.Enter)
            {
                _frameAdvanceOn = !_frameAdvanceOn;
            }
            else if (e.Key == Key.Period)
            {
                if (_frameAdvanceOn)
                {
                    _advanceOneFrame = true;
                }
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
                    if (e.Control)
                    {
                        if (_selectionMode == SelectionMode.Model)
                        {
                            await InputSelectModel();
                        }
                        else if (_selectionMode == SelectionMode.Node)
                        {
                            await InputSelectNode();
                        }
                        else if (_selectionMode == SelectionMode.Mesh)
                        {
                            await InputSelectMesh();
                        }
                    }
                    else
                    {
                        Deselect();
                        if (_selectedModelId < 0)
                        {
                            _selectedModelId = _models[0].SceneId;
                        }
                        if (_selectionMode == SelectionMode.None)
                        {
                            _selectionMode = SelectionMode.Model;
                            SetSelectedModel(_selectedModelId);
                        }
                        else if (_selectionMode == SelectionMode.Model)
                        {
                            _selectionMode = SelectionMode.Node;
                            SetSelectedNode(_selectedModelId, _selectedNodeId);
                        }
                        else if (_selectionMode == SelectionMode.Node)
                        {
                            if (!SelectedModel.GetNodeMeshes(_selectedNodeId).Any())
                            {
                                _selectionMode = SelectionMode.None;
                            }
                            else
                            {
                                _selectionMode = SelectionMode.Mesh;
                                SetSelectedMesh(_selectedModelId, _selectedMeshId);
                            }
                        }
                        else
                        {
                            _selectionMode = SelectionMode.None;
                        }
                        await PrintOutput();
                    }
                }
            }
            else if (e.Key == Key.Plus || e.Key == Key.KeypadPlus)
            {
                if (_modelMap.TryGetValue(_selectedModelId, out Model? model) && model.Meshes.Count > 0)
                {
                    if (_selectionMode == SelectionMode.Mesh)
                    {
                        int meshIndex = _selectedMeshId + 1;
                        if (meshIndex > model.Meshes.Count - 1)
                        {
                            meshIndex = 0;
                        }
                        if (!SelectedModel.Nodes[_selectedNodeId].GetMeshIds().Contains(meshIndex))
                        {
                            for (int i = 0; i < SelectedModel.Nodes.Count; i++)
                            {
                                if (SelectedModel.Nodes[i].GetMeshIds().Contains(meshIndex))
                                {
                                    SetSelectedNode(_selectedModelId, i);
                                }
                            }
                        }
                        SetSelectedMesh(_selectedModelId, meshIndex);
                    }
                    else if (_selectionMode == SelectionMode.Node)
                    {
                        int nodeIndex;
                        if (e.Shift)
                        {
                            nodeIndex = model.GetNextRoomNodeId(_selectedNodeId);
                        }
                        else
                        {
                            nodeIndex = _selectedNodeId + 1;
                            if (nodeIndex > model.Nodes.Count - 1)
                            {
                                nodeIndex = 0;
                            }
                        }
                        if (model.Nodes[nodeIndex].MeshCount > 0)
                        {
                            _selectedMeshId = model.Nodes[nodeIndex].GetMeshIds().First();
                        }
                        else
                        {
                            _selectedMeshId = 0;
                        }
                        SetSelectedNode(_selectedModelId, nodeIndex);
                    }
                    else if (_selectionMode == SelectionMode.Model)
                    {
                        Model? nextModel = _models.Where(m => m.SceneId > model.SceneId &&
                            (_showInvisible || m.Type != ModelType.Placeholder) &&
                            (_scanVisor || !m.ScanVisorOnly)).OrderBy(m => m.SceneId).FirstOrDefault();
                        if (nextModel == null)
                        {
                            nextModel = _models.OrderBy(m => m.SceneId).First(m => m.Meshes.Count > 0);
                        }
                        _selectedMeshId = 0;
                        _selectedNodeId = 0;
                        SetSelectedModel(nextModel.SceneId);
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
                        int meshIndex = _selectedMeshId - 1;
                        if (meshIndex < 0)
                        {
                            meshIndex = model.Meshes.Count - 1;
                        }
                        if (!SelectedModel.Nodes[_selectedNodeId].GetMeshIds().Contains(meshIndex))
                        {
                            for (int i = 0; i < SelectedModel.Nodes.Count; i++)
                            {
                                if (SelectedModel.Nodes[i].GetMeshIds().Contains(meshIndex))
                                {
                                    SetSelectedNode(_selectedModelId, i);
                                }
                            }
                        }
                        SetSelectedMesh(_selectedModelId, meshIndex);
                    }
                    else if (_selectionMode == SelectionMode.Node)
                    {
                        int nodeIndex;
                        if (e.Shift)
                        {
                            nodeIndex = model.GetPreviousRoomNodeId(_selectedNodeId);
                        }
                        else
                        {
                            nodeIndex = _selectedNodeId - 1;
                            if (nodeIndex < 0)
                            {
                                nodeIndex = model.Nodes.Count - 1;
                            }
                        }
                        if (model.Nodes[nodeIndex].MeshCount > 0)
                        {
                            _selectedMeshId = model.Nodes[nodeIndex].GetMeshIds().First();
                        }
                        else
                        {
                            _selectedMeshId = 0;
                        }
                        SetSelectedNode(_selectedModelId, nodeIndex);
                    }
                    else if (_selectionMode == SelectionMode.Model)
                    {
                        Model? nextModel = _models.Where(m => m.SceneId < model.SceneId &&
                            (_showInvisible || m.Type != ModelType.Placeholder) &&
                            (_scanVisor || !m.ScanVisorOnly)).OrderBy(m => m.SceneId).LastOrDefault();
                        if (nextModel == null)
                        {
                            nextModel = _models.OrderBy(m => m.SceneId).Last(m => m.Meshes.Count > 0);
                        }
                        if (nextModel == null)
                        {
                            nextModel = model;
                        }
                        _selectedMeshId = 0;
                        SetSelectedModel(nextModel.SceneId);
                    }
                    await PrintOutput();
                }
            }
            else if (e.Key == Key.X)
            {
                if (_selectionMode == SelectionMode.Model)
                {
                    LookAt(SelectedModel.Position, e.Control);
                }
                else if (_selectionMode == SelectionMode.Node || _selectionMode == SelectionMode.Mesh)
                {
                    // todo: could keep track of vertex positions during rendering and use them here to locate the mesh
                    LookAt(SelectedModel.Nodes[_selectedNodeId].Position, e.Control);
                }
            }
            else if (e.Key == Key.Number0 || e.Key == Key.Keypad0)
            {
                if (_selectionMode == SelectionMode.Model)
                {
                    SelectedModel.Visible = !SelectedModel.Visible;
                    await PrintOutput();
                }
                else if (_selectionMode == SelectionMode.Node)
                {
                    SelectedModel.Nodes[_selectedNodeId].Enabled = !SelectedModel.Nodes[_selectedNodeId].Enabled;
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
                if (_selectionMode == SelectionMode.Model && SelectedModel.Recolors.Count > 1)
                {
                    int recolor = SelectedModel.CurrentRecolor - 1;
                    if (recolor < 0)
                    {
                        recolor = SelectedModel.Recolors.Count - 1;
                    }
                    SelectedModel.CurrentRecolor = recolor;
                    DeleteTextures(SelectedModel.SceneId);
                    InitTextures(SelectedModel);
                    await PrintOutput();
                }
            }
            else if (e.Key == Key.Number2 || e.Key == Key.Keypad2)
            {
                if (_selectionMode == SelectionMode.Model && SelectedModel.Recolors.Count > 1)
                {
                    int recolor = SelectedModel.CurrentRecolor + 1;
                    if (recolor > SelectedModel.Recolors.Count - 1)
                    {
                        recolor = 0;
                    }
                    SelectedModel.CurrentRecolor = recolor;
                    DeleteTextures(SelectedModel.SceneId);
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
                            step * MathF.Sin(MathHelper.DegreesToRadians(-1 * _angleY))
                            * MathF.Cos(MathHelper.DegreesToRadians(_angleX)) * 0.1f,
                        _cameraPosition.Y +
                            step * MathF.Sin(MathHelper.DegreesToRadians(_angleX)) * 0.1f,
                        _cameraPosition.Z +
                            step * MathF.Cos(MathHelper.DegreesToRadians(_angleY))
                            * MathF.Cos(MathHelper.DegreesToRadians(_angleX)) * 0.1f
                    );
                }
                else if (KeyboardState.IsKeyDown(Key.S)) // move backward
                {
                    _cameraPosition = new Vector3(
                        _cameraPosition.X -
                            step * MathF.Sin(MathHelper.DegreesToRadians(-1 * _angleY))
                            * MathF.Cos(MathHelper.DegreesToRadians(_angleX)) * 0.1f,
                        _cameraPosition.Y -
                            step * MathF.Sin(MathHelper.DegreesToRadians(_angleX)) * 0.1f,
                        _cameraPosition.Z -
                            step * MathF.Cos(MathHelper.DegreesToRadians(_angleY))
                            * MathF.Cos(MathHelper.DegreesToRadians(_angleX)) * 0.1f
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
                    float angleX = _angleY - 90;
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
                    float angleX = _angleY + 90;
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
                _angleX += step;
                _angleX = Math.Clamp(_angleX, -90.0f, 90.0f);
            }
            else if (KeyboardState.IsKeyDown(Key.Down)) // rotate down
            {
                _angleX -= step;
                _angleX = Math.Clamp(_angleX, -90.0f, 90.0f);
            }
            if (KeyboardState.IsKeyDown(Key.Left)) // rotate left
            {
                _angleY += step;
                _angleY %= 360f;
            }
            else if (KeyboardState.IsKeyDown(Key.Right)) // rotate right
            {
                _angleY -= step;
                _angleY %= 360f;
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
            string recording = _recording ? " - Recording" : "";
            string frameAdvance = _recording ? " - Frame Advance" : "";
            await Output.Write($"MphRead Version {Program.Version}{recording}{frameAdvance}", guid);
            if (_selectionMode == SelectionMode.Model)
            {
                await PrintModelInfo(guid);
            }
            else if (_selectionMode == SelectionMode.Node)
            {
                await PrintNodeInfo(guid);
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
            await Output.Write($"Camera ({_cameraPosition.X * -1}, {_cameraPosition.Y * -1}, {_cameraPosition.Z * -1})", guid);
            await Output.Write(guid);
            await Output.Write($"Model: {model.Name} [{model.SceneId}] {(model.Visible ? "On " : "Off")} - " +
                $"Color {model.CurrentRecolor} / {model.Recolors.Count - 1}", guid);
            string type = $"{model.Type}";
            if (model.Type == ModelType.Room)
            {
                type += $" ({model.Nodes.Count(n => n.IsRoomNode)})";
            }
            else if (model.Type == ModelType.Placeholder)
            {
                type += $" - {model.EntityType}";
            }
            await Output.Write(type, guid);
            // todo: pickup rotation shows up, but the floating height change does not, would be nice to be consistent
            await Output.Write($"Position ({model.Position.X}, {model.Position.Y}, {model.Position.Z})", guid);
            await Output.Write($"Rotation ({model.Rotation.X}, {model.Rotation.Y}, {model.Rotation.Z})", guid);
            await Output.Write($"   Scale ({model.Scale.X}, {model.Scale.Y}, {model.Scale.Z})", guid);
            await Output.Write($"Nodes {model.Nodes.Count}, Meshes {model.Meshes.Count}, Materials {model.Materials.Count}, " +
                $"Textures {model.Textures.Count}, Palettes {model.Palettes.Count}", guid);
            await Output.Write(guid);
        }

        private async Task PrintNodeInfo(Guid guid)
        {
            string FormatNode(int otherId)
            {
                if (otherId == UInt16.MaxValue)
                {
                    return "None";
                }
                return $"{SelectedModel.Nodes[otherId].Name} [{otherId}]";
            }
            await PrintModelInfo(guid);
            Node node = SelectedModel.Nodes[_selectedNodeId];
            string mesh = $" - Meshes {node.MeshCount}";
            IEnumerable<int> meshIds = node.GetMeshIds().OrderBy(m => m);
            if (meshIds.Count() == 1)
            {
                mesh += $" ({meshIds.First()})";
            }
            else if (meshIds.Count() > 1)
            {
                mesh += $" ({meshIds.First()} - {meshIds.Last()})";
            }
            string enabled = node.Enabled ? (SelectedModel.NodeParentsEnabled(node) ? "On " : "On*") : "Off";
            string billboard = node.Billboard ? " - Billboard" : "";
            await Output.Write($"Node: {node.Name} [{_selectedNodeId}] {enabled}{mesh}{billboard}", guid);
            await Output.Write($"Parent {FormatNode(node.ParentIndex)}", guid);
            await Output.Write($" Child {FormatNode(node.ChildIndex)}", guid);
            await Output.Write($"  Next {FormatNode(node.NextIndex)}", guid);
            await Output.Write($"Position ({node.Position.X}, {node.Position.Y}, {node.Position.Z})", guid);
            await Output.Write($"Rotation ({node.Angle.X}, {node.Angle.Y}, {node.Angle.Z})", guid);
            await Output.Write($"   Scale ({node.Scale.X}, {node.Scale.Y}, {node.Scale.Z})", guid);
            //await Output.Write($"   ??? 1 ({node.Vector1.X}, {node.Vector1.Y}, {node.Vector1.Z})", guid);
            //await Output.Write($"   ??? 2 ({node.Vector2.X}, {node.Vector2.Y}, {node.Vector2.Z})", guid);
            await Output.Write(guid);
        }

        private async Task PrintMeshInfo(Guid guid)
        {
            await PrintNodeInfo(guid);
            Mesh mesh = SelectedModel.Meshes[_selectedMeshId];
            await Output.Write($"Mesh: [{_selectedMeshId}] {(mesh.Visible ? "On " : "Off")} - " +
                $"Material ID {mesh.MaterialId}, DList ID {mesh.DlistId}", guid);
            await Output.Write(guid);
            Material material = SelectedModel.Materials[mesh.MaterialId];
            await Output.Write($"Material: {material.Name} [{mesh.MaterialId}] - {material.RenderMode}, {material.PolygonMode}", guid);
            await Output.Write($"Lighting {material.Lighting}, Alpha {material.Alpha}, " +
                $"XRepeat {material.XRepeat}, YRepeat {material.YRepeat}", guid);
            await Output.Write($"Texture ID {material.CurrentTextureId}, Palette ID {material.CurrentPaletteId}", guid);
            await Output.Write($"Diffuse ({material.Diffuse.Red}, {material.Diffuse.Green}, {material.Diffuse.Blue})" +
                $" Ambient ({material.Ambient.Red}, {material.Ambient.Green}, {material.Ambient.Blue})" +
                $" Specular({ material.Specular.Red}, { material.Specular.Green}, { material.Specular.Blue})", guid);
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
            await Output.Write($" - E toggles Scan Visor ({FormatOnOff(_scanVisor)})", guid);
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
