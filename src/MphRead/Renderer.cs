using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

        public void AddRoom(int id, GameMode mode = GameMode.None, int playerCount = 0,
            BossFlags bossFlags = BossFlags.None, int nodeLayerMask = 0, int entityLayerId = -1)
        {
            RoomMetadata? meta = Metadata.GetRoomById(id);
            if (meta != null)
            {
                _window.AddRoom(meta.Name, mode, playerCount, bossFlags, nodeLayerMask, entityLayerId);
            }
        }

        public void AddRoom(string name, GameMode mode = GameMode.None, int playerCount = 0,
            BossFlags bossFlags = BossFlags.None, int nodeLayerMask = 0, int entityLayerId = -1)
        {
            _window.AddRoom(name, mode, playerCount, bossFlags, nodeLayerMask, entityLayerId);
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
        private readonly Dictionary<int, LightSource> _lightSources = new Dictionary<int, LightSource>();
        private readonly Dictionary<int, DisplayVolume> _unknown8s = new Dictionary<int, DisplayVolume>();

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
        private int _showVolumes = 0;
        private bool _transformRoomNodes = false; // undocumented

        private static readonly Color4 _clearColor = new Color4(0, 0, 0, 1);

        private Vector3 _light1Vector = default;
        private Vector3 _light1Color = default;
        private Vector3 _light2Vector = default;
        private Vector3 _light2Color = default;
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

        public void AddRoom(string name, GameMode mode = GameMode.None, int playerCount = 0,
            BossFlags bossFlags = BossFlags.None, int nodeLayerMask = 0, int entityLayerId = -1)
        {
            if (_roomLoaded)
            {
                throw new InvalidOperationException();
            }
            _roomLoaded = true;
            (Model room, RoomMetadata roomMeta, IReadOnlyList<Model> entities)
                = SceneSetup.LoadRoom(name, mode, playerCount, bossFlags, nodeLayerMask, entityLayerId);
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
                if (entity.Entity is Entity<LightSourceEntityData> lightSource)
                {
                    _lightSources.Add(entity.SceneId, new LightSource(lightSource));
                }
                else if (entity.Entity is Entity<Unknown8EntityData> unknown8)
                {
                    Unknown8EntityData data = unknown8.Data;
                    _unknown8s.Add(entity.SceneId, new DisplayVolume(data.Position, data.Volume, data.VolumeType));
                }
            }
            _light1Vector = roomMeta.Light1Vector;
            _light1Color = new Vector3(
                roomMeta.Light1Color.Red / 31.0f,
                roomMeta.Light1Color.Green / 31.0f,
                roomMeta.Light1Color.Blue / 31.0f
            );
            _light2Vector = roomMeta.Light2Vector;
            _light2Color = new Vector3(
                roomMeta.Light2Color.Red / 31.0f,
                roomMeta.Light2Color.Green / 31.0f,
                roomMeta.Light2Color.Blue / 31.0f
            );
            _lighting = true;
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
            MakeCurrent();
            await Output.Begin();
            GL.ClearColor(_clearColor);

            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Texture2D);

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
            string vertexLog = GL.GetShaderInfoLog(vertexShader);
            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, Shaders.FragmentShader);
            GL.CompileShader(fragmentShader);
            string fragmentLog = GL.GetShaderInfoLog(fragmentShader);
            if (vertexLog != "" || fragmentLog != "")
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                throw new ProgramException("Failed to compile shaders.");
            }
            _shaderProgramId = GL.CreateProgram();
            GL.AttachShader(_shaderProgramId, vertexShader);
            GL.AttachShader(_shaderProgramId, fragmentShader);
            GL.LinkProgram(_shaderProgramId);
            GL.DetachShader(_shaderProgramId, vertexShader);
            GL.DetachShader(_shaderProgramId, fragmentShader);
            GL.DeleteShader(fragmentShader);
            GL.DeleteShader(vertexShader);

            _shaderLocations.UseLight = GL.GetUniformLocation(_shaderProgramId, "use_light");
            _shaderLocations.ShowColors = GL.GetUniformLocation(_shaderProgramId, "show_colors");
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
            _shaderLocations.MaterialAlpha = GL.GetUniformLocation(_shaderProgramId, "mat_alpha");
            _shaderLocations.MaterialMode = GL.GetUniformLocation(_shaderProgramId, "mat_mode");
            _shaderLocations.ModelMatrix = GL.GetUniformLocation(_shaderProgramId, "model_mtx");
            _shaderLocations.ViewMatrix = GL.GetUniformLocation(_shaderProgramId, "view_mtx");
            _shaderLocations.ProjectionMatrix = GL.GetUniformLocation(_shaderProgramId, "proj_mtx");
            _shaderLocations.TextureMatrix = GL.GetUniformLocation(_shaderProgramId, "tex_mtx");
            _shaderLocations.TexgenMode = GL.GetUniformLocation(_shaderProgramId, "texgen_mode");
            _shaderLocations.ToonTable = GL.GetUniformLocation(_shaderProgramId, "toon_table");

            GL.UseProgram(_shaderProgramId);

            var floats = new List<float>();
            foreach (Vector3 vector in Metadata.ToonTable)
            {
                floats.Add(vector.X);
                floats.Add(vector.Y);
                floats.Add(vector.Z);
            }
            GL.Uniform3(_shaderLocations.ToonTable, Metadata.ToonTable.Count, floats.ToArray());
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
                combos.Add((material.TextureId, material.PaletteId));
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
                _updateLists = true;
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
                _updateLists = true;
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

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
            GL.ClearStencil(0);

            // todo: calculate this in the resize event
            GL.GetFloat(GetPName.Viewport, out Vector4 viewport);
            float aspect = (viewport.Z - viewport.X) / (viewport.W - viewport.Y);
            float fov = MathHelper.DegreesToRadians(80.0f);
            var perspectiveMatrix = Matrix4.CreatePerspectiveFieldOfView(fov, aspect, 0.001f, 10000.0f);
            GL.UniformMatrix4(_shaderLocations.ProjectionMatrix, transpose: false, ref perspectiveMatrix);

            TransformCamera();
            UpdateCameraPosition();

            if (_updateLists)
            {
                UpdateLists();
                _updateLists = false;
            }
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

        private Matrix4 _viewMatrix = Matrix4.Identity;
        private Matrix4 _viewInvRotMatrix = Matrix4.Identity;

        private void TransformCamera()
        {
            // todo: only update this when the camera position changes
            _viewMatrix = Matrix4.Identity;
            _viewInvRotMatrix = Matrix4.Identity;
            if (_cameraMode == CameraMode.Pivot)
            {
                _viewMatrix = Matrix4.CreateTranslation(new Vector3(0, 0, _distance * -1));
                _viewMatrix = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(_angleX)) * _viewMatrix;
                _viewMatrix = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_angleY)) * _viewMatrix;
                _viewInvRotMatrix = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(-1 * _angleY));
                _viewInvRotMatrix = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-1 * _angleX)) * _viewInvRotMatrix;
            }
            else if (_cameraMode == CameraMode.Roam)
            {
                _viewMatrix = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(_angleX));
                _viewMatrix = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_angleY)) * _viewMatrix;
                _viewMatrix = Matrix4.CreateTranslation(_cameraPosition) * _viewMatrix;
                _viewInvRotMatrix = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(-1 * _angleY));
                _viewInvRotMatrix = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-1 * _angleX)) * _viewInvRotMatrix;
            }
            GL.UniformMatrix4(_shaderLocations.ViewMatrix, transpose: false, ref _viewMatrix);
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

        private bool _updateLists = true;
        private int _maxListId = 0;
        private readonly Dictionary<int, int> _listIds = new Dictionary<int, int>();

        private void UpdateLists()
        {
            for (int i = 1; i <= _maxListId; i++)
            {
                GL.DeleteLists(i, 1);
            }
            _maxListId = 0;
            for (int i = 0; i < _models.Count; i++)
            {
                _listIds.Clear();
                Model model = _models[i];
                for (int j = 0; j < model.Meshes.Count; j++)
                {
                    Mesh mesh = model.Meshes[j];
                    if (!_listIds.TryGetValue(mesh.DlistId, out int listId))
                    {
                        int textureWidth = 0;
                        int textureHeight = 0;
                        Material material = model.Materials[mesh.MaterialId];
                        if (material.TextureId != UInt16.MaxValue)
                        {
                            Texture texture = model.Textures[material.TextureId];
                            textureWidth = texture.Width;
                            textureHeight = texture.Height;
                        }
                        listId = GL.GenLists(1);
                        GL.NewList(listId, ListMode.Compile);
                        DoDlist(model, mesh, textureWidth, textureHeight);
                        GL.EndList();
                        _maxListId = Math.Max(listId, _maxListId);
                    }
                    mesh.ListId = listId;
                }
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

        private readonly struct MeshInfo
        {
            public readonly Model Model;
            public readonly Node Node;
            public readonly Mesh Mesh;
            public readonly Material Material;
            public readonly int PolygonId;

            public MeshInfo(Model model, Node node, Mesh mesh, Material material, int polygonId)
            {
                Model = model;
                Node = node;
                Mesh = mesh;
                Material = material;
                PolygonId = polygonId;
            }
        }

        // avoiding overhead by duplicating things in these lists
        private readonly List<MeshInfo> _decalMeshes = new List<MeshInfo>();
        private readonly List<MeshInfo> _nonDecalMeshes = new List<MeshInfo>();
        private readonly List<MeshInfo> _translucentMeshes = new List<MeshInfo>();

        private void RenderScene(double elapsedTime)
        {
            _decalMeshes.Clear();
            _nonDecalMeshes.Clear();
            _translucentMeshes.Clear();
            int polygonId = 1;
            _models.Sort(CompareModels);
            for (int i = 0; i < _models.Count; i++)
            {
                Model model = _models[i];
                if (!_frameAdvanceOn || _advanceOneFrame)
                {
                    // todo: FPS stuff
                    if (_frameCount != 0 && _frameCount % 2 == 0)
                    {
                        UpdateAnimationFrames(model);
                    }
                    model.Process(elapsedTime);
                }
                if (!model.Visible || (model.Type == ModelType.Placeholder && !_showInvisible) || (model.ScanVisorOnly && !_scanVisor))
                {
                    continue;
                }
                int modelPolygonId = model.Type == ModelType.Room ? 0 : polygonId++;
                for (int j = 0; j < model.Nodes.Count; j++)
                {
                    Node node = model.Nodes[j];
                    if (node.MeshCount == 0 || !node.Enabled || !model.NodeParentsEnabled(node))
                    {
                        continue;
                    }
                    foreach (Mesh mesh in model.GetNodeMeshes(j))
                    {
                        if (!mesh.Visible)
                        {
                            continue;
                        }
                        Material material = model.Materials[mesh.MaterialId];
                        int meshPolygonId;
                        if (model.Type == ModelType.Room)
                        {
                            if (material.RenderMode == RenderMode.Translucent)
                            {
                                meshPolygonId = polygonId++;
                            }
                            else
                            {
                                meshPolygonId = 0;
                            }
                        }
                        else
                        {
                            meshPolygonId = modelPolygonId;
                        }
                        var meshInfo = new MeshInfo(model, node, mesh, material, meshPolygonId);
                        if (material.RenderMode != RenderMode.Decal)
                        {
                            _nonDecalMeshes.Add(meshInfo);
                        }
                        else
                        {
                            _decalMeshes.Add(meshInfo);
                        }
                        if (material.RenderMode == RenderMode.Translucent)
                        {
                            _translucentMeshes.Add(meshInfo);
                        }
                    }
                }
            }
            UpdateUniforms();
            // pass 1: opaque
            GL.ColorMask(true, true, true, true);
            GL.Enable(EnableCap.AlphaTest);
            GL.AlphaFunc(AlphaFunction.Equal, 1.0f);
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);
            GL.Enable(EnableCap.StencilTest);
            GL.StencilMask(0xFF);
            GL.StencilOp(StencilOp.Zero, StencilOp.Zero, StencilOp.Zero);
            GL.StencilFunc(StencilFunction.Always, 0, 0xFF);
            for (int i = 0; i < _nonDecalMeshes.Count; i++)
            {
                MeshInfo item = _nonDecalMeshes[i];
                RenderMesh(item);
            }
            GL.Disable(EnableCap.AlphaTest);
            // pass 2: decal
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.PolygonOffset(-1, -1);
            // todo?: decals shouldn't render unless they have ~equal depth to the previous polygon,
            // which means the rendering order here needs to be the same as it is in-game
            GL.DepthFunc(DepthFunction.Lequal);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            for (int i = 0; i < _decalMeshes.Count; i++)
            {
                MeshInfo item = _decalMeshes[i];
                RenderMesh(item);
            }
            GL.PolygonOffset(0, 0);
            GL.Disable(EnableCap.PolygonOffsetFill);
            // pass 3: mark transparent faces in stencil
            GL.Enable(EnableCap.AlphaTest);
            GL.AlphaFunc(AlphaFunction.Less, 1.0f);
            GL.ColorMask(false, false, false, false);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
            for (int i = 0; i < _translucentMeshes.Count; i++)
            {
                MeshInfo item = _translucentMeshes[i];
                GL.StencilFunc(StencilFunction.Greater, item.PolygonId, 0xFF);
                RenderMesh(item);
            }
            // pass 4: rebuild depth buffer
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
            GL.StencilFunc(StencilFunction.Always, 0, 0xFF);
            GL.AlphaFunc(AlphaFunction.Equal, 1.0f);
            for (int i = 0; i < _nonDecalMeshes.Count; i++)
            {
                MeshInfo item = _nonDecalMeshes[i];
                RenderMesh(item);
            }
            // pass 5: translucent (behind)
            GL.AlphaFunc(AlphaFunction.Less, 1.0f);
            GL.ColorMask(true, true, true, true);
            GL.DepthMask(false);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
            for (int i = 0; i < _translucentMeshes.Count; i++)
            {
                MeshInfo item = _translucentMeshes[i];
                GL.StencilFunc(StencilFunction.Notequal, item.PolygonId, 0xFF);
                RenderMesh(item);
            }
            // pass 6: translucent (before)
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
            for (int i = 0; i < _translucentMeshes.Count; i++)
            {
                MeshInfo item = _translucentMeshes[i];
                GL.StencilFunc(StencilFunction.Equal, item.PolygonId, 0xFF);
                RenderMesh(item);
            }
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.AlphaTest);
            GL.Disable(EnableCap.StencilTest);
            if (_showVolumes > 0 && (_lightSources.Count > 0 || _unknown8s.Count > 0))
            {
                GL.PolygonMode(MaterialFace.FrontAndBack, OpenToolkit.Graphics.OpenGL.PolygonMode.Fill);
                GL.Uniform1(_shaderLocations.UseLight, 0);
                GL.Uniform1(_shaderLocations.UseFog, 0);
                GL.Uniform1(_shaderLocations.UseTexture, 0);
                GL.Uniform1(_shaderLocations.UseOverride, 1);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.Enable(EnableCap.CullFace);
                // alternative if the depth buffer can't handle the above:
                //GL.Disable(EnableCap.CullFace);
                if (_showVolumes == 1 || _showVolumes == 2)
                {
                    foreach (KeyValuePair<int, LightSource> kvp in _lightSources)
                    {
                        RenderLightVolume(kvp.Value);
                    }
                }
                else if (_showVolumes == 3)
                {
                    foreach (KeyValuePair<int, DisplayVolume> kvp in _unknown8s)
                    {
                        RenderUnknown8Volume(kvp.Value);
                    }
                }
                GL.Disable(EnableCap.Blend);
            }
        }

        private void RenderUnknown8Volume(DisplayVolume volume)
        {
            GL.CullFace(volume.TestPoint(_cameraPosition * -1) ? CullFaceMode.Front : CullFaceMode.Back);
            var transform = Matrix4.CreateTranslation(volume.Position);
            GL.UniformMatrix4(_shaderLocations.ModelMatrix, transpose: false, ref transform);
            var color = new Vector4(1, 1, 1, 0.5f);
            GL.Uniform4(_shaderLocations.OverrideColor, color);
            RenderVolume(volume.Volume);
        }

        private void RenderLightVolume(LightSource lightSource)
        {
            GL.CullFace(lightSource.TestPoint(_cameraPosition * -1) ? CullFaceMode.Front : CullFaceMode.Back);
            var transform = Matrix4.CreateTranslation(lightSource.Position);
            GL.UniformMatrix4(_shaderLocations.ModelMatrix, transpose: false, ref transform);
            Vector3 color = _showVolumes == 1
                ? lightSource.Light1Enabled ? lightSource.Light1Color : Vector3.Zero
                : lightSource.Light2Enabled ? lightSource.Light2Color : Vector3.Zero;
            GL.Uniform4(_shaderLocations.OverrideColor, new Vector4(color, 0.5f));
            RenderVolume(lightSource.Volume);
        }

        private void UpdateUniforms()
        {
            UseRoomLights();
            GL.Uniform1(_shaderLocations.UseFog, _hasFog && _showFog ? 1 : 0);
            GL.Uniform4(_shaderLocations.FogColor, _fogColor);
            GL.Uniform1(_shaderLocations.FogOffset, _fogOffset);
            GL.Uniform1(_shaderLocations.ShowColors, _showColors ? 1 : 0);
        }

        private void UseRoomLights()
        {
            GL.Uniform3(_shaderLocations.Light1Vector, _light1Vector);
            GL.Uniform3(_shaderLocations.Light1Color, _light1Color);
            GL.Uniform3(_shaderLocations.Light2Vector, _light2Vector);
            GL.Uniform3(_shaderLocations.Light2Color, _light2Color);
        }

        private void UseLight1(Vector3 vector, Vector3 color)
        {
            GL.Uniform3(_shaderLocations.Light1Vector, vector);
            GL.Uniform3(_shaderLocations.Light1Color, color);
        }

        private void UseLight2(Vector3 vector, Vector3 color)
        {
            GL.Uniform3(_shaderLocations.Light2Vector, vector);
            GL.Uniform3(_shaderLocations.Light2Color, color);
        }

        private void UpdateMaterials(Model model)
        {
            for (int i = 0; i < model.Materials.Count; i++)
            {
                Material material = model.Materials[i];
                int textureId = material.CurrentTextureId;
                if (textureId == UInt16.MaxValue)
                {
                    continue;
                }
                int paletteId = material.CurrentPaletteId;
                // todo: group indexing
                if (model.TextureAnimationGroups.Count > 0)
                {
                    TextureAnimationGroup group = model.TextureAnimationGroups[0];
                    if (group.Animations.TryGetValue(material.Name, out TextureAnimation animation))
                    {
                        for (int j = animation.StartIndex; j < animation.StartIndex + animation.Count; j++)
                        {
                            if (group.FrameIndices[j] == group.CurrentFrame)
                            {
                                textureId = group.TextureIds[j];
                                paletteId = group.PaletteIds[j];
                                break;
                            }
                        }
                    }
                }
                (int bindingId, bool onlyOpaque) = _texPalMap[model.SceneId].Get(textureId, paletteId);
                material.TextureBindingId = bindingId;
                material.CurrentTextureId = textureId;
                material.CurrentPaletteId = paletteId;
                UpdateMaterial(material, onlyOpaque);
            }
        }

        private void UpdateMaterial(Material material, bool onlyOpaque)
        {
            if (material.CurrentAlpha < 1.0f)
            {
                material.RenderMode = RenderMode.Translucent;
            }
            else if (material.RenderMode != RenderMode.Normal && onlyOpaque)
            {
                material.RenderMode = RenderMode.Normal;
            }
            else if (material.RenderMode == RenderMode.Normal && !onlyOpaque)
            {
                material.RenderMode = RenderMode.Translucent;
            }
        }

        private void RenderMesh(MeshInfo item)
        {
            Model model = item.Model;
            _modelMatrix = model.ExtraTransform;
            if (item.Node.Billboard)
            {
                _modelMatrix = _viewInvRotMatrix * _modelMatrix.ClearRotation();
            }
            UseRoomLights();
            if (model.UseLightOverride)
            {
                // todo: could add a height offset to really match the player position vs. the camera
                Vector3 player = _cameraPosition * -1;
                var vector1 = new Vector3(0, 1, 0); // Octolith's up vector
                Vector3 vector2 = new Vector3(player.X - model.Position.X, 0, player.Z - model.Position.Z).Normalized();
                Matrix3 lightTransform = SceneSetup.GetTransformMatrix(vector2, vector1);
                Vector3 lightVector = (Metadata.OctolithLight1Vector * lightTransform).Normalized();
                GL.Uniform3(_shaderLocations.Light1Vector, lightVector);
                GL.Uniform3(_shaderLocations.Light1Color, Metadata.OctolithLightColor);
                lightVector = (Metadata.OctolithLight2Vector * lightTransform).Normalized();
                GL.Uniform3(_shaderLocations.Light2Vector, lightVector);
                GL.Uniform3(_shaderLocations.Light2Color, Metadata.OctolithLightColor);
            }
            else if (model.UseLightSources)
            {
                UpdateLightSources(model.Position);
            }
            Node node = item.Node;
            Matrix4 nodeTransform = node.Transform;
            if (model.Type == ModelType.Room && !_transformRoomNodes)
            {
                // todo: this is unlikely to matter, but should we pass this for texgen purposes?
                nodeTransform = Matrix4.Identity;
            }
            _modelMatrix = nodeTransform * _modelMatrix;
            GL.UniformMatrix4(_shaderLocations.ModelMatrix, transpose: false, ref _modelMatrix);
            RenderMesh(model, node, item.Mesh, item.Material);
        }

        // todo?: does anything special need to happen for overlapping light sources?
        private void UpdateLightSources(Vector3 position)
        {
            foreach (LightSource lightSource in _lightSources.Values)
            {
                if (lightSource.TestPoint(position))
                {
                    if (lightSource.Light1Enabled)
                    {
                        UseLight1(lightSource.Light1Vector, lightSource.Light1Color);
                    }
                    if (lightSource.Light2Enabled)
                    {
                        UseLight2(lightSource.Light2Vector, lightSource.Light2Color);
                    }
                    break;
                }
            }
        }

        private void UpdateAnimationFrames(Model model)
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
            foreach (MaterialAnimationGroup group in model.MaterialAnimationGroups)
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

        private Matrix4 AnimateTexcoords(TexcoordAnimationGroup group, TexcoordAnimation animation)
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
            var textureMatrix = Matrix4.CreateTranslation(translateS, translateT, 0.0f);
            if (rotate != 0)
            {
                textureMatrix = Matrix4.CreateTranslation(0.5f, 0.5f, 0.0f) * textureMatrix;
                textureMatrix = Matrix4.CreateRotationZ(rotate) * textureMatrix;
                textureMatrix = Matrix4.CreateTranslation(-0.5f, -0.5f, 0.0f) * textureMatrix;
            }
            textureMatrix = Matrix4.CreateScale(scaleS, scaleT, 1) * textureMatrix;
            return textureMatrix;
        }

        private void RenderMesh(Model model, Node node, Mesh mesh, Material material)
        {
            DoMaterial(model, mesh, material);
            DoTexture(model, node, mesh, material);
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
            GL.PolygonMode(MaterialFace.FrontAndBack,
                _wireframe || material.Wireframe != 0
                ? OpenToolkit.Graphics.OpenGL.PolygonMode.Line
                : OpenToolkit.Graphics.OpenGL.PolygonMode.Fill);
            GL.CallList(mesh.ListId);
        }

        private void DoTexture(Model model, Node node, Mesh mesh, Material material)
        {
            int textureId = material.CurrentTextureId;
            if (textureId != UInt16.MaxValue)
            {
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
                Matrix4 texcoordMatrix = Matrix4.Identity;
                TexcoordAnimationGroup? group = null;
                TexcoordAnimation? animation = null;
                // todo: was there a reason animation couldn't be put inside the texgen condition?
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
                    texcoordMatrix = AnimateTexcoords(group, animation.Value);
                }
                if (material.TexgenMode != TexgenMode.None)
                {
                    Matrix4 materialMatrix;
                    // in-game, this is a list of precomputed matrices that we compute on the fly in the next block;
                    // however, we only use it for the one hard-coded matrix in AlimbicCapsule
                    if (model.TextureMatrices.Count > 0)
                    {
                        materialMatrix = model.TextureMatrices[material.MatrixId];
                    }
                    else
                    {
                        materialMatrix = Matrix4.CreateTranslation(material.ScaleS * material.TranslateS,
                            material.ScaleT * material.TranslateT, 0.0f);
                        materialMatrix = Matrix4.CreateScale(material.ScaleS, material.ScaleT, 1.0f) * materialMatrix;
                        materialMatrix = Matrix4.CreateRotationZ(material.RotateZ) * materialMatrix;
                    }
                    // for texcoord texgen, the animation result is used if any, otherwise the material matrix is used.
                    // for normal texgen, two matrices are multiplied. the first is always the material matrix.
                    // the second is the animation result if any, otherwise it's the material matrix again.
                    if (group == null || animation == null)
                    {
                        texcoordMatrix = materialMatrix;
                    }
                    if (material.TexgenMode == TexgenMode.Normal)
                    {
                        Texture texture = model.Textures[material.TextureId];
                        Debug.Assert(texture.Width == texture.Height && texture.Width > 0 && texture.Width % 2 == 0);
                        // the nodeTransform * currentTextureMatrix multiplication is between two 4x3 into a 4x4,
                        // but only reads/writes the upper-left 3x3 of all three matrices
                        // todo: billboard node transforms need to work as they do in-game
                        Matrix4 product = node.Transform.Keep3x3();
                        // todo: this may not be exactly equivalent to the game checking the node animation pointer
                        // note: in-game, this also uses the some_flag CModel field or constant 0,
                        // byt none of the models with normal texgen seem to set bit 0 of that flag, so we can ignore it
                        // --> also, Dialanche sets it pointed to 0 while attacking, but that doesn't seem to chnage its appearance
                        // (at least from the controlling player's camera), so we're ignoring that too
                        if (!model.NodeAnimationGroups.Any(n => n.Animations.Any()))
                        {
                            var currentTextureMatrix = new Matrix4x3(
                                model.ExtraTransform.Row0.Xyz,
                                model.ExtraTransform.Row1.Xyz,
                                model.ExtraTransform.Row2.Xyz,
                                model.ExtraTransform.Row3.Xyz
                            );
                            // in-game, there's only one uniform scale factor for models
                            if (model.Scale.X != 1 || model.Scale.Y != 1 || model.Scale.Z != 1)
                            {
                                Matrix4x3 scaleMatrix = Matrix4x3.Zero;
                                scaleMatrix.M11 = model.Scale.X;
                                scaleMatrix.M22 = model.Scale.Y;
                                scaleMatrix.M33 = model.Scale.Z;
                                currentTextureMatrix = Matrix.Concat43(scaleMatrix, currentTextureMatrix);
                            }
                            if ((model.Flags & 1) > 0)
                            {
                                var cameraMatrix = new Matrix4x3(
                                    _viewMatrix.Row0.Xyz,
                                    _viewMatrix.Row1.Xyz,
                                    _viewMatrix.Row2.Xyz,
                                    _viewMatrix.Row3.Xyz
                                );
                                currentTextureMatrix = Matrix.Concat43(currentTextureMatrix, cameraMatrix);
                            }
                            product *= currentTextureMatrix.Keep3x3();
                        }
                        product.M12 *= -1;
                        product.M13 *= -1;
                        product.M22 *= -1;
                        product.M23 *= -1;
                        product.M32 *= -1;
                        product.M33 *= -1;
                        // the texenv * modelTextureMatrix multiplications are between two 4x4 into a 4x4,
                        // but only reads the upper 3x3 of the first matrix and upper 3x2 of the second,
                        // and only writes the upper 3x3 of the destination
                        product = Matrix.Multiply44(product, materialMatrix);
                        product = Matrix.Multiply44(product, texcoordMatrix) * (1.0f / (texture.Width / 2));
                        texcoordMatrix = new Matrix4(
                            product.Row0 * 16.0f,
                            product.Row1 * 16.0f,
                            product.Row2 * 16.0f,
                            product.Row3
                        );
                        texcoordMatrix.Transpose();
                        GL.TexCoord2(0.5f, 0.5f); // this may be changed by the dlist
                    }
                }
                GL.Uniform1(_shaderLocations.TexgenMode, (int)material.TexgenMode);
                GL.UniformMatrix4(_shaderLocations.TextureMatrix, transpose: false, ref texcoordMatrix);
            }
            GL.Uniform1(_shaderLocations.UseTexture, textureId != UInt16.MaxValue && _showTextures ? 1 : 0);
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
        }

        private void DoMaterial(Model model, Mesh mesh, Material material)
        {
            // todo: control animations so everything isn't playing at once, and remove this temporary line
            material.AnimationFlags = AnimationFlags.DisableAlpha;
            if (_lighting && material.Lighting != 0 && (mesh.OverrideColor == null || !_showSelection))
            {
                GL.Uniform1(_shaderLocations.UseLight, 1);
            }
            else
            {
                GL.Uniform1(_shaderLocations.UseLight, 0);
            }
            Vector3 diffuse = material.CurrentDiffuse;
            Vector3 ambient = material.CurrentAmbient;
            Vector3 specular = material.CurrentSpecular;
            float alpha = material.CurrentAlpha;
            // todo: group indexing
            MaterialAnimationGroup group;
            if (model.MaterialAnimationGroups.Count > 0
                && (group = model.MaterialAnimationGroups[0]).Animations.TryGetValue(material.Name, out MaterialAnimation animation))
            {
                if (!material.AnimationFlags.HasFlag(AnimationFlags.DisableColor))
                {
                    float diffuseR = InterpolateAnimation(group.Colors, animation.DiffuseLutStartIndexR, group.CurrentFrame,
                        animation.DiffuseBlendFactorR, animation.DiffuseLutLengthR, group.FrameCount);
                    float diffuseG = InterpolateAnimation(group.Colors, animation.DiffuseLutStartIndexG, group.CurrentFrame,
                        animation.DiffuseBlendFactorG, animation.DiffuseLutLengthG, group.FrameCount);
                    float diffuseB = InterpolateAnimation(group.Colors, animation.DiffuseLutStartIndexB, group.CurrentFrame,
                        animation.DiffuseBlendFactorB, animation.DiffuseLutLengthB, group.FrameCount);
                    float ambientR = InterpolateAnimation(group.Colors, animation.AmbientLutStartIndexR, group.CurrentFrame,
                        animation.AmbientBlendFactorR, animation.AmbientLutLengthR, group.FrameCount);
                    float ambientG = InterpolateAnimation(group.Colors, animation.AmbientLutStartIndexG, group.CurrentFrame,
                        animation.AmbientBlendFactorG, animation.AmbientLutLengthG, group.FrameCount);
                    float ambientB = InterpolateAnimation(group.Colors, animation.AmbientLutStartIndexB, group.CurrentFrame,
                        animation.AmbientBlendFactorB, animation.AmbientLutLengthB, group.FrameCount);
                    float specularR = InterpolateAnimation(group.Colors, animation.SpecularLutStartIndexR, group.CurrentFrame,
                        animation.SpecularBlendFactorR, animation.SpecularLutLengthR, group.FrameCount);
                    float specularG = InterpolateAnimation(group.Colors, animation.SpecularLutStartIndexG, group.CurrentFrame,
                        animation.SpecularBlendFactorG, animation.SpecularLutLengthG, group.FrameCount);
                    float specularB = InterpolateAnimation(group.Colors, animation.SpecularLutStartIndexB, group.CurrentFrame,
                        animation.SpecularBlendFactorB, animation.SpecularLutLengthB, group.FrameCount);
                    diffuse = new Vector3(diffuseR / 31.0f, diffuseG / 31.0f, diffuseB / 31.0f);
                    ambient = new Vector3(ambientR / 31.0f, ambientG / 31.0f, ambientB / 31.0f);
                    specular = new Vector3(specularR / 31.0f, specularG / 31.0f, specularB / 31.0f);
                }
                if (!material.AnimationFlags.HasFlag(AnimationFlags.DisableAlpha))
                {
                    alpha = InterpolateAnimation(group.Colors, animation.AlphaLutStartIndex, group.CurrentFrame,
                    animation.AlphaBlendFactor, animation.AlphaLutLength, group.FrameCount);
                    alpha /= 31.0f;
                }
            }
            // MPH applies the material colors initially by calling DIF_AMB with bit 15 set,
            // so the diffuse color is always set as the vertex color to start
            // (the emission color is set to white if lighting is disabled or black if lighting is enabled; we can just ignore that)
            GL.Color3(diffuse);
            GL.Uniform3(_shaderLocations.Diffuse, diffuse);
            GL.Uniform3(_shaderLocations.Ambient, ambient);
            GL.Uniform3(_shaderLocations.Specular, specular);
            GL.Uniform1(_shaderLocations.MaterialAlpha, alpha);
            GL.Uniform1(_shaderLocations.MaterialMode, (int)material.PolygonMode);
            material.CurrentAlpha = alpha;
            UpdateMaterials(model);
        }

        private void DoDlist(Model model, Mesh mesh, int textureWidth, int textureHeight)
        {
            IReadOnlyList<RenderInstruction> list = model.RenderInstructionLists[mesh.DlistId];
            float vtxX = 0;
            float vtxY = 0;
            float vtxZ = 0;
            // note: calling this every frame will have some overhead,
            // but baking it in on load would prevent e.g. vertex color toggle
            for (int i = 0; i < list.Count; i++)
            {
                RenderInstruction instruction = list[i];
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
                    // shader - if (_showColors && (mesh.OverrideColor == null || !_showSelection))
                    {
                        uint rgb = instruction.Arguments[0];
                        uint r = (rgb >> 0) & 0x1F;
                        uint g = (rgb >> 5) & 0x1F;
                        uint b = (rgb >> 10) & 0x1F;
                        GL.Color3(r / 31.0f, g / 31.0f, b / 31.0f);
                    }
                    break;
                case InstructionCode.DIF_AMB:
                    {
                        uint rgb = instruction.Arguments[0];
                        uint dr = (rgb >> 0) & 0x1F;
                        uint dg = (rgb >> 5) & 0x1F;
                        uint db = (rgb >> 10) & 0x1F;
                        uint set = (rgb >> 15) & 1;
                        uint ar = (rgb >> 16) & 0x1F;
                        uint ag = (rgb >> 21) & 0x1F;
                        uint ab = (rgb >> 26) & 0x1F;
                        var diffuse = new Vector4(dr / 31.0f, dg / 31.0f, db / 31.0f, 1.0f);
                        var ambient = new Vector4(ar / 31.0f, ag / 31.0f, ab / 31.0f, 1.0f);
                        if (mesh.OverrideColor == null || !_showSelection)
                        {
                            // shader - if (_lighting)
                            // MPH only calls this with zero ambient, and we need to rely on that in order to
                            // use GL.Color to smuggle in the diffuse, since setting uniforms here doesn't work
                            Debug.Assert(ambient.X == 0 && ambient.Y == 0 && ambient.Z == 0);
                            GL.Color4(diffuse.X, diffuse.Y, diffuse.Z, 0.0f);
                            if (set != 0) // shader - && _showColors
                            {
                                // MPH never does this in a dlist
                                Debug.Assert(false);
                                GL.Color3(dr / 31.0f, dg / 31.0f, db / 31.0f);
                            }
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
                        Debug.Assert(textureWidth > 0 && textureHeight > 0);
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
                        GL.TexCoord2(s / 16.0f / textureWidth, t / 16.0f / textureHeight);
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
            else if (e.Key == Key.Z)
            {
                _showVolumes++;
                if (_showVolumes > 3)
                {
                    _showVolumes = 0;
                }
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
            float step = 0.1f;
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
            step = 2.5f;
            Vector3 rotation = SelectedModel.Rotation;
            if (KeyboardState.IsKeyDown(Key.Up)) // rotate up
            {
                rotation.X += step;
                rotation.X %= 360f;
            }
            else if (KeyboardState.IsKeyDown(Key.Down)) // rotate down
            {
                rotation.X -= step;
                rotation.X %= 360f;
            }
            if (KeyboardState.IsKeyDown(Key.Left)) // rotate left
            {
                rotation.Y += step;
                rotation.Y %= 360f;
            }
            else if (KeyboardState.IsKeyDown(Key.Right)) // rotate right
            {
                rotation.Y -= step;
                rotation.Y %= 360f;
            }
            SelectedModel.Rotation = rotation;
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
            string frameAdvance = _frameAdvanceOn ? " - Frame Advance" : "";
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
            string header = "";
            if (_roomLoaded)
            {
                string string1 = $"{(int)(_light1Color.X * 255)};{(int)(_light1Color.Y * 255)};{(int)(_light1Color.Z * 255)}";
                string string2 = $"{(int)(_light2Color.X * 255)};{(int)(_light2Color.Y * 255)};{(int)(_light2Color.Z * 255)}";
                header += $"Room \u001b[38;2;{string1}m████\u001b[0m \u001b[38;2;{string2}m████\u001b[0m";
                header += $" ({_light1Vector.X}, {_light1Vector.Y}, {_light1Vector.Z}) " +
                    $"({_light2Vector.X}, {_light2Vector.Y}, {_light2Vector.Z})";
            }
            else
            {
                header = "No room loaded";
            }
            await Output.Write(header, guid);
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
                if (model.Entity is Entity<LightSourceEntityData> entity)
                {
                    ColorRgb color1 = entity.Data.Light1Color;
                    ColorRgb color2 = entity.Data.Light2Color;
                    string string1 = $"{color1.Red};{color1.Green};{color1.Blue}";
                    string string2 = $"{color2.Red};{color2.Green};{color2.Blue}";
                    type += $" \u001b[38;2;{string1}m████\u001b[0m \u001b[38;2;{string2}m████\u001b[0m";
                    type += $" {entity.Data.Light1Enabled} / {entity.Data.Light2Enabled}";
                    Vector3Fx vector1 = entity.Data.Light1Vector;
                    Vector3Fx vector2 = entity.Data.Light2Vector;
                    type += $" ({vector1.X.FloatValue}, {vector1.Y.FloatValue}, {vector1.Z.FloatValue}) " +
                        $"({vector2.X.FloatValue}, {vector2.Y.FloatValue}, {vector2.Z.FloatValue})";
                }
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
            await Output.Write($"Material: {material.Name} [{mesh.MaterialId}] - {material.RenderMode}, {material.PolygonMode}" +
                $" - {material.TexgenMode}", guid);
            await Output.Write($"Lighting {material.Lighting}, Alpha {material.Alpha}, " +
                $"XRepeat {material.XRepeat}, YRepeat {material.YRepeat}", guid);
            await Output.Write($"Texture ID {material.CurrentTextureId}, Palette ID {material.CurrentPaletteId}", guid);
            await Output.Write($"Diffuse ({material.Diffuse.Red}, {material.Diffuse.Green}, {material.Diffuse.Blue})" +
                $" Ambient ({material.Ambient.Red}, {material.Ambient.Green}, {material.Ambient.Blue})" +
                $" Specular ({ material.Specular.Red}, { material.Specular.Green}, { material.Specular.Blue})", guid);
            await Output.Write(guid);
        }

        private string FormatOnOff(bool setting)
        {
            return setting ? "on" : "off";
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

        private Vector3 GetDiscVertices(Vector3 center, float radius, int index)
        {
            return new Vector3(
                center.X + radius * MathF.Cos(2f * MathF.PI * index / 16f),
                center.Y,
                center.Z + radius * MathF.Sin(2f * MathF.PI * index / 16f)
            );
        }

        private readonly List<Vector3> _sphereVertices = new List<Vector3>();

        private void RenderVolume(CollisionVolume volume)
        {
            if (volume.Type == VolumeType.Box)
            {
                Vector3 point0 = volume.BoxPosition;
                Vector3 sideX = volume.BoxVector1 * volume.BoxDot1;
                Vector3 sideY = volume.BoxVector2 * volume.BoxDot2;
                Vector3 sideZ = volume.BoxVector3 * volume.BoxDot3;
                Vector3 point1 = point0 + sideZ;
                Vector3 point2 = point0 + sideX;
                Vector3 point3 = point0 + sideX + sideZ;
                Vector3 point4 = point0 + sideY;
                Vector3 point5 = point0 + sideY + sideZ;
                Vector3 point6 = point0 + sideX + sideY;
                Vector3 point7 = point0 + sideX + sideY + sideZ;
                // sides
                GL.Begin(PrimitiveType.TriangleStrip);
                GL.Vertex3(point2);
                GL.Vertex3(point6);
                GL.Vertex3(point0);
                GL.Vertex3(point4);
                GL.Vertex3(point1);
                GL.Vertex3(point5);
                GL.Vertex3(point3);
                GL.Vertex3(point7);
                GL.Vertex3(point2);
                GL.Vertex3(point6);
                GL.End();
                // top
                GL.Begin(PrimitiveType.TriangleStrip);
                GL.Vertex3(point5);
                GL.Vertex3(point4);
                GL.Vertex3(point7);
                GL.Vertex3(point6);
                GL.End();
                // bottom
                GL.Begin(PrimitiveType.TriangleStrip);
                GL.Vertex3(point3);
                GL.Vertex3(point2);
                GL.Vertex3(point1);
                GL.Vertex3(point0);
                GL.End();
            }
            else if (volume.Type == VolumeType.Cylinder)
            {
                Vector3 center = volume.CylinderPosition;
                float radius = volume.CylinderRadius;
                Vector3 height = volume.CylinderPosition + volume.CylinderVector * volume.CylinderDot;
                Vector3 pointB1 = GetDiscVertices(center, radius, 0);
                Vector3 pointB2 = GetDiscVertices(center, radius, 1);
                Vector3 pointB3 = GetDiscVertices(center, radius, 2);
                Vector3 pointB4 = GetDiscVertices(center, radius, 3);
                Vector3 pointB5 = GetDiscVertices(center, radius, 4);
                Vector3 pointB6 = GetDiscVertices(center, radius, 5);
                Vector3 pointB7 = GetDiscVertices(center, radius, 6);
                Vector3 pointB8 = GetDiscVertices(center, radius, 7);
                Vector3 pointB9 = GetDiscVertices(center, radius, 8);
                Vector3 pointB10 = GetDiscVertices(center, radius, 9);
                Vector3 pointB11 = GetDiscVertices(center, radius, 10);
                Vector3 pointB12 = GetDiscVertices(center, radius, 11);
                Vector3 pointB13 = GetDiscVertices(center, radius, 12);
                Vector3 pointB14 = GetDiscVertices(center, radius, 13);
                Vector3 pointB15 = GetDiscVertices(center, radius, 14);
                Vector3 pointB16 = GetDiscVertices(center, radius, 15);
                Vector3 pointT1 = pointB1 + height;
                Vector3 pointT2 = pointB2 + height;
                Vector3 pointT3 = pointB3 + height;
                Vector3 pointT4 = pointB4 + height;
                Vector3 pointT5 = pointB5 + height;
                Vector3 pointT6 = pointB6 + height;
                Vector3 pointT7 = pointB7 + height;
                Vector3 pointT8 = pointB8 + height;
                Vector3 pointT9 = pointB9 + height;
                Vector3 pointT10 = pointB10 + height;
                Vector3 pointT11 = pointB11 + height;
                Vector3 pointT12 = pointB12 + height;
                Vector3 pointT13 = pointB13 + height;
                Vector3 pointT14 = pointB14 + height;
                Vector3 pointT15 = pointB15 + height;
                Vector3 pointT16 = pointB16 + height;
                // bottom
                GL.Begin(PrimitiveType.TriangleFan);
                GL.Vertex3(center);
                GL.Vertex3(pointB1);
                GL.Vertex3(pointB2);
                GL.Vertex3(pointB3);
                GL.Vertex3(pointB4);
                GL.Vertex3(pointB5);
                GL.Vertex3(pointB6);
                GL.Vertex3(pointB7);
                GL.Vertex3(pointB8);
                GL.Vertex3(pointB9);
                GL.Vertex3(pointB10);
                GL.Vertex3(pointB11);
                GL.Vertex3(pointB12);
                GL.Vertex3(pointB13);
                GL.Vertex3(pointB14);
                GL.Vertex3(pointB15);
                GL.Vertex3(pointB16);
                GL.Vertex3(pointB1);
                GL.End();
                // top
                GL.Begin(PrimitiveType.TriangleFan);
                GL.Vertex3(center + height);
                GL.Vertex3(pointT16);
                GL.Vertex3(pointT15);
                GL.Vertex3(pointT14);
                GL.Vertex3(pointT13);
                GL.Vertex3(pointT12);
                GL.Vertex3(pointT11);
                GL.Vertex3(pointT10);
                GL.Vertex3(pointT9);
                GL.Vertex3(pointT8);
                GL.Vertex3(pointT7);
                GL.Vertex3(pointT6);
                GL.Vertex3(pointT5);
                GL.Vertex3(pointT4);
                GL.Vertex3(pointT3);
                GL.Vertex3(pointT2);
                GL.Vertex3(pointT1);
                GL.Vertex3(pointT16);
                GL.End();
                // sides
                GL.Begin(PrimitiveType.TriangleStrip);
                GL.Vertex3(pointB1);
                GL.Vertex3(pointT1);
                GL.Vertex3(pointB2);
                GL.Vertex3(pointT2);
                GL.Vertex3(pointB3);
                GL.Vertex3(pointT3);
                GL.Vertex3(pointB4);
                GL.Vertex3(pointT4);
                GL.Vertex3(pointB5);
                GL.Vertex3(pointT5);
                GL.Vertex3(pointB6);
                GL.Vertex3(pointT6);
                GL.Vertex3(pointB7);
                GL.Vertex3(pointT7);
                GL.Vertex3(pointB8);
                GL.Vertex3(pointT8);
                GL.Vertex3(pointB9);
                GL.Vertex3(pointT9);
                GL.Vertex3(pointB10);
                GL.Vertex3(pointT10);
                GL.Vertex3(pointB11);
                GL.Vertex3(pointT11);
                GL.Vertex3(pointB12);
                GL.Vertex3(pointT12);
                GL.Vertex3(pointB13);
                GL.Vertex3(pointT13);
                GL.Vertex3(pointB14);
                GL.Vertex3(pointT14);
                GL.Vertex3(pointB15);
                GL.Vertex3(pointT15);
                GL.Vertex3(pointB16);
                GL.Vertex3(pointT16);
                GL.Vertex3(pointB1);
                GL.Vertex3(pointT1);
                GL.End();
            }
            else if (volume.Type == VolumeType.Sphere)
            {
                _sphereVertices.Clear();
                int stackCount = 16;
                int sectorCount = 24;
                float radius = volume.SphereRadius;
                float sectorStep = 2 * MathF.PI / sectorCount;
                float stackStep = MathF.PI / stackCount;
                float sectorAngle, stackAngle, x, y, z, xy;
                for (int i = 0; i <= stackCount; i++)
                {
                    stackAngle = MathF.PI / 2 - i * stackStep;
                    xy = radius * MathF.Cos(stackAngle);
                    z = radius * MathF.Sin(stackAngle);
                    for (int j = 0; j <= sectorCount; j++)
                    {
                        sectorAngle = j * sectorStep;
                        x = xy * MathF.Cos(sectorAngle);
                        y = xy * MathF.Sin(sectorAngle);
                        _sphereVertices.Add(new Vector3(x, z, y));
                    }
                }
                GL.Begin(PrimitiveType.Triangles);
                int k1, k2;
                for (int i = 0; i < stackCount; i++)
                {
                    k1 = i * (sectorCount + 1);
                    k2 = k1 + sectorCount + 1;
                    for (int j = 0; j < sectorCount; j++, k1++, k2++)
                    {
                        if (i != 0)
                        {
                            GL.Vertex3(_sphereVertices[k1 + 1] + volume.SpherePosition);
                            GL.Vertex3(_sphereVertices[k2] + volume.SpherePosition);
                            GL.Vertex3(_sphereVertices[k1] + volume.SpherePosition);
                        }
                        if (i != (stackCount - 1))
                        {
                            GL.Vertex3(_sphereVertices[k2 + 1] + volume.SpherePosition);
                            GL.Vertex3(_sphereVertices[k2] + volume.SpherePosition);
                            GL.Vertex3(_sphereVertices[k1 + 1] + volume.SpherePosition);
                        }
                    }
                }
                GL.End();
            }
        }
    }
}
