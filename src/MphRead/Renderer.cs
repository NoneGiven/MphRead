using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using MphRead.Effects;
using MphRead.Export;
using MphRead.Formats.Collision;
using MphRead.Models;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

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
        public (int BindingId, bool OnlyOpaque) Get(int textureId, int paletteId)
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

        private float _elapsedTime = 0;
        private long _frameCount = -1;
        private bool _roomLoaded = false;
        private readonly List<Model> _models = new List<Model>();
        private readonly Dictionary<int, Model> _modelMap = new Dictionary<int, Model>();
        private readonly List<Model> _entities = new List<Model>();
        private readonly ConcurrentQueue<Model> _loadQueue = new ConcurrentQueue<Model>();
        private readonly ConcurrentQueue<Model> _unloadQueue = new ConcurrentQueue<Model>();
        private readonly Dictionary<int, LightSource> _lightSourceMap = new Dictionary<int, LightSource>();
        // light sources need to be processed in entity ID order
        private readonly List<LightSource> _lightSources = new List<LightSource>();
        private readonly Dictionary<int, DisplayVolume> _displayVolumes = new Dictionary<int, DisplayVolume>();
        private readonly List<CollisionPortal> _displayPlanes = new List<CollisionPortal>();

        // map each model's texture ID/palette ID combinations to the bound OpenGL texture ID and "onlyOpaque" boolean
        private int _textureCount = 0;
        private readonly Dictionary<int, TextureMap> _texPalMap = new Dictionary<int, TextureMap>();

        private SelectionMode _selectionMode = SelectionMode.None;
        private int _selectedModelId = -1;
        private int _selectedMeshId = 0;
        private int _selectedNodeId = 0;
        private bool _showSelection = true;
        private int _lastPointModule = -1;
        private int _endPointModule = -1;

        private Model SelectedModel => _modelMap[_selectedModelId];

        private CameraMode _cameraMode = CameraMode.Pivot;
        private float _angleY = 0.0f;
        private float _angleX = 0.0f;
        private float _distance = 5.0f;
        // note: the axes are reversed from the model coordinates
        private Vector3 _cameraPosition = new Vector3(0, 0, 0);
        private bool _leftMouse = false;

        private bool _showTextures = true;
        private bool _showColors = true;
        private bool _wireframe = false;
        private bool _volumeEdges = false;
        private bool _faceCulling = true;
        private bool _textureFiltering = false;
        private bool _lighting = false;
        private bool _scanVisor = false;
        private bool _showInvisible = false;
        private int _showVolumes = 0;
        private bool _showKillPlane = false; // undocumented
        private bool _transformRoomNodes = false; // undocumented

        private Color4 _clearColor = new Color4(0, 0, 0, 1);
        private float _farClip = 10000f;
        private bool _useClip = true; // undocumented
        private float _killHeight = 0f;
        private int _dblDmgBindingId = -1;

        private Vector3 _light1Vector = default;
        private Vector3 _light1Color = default;
        private Vector3 _light2Vector = default;
        private Vector3 _light2Color = default;
        private bool _hasFog = false;
        private bool _showFog = true;
        private Vector4 _fogColor = default;
        private int _fogOffset = 0;
        private int _fogSlope = 0;
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
            (Model room, RoomMetadata roomMeta, CollisionInfo collision, IReadOnlyList<Model> entities, int updatedMask)
                = SceneSetup.LoadRoom(name, mode, playerCount, bossFlags, nodeLayerMask, entityLayerId);
            nodeLayerMask = updatedMask;
            if (roomMeta.InGameName != null)
            {
                Title = roomMeta.InGameName;
            }
            foreach (Model model in _models.Where(m => m.UseLightOverride))
            {
                model.Light1Color = _light1Color;
                model.Light1Vector = _light1Vector;
                model.Light2Color = _light2Color;
                model.Light2Vector = _light2Vector;
            }
            _models.Insert(0, room);
            _models.AddRange(entities);
            _modelMap.Add(room.SceneId, room);
            foreach (Model entity in entities)
            {
                _modelMap.Add(entity.SceneId, entity);
                if (entity.Entity is Entity<LightSourceEntityData> lightSource)
                {
                    var display = new LightSource(lightSource, entity.Transform);
                    _displayVolumes.Add(entity.SceneId, display);
                    _lightSourceMap.Add(entity.SceneId, display);
                    _lightSources.Add(display);
                }
                else if (entity.Entity is Entity<TriggerVolumeEntityData> trigger)
                {
                    _displayVolumes.Add(entity.SceneId, new TriggerVolumeDisplay(trigger, entity.Transform));
                }
                else if (entity.Entity is Entity<FhTriggerVolumeEntityData> fhTrigger)
                {
                    if (fhTrigger.Data.Subtype != 0)
                    {
                        _displayVolumes.Add(entity.SceneId, new TriggerVolumeDisplay(fhTrigger, entity.Transform));
                    }
                }
                else if (entity.Entity is Entity<AreaVolumeEntityData> area)
                {
                    _displayVolumes.Add(entity.SceneId, new AreaVolumeDisplay(area, entity.Transform));
                }
                else if (entity.Entity is Entity<FhAreaVolumeEntityData> fhArea)
                {
                    if (fhArea.Data.Subtype != 0)
                    {
                        _displayVolumes.Add(entity.SceneId, new AreaVolumeDisplay(fhArea, entity.Transform));
                    }
                }
                else if (entity.Entity is Entity<MorphCameraEntityData> morphCamera)
                {
                    _displayVolumes.Add(entity.SceneId, new MorphCameraDisplay(morphCamera, entity.Transform));
                }
                else if (entity.Entity is Entity<FhMorphCameraEntityData> fhMorphCamera)
                {
                    _displayVolumes.Add(entity.SceneId, new MorphCameraDisplay(fhMorphCamera, entity.Transform));
                }
                else if (entity.Entity is Entity<JumpPadEntityData> jumpPad && entity.Name != "JumpPad_Beam")
                {
                    _displayVolumes.Add(entity.SceneId, new JumpPadDisplay(jumpPad, entity.Transform));
                }
                else if (entity.Entity is Entity<FhJumpPadEntityData> fhJumpPad)
                {
                    _displayVolumes.Add(entity.SceneId, new JumpPadDisplay(fhJumpPad, entity.Transform));
                }
                else if (entity.Entity is Entity<ObjectEntityData> obj && obj.Data.EffectId > 0 && (obj.Data.EffectFlags & 1) != 0)
                {
                    _displayVolumes.Add(entity.SceneId, new ObjectDisplay(obj, entity.Transform));
                }
                else if (entity.Entity is Entity<FlagBaseEntityData> flag)
                {
                    _displayVolumes.Add(entity.SceneId, new FlagBaseDisplay(flag, entity.Transform));
                }
                else if (entity.Entity is Entity<NodeDefenseEntityData> defense)
                {
                    _displayVolumes.Add(entity.SceneId, new NodeDefenseDisplay(defense, entity.Transform));
                }
                else if (entity.Entity is Entity<PointModuleEntityData> module)
                {
                    if (_lastPointModule == -1)
                    {
                        _lastPointModule = entity.Entity.EntityId;
                    }
                    if (module.Data.NextId == 0)
                    {
                        _endPointModule = entity.Entity.EntityId;
                    }
                    // hack to allow easily toggling all
                    entity.ScanVisorOnly = true;
                }
            }
            // todo: move more stuff to mutable class state
            if (_lastPointModule != -1)
            {
                ushort nextId = entities[_lastPointModule].Entity!.GetChildId();
                for (int i = 0; i < 5; i++)
                {
                    Model model = entities[nextId];
                    model.ScanVisorOnly = false;
                    nextId = model.Entity!.GetChildId();
                }
            }
            _entities.AddRange(entities);
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
            _hasFog = roomMeta.FogEnabled;
            _fogColor = new Vector4(
                roomMeta.FogColor.Red / 31f,
                roomMeta.FogColor.Green / 31f,
                roomMeta.FogColor.Blue / 31f,
                1.0f
            );
            _fogOffset = roomMeta.FogOffset;
            _fogSlope = roomMeta.FogSlope;
            if (roomMeta.ClearFog && roomMeta.FirstHunt)
            {
                _clearColor = new Color4(_fogColor.X, _fogColor.Y, _fogColor.Z, _fogColor.W);
            }
            _killHeight = roomMeta.KillHeight;
            _farClip = roomMeta.FarClip;
            foreach (CollisionPortal portal in collision.Portals)
            {
                if ((portal.LayerMask & 4) != 0 || (portal.LayerMask & updatedMask) != 0)
                {
                    _displayPlanes.Add(portal);
                }
            }
            _cameraMode = CameraMode.Roam;
            Model dblDmgModel = Read.GetModelByName("doubleDamage_img");
            BindTexture(dblDmgModel, 0, 0);
            _dblDmgBindingId = _textureCount;
        }

        public void AddModel(string name, int recolor, bool firstHunt)
        {
            Model model = Read.GetModelByName(name, recolor, firstHunt);
            SceneSetup.ComputeNodeMatrices(model, index: 0);
            if (_roomLoaded && model.UseLightSources)
            {
                model.Light1Color = _light1Color;
                model.Light1Vector = _light1Vector;
                model.Light2Color = _light2Color;
                model.Light2Vector = _light2Vector;
            }
            _models.Add(model);
            _modelMap.Add(model.SceneId, model);
        }

        protected override async void OnLoad()
        {
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
            _shaderLocations.Emission = GL.GetUniformLocation(_shaderProgramId, "emission");
            _shaderLocations.UseFog = GL.GetUniformLocation(_shaderProgramId, "fog_enable");
            _shaderLocations.FogColor = GL.GetUniformLocation(_shaderProgramId, "fog_color");
            _shaderLocations.FogMinDistance = GL.GetUniformLocation(_shaderProgramId, "fog_min");
            _shaderLocations.FogMaxDistance = GL.GetUniformLocation(_shaderProgramId, "fog_max");

            _shaderLocations.UseOverride = GL.GetUniformLocation(_shaderProgramId, "use_override");
            _shaderLocations.OverrideColor = GL.GetUniformLocation(_shaderProgramId, "override_color");
            _shaderLocations.UsePaletteOverride = GL.GetUniformLocation(_shaderProgramId, "use_pal_override");
            _shaderLocations.PaletteOverrideColor = GL.GetUniformLocation(_shaderProgramId, "pal_override_color");
            _shaderLocations.MaterialAlpha = GL.GetUniformLocation(_shaderProgramId, "mat_alpha");
            _shaderLocations.MaterialMode = GL.GetUniformLocation(_shaderProgramId, "mat_mode");
            _shaderLocations.ViewMatrix = GL.GetUniformLocation(_shaderProgramId, "view_mtx");
            _shaderLocations.ProjectionMatrix = GL.GetUniformLocation(_shaderProgramId, "proj_mtx");
            _shaderLocations.TextureMatrix = GL.GetUniformLocation(_shaderProgramId, "tex_mtx");
            _shaderLocations.TexgenMode = GL.GetUniformLocation(_shaderProgramId, "texgen_mode");
            _shaderLocations.MatrixStack = GL.GetUniformLocation(_shaderProgramId, "mtx_stack");
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

            float fogMin = _fogOffset / (float)0x7FFF;
            float fogMax = (_fogOffset + 32 * (0x400 >> _fogSlope)) / (float)0x7FFF;
            GL.Uniform4(_shaderLocations.FogColor, _fogColor);
            GL.Uniform1(_shaderLocations.FogMinDistance, fogMin);
            GL.Uniform1(_shaderLocations.FogMaxDistance, fogMax);
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
            foreach (TextureAnimationGroup group in model.Animations.TextureGroups)
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
                    bool onlyOpaque = BindTexture(model, textureId, paletteId);
                    map.Add(textureId, paletteId, _textureCount, onlyOpaque);
                }
                _texPalMap.Add(model.SceneId, map);
            }
        }

        private bool BindTexture(Model model, int textureId, int paletteId)
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
            return onlyOpaque;
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

        private async Task LoadAndUnload()
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
                if (_lightSourceMap.TryGetValue(model.SceneId, out LightSource? lightSource))
                {
                    _lightSources.Remove(lightSource);
                    _lightSourceMap.Remove(model.SceneId);
                }
                _displayVolumes.Remove(model.SceneId);
                _updateLists = true;
                await PrintOutput();
            }
        }

        protected override async void OnRenderFrame(FrameEventArgs args)
        {
            // extra non-rendering updates
            _frameCount++;
            await LoadAndUnload();
            OnKeyHeld();

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
            GL.ClearStencil(0);

            // todo: confirm fov and recalculate this only in the resize event
            GL.GetFloat(GetPName.Viewport, out Vector4 viewport);
            float aspect = (viewport.Z - viewport.X) / (viewport.W - viewport.Y);
            float fov = MathHelper.DegreesToRadians(80.0f);
            var perspectiveMatrix = Matrix4.CreatePerspectiveFieldOfView(fov, aspect, 0.0625f, _useClip ? _farClip : 10000f);
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
                mesh.Selection = Selection.Selected;
            }
            if (SelectedModel.Entity != null)
            {
                ushort parentId = SelectedModel.Entity.GetParentId();
                if (TryGetByEntityId(parentId, out Model? parent))
                {
                    foreach (Mesh mesh in parent.Meshes)
                    {
                        mesh.Selection = Selection.Parent;
                    }
                }
                ushort childId = SelectedModel.Entity.GetChildId();
                if (childId != parentId && TryGetByEntityId(childId, out Model? child))
                {
                    foreach (Mesh mesh in child.Meshes)
                    {
                        mesh.Selection = Selection.Child;
                    }
                }
            }
        }

        private bool TryGetByEntityId(ushort id, [NotNullWhen(true)] out Model? model)
        {
            model = _models.FirstOrDefault(m => m.Entity?.EntityId == id);
            return model != null;
        }

        private void SetSelectedNode(int sceneId, int nodeId)
        {
            Deselect();
            _selectedModelId = sceneId;
            _selectedNodeId = nodeId;
            foreach (Mesh mesh in SelectedModel.GetNodeMeshes(_selectedNodeId))
            {
                mesh.Selection = Selection.Selected;
            }
        }

        private void SetSelectedMesh(int sceneId, int meshId)
        {
            Deselect();
            _selectedModelId = sceneId;
            _selectedMeshId = meshId;
            SelectedModel.Meshes[meshId].Selection = Selection.Selected;
        }

        private void Deselect()
        {
            if (_selectedModelId > -1)
            {
                foreach (Mesh mesh in SelectedModel.Meshes)
                {
                    mesh.Selection = Selection.None;
                }
                if (SelectedModel.Entity != null)
                {
                    ushort parentId = SelectedModel.Entity.GetParentId();
                    if (TryGetByEntityId(parentId, out Model? parent))
                    {
                        foreach (Mesh mesh in parent.Meshes)
                        {
                            mesh.Selection = Selection.None;
                        }
                    }
                    ushort childId = SelectedModel.Entity.GetChildId();
                    if (childId != parentId && TryGetByEntityId(childId, out Model? child))
                    {
                        foreach (Mesh mesh in child.Meshes)
                        {
                            mesh.Selection = Selection.None;
                        }
                    }
                }
            }
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

        private void LookAt(Vector3 target)
        {
            if (_cameraMode == CameraMode.Roam)
            {
                _cameraPosition = -1 * target.WithZ(target.Z + 5);
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
        private Matrix4 _viewInvRotYMatrix = Matrix4.Identity;

        private void TransformCamera()
        {
            // todo: only update this when the camera position changes
            _viewMatrix = Matrix4.Identity;
            _viewInvRotMatrix = Matrix4.Identity;
            _viewInvRotYMatrix = Matrix4.Identity;
            if (_cameraMode == CameraMode.Pivot)
            {
                _viewMatrix = Matrix4.CreateTranslation(new Vector3(0, 0, _distance * -1));
                _viewMatrix = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(_angleX)) * _viewMatrix;
                _viewMatrix = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_angleY)) * _viewMatrix;
                _viewInvRotMatrix = _viewInvRotYMatrix = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(-1 * _angleY));
                _viewInvRotMatrix = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-1 * _angleX)) * _viewInvRotMatrix;
            }
            else if (_cameraMode == CameraMode.Roam)
            {
                _viewMatrix = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(_angleX));
                _viewMatrix = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_angleY)) * _viewMatrix;
                _viewMatrix = Matrix4.CreateTranslation(_cameraPosition) * _viewMatrix;
                _viewInvRotMatrix = _viewInvRotYMatrix = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(-1 * _angleY));
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
        private readonly Dictionary<int, int> _tempListIds = new Dictionary<int, int>();

        private void UpdateLists()
        {
            for (int i = 1; i <= _maxListId; i++)
            {
                GL.DeleteLists(i, 1);
            }
            _maxListId = 0;
            foreach (Model model in _models)
            {
                GenerateLists(model);
            }
            foreach (Model model in Read.EffectModels.Values)
            {
                GenerateLists(model);
            }
        }

        private void GenerateLists(Model model)
        {
            _tempListIds.Clear();
            for (int j = 0; j < model.Nodes.Count; j++)
            {
                foreach (Mesh mesh in model.GetNodeMeshes(j))
                {
                    if (!_tempListIds.TryGetValue(mesh.DlistId, out int listId))
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
                        bool texgen = material.TexgenMode == TexgenMode.Normal;
                        DoDlist(model, mesh, textureWidth, textureHeight, texgen);
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
            public readonly bool IsParticle;
            public readonly bool ParticleNode;
            public readonly Model Model;
            public readonly Node Node;
            public readonly Mesh Mesh;
            public readonly Material Material;
            public readonly EffectParticle Particle;
            public readonly int PolygonId;
            public readonly float Alpha;

            public MeshInfo(Model model, Node node, Mesh mesh, Material material, int polygonId, float alpha)
            {
                Model = model;
                Node = node;
                Mesh = mesh;
                Material = material;
                PolygonId = polygonId;
                Alpha = alpha;
                IsParticle = false;
                ParticleNode = false;
                Particle = null!;
            }

            public MeshInfo(EffectParticle particle, Material material, int polygonId, float alpha)
            {
                Particle = particle;
                Material = material;
                PolygonId = polygonId;
                Alpha = alpha;
                IsParticle = true;
                ParticleNode = false;
                Model = null!;
                Node = null!;
                Mesh = null!;
            }

            public MeshInfo(EffectParticle particle, Model model, Node node, Mesh mesh, Material material, int polygonId, float alpha)
            {
                Particle = particle;
                Material = material;
                PolygonId = polygonId;
                Alpha = alpha;
                IsParticle = true;
                ParticleNode = true;
                Model = model;
                Node = node;
                Mesh = mesh;
            }
        }

        private bool _effectSetupDone = false;
        // ptodo: effect limits
        // in-game: 64 effects, 96 elements, 200 particles
        private static readonly int _effectEntryMax = 100;
        private static readonly int _effectElementMax = 200;
        private static readonly int _effectParticleMax = 3000;

        private readonly Queue<EffectEntry> _inactiveEffects = new Queue<EffectEntry>(_effectEntryMax);
        private readonly Queue<EffectElementEntry> _inactiveElements = new Queue<EffectElementEntry>(_effectElementMax);
        private readonly List<EffectElementEntry> _activeElements = new List<EffectElementEntry>(_effectElementMax);
        private readonly Queue<EffectParticle> _inactiveParticles = new Queue<EffectParticle>(_effectParticleMax);

        private void AllocateEffects()
        {
            for (int i = 0; i < _effectEntryMax; i++)
            {
                _inactiveEffects.Enqueue(new EffectEntry());
            }
            for (int i = 0; i < _effectElementMax; i++)
            {
                _inactiveElements.Enqueue(new EffectElementEntry());
            }
            for (int i = 0; i < _effectParticleMax; i++)
            {
                _inactiveParticles.Enqueue(new EffectParticle());
            }
        }

        private EffectEntry InitEffectEntry()
        {
            EffectEntry entry = _inactiveEffects.Dequeue();
            entry.EffectId = 0;
            Debug.Assert(entry.Elements.Count == 0);
            return entry;
        }

        public void UnlinkEffectEntry(EffectEntry entry)
        {
            for (int i = 0; i < entry.Elements.Count; i++)
            {
                EffectElementEntry element = entry.Elements[i];
                UnlinkEffectElement(element);
            }
            _inactiveEffects.Enqueue(entry);
        }

        public void DetachEffectEntry(EffectEntry entry, bool setExpired)
        {
            for (int i = 0; i < entry.Elements.Count; i++)
            {
                EffectElementEntry element = entry.Elements[i];
                if ((element.Flags & 0x100) != 0)
                {
                    UnlinkEffectElement(element);
                }
                else
                {
                    element.Flags &= 0xFFF7FFFF; // clear bit 19 (lifetime extension)
                    element.Flags |= 0x10; // set big 4 (keep alive until particles expire)
                    element.EffectEntry = null;
                    if (setExpired)
                    {
                        element.Expired = true;
                    }
                }
            }
            entry.Elements.Clear();
            UnlinkEffectEntry(entry);
        }

        private EffectElementEntry InitEffectElement(Effect effect, EffectElement element)
        {
            EffectElementEntry entry = _inactiveElements.Dequeue();
            entry.EffectName = effect.Name;
            entry.ElementName = element.Name;
            entry.BufferTime = element.BufferTime;
            // todo: if created during effect processing (child effect), increase creation time by one frame
            entry.CreationTime = _elapsedTime;
            entry.DrainTime = element.DrainTime;
            entry.DrawType = element.DrawType;
            entry.Lifespan = element.Lifespan;
            entry.ExpirationTime = entry.CreationTime + entry.Lifespan;
            entry.Flags = element.Flags;
            entry.Flags |= 0x100000; // set bit 20 (draw enabled)
            entry.Func39Called = false;
            entry.Funcs = element.Funcs;
            entry.Actions = element.Actions;
            entry.Position = Vector3.Zero;
            entry.Transform = Matrix4.Identity;
            entry.ParticleAmount = 0;
            entry.Expired = false;
            entry.Acceleration = element.Acceleration;
            entry.ParticleDefinitions.AddRange(element.Particles);
            entry.Parity = (int)(_frameCount % 2);
            _activeElements.Add(entry);
            return entry;
        }

        private void UnlinkEffectElement(EffectElementEntry element)
        {
            while (element.Particles.Count > 0)
            {
                EffectParticle particle = element.Particles[0];
                element.Particles.Remove(particle);
                UnlinkEffectParticle(particle);
            }
            _activeElements.Remove(element);
            element.Model = null!;
            element.Nodes.Clear();
            element.EffectName = "";
            element.ElementName = "";
            element.ParticleDefinitions.Clear();
            element.TextureBindingIds.Clear();
            Debug.Assert(element.Particles.Count == 0);
            _inactiveElements.Enqueue(element);
        }

        private EffectParticle InitEffectParticle()
        {
            EffectParticle particle = _inactiveParticles.Dequeue();
            particle.Position = Vector3.Zero;
            particle.Speed = Vector3.Zero;
            particle.ParticleId = 0;
            particle.RoField1 = 0;
            particle.RoField2 = 0;
            particle.RoField3 = 0;
            particle.RoField4 = 0;
            particle.RwField1 = 0;
            particle.RwField2 = 0;
            particle.RwField3 = 0;
            particle.RwField4 = 0;
            particle.CreationTime = _elapsedTime;
            return particle;
        }

        private void UnlinkEffectParticle(EffectParticle particle)
        {
            _inactiveParticles.Enqueue(particle);
        }

        public EffectEntry SpawnEffectGetEntry(int effectId, Matrix4 transform)
        {
            EffectEntry entry = InitEffectEntry();
            entry.EffectId = effectId;
            SpawnEffect(effectId, transform, entry);
            return entry;
        }

        public void SpawnEffect(int effectId, Matrix4 transform, EffectEntry? entry = null)
        {
            // ptodo: this should be loaded when the object/whatever is loaded, not when the effect is first spawned
            Effect effect = Read.LoadEffect(effectId);
            var position = new Vector3(transform.Row3);
            for (int i = 0; i < effect.Elements.Count; i++)
            {
                EffectElement elementDef = effect.Elements[i];
                EffectElementEntry element = InitEffectElement(effect, elementDef);
                if (entry != null)
                {
                    element.EffectEntry = entry;
                    entry.Elements.Add(element);
                }
                element.Position = position;
                if ((element.Flags & 8) != 0)
                {
                    Vector3 vec1 = Vector3.UnitY;
                    Vector3 vec2 = Vector3.UnitX;
                    Matrix3 temp = SceneSetup.GetTransformMatrix(vec2, vec1);
                    transform = new Matrix4(
                        new Vector4(temp.Row0),
                        new Vector4(temp.Row1),
                        new Vector4(temp.Row2),
                        new Vector4(position, 1)
                    );
                }
                element.Transform = transform;
                for (int j = 0; j < elementDef.Particles.Count; j++)
                {
                    Particle particleDef = elementDef.Particles[j];
                    if (j == 0)
                    {
                        if (!_texPalMap.ContainsKey(particleDef.Model.SceneId))
                        {
                            InitTextures(particleDef.Model);
                        }
                        element.Model = particleDef.Model;
                        // ptodo: see above (effect loading)
                        if (element.Model.Meshes.Count > 0 && element.Model.Meshes[0].ListId == 0)
                        {
                            GenerateLists(element.Model);
                        }
                    }
                    element.Nodes.Add(particleDef.Node);
                    Material material = particleDef.Model.Materials[particleDef.MaterialId];
                    element.TextureBindingIds.Add(_texPalMap[particleDef.Model.SceneId].Get(material.TextureId, material.PaletteId).BindingId);
                }
            }
        }

        private void ProcessEffects()
        {
            for (int i = 0; i < _activeElements.Count; i++)
            {
                EffectElementEntry element = _activeElements[i];
                if (!element.Expired && _elapsedTime > element.ExpirationTime)
                {
                    if (element.EffectEntry == null && (element.Flags & 0x10) == 0)
                    {
                        UnlinkEffectElement(element);
                        i--;
                        continue;
                    }
                    element.Expired = true;
                }
                if (element.Expired)
                {
                    // if EffectEntry is non-null, keep the element alive indefinitely;
                    // else (if bit 4 of Flags is set), keep the element alive until its particles have all expired
                    if (element.EffectEntry == null && element.Particles.Count == 0)
                    {
                        UnlinkEffectElement(element);
                        i--;
                        continue;
                    }
                }
                else
                {
                    if ((element.Flags & 0x80000) != 0)
                    {
                        if (_elapsedTime - element.CreationTime > element.BufferTime)
                        {
                            element.CreationTime += element.BufferTime - element.DrainTime;
                            element.ExpirationTime += element.BufferTime - element.DrainTime;
                        }
                    }
                    var times = new TimeValues(_elapsedTime, _elapsedTime - element.CreationTime, element.Lifespan);
                    // ptodo: mtxptr stuff
                    if (_frameCount % 2 == element.Parity
                        && element.Actions.TryGetValue(FuncAction.IncreaseParticleAmount, out FxFuncInfo? info))
                    {
                        // todo: maybe revisit this fram time hack
                        // --> halving the amount doesn't work because it breaks one-time return values of 1.0
                        float amount = element.InvokeFloatFunc(info, times);
                        element.ParticleAmount += amount;
                    }
                    int spawnCount = (int)MathF.Floor(element.ParticleAmount);
                    element.ParticleAmount -= spawnCount;
                    float portionTotal = 0;
                    for (int j = 0; j < spawnCount; j++)
                    {
                        Vector3 temp = Vector3.Zero;
                        EffectParticle particle = InitEffectParticle();
                        element.Particles.Add(particle);
                        particle.Owner = element;
                        particle.SetFuncIds();
                        particle.PortionTotal = portionTotal;
                        particle.MaterialId = element.ParticleDefinitions[0].MaterialId;
                        if (element.Actions.TryGetValue(FuncAction.SetNewParticlePosition, out info))
                        {
                            particle.InvokeVecFunc(info, times, ref temp);
                            particle.Position = temp;
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetNewParticleSpeed, out info))
                        {
                            particle.InvokeVecFunc(info, times, ref temp);
                            particle.Speed = temp;
                        }
                        if ((element.Flags & 1) == 0)
                        {
                            particle.Position = Matrix.Vec3MultMtx4(particle.Position, element.Transform);
                            particle.Speed = Matrix.Vec3MultMtx3(particle.Speed, element.Transform);
                        }
                        // todo: these should really just be FxFuncInfo properties instead of a dictionary
                        // --> still need the dictionary for the offset lookups, though
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRoField1, out info))
                        {
                            particle.RoField1 = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRoField2, out info))
                        {
                            particle.RoField2 = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRoField3, out info))
                        {
                            particle.RoField3 = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRoField4, out info))
                        {
                            particle.RoField4 = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRwField1, out info))
                        {
                            particle.RwField1 = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRwField2, out info))
                        {
                            particle.RwField2 = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRwField3, out info))
                        {
                            particle.RwField3 = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRwField4, out info))
                        {
                            particle.RwField4 = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetNewParticleLifespan, out info))
                        {
                            var tempTimes = new TimeValues(_elapsedTime, 1.0f, element.Lifespan);
                            particle.Lifespan = particle.InvokeFloatFunc(info, tempTimes);
                            particle.ExpirationTime = particle.CreationTime + particle.Lifespan;
                        }
                        else
                        {
                            particle.Lifespan = element.Lifespan;
                            particle.ExpirationTime = element.ExpirationTime;
                        }
                        portionTotal += 1f / spawnCount;
                    }
                }
                for (int j = 0; j < element.Particles.Count; j++)
                {
                    EffectParticle particle = element.Particles[j];
                    if ((element.Flags & 0x80000) != 0 && (element.Flags & 0x20) != 0)
                    {
                        if (_elapsedTime - particle.CreationTime > element.BufferTime)
                        {
                            particle.CreationTime += element.BufferTime - element.DrainTime;
                            particle.ExpirationTime += element.BufferTime - element.DrainTime;
                        }
                    }
                    if (_elapsedTime < particle.ExpirationTime)
                    {
                        var times = new TimeValues(_elapsedTime, _elapsedTime - particle.CreationTime, particle.Lifespan);
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRwField1, out FxFuncInfo? info))
                        {
                            particle.RwField1 = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRwField2, out info))
                        {
                            particle.RwField2 = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRwField3, out info))
                        {
                            particle.RwField3 = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRwField4, out info))
                        {
                            particle.RwField4 = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleId, out info))
                        {
                            particle.ParticleId = (int)particle.InvokeFloatFunc(info, times);
                            if (particle.ParticleId >= element.ParticleDefinitions.Count)
                            {
                                particle.ParticleId = element.ParticleDefinitions.Count - 1;
                            }
                            particle.MaterialId = element.ParticleDefinitions[particle.ParticleId].MaterialId;
                        }
                        if (element.Actions.TryGetValue(FuncAction.UpdateParticleSpeed, out info))
                        {
                            Vector3 temp = particle.Speed;
                            particle.InvokeVecFunc(info, times, ref temp);
                            particle.Speed = temp;
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRed, out info))
                        {
                            particle.Red = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleGreen, out info))
                        {
                            particle.Green = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleBlue, out info))
                        {
                            particle.Blue = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleAlpha, out info))
                        {
                            particle.Alpha = particle.InvokeFloatFunc(info, times);
                            if (particle.Alpha < 0)
                            {
                                particle.Alpha = 0;
                            }
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleScale, out info))
                        {
                            particle.Scale = particle.InvokeFloatFunc(info, times);
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRotation, out info))
                        {
                            particle.Rotation = particle.InvokeFloatFunc(info, times);
                        }
                        // ptodo: frame time scaling for speed/accel
                        if ((element.Flags & 2) != 0)
                        {
                            particle.Speed = new Vector3(
                                particle.Speed.X + element.Acceleration.X * (1 / 60f),
                                particle.Speed.Y + element.Acceleration.Y * (1 / 60f),
                                particle.Speed.Z + element.Acceleration.Z * (1 / 60f)
                            );
                        }
                        Vector3 prevPos = particle.Position;
                        particle.Position = new Vector3(
                            particle.Position.X + particle.Speed.X * (1 / 60f),
                            particle.Position.Y + particle.Speed.Y * (1 / 60f),
                            particle.Position.Z + particle.Speed.Z * (1 / 60f)
                        );
                        if ((element.Flags & 0x40) != 0)
                        {
                            // ptodo: collision check between previous and new positions
                        }
                    }
                    else
                    {
                        if ((element.Flags & 0x80) != 0)
                        {
                            // ptodo: spawn child effect
                        }
                        element.Particles.Remove(particle);
                        UnlinkEffectParticle(particle);
                        j--;
                    }
                }
            }
        }

        // avoiding overhead by duplicating things in these lists
        private readonly List<MeshInfo> _decalMeshes = new List<MeshInfo>();
        private readonly List<MeshInfo> _nonDecalMeshes = new List<MeshInfo>();
        private readonly List<MeshInfo> _translucentMeshes = new List<MeshInfo>();

        private void RenderScene(double elapsedTime)
        {
            if (!_effectSetupDone)
            {
                AllocateEffects();
                _effectSetupDone = true;
            }
            if (!_frameAdvanceOn || _advanceOneFrame)
            {
                _elapsedTime += 1 / 60f;
            }
            _decalMeshes.Clear();
            _nonDecalMeshes.Clear();
            _translucentMeshes.Clear();
            int polygonId = 1;
            _models.Sort(CompareModels);
            // todo: consolidate this
            Vector3 cameraPosition = _cameraPosition * (_cameraMode == CameraMode.Roam ? -1 : 1);
            for (int i = 0; i < _models.Count; i++)
            {
                Model model = _models[i];
                if (!_frameAdvanceOn || _advanceOneFrame)
                {
                    bool useTransform = _transformRoomNodes || model.Type != ModelType.Room;
                    model.Process(this, elapsedTime, _frameCount, cameraPosition, _viewInvRotMatrix, _viewInvRotYMatrix, useTransform);
                }
                if (model.UseLightSources)
                {
                    UpdateLightSources(model, elapsedTime);
                }
                else if (model.UseLightOverride)
                {
                    UpdateLightOverride(model);
                }
                if (!model.Visible || (model.Type == ModelType.Placeholder && !_showInvisible) || (model.ScanVisorOnly && !_scanVisor))
                {
                    continue;
                }
                int modelPolygonId = model.Type == ModelType.Room ? 0 : polygonId++;
                foreach (NodeInfo nodeInfo in model.GetDrawNodes(includeForceFields: _showVolumes != 12))
                {
                    Node node = nodeInfo.Node;
                    if (node.MeshCount == 0 || !node.Enabled || !model.NodeParentsEnabled(node))
                    {
                        continue;
                    }
                    foreach (Mesh mesh in model.GetNodeMeshes(node))
                    {
                        if (!mesh.Visible)
                        {
                            continue;
                        }
                        float alpha = 1f;
                        Material material = model.Materials[mesh.MaterialId];
                        int meshPolygonId;
                        if (model.Type == ModelType.Room)
                        {
                            if (nodeInfo.Portal != null)
                            {
                                meshPolygonId = UInt16.MaxValue;
                                alpha = GetPortalAlpha(nodeInfo.Portal.Position);
                            }
                            else if (material.RenderMode == RenderMode.Translucent)
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
                        alpha *= model.Alpha;
                        var meshInfo = new MeshInfo(model, node, mesh, material, meshPolygonId, alpha);
                        if (material.RenderMode != RenderMode.Decal)
                        {
                            _nonDecalMeshes.Add(meshInfo);
                        }
                        else
                        {
                            _decalMeshes.Add(meshInfo);
                        }
                        if (material.RenderMode == RenderMode.Translucent || alpha < 1f)
                        {
                            _translucentMeshes.Add(meshInfo);
                        }
                    }
                }
            }

            if (!_frameAdvanceOn || _advanceOneFrame)
            {
                ProcessEffects();
            }
            for (int i = 0; i < _activeElements.Count; i++)
            {
                EffectElementEntry element = _activeElements[i];
                for (int j = 0; j < element.Particles.Count; j++)
                {
                    EffectParticle particle = element.Particles[j];
                    Matrix4 matrix = _viewMatrix;
                    if ((particle.Owner.Flags & 1) != 0 && (particle.Owner.Flags & 4) == 0)
                    {
                        matrix = particle.Owner.Transform * matrix;
                    }
                    particle.InvokeSetVecsFunc(matrix);
                    particle.InvokeDrawFunc(1);
                    if (particle.ShouldDraw)
                    {
                        Material material = particle.Owner.Model.Materials[particle.MaterialId];
                        MeshInfo meshInfo;
                        if (particle.DrawNode)
                        {
                            Model model = particle.Owner.Model;
                            Node node = particle.Owner.Nodes[particle.ParticleId];
                            Mesh mesh = particle.Owner.Model.Meshes[node.MeshId / 2];
                            meshInfo = new MeshInfo(particle, model, node, mesh, material, polygonId++, particle.Alpha);
                        }
                        else
                        {
                            meshInfo = new MeshInfo(particle, material, polygonId++, particle.Alpha);
                        }
                        _nonDecalMeshes.Add(meshInfo);
                        _translucentMeshes.Add(meshInfo);
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
                RenderItem(item);
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
                RenderItem(item);
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
                RenderItem(item);
            }
            // pass 4: rebuild depth buffer
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
            GL.StencilFunc(StencilFunction.Always, 0, 0xFF);
            GL.AlphaFunc(AlphaFunction.Equal, 1.0f);
            for (int i = 0; i < _nonDecalMeshes.Count; i++)
            {
                MeshInfo item = _nonDecalMeshes[i];
                RenderItem(item);
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
                RenderItem(item);
            }
            // pass 6: translucent (before)
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
            for (int i = 0; i < _translucentMeshes.Count; i++)
            {
                MeshInfo item = _translucentMeshes[i];
                GL.StencilFunc(StencilFunction.Equal, item.PolygonId, 0xFF);
                RenderItem(item);
            }
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.AlphaTest);
            GL.Disable(EnableCap.StencilTest);
            RenderDisplayVolumes();
        }

        private void RenderDisplayVolumes()
        {
            if ((_showVolumes > 0 && (_displayVolumes.Count > 0 || _displayPlanes.Count > 0)) || _showKillPlane)
            {
                GL.Uniform1(_shaderLocations.UseLight, 0);
                GL.Uniform1(_shaderLocations.UseTexture, 0);
                GL.Uniform1(_shaderLocations.UseOverride, 1);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.Enable(EnableCap.CullFace);
                foreach (KeyValuePair<int, DisplayVolume> kvp in _displayVolumes)
                {
                    if (_selectionMode == SelectionMode.None || _selectedModelId == kvp.Key)
                    {
                        GL.PolygonMode(MaterialFace.FrontAndBack, OpenTK.Graphics.OpenGL.PolygonMode.Fill);
                        RenderDisplayVolume(kvp.Value);
                        if (_volumeEdges)
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, OpenTK.Graphics.OpenGL.PolygonMode.Line);
                            RenderDisplayVolume(kvp.Value);
                        }
                    }
                }
                if (_showVolumes == 12)
                {
                    GL.Disable(EnableCap.CullFace);
                    GL.PolygonMode(MaterialFace.FrontAndBack, OpenTK.Graphics.OpenGL.PolygonMode.Fill);
                    Matrix4 transform = Matrix4.Identity;
                    GL.UniformMatrix4(_shaderLocations.MatrixStack, transpose: false, ref transform);
                    foreach (CollisionPortal plane in _displayPlanes)
                    {
                        RenderDisplayPlane(plane);
                        if (_volumeEdges)
                        {
                            RenderDisplayLines(plane);
                        }
                    }
                }
                if (_showKillPlane)
                {
                    GL.Disable(EnableCap.CullFace);
                    GL.PolygonMode(MaterialFace.FrontAndBack, OpenTK.Graphics.OpenGL.PolygonMode.Fill);
                    Matrix4 transform = Matrix4.Identity;
                    GL.UniformMatrix4(_shaderLocations.MatrixStack, transpose: false, ref transform);
                    RenderKillPlane();
                }
                GL.Disable(EnableCap.Blend);
                if (_faceCulling)
                {
                    GL.Enable(EnableCap.CullFace);
                }
                else
                {
                    GL.Disable(EnableCap.CullFace);
                }
            }
        }

        private void RenderDisplayVolume(DisplayVolume volume)
        {
            Vector3? color = volume.GetColor(_showVolumes);
            if (color != null)
            {
                GL.CullFace(volume.Volume.TestPoint(_cameraPosition * -1) ? CullFaceMode.Front : CullFaceMode.Back);
                Matrix4 transform = Matrix4.Identity;
                GL.UniformMatrix4(_shaderLocations.MatrixStack, transpose: false, ref transform);
                GL.Uniform4(_shaderLocations.OverrideColor, new Vector4(color.Value, 0.5f));
                RenderVolume(volume.Volume);
            }
        }

        private float GetPortalAlpha(Vector3 position)
        {
            float between = (position - _cameraPosition * -1).Length;
            return MathF.Min(between / 8, 1);
        }

        private void RenderDisplayPlane(CollisionPortal plane)
        {
            float alpha = GetPortalAlpha(plane.Position);
            Vector4 color;
            if (plane.IsForceField)
            {
                color = new Vector4(16 / 31f, 16 / 31f, 1f, alpha);
            }
            else
            {
                color = new Vector4(16 / 31f, 1f, 16 / 31f, alpha);
            }
            GL.Uniform4(_shaderLocations.OverrideColor, color);
            GL.Begin(PrimitiveType.TriangleStrip);
            GL.Vertex3(plane.Point1);
            GL.Vertex3(plane.Point4);
            GL.Vertex3(plane.Point2);
            GL.Vertex3(plane.Point3);
            GL.End();
        }

        private void RenderKillPlane()
        {
            var color = new Vector4(1f, 0f, 1f, 0.5f);
            GL.Uniform4(_shaderLocations.OverrideColor, color);
            GL.Begin(PrimitiveType.TriangleStrip);
            GL.Vertex3(10000f, _killHeight, 10000f);
            GL.Vertex3(-10000f, _killHeight, 10000f);
            GL.Vertex3(10000f, _killHeight, -10000f);
            GL.Vertex3(-10000f, _killHeight, -10000f);
            GL.End();
        }

        private void RenderDisplayLines(CollisionPortal plane)
        {
            GL.Uniform4(_shaderLocations.OverrideColor, new Vector4(1f, 0f, 0f, 1f));
            GL.Begin(PrimitiveType.LineLoop);
            GL.Vertex3(plane.Point1);
            GL.Vertex3(plane.Point2);
            GL.Vertex3(plane.Point3);
            GL.Vertex3(plane.Point4);
            GL.End();
        }

        private void UpdateUniforms()
        {
            UseRoomLights();
            GL.Uniform1(_shaderLocations.UseFog, _hasFog && _showFog ? 1 : 0);
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
                TextureAnimationGroup? group = model.Animations.TextureGroup;
                if (group != null && group.Animations.TryGetValue(material.Name, out TextureAnimation animation))
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

        private void RenderItem(MeshInfo info)
        {
            if (info.IsParticle)
            {
                if (info.ParticleNode)
                {
                    info.Material.CurrentDiffuse = info.Particle.Color;
                    info.Node.Animation = info.Particle.NodeTransform;
                    RenderMesh(info);
                }
                else
                {
                    RenderParticle(info);
                }
            }
            else
            {
                RenderMesh(info);
            }
        }

        private void RenderParticle(MeshInfo item)
        {
            Matrix4 transform = Matrix4.Identity;
            if ((item.Particle.Owner.Flags & 1) != 0)
            {
                // ptodo: confirm this works (i.e. rotation)
                transform = item.Particle.Owner.Transform;
            }
            GL.UniformMatrix4(_shaderLocations.MatrixStack, transpose: false, ref transform);

            GL.Uniform1(_shaderLocations.UseLight, 0);
            GL.Color3(item.Particle.Color);
            GL.Uniform1(_shaderLocations.MaterialAlpha, item.Alpha);
            GL.Uniform1(_shaderLocations.MaterialMode, (int)PolygonMode.Modulate);

            int bindingId = item.Particle.Owner.TextureBindingIds[item.Particle.ParticleId];
            int textureId = item.Material.TextureId;
            RepeatMode xRepeat = item.Material.XRepeat;
            RepeatMode yRepeat = item.Material.YRepeat;
            if (textureId != UInt16.MaxValue)
            {
                GL.BindTexture(TextureTarget.Texture2D, bindingId);
                int minParameter = _textureFiltering ? (int)TextureMinFilter.Linear : (int)TextureMinFilter.Nearest;
                int magParameter = _textureFiltering ? (int)TextureMagFilter.Linear : (int)TextureMagFilter.Nearest;
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, minParameter);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, magParameter);
                switch (xRepeat)
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
                switch (yRepeat)
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
                GL.Uniform1(_shaderLocations.TexgenMode, (int)TexgenMode.None);
                GL.UniformMatrix4(_shaderLocations.TextureMatrix, transpose: false, ref texcoordMatrix);
            }
            GL.Uniform1(_shaderLocations.UseTexture, textureId != UInt16.MaxValue && _showTextures ? 1 : 0);
            GL.Uniform1(_shaderLocations.UseOverride, 0);
            GL.Uniform1(_shaderLocations.UsePaletteOverride, 0);

            GL.Disable(EnableCap.CullFace);
            GL.PolygonMode(MaterialFace.FrontAndBack,
                _wireframe || item.Material.Wireframe != 0
                ? OpenTK.Graphics.OpenGL.PolygonMode.Line
                : OpenTK.Graphics.OpenGL.PolygonMode.Fill);

            float scaleS = 1;
            float scaleT = 1;
            if (item.Material.XRepeat == RepeatMode.Mirror)
            {
                scaleS = item.Material.ScaleS;
            }
            if (item.Material.YRepeat == RepeatMode.Mirror)
            {
                scaleT = item.Material.ScaleT;
            }
            GL.Begin(PrimitiveType.Quads);
            GL.TexCoord3(item.Particle.Texcoord0.X * scaleS, item.Particle.Texcoord0.Y * scaleT, 0f);
            GL.Vertex3(item.Particle.Vertex0);
            GL.TexCoord3(item.Particle.Texcoord1.X * scaleS, item.Particle.Texcoord1.Y * scaleT, 0f);
            GL.Vertex3(item.Particle.Vertex1);
            GL.TexCoord3(item.Particle.Texcoord2.X * scaleS, item.Particle.Texcoord2.Y * scaleT, 0f);
            GL.Vertex3(item.Particle.Vertex2);
            GL.TexCoord3(item.Particle.Texcoord3.X * scaleS, item.Particle.Texcoord3.Y * scaleT, 0f);
            GL.Vertex3(item.Particle.Vertex3);
            GL.End();
        }

        private void RenderMesh(MeshInfo item)
        {
            Model model = item.Model;
            UseRoomLights();
            if (model.UseLightSources || model.UseLightOverride)
            {
                UseLight1(model.Light1Vector, model.Light1Color);
                UseLight2(model.Light2Vector, model.Light2Color);
            }
            if (model.NodeMatrixIds.Count == 0)
            {
                Matrix4 transform = item.Node.Animation;
                GL.UniformMatrix4(_shaderLocations.MatrixStack, transpose: false, ref transform);
            }
            else
            {
                GL.UniformMatrix4(_shaderLocations.MatrixStack, model.NodeMatrixIds.Count, transpose: false, model.MatrixStackValues);
            }
            DoMaterial(model, item.Material, item.Node, item.Alpha);
            // texgen actually uses the transform from the current node, not the matrix stack
            DoTexture(model, item.Node, item.Mesh, item.Material);
            if (_faceCulling)
            {
                GL.Enable(EnableCap.CullFace);
                if (item.Material.Culling == CullingMode.Neither)
                {
                    GL.Disable(EnableCap.CullFace);
                }
                else if (item.Material.Culling == CullingMode.Back)
                {
                    GL.CullFace(CullFaceMode.Back);
                }
                else if (item.Material.Culling == CullingMode.Front)
                {
                    GL.CullFace(CullFaceMode.Front);
                }
            }
            GL.PolygonMode(MaterialFace.FrontAndBack,
                _wireframe || item.Material.Wireframe != 0
                ? OpenTK.Graphics.OpenGL.PolygonMode.Line
                : OpenTK.Graphics.OpenGL.PolygonMode.Fill);
            GL.CallList(item.Mesh.ListId);
        }

        private const float _colorStep = 8 / 255f;

        private void UpdateLightSources(Model model, double elapsedTime)
        {
            static float UpdateChannel(float current, float source, float frames)
            {
                float diff = source - current;
                if (MathF.Abs(diff) < _colorStep)
                {
                    return source;
                }
                int factor;
                if (current > source)
                {
                    factor = (int)MathF.Truncate((diff + _colorStep) / (8 * _colorStep));
                    if (factor <= -1)
                    {
                        return current + (factor - 1) * _colorStep * frames;
                    }
                    return current - _colorStep * frames;
                }
                factor = (int)MathF.Truncate(diff / (8 * _colorStep));
                if (factor >= 1)
                {
                    return current + factor * _colorStep * frames;
                }
                return current + _colorStep * frames;
            }
            bool hasLight1 = false;
            bool hasLight2 = false;
            Vector3 light1Color = model.Light1Color;
            Vector3 light1Vector = model.Light1Vector;
            Vector3 light2Color = model.Light2Color;
            Vector3 light2Vector = model.Light2Vector;
            float frames = (float)elapsedTime * 30;
            foreach (LightSource lightSource in _lightSources)
            {
                if (lightSource.Volume.TestPoint(model.Position))
                {
                    if (lightSource.Light1Enabled)
                    {
                        hasLight1 = true;
                        light1Vector.X += (lightSource.Light1Vector.X - light1Vector.X) / 8f * frames;
                        light1Vector.Y += (lightSource.Light1Vector.Y - light1Vector.Y) / 8f * frames;
                        light1Vector.Z += (lightSource.Light1Vector.Z - light1Vector.Z) / 8f * frames;
                        light1Color.X = UpdateChannel(light1Color.X, lightSource.Color1.X, frames);
                        light1Color.Y = UpdateChannel(light1Color.Y, lightSource.Color1.Y, frames);
                        light1Color.Z = UpdateChannel(light1Color.Z, lightSource.Color1.Z, frames);
                    }
                    if (lightSource.Light2Enabled)
                    {
                        hasLight2 = true;
                        light2Vector.X += (lightSource.Light2Vector.X - light2Vector.X) / 8f * frames;
                        light2Vector.Y += (lightSource.Light2Vector.Y - light2Vector.Y) / 8f * frames;
                        light2Vector.Z += (lightSource.Light2Vector.Z - light2Vector.Z) / 8f * frames;
                        light2Color.X = UpdateChannel(light2Color.X, lightSource.Color2.X, frames);
                        light2Color.Y = UpdateChannel(light2Color.Y, lightSource.Color2.Y, frames);
                        light2Color.Z = UpdateChannel(light2Color.Z, lightSource.Color2.Z, frames);
                    }
                }
            }
            if (!hasLight1)
            {
                light1Vector.X += (_light1Vector.X - light1Vector.X) / 8f * frames;
                light1Vector.Y += (_light1Vector.Y - light1Vector.Y) / 8f * frames;
                light1Vector.Z += (_light1Vector.Z - light1Vector.Z) / 8f * frames;
                light1Color.X = UpdateChannel(light1Color.X, _light1Color.X, frames);
                light1Color.Y = UpdateChannel(light1Color.Y, _light1Color.Y, frames);
                light1Color.Z = UpdateChannel(light1Color.Z, _light1Color.Z, frames);
            }
            if (!hasLight2)
            {
                light2Vector.X += (_light2Vector.X - light2Vector.X) / 8f * frames;
                light2Vector.Y += (_light2Vector.Y - light2Vector.Y) / 8f * frames;
                light2Vector.Z += (_light2Vector.Z - light2Vector.Z) / 8f * frames;
                light2Color.X = UpdateChannel(light2Color.X, _light2Color.X, frames);
                light2Color.Y = UpdateChannel(light2Color.Y, _light2Color.Y, frames);
                light2Color.Z = UpdateChannel(light2Color.Z, _light2Color.Z, frames);
            }
            model.Light1Color = light1Color;
            model.Light1Vector = light1Vector.Normalized();
            model.Light2Color = light2Color;
            model.Light2Vector = light2Vector.Normalized();
            UseLight1(model.Light1Vector, model.Light1Color);
            UseLight2(model.Light2Vector, model.Light2Color);
        }

        private void UpdateLightOverride(Model model)
        {
            Vector3 player = _cameraPosition * (_cameraMode == CameraMode.Roam ? -1 : 1);
            var vector1 = new Vector3(0, 1, 0);
            Vector3 vector2 = new Vector3(player.X - model.Position.X, 0, player.Z - model.Position.Z).Normalized();
            Matrix3 lightTransform = SceneSetup.GetTransformMatrix(vector2, vector1);
            model.Light1Vector = (Metadata.OctolithLight1Vector * lightTransform).Normalized();
            model.Light1Color = Metadata.OctolithLightColor;
            model.Light2Vector = (Metadata.OctolithLight2Vector * lightTransform).Normalized();
            model.Light2Color = Metadata.OctolithLightColor;
        }

        public static float InterpolateAnimation(IReadOnlyList<float> values, int start, int frame, int blend, int lutLength, int frameCount,
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

        public static Matrix4 AnimateNode(NodeAnimationGroup group, NodeAnimation animation, Vector3 modelScale)
        {
            float scaleX = InterpolateAnimation(group.Scales, animation.ScaleLutIndexX, group.CurrentFrame,
                animation.ScaleBlendX, animation.ScaleLutLengthX, group.FrameCount);
            float scaleY = InterpolateAnimation(group.Scales, animation.ScaleLutIndexY, group.CurrentFrame,
                animation.ScaleBlendY, animation.ScaleLutLengthY, group.FrameCount);
            float scaleZ = InterpolateAnimation(group.Scales, animation.ScaleLutIndexZ, group.CurrentFrame,
                animation.ScaleBlendZ, animation.ScaleLutLengthZ, group.FrameCount);
            float rotateX = InterpolateAnimation(group.Rotations, animation.RotateLutIndexX, group.CurrentFrame,
                animation.RotateBlendX, animation.RotateLutLengthX, group.FrameCount, isRotation: true);
            float rotateY = InterpolateAnimation(group.Rotations, animation.RotateLutIndexY, group.CurrentFrame,
                animation.RotateBlendY, animation.RotateLutLengthY, group.FrameCount, isRotation: true);
            float rotateZ = InterpolateAnimation(group.Rotations, animation.RotateLutIndexZ, group.CurrentFrame,
                animation.RotateBlendZ, animation.RotateLutLengthZ, group.FrameCount, isRotation: true);
            float translateX = InterpolateAnimation(group.Translations, animation.TranslateLutIndexX, group.CurrentFrame,
                animation.TranslateBlendX, animation.TranslateLutLengthX, group.FrameCount);
            float translateY = InterpolateAnimation(group.Translations, animation.TranslateLutIndexY, group.CurrentFrame,
                animation.TranslateBlendY, animation.TranslateLutLengthY, group.FrameCount);
            float translateZ = InterpolateAnimation(group.Translations, animation.TranslateLutIndexZ, group.CurrentFrame,
                animation.TranslateBlendZ, animation.TranslateLutLengthZ, group.FrameCount);
            // todo: hunter scale factors/any others?
            var nodeMatrix = Matrix4.CreateTranslation(translateX / modelScale.X, translateY / modelScale.Y, translateZ / modelScale.Z);
            nodeMatrix = Matrix4.CreateRotationX(rotateX) * Matrix4.CreateRotationY(rotateY) * Matrix4.CreateRotationZ(rotateZ) * nodeMatrix;
            nodeMatrix = Matrix4.CreateScale(scaleX, scaleY, scaleZ) * nodeMatrix;
            return nodeMatrix;
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

        private void DoTexture(Model model, Node node, Mesh mesh, Material material)
        {
            TexgenMode texgenMode = material.TexgenMode;
            RepeatMode xRepeat = material.XRepeat;
            RepeatMode yRepeat = material.YRepeat;
            int bindingId = material.TextureBindingId;
            if (model.DoubleDamage && !model.DoubleDamageSkipMaterials.Contains(material)
                && material.Lighting > 0 && node.BillboardMode == BillboardMode.None)
            {
                texgenMode = TexgenMode.Normal;
                xRepeat = RepeatMode.Mirror;
                yRepeat = RepeatMode.Mirror;
                bindingId = _dblDmgBindingId;
            }
            int textureId = material.CurrentTextureId;
            if (textureId != UInt16.MaxValue)
            {
                GL.BindTexture(TextureTarget.Texture2D, bindingId);
                int minParameter = _textureFiltering ? (int)TextureMinFilter.Linear : (int)TextureMinFilter.Nearest;
                int magParameter = _textureFiltering ? (int)TextureMagFilter.Linear : (int)TextureMagFilter.Nearest;
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, minParameter);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, magParameter);
                switch (xRepeat)
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
                switch (yRepeat)
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
                TexcoordAnimationGroup? group = model.Animations.TexcoordGroup;
                TexcoordAnimation? animation = null;
                if (group != null && group.Animations.TryGetValue(material.Name, out TexcoordAnimation result))
                {
                    animation = result;
                }
                if (group != null && animation != null)
                {
                    texcoordMatrix = AnimateTexcoords(group, animation.Value);
                }
                if (texgenMode != TexgenMode.None)
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
                    if (texgenMode == TexgenMode.Normal)
                    {
                        Texture texture = model.Textures[material.TextureId];
                        Matrix4 product = node.Animation.Keep3x3();
                        Matrix4 texgenMatrix = Matrix4.Identity;
                        // in-game, there's only one uniform scale factor for models
                        if (model.Scale.X != 1 || model.Scale.Y != 1 || model.Scale.Z != 1)
                        {
                            texgenMatrix = Matrix4.CreateScale(model.Scale) * texgenMatrix;
                        }
                        if ((model.Flags & 1) > 0)
                        {
                            texgenMatrix = _viewMatrix * texgenMatrix;
                        }
                        product *= texgenMatrix;
                        product.M12 *= -1;
                        product.M13 *= -1;
                        product.M22 *= -1;
                        product.M23 *= -1;
                        product.M32 *= -1;
                        product.M33 *= -1;
                        if (model.DoubleDamage && !model.DoubleDamageSkipMaterials.Contains(material)
                            && material.Lighting > 0 && node.BillboardMode == BillboardMode.None)
                        {
                            long frame = _frameCount / 2;
                            float rotZ = ((int)(16 * ((781874935307L * (ulong)(53248 * frame) >> 32) + 2048)) >> 20) * (360 / 4096f);
                            float rotY = ((int)(16 * ((781874935307L * (ulong)(26624 * frame) + 0x80000000000) >> 32)) >> 20) * (360 / 4096f);
                            var rot = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(rotZ));
                            rot *= Matrix4.CreateRotationY(MathHelper.DegreesToRadians(rotY));
                            product = rot * product;
                        }
                        else
                        {
                            product *= materialMatrix;
                            product *= texcoordMatrix;
                        }
                        product *= (1.0f / (texture.Width / 2));
                        texcoordMatrix = new Matrix4(
                            product.Row0 * 16.0f,
                            product.Row1 * 16.0f,
                            product.Row2 * 16.0f,
                            product.Row3
                        );
                        texcoordMatrix.Transpose();
                    }
                }
                GL.Uniform1(_shaderLocations.TexgenMode, (int)texgenMode);
                GL.UniformMatrix4(_shaderLocations.TextureMatrix, transpose: false, ref texcoordMatrix);
            }
            GL.Uniform1(_shaderLocations.UseTexture, textureId != UInt16.MaxValue && _showTextures ? 1 : 0);
            Vector4? overrideColor = null;
            if (_showSelection)
            {
                overrideColor = mesh.OverrideColor;
            }
            if (overrideColor == null)
            {
                overrideColor = mesh.PlaceholderColor;
            }
            if (overrideColor != null)
            {
                Vector4 overrideColorValue = overrideColor.Value;
                GL.Uniform1(_shaderLocations.UseOverride, 1);
                GL.Uniform4(_shaderLocations.OverrideColor, ref overrideColorValue);
            }
            else
            {
                GL.Uniform1(_shaderLocations.UseOverride, 0);
            }
            if (model.PaletteOverride != null)
            {
                Vector4 overrideColorValue = model.PaletteOverride.Value;
                GL.Uniform1(_shaderLocations.UsePaletteOverride, 1);
                GL.Uniform4(_shaderLocations.PaletteOverrideColor, ref overrideColorValue);
            }
            else
            {
                GL.Uniform1(_shaderLocations.UsePaletteOverride, 0);
            }
        }

        private void DoMaterial(Model model, Material material, Node node, float alphaScale)
        {
            if (_lighting && material.Lighting != 0)
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
            MaterialAnimationGroup? group = model.Animations.MaterialGroup;
            if (group != null && group.Animations.TryGetValue(material.Name, out MaterialAnimation animation))
            {
                if (!material.AnimationFlags.HasFlag(AnimationFlags.DisableColor))
                {
                    float diffuseR = InterpolateAnimation(group.Colors, animation.DiffuseLutIndexR, group.CurrentFrame,
                        animation.DiffuseBlendR, animation.DiffuseLutLengthR, group.FrameCount);
                    float diffuseG = InterpolateAnimation(group.Colors, animation.DiffuseLutIndexG, group.CurrentFrame,
                        animation.DiffuseBlendG, animation.DiffuseLutLengthG, group.FrameCount);
                    float diffuseB = InterpolateAnimation(group.Colors, animation.DiffuseLutIndexB, group.CurrentFrame,
                        animation.DiffuseBlendB, animation.DiffuseLutLengthB, group.FrameCount);
                    float ambientR = InterpolateAnimation(group.Colors, animation.AmbientLutIndexR, group.CurrentFrame,
                        animation.AmbientBlendR, animation.AmbientLutLengthR, group.FrameCount);
                    float ambientG = InterpolateAnimation(group.Colors, animation.AmbientLutIndexG, group.CurrentFrame,
                        animation.AmbientBlendG, animation.AmbientLutLengthG, group.FrameCount);
                    float ambientB = InterpolateAnimation(group.Colors, animation.AmbientLutIndexB, group.CurrentFrame,
                        animation.AmbientBlendB, animation.AmbientLutLengthB, group.FrameCount);
                    float specularR = InterpolateAnimation(group.Colors, animation.SpecularLutIndexR, group.CurrentFrame,
                        animation.SpecularBlendR, animation.SpecularLutLengthR, group.FrameCount);
                    float specularG = InterpolateAnimation(group.Colors, animation.SpecularLutIndexG, group.CurrentFrame,
                        animation.SpecularBlendG, animation.SpecularLutLengthG, group.FrameCount);
                    float specularB = InterpolateAnimation(group.Colors, animation.SpecularLutIndexB, group.CurrentFrame,
                        animation.SpecularBlendB, animation.SpecularLutLengthB, group.FrameCount);
                    diffuse = new Vector3(diffuseR / 31.0f, diffuseG / 31.0f, diffuseB / 31.0f);
                    ambient = new Vector3(ambientR / 31.0f, ambientG / 31.0f, ambientB / 31.0f);
                    specular = new Vector3(specularR / 31.0f, specularG / 31.0f, specularB / 31.0f);
                }
                if (!material.AnimationFlags.HasFlag(AnimationFlags.DisableAlpha))
                {
                    alpha = InterpolateAnimation(group.Colors, animation.AlphaLutIndex, group.CurrentFrame,
                    animation.AlphaBlend, animation.AlphaLutLength, group.FrameCount);
                    alpha /= 31.0f;
                }
            }
            // MPH applies the material colors initially by calling DIF_AMB with bit 15 set,
            // so the diffuse color is always set as the vertex color to start
            // (the emission color is set to white if lighting is disabled or black if lighting is enabled; we can just ignore that)
            // --> ...except for hunter models with teams enabled
            GL.Color3(diffuse);
            GL.Uniform3(_shaderLocations.Diffuse, diffuse);
            GL.Uniform3(_shaderLocations.Ambient, ambient);
            GL.Uniform3(_shaderLocations.Specular, specular);
            Vector3 emission = Vector3.Zero;
            if (model.DoubleDamage && !model.DoubleDamageSkipMaterials.Contains(material))
            {
                if (material.Lighting > 0 && node.BillboardMode == BillboardMode.None)
                {
                    emission = Metadata.EmissionGray;
                }
            }
            else if (model.Team == Team.Orange)
            {
                emission = Metadata.EmissionOrange;
            }
            else if (model.Team == Team.Green)
            {
                emission = Metadata.EmissionGreen;
            }
            GL.Uniform3(_shaderLocations.Emission, emission);
            GL.Uniform1(_shaderLocations.MaterialAlpha, alpha * alphaScale);
            GL.Uniform1(_shaderLocations.MaterialMode, (int)material.PolygonMode);
            material.CurrentAlpha = alpha;
            UpdateMaterials(model);
        }

        private void DoDlist(Model model, Mesh mesh, int textureWidth, int textureHeight, bool texgen)
        {
            IReadOnlyList<RenderInstruction> list = model.RenderInstructionLists[mesh.DlistId];
            float vtxX = 0;
            float vtxY = 0;
            float vtxZ = 0;
            float texX = texgen ? 0.5f : 0f;
            float texY = texgen ? 0.5f : 0f;
            uint matrixId = 0;
            GL.TexCoord3(texX, texY, 0f);
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
                        texX = s / 16.0f / textureWidth;
                        texY = t / 16.0f / textureHeight;
                        GL.TexCoord3(texX, texY, matrixId);
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
                    // in order to allow toggling room node transforms, keep the matrix ID at 0
                    if (model.Type != ModelType.Room)
                    {
                        matrixId = instruction.Arguments[0];
                    }
                    GL.TexCoord3(texX, texY, matrixId);
                    break;
                case InstructionCode.NOP:
                    break;
                default:
                    throw new ProgramException("Unknown opcode");
                }
            }
            // leave the ID at 0 in case the next thing we draw doesn't use the stack
            GL.TexCoord3(0f, 0f, 0f);
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
                _angleX += e.DeltaY / 1.5f;
                _angleX = Math.Clamp(_angleX, -90.0f, 90.0f);
                _angleY += e.DeltaX / 1.5f;
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

        private readonly HashSet<EntityType> _targetTypes = new HashSet<EntityType>();

        protected override async void OnKeyDown(KeyboardKeyEventArgs e)
        {
            if (e.Key == Keys.D5)
            {
                if (!_recording)
                {
                    Images.Screenshot(Size.X, Size.Y);
                }
            }
            else if (e.Key == Keys.T)
            {
                _showTextures = !_showTextures;
                await PrintOutput();
            }
            else if (e.Key == Keys.C)
            {
                _showColors = !_showColors;
                await PrintOutput();
            }
            else if (e.Key == Keys.Q)
            {
                if (_showVolumes > 0)
                {
                    _volumeEdges = !_volumeEdges;
                }
                else
                {
                    _wireframe = !_wireframe;
                }
                await PrintOutput();
            }
            else if (e.Key == Keys.B)
            {
                _faceCulling = !_faceCulling;
                if (!_faceCulling)
                {
                    GL.Disable(EnableCap.CullFace);
                }
                await PrintOutput();
            }
            else if (e.Key == Keys.F)
            {
                _textureFiltering = !_textureFiltering;
                await PrintOutput();
            }
            else if (e.Key == Keys.L)
            {
                _lighting = !_lighting;
                await PrintOutput();
            }
            else if (e.Key == Keys.Z)
            {
                // todo: this needs to be organized
                if (e.Control)
                {
                    _showVolumes = 0;
                }
                else if (e.Shift)
                {
                    _showVolumes--;
                    if (_showVolumes < 0)
                    {
                        _showVolumes = 12;
                    }
                    if (_showVolumes != 0 && _selectionMode == SelectionMode.Model)
                    {
                        int previousSelection = _selectedModelId;
                        Deselect();
                        _selectedModelId = 0;
                        await SelectNextModel();
                        if (!_modelMap.ContainsKey(_selectedModelId))
                        {
                            _selectedModelId = previousSelection;
                            SetSelectedModel(previousSelection);
                        }
                    }
                }
                else
                {
                    _showVolumes++;
                    if (_showVolumes > 12)
                    {
                        _showVolumes = 0;
                    }
                    if (_showVolumes != 0 && _selectionMode == SelectionMode.Model)
                    {
                        int previousSelection = _selectedModelId;
                        Deselect();
                        _selectedModelId = 0;
                        await SelectNextModel();
                        if (!_modelMap.ContainsKey(_selectedModelId))
                        {
                            _selectedModelId = previousSelection;
                            SetSelectedModel(previousSelection);
                        }
                    }
                }
                await PrintOutput();
            }
            else if (e.Key == Keys.G)
            {
                if (e.Alt)
                {
                    _useClip = !_useClip;
                }
                else
                {
                    _showFog = !_showFog;
                }
                await PrintOutput();
            }
            else if (e.Key == Keys.N)
            {
                _transformRoomNodes = !_transformRoomNodes;
                await PrintOutput();
            }
            else if (e.Key == Keys.H)
            {
                if (e.Alt)
                {
                    _showKillPlane = !_showKillPlane;
                }
                else
                {
                    _showSelection = !_showSelection;
                }
            }
            else if (e.Key == Keys.I)
            {
                _showInvisible = !_showInvisible;
                await PrintOutput();
            }
            else if (e.Key == Keys.E)
            {
                if (e.Alt)
                {
                    // undocumented -- might not be needed once we have an animation index setter
                    if (_selectionMode == SelectionMode.Model
                        && SelectedModel.Entity is Entity<ObjectEntityData> obj && obj.Data.EffectId != 0)
                    {
                        ((ObjectModel)SelectedModel).ForceSpawnEffect = true;
                    }
                }
                else
                {
                    _scanVisor = !_scanVisor;
                    await PrintOutput();
                }
            }
            else if (e.Key == Keys.R)
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
            else if (e.Key == Keys.P)
            {
                if (e.Alt)
                {
                    // undocumented
                    UpdatePointModule();
                }
                else
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
            }
            else if (e.Key == Keys.Enter)
            {
                _frameAdvanceOn = !_frameAdvanceOn;
            }
            else if (e.Key == Keys.Period)
            {
                if (_frameAdvanceOn)
                {
                    _advanceOneFrame = true;
                }
            }
            else if (e.Control && e.Key == Keys.O)
            {
                await LoadModel();
            }
            else if (e.Control && e.Key == Keys.U)
            {
                await UnloadModel();
            }
            else if (e.Key == Keys.M)
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
            else if (e.Key == Keys.Equal || e.Key == Keys.KeyPadEqual)
            {
                if (e.Alt)
                {
                    // todo: select other animation types, and enable playing in reverse
                    if (_selectionMode == SelectionMode.Model && _modelMap.TryGetValue(_selectedModelId, out Model? model))
                    {
                        if (e.Control)
                        {
                            int id = model.Animations.MaterialGroupId + 1;
                            if (id >= model.Animations.MaterialGroups.Count)
                            {
                                id = -1;
                            }
                            model.Animations.MaterialGroupId = id;
                            await PrintOutput();
                        }
                        else
                        {
                            int id = model.Animations.NodeGroupId + 1;
                            if (id >= model.Animations.NodeGroups.Count)
                            {
                                id = -1;
                            }
                            model.Animations.NodeGroupId = id;
                            await PrintOutput();
                        }
                    }
                }
                else
                {
                    await SelectNextModel(e.Shift);
                }
            }
            else if (e.Key == Keys.Minus || e.Key == Keys.KeyPadSubtract)
            {
                if (e.Alt)
                {
                    if (_selectionMode == SelectionMode.Model && _modelMap.TryGetValue(_selectedModelId, out Model? model))
                    {
                        if (e.Control)
                        {
                            int id = model.Animations.MaterialGroupId - 1;
                            if (id < -1)
                            {
                                id = model.Animations.MaterialGroups.Count - 1;
                            }
                            model.Animations.MaterialGroupId = id;
                            await PrintOutput();
                        }
                        else
                        {
                            int id = model.Animations.NodeGroupId - 1;
                            if (id < -1)
                            {
                                id = model.Animations.NodeGroups.Count - 1;
                            }
                            model.Animations.NodeGroupId = id;
                            await PrintOutput();
                        }
                    }
                }
                else
                {
                    await SelectPreviousModel(e.Shift);
                }
            }
            else if (e.Key == Keys.X)
            {
                if (_selectionMode == SelectionMode.Model)
                {
                    if (e.Control)
                    {
                        if (SelectedModel.Entity != null)
                        {
                            if (e.Shift)
                            {
                                ushort childId = SelectedModel.Entity.GetChildId();
                                Model? child = _models.FirstOrDefault(m => m.Entity?.EntityId == childId);
                                if (child != null)
                                {
                                    LookAt(child.Position);
                                }
                            }
                            else
                            {
                                ushort parentId = SelectedModel.Entity.GetParentId();
                                Model? parent = _models.FirstOrDefault(m => m.Entity?.EntityId == parentId);
                                if (parent != null)
                                {
                                    LookAt(parent.Position);
                                }
                            }
                        }
                    }
                    else
                    {
                        LookAt(SelectedModel.Position);
                    }
                }
                else if (_selectionMode == SelectionMode.Node || _selectionMode == SelectionMode.Mesh)
                {
                    LookAt(SelectedModel.Nodes[_selectedNodeId].Position);
                }
            }
            else if (e.Key == Keys.D0 || e.Key == Keys.KeyPad0)
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
            else if (e.Key == Keys.D1 || e.Key == Keys.KeyPad1)
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
            else if (e.Key == Keys.D2 || e.Key == Keys.KeyPad2)
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
            else if (e.Key == Keys.Escape)
            {
                await Output.End();
                Close();
            }
            base.OnKeyDown(e);
        }

        private IEnumerable<Model> GetModelMatches()
        {
            UpdateTargetTypes();
            return _models.Where(m => (_showInvisible || m.Type != ModelType.Placeholder) &&
                (_showVolumes == 0 || (m.Entity != null && _targetTypes.Contains(m.Entity.Type))) &&
                (_scanVisor || !m.ScanVisorOnly)).OrderBy(m => m.SceneId);
        }

        private async Task SelectNextModel(bool shift = false)
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
                    if (shift)
                    {
                        nodeIndex = model.GetNextRoomPartId(_selectedNodeId);
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
                    Model? nextModel = model;
                    IEnumerable<Model> matches = GetModelMatches();
                    if (matches.Any())
                    {
                        nextModel = matches.FirstOrDefault(m => m.SceneId > model.SceneId);
                        if (nextModel == null)
                        {
                            nextModel = matches.First();
                        }
                    }
                    _selectedMeshId = 0;
                    _selectedNodeId = 0;
                    SetSelectedModel(nextModel.SceneId);
                }
                await PrintOutput();
            }
        }

        private async Task SelectPreviousModel(bool shift)
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
                    if (shift)
                    {
                        nodeIndex = model.GetPrevRoomPartId(_selectedNodeId);
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
                    Model? nextModel = model;
                    IEnumerable<Model> matches = GetModelMatches();
                    if (matches.Any())
                    {
                        nextModel = matches.LastOrDefault(m => m.SceneId < model.SceneId);
                        if (nextModel == null)
                        {
                            nextModel = matches.Last();
                        }
                    }
                    _selectedMeshId = 0;
                    _selectedNodeId = 0;
                    SetSelectedModel(nextModel.SceneId);
                }
                await PrintOutput();
            }
        }

        private void UpdateTargetTypes()
        {
            _targetTypes.Clear();
            if (_showVolumes == 1 || _showVolumes == 2)
            {
                _targetTypes.Add(EntityType.LightSource);
            }
            else if (_showVolumes == 3 || _showVolumes == 4)
            {
                _targetTypes.Add(EntityType.TriggerVolume);
                _targetTypes.Add(EntityType.FhTriggerVolume);
            }
            else if (_showVolumes == 5 || _showVolumes == 6)
            {
                _targetTypes.Add(EntityType.AreaVolume);
                _targetTypes.Add(EntityType.FhAreaVolume);
            }
            else if (_showVolumes == 7)
            {
                _targetTypes.Add(EntityType.MorphCamera);
                _targetTypes.Add(EntityType.FhMorphCamera);
            }
            else if (_showVolumes == 8)
            {
                _targetTypes.Add(EntityType.JumpPad);
            }
            else if (_showVolumes == 9)
            {
                _targetTypes.Add(EntityType.Object);
            }
            else if (_showVolumes == 10)
            {
                _targetTypes.Add(EntityType.FlagBase);
            }
            else if (_showVolumes == 11)
            {
                _targetTypes.Add(EntityType.NodeDefense);
            }
        }

        private void UpdatePointModule(bool reset = false)
        {
            if (_lastPointModule != -1)
            {
                Model current = _entities[_lastPointModule];
                current.ScanVisorOnly = true;
                ushort nextId = current.Entity!.GetChildId();
                if (!reset)
                {
                    _lastPointModule = nextId;
                }
                int count = reset ? 5 : 6;
                for (int i = 0; i < count; i++)
                {
                    Model next = _entities[nextId];
                    next.ScanVisorOnly = i == 0 && !reset;
                    nextId = next.Entity!.GetChildId();
                    if (nextId == UInt16.MaxValue)
                    {
                        break;
                    }
                }
                if (!reset && _lastPointModule == _endPointModule)
                {
                    Model first = _entities.First(e => e.EntityType == EntityType.PointModule || e.EntityType == EntityType.FhPointModule);
                    _lastPointModule = first.Entity!.EntityId;
                    UpdatePointModule(reset: true);
                }
            }
        }

        private void OnKeyHeld()
        {
            if ((KeyboardState.IsKeyDown(Keys.LeftAlt) || KeyboardState.IsKeyDown(Keys.RightAlt))
                && _selectionMode == SelectionMode.Model)
            {
                MoveModel();
                return;
            }
            // sprint
            float step = KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift) ? 5 : 1;
            if (_cameraMode == CameraMode.Roam)
            {
                if (KeyboardState.IsKeyDown(Keys.W)) // move forward
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
                else if (KeyboardState.IsKeyDown(Keys.S)) // move backward
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
                if (KeyboardState.IsKeyDown(Keys.Space)) // move up
                {
                    _cameraPosition = new Vector3(_cameraPosition.X, _cameraPosition.Y - step * 0.1f, _cameraPosition.Z);
                }
                else if (KeyboardState.IsKeyDown(Keys.V)) // move down
                {
                    _cameraPosition = new Vector3(_cameraPosition.X, _cameraPosition.Y + step * 0.1f, _cameraPosition.Z);
                }
                if (KeyboardState.IsKeyDown(Keys.A)) // move left
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
                else if (KeyboardState.IsKeyDown(Keys.D)) // move right
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
                step = KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift) ? -3 : -1.5f;
            }
            if (KeyboardState.IsKeyDown(Keys.Up)) // rotate up
            {
                _angleX += step;
                _angleX = Math.Clamp(_angleX, -90.0f, 90.0f);
            }
            else if (KeyboardState.IsKeyDown(Keys.Down)) // rotate down
            {
                _angleX -= step;
                _angleX = Math.Clamp(_angleX, -90.0f, 90.0f);
            }
            if (KeyboardState.IsKeyDown(Keys.Left)) // rotate left
            {
                _angleY += step;
                _angleY %= 360f;
            }
            else if (KeyboardState.IsKeyDown(Keys.Right)) // rotate right
            {
                _angleY -= step;
                _angleY %= 360f;
            }
        }

        private void MoveModel()
        {
            float step = 0.1f;
            if (KeyboardState.IsKeyDown(Keys.W)) // move Z-
            {
                SelectedModel.Position = SelectedModel.Position.WithZ(SelectedModel.Position.Z - step);
            }
            else if (KeyboardState.IsKeyDown(Keys.S)) // move Z+
            {
                SelectedModel.Position = SelectedModel.Position.WithZ(SelectedModel.Position.Z + step);
            }
            if (KeyboardState.IsKeyDown(Keys.Space)) // move Y+
            {
                SelectedModel.Position = SelectedModel.Position.WithY(SelectedModel.Position.Y + step);
            }
            else if (KeyboardState.IsKeyDown(Keys.V)) // move Y-
            {
                SelectedModel.Position = SelectedModel.Position.WithY(SelectedModel.Position.Y - step);
            }
            if (KeyboardState.IsKeyDown(Keys.A)) // move X-
            {
                SelectedModel.Position = SelectedModel.Position.WithX(SelectedModel.Position.X - step);
            }
            else if (KeyboardState.IsKeyDown(Keys.D)) // move X+
            {
                SelectedModel.Position = SelectedModel.Position.WithX(SelectedModel.Position.X + step);
            }
            // todo: some transforms (sniper targets in UNIT4_RM2) aren't consistent when first changing the rotation
            step = 2.5f;
            Vector3 rotation = SelectedModel.Rotation;
            if (KeyboardState.IsKeyDown(Keys.Up)) // rotate up
            {
                rotation.X += step;
                rotation.X %= 360f;
                SelectedModel.Rotation = rotation;
            }
            else if (KeyboardState.IsKeyDown(Keys.Down)) // rotate down
            {
                rotation.X -= step;
                rotation.X %= 360f;
                SelectedModel.Rotation = rotation;
            }
            if (KeyboardState.IsKeyDown(Keys.Left)) // rotate left
            {
                rotation.Y += step;
                rotation.Y %= 360f;
                SelectedModel.Rotation = rotation;
            }
            else if (KeyboardState.IsKeyDown(Keys.Right)) // rotate right
            {
                rotation.Y -= step;
                rotation.Y %= 360f;
                SelectedModel.Rotation = rotation;
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
            Vector3 cam = _cameraPosition * (_cameraMode == CameraMode.Roam ? -1 : 1);
            await Output.Write($"Camera ({cam.X}, {cam.Y}, {cam.Z})", guid);
            await Output.Write(guid);
            await Output.Write($"Model: {model.Name} [{model.SceneId}] {(model.Visible ? "On " : "Off")} - " +
                $"Color {model.CurrentRecolor} / {model.Recolors.Count - 1}", guid);
            string type = $"{model.Type}";
            if (model.Entity != null)
            {
                type = $"{model.EntityType} {model.Entity.EntityId}";
            }
            if (model.Type == ModelType.Room)
            {
                type += $" ({model.Nodes.Count(n => n.IsRoomPartNode)})";
            }
            else if (model.Type == ModelType.Placeholder)
            {
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
                else if (model.Entity is Entity<AreaVolumeEntityData> area)
                {
                    type += Environment.NewLine + $"Entry: {area.Data.InsideEvent}";
                    type += $", Param1: {area.Data.InsideEventParam1}, Param2: {area.Data.InsideEventParam1}";
                    type += Environment.NewLine + $" Exit: {area.Data.ExitEvent}";
                    type += $", Param1: {area.Data.ExitEventParam1}, Param2: {area.Data.ExitEventParam2}";
                    if (TryGetByEntityId(area.Data.ParentId, out Model? parent))
                    {
                        type += Environment.NewLine + $"Target: {parent.Entity?.Type} ({area.Data.ParentId})";
                    }
                    else
                    {
                        type += Environment.NewLine + "Target: None";
                    }
                }
                else if (model.Entity is Entity<FhAreaVolumeEntityData> fhArea)
                {
                    type += Environment.NewLine + $"Entry: {fhArea.Data.InsideEvent}";
                    type += $", Param1: {fhArea.Data.InsideParam1}, Param2: 0";
                    type += Environment.NewLine + $" Exit: {fhArea.Data.ExitEvent}";
                    type += $", Param1: {fhArea.Data.ExitParam1}, Param2: 0";
                    type += Environment.NewLine + "Target: None";
                }
                else if (model.Entity is Entity<TriggerVolumeEntityData> trigger)
                {
                    type += $" ({trigger.Data.Type})";
                    if (trigger.Data.Type == TriggerType.Threshold)
                    {
                        type += $" x{trigger.Data.TriggerThreshold}";
                    }
                    type += Environment.NewLine + $"Parent: {trigger.Data.ParentEvent}";
                    if (trigger.Data.ParentEvent != Message.None && TryGetByEntityId(trigger.Data.ParentId, out Model? parent))
                    {
                        type += $", Target: {parent.Entity?.Type} ({trigger.Data.ParentId})";
                    }
                    else
                    {
                        type += ", Target: None";
                    }
                    type += $", Param1: {trigger.Data.ParentEventParam1}, Param2: {trigger.Data.ParentEventParam2}";
                    type += Environment.NewLine + $" Child: {trigger.Data.ChildEvent}";
                    if (trigger.Data.ChildEvent != Message.None && TryGetByEntityId(trigger.Data.ChildId, out Model? child))
                    {
                        type += $", Target: {child.Entity?.Type} ({trigger.Data.ChildId})";
                    }
                    else
                    {
                        type += ", Target: None";
                    }
                    type += $", Param1: {trigger.Data.ChildEventParam1}, Param2: {trigger.Data.ChildEventParam2}";
                }
                else if (model.Entity is Entity<FhTriggerVolumeEntityData> fhTrigger)
                {
                    type += $" ({fhTrigger.Data.Subtype})";
                    if (fhTrigger.Data.Subtype == 3)
                    {
                        type += $" x{fhTrigger.Data.Threshold}";
                    }
                    type += Environment.NewLine + $"Parent: {fhTrigger.Data.ParentEvent}";
                    if (fhTrigger.Data.ParentEvent != FhMessage.None && TryGetByEntityId(fhTrigger.Data.ParentId, out Model? parent))
                    {
                        type += $", Target: {parent.Entity?.Type} ({fhTrigger.Data.ParentId})";
                    }
                    else
                    {
                        type += ", Target: None";
                    }
                    type += $", Param1: {fhTrigger.Data.ParentParam1}, Param2: 0";
                    type += Environment.NewLine + $" Child: {fhTrigger.Data.ChildEvent}";
                    if (fhTrigger.Data.ChildEvent != FhMessage.None && TryGetByEntityId(fhTrigger.Data.ChildId, out Model? child))
                    {
                        type += $", Target: {child.Entity?.Type} ({fhTrigger.Data.ChildId})";
                    }
                    else
                    {
                        type += ", Target: None";
                    }
                    type += $", Param1: {fhTrigger.Data.ChildParam1}, Param2: 0";
                }
                else if (model.Entity is Entity<ObjectEntityData> obj)
                {
                    if (obj.Data.EffectId != 0)
                    {
                        type += $" ({obj.Data.EffectId}, {Metadata.Effects[(int)obj.Data.EffectId].Name})";
                    }
                }
            }
            await Output.Write(type, guid);
            await Output.Write($"Position ({model.Position.X}, {model.Position.Y}, {model.Position.Z})", guid);
            await Output.Write($"Rotation ({model.Rotation.X}, {model.Rotation.Y}, {model.Rotation.Z})", guid);
            await Output.Write($"   Scale ({model.Scale.X}, {model.Scale.Y}, {model.Scale.Z})", guid);
            await Output.Write($"Nodes {model.Nodes.Count}, Meshes {model.Meshes.Count}, Materials {model.Materials.Count}," +
                $" Textures {model.Textures.Count}, Palettes {model.Palettes.Count}", guid);
            AnimationInfo a = model.Animations;
            await Output.Write($"Anim: Node {a.NodeGroupId} / {a.NodeGroups.Count}, Material {a.MaterialGroupId} / {a.MaterialGroups.Count}," +
                $" Texcoord {a.TexcoordGroupId} / {a.TexcoordGroups.Count}, Texture {a.TextureGroupId} / {a.TextureGroups.Count}", guid);
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
            string billboard = node.BillboardMode != BillboardMode.None ? $" - {node.BillboardMode} Billboard" : "";
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
            string volume = _showVolumes switch
            {
                1 => "light sources, color 1",
                2 => "light sources, color 2",
                3 => "trigger volumes, parent event",
                4 => "trigger volumes, child event",
                5 => "area volumes, inside event",
                6 => "area volumes, exit event",
                7 => "morph cameras",
                8 => "jump pads",
                9 => "objects",
                10 => "flag bases",
                11 => "defense nodes",
                12 => "portals",
                _ => "off"
            };
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
            await Output.Write($" - Z toggles volume display ({volume})", guid);
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

        private Vector3 GetDiscVertices(float radius, int index)
        {
            return new Vector3(
                radius * MathF.Cos(2f * MathF.PI * index / 16f),
                0.0f,
                radius * MathF.Sin(2f * MathF.PI * index / 16f)
            );
        }

        private readonly List<Vector3> _vertices = new List<Vector3>();

        // matrix transform accounts for the entity position
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
                _vertices.Clear();
                Vector3 vector = volume.CylinderVector.Normalized();
                float radius = volume.CylinderRadius;
                Matrix3 rotation = Matrix.RotateAlign(Vector3.UnitY, vector);
                Vector3 start;
                Vector3 end;
                // cylinder volumes are always axis-aligned, so we can use this hack to avoid normal issues
                if (vector == Vector3.UnitX || vector == Vector3.UnitY || vector == Vector3.UnitZ)
                {
                    start = volume.CylinderPosition;
                    end = volume.CylinderPosition + vector * volume.CylinderDot;
                }
                else
                {
                    start = volume.CylinderPosition + vector * volume.CylinderDot;
                    end = volume.CylinderPosition;
                }
                for (int i = 0; i < 16; i++)
                {
                    _vertices.Add(GetDiscVertices(radius, i) * rotation + start);
                }
                for (int i = 0; i < 16; i++)
                {
                    _vertices.Add(GetDiscVertices(radius, i) * rotation + end);
                }
                // bottom
                GL.Begin(PrimitiveType.TriangleFan);
                GL.Vertex3(start);
                GL.Vertex3(_vertices[0]);
                GL.Vertex3(_vertices[1]);
                GL.Vertex3(_vertices[2]);
                GL.Vertex3(_vertices[3]);
                GL.Vertex3(_vertices[4]);
                GL.Vertex3(_vertices[5]);
                GL.Vertex3(_vertices[6]);
                GL.Vertex3(_vertices[7]);
                GL.Vertex3(_vertices[8]);
                GL.Vertex3(_vertices[9]);
                GL.Vertex3(_vertices[10]);
                GL.Vertex3(_vertices[11]);
                GL.Vertex3(_vertices[12]);
                GL.Vertex3(_vertices[13]);
                GL.Vertex3(_vertices[14]);
                GL.Vertex3(_vertices[15]);
                GL.Vertex3(_vertices[0]);
                GL.End();
                // top
                GL.Begin(PrimitiveType.TriangleFan);
                GL.Vertex3(end);
                GL.Vertex3(_vertices[31]);
                GL.Vertex3(_vertices[30]);
                GL.Vertex3(_vertices[29]);
                GL.Vertex3(_vertices[28]);
                GL.Vertex3(_vertices[27]);
                GL.Vertex3(_vertices[26]);
                GL.Vertex3(_vertices[25]);
                GL.Vertex3(_vertices[24]);
                GL.Vertex3(_vertices[23]);
                GL.Vertex3(_vertices[22]);
                GL.Vertex3(_vertices[21]);
                GL.Vertex3(_vertices[20]);
                GL.Vertex3(_vertices[19]);
                GL.Vertex3(_vertices[18]);
                GL.Vertex3(_vertices[17]);
                GL.Vertex3(_vertices[16]);
                GL.Vertex3(_vertices[31]);
                GL.End();
                // sides
                GL.Begin(PrimitiveType.TriangleStrip);
                GL.Vertex3(_vertices[0]);
                GL.Vertex3(_vertices[16]);
                GL.Vertex3(_vertices[1]);
                GL.Vertex3(_vertices[17]);
                GL.Vertex3(_vertices[2]);
                GL.Vertex3(_vertices[18]);
                GL.Vertex3(_vertices[3]);
                GL.Vertex3(_vertices[19]);
                GL.Vertex3(_vertices[4]);
                GL.Vertex3(_vertices[20]);
                GL.Vertex3(_vertices[5]);
                GL.Vertex3(_vertices[21]);
                GL.Vertex3(_vertices[6]);
                GL.Vertex3(_vertices[22]);
                GL.Vertex3(_vertices[7]);
                GL.Vertex3(_vertices[23]);
                GL.Vertex3(_vertices[8]);
                GL.Vertex3(_vertices[24]);
                GL.Vertex3(_vertices[9]);
                GL.Vertex3(_vertices[25]);
                GL.Vertex3(_vertices[10]);
                GL.Vertex3(_vertices[26]);
                GL.Vertex3(_vertices[11]);
                GL.Vertex3(_vertices[27]);
                GL.Vertex3(_vertices[12]);
                GL.Vertex3(_vertices[28]);
                GL.Vertex3(_vertices[13]);
                GL.Vertex3(_vertices[29]);
                GL.Vertex3(_vertices[14]);
                GL.Vertex3(_vertices[30]);
                GL.Vertex3(_vertices[15]);
                GL.Vertex3(_vertices[31]);
                GL.Vertex3(_vertices[0]);
                GL.Vertex3(_vertices[16]);
                GL.End();
            }
            else if (volume.Type == VolumeType.Sphere)
            {
                _vertices.Clear();
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
                        _vertices.Add(new Vector3(x, z, y));
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
                            GL.Vertex3(_vertices[k1 + 1] + volume.SpherePosition);
                            GL.Vertex3(_vertices[k2] + volume.SpherePosition);
                            GL.Vertex3(_vertices[k1] + volume.SpherePosition);
                        }
                        if (i != (stackCount - 1))
                        {
                            GL.Vertex3(_vertices[k2 + 1] + volume.SpherePosition);
                            GL.Vertex3(_vertices[k2] + volume.SpherePosition);
                            GL.Vertex3(_vertices[k1 + 1] + volume.SpherePosition);
                        }
                    }
                }
                GL.End();
            }
        }
    }
}
