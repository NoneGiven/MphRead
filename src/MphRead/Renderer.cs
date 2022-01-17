using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MphRead.Effects;
using MphRead.Entities;
using MphRead.Export;
using MphRead.Formats;
using MphRead.Formats.Collision;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MphRead
{
    public enum VolumeDisplay
    {
        None,
        LightColor1,
        LightColor2,
        TriggerParent,
        TriggerChild,
        AreaInside,
        AreaExit,
        MorphCamera,
        JumpPad,
        Teleporter,
        EnemyHurt,
        Object,
        FlagBase,
        DefenseNode,
        KillPlane,
        PlayerLimit,
        CameraLimit,
        Portal
    }

    public enum CollisionType
    {
        Any,
        Player,
        Beam,
        Both
    }

    public enum CollisionColor
    {
        None,
        Entity,
        Terrain,
        Type
    }

    public enum CameraMode
    {
        Pivot,
        Roam,
        Player
    }

    public class FrustumInfo
    {
        public Vector3 Position;
        public Vector3 FarTopLeft;
        public Vector3 FarTopRight;
        public Vector3 FarBottomLeft;
        public Vector3 FarBottomRight;
        public Vector4 NearPlane;
        public Vector4 FarPlane;
        public Vector4 TopPlane;
        public Vector4 BottomPlane;
        public Vector4 LeftPlane;
        public Vector4 RightPlane;
    }

    public partial class Scene
    {
        public Vector2i Size { get; set; }
        private Matrix4 _viewMatrix = Matrix4.Identity;
        private Matrix4 _viewInvRotMatrix = Matrix4.Identity;
        private Matrix4 _viewInvRotYMatrix = Matrix4.Identity;
        private Matrix4 _perspectiveMatrix = Matrix4.Identity;
        public Matrix4 PerspectiveMatrix => _perspectiveMatrix;

        private CameraMode _cameraMode = CameraMode.Pivot;
        public CameraMode CameraMode => _cameraMode;
        private float _pivotAngleY = 0.0f;
        private float _pivotAngleX = 0.0f;
        private float _pivotDistance = 5.0f;
        private Vector3 _cameraPosition = Vector3.Zero;
        private Vector3 _cameraFacing = -Vector3.UnitZ;
        private Vector3 _cameraUp = Vector3.UnitY;
        private Vector3 _cameraRight = Vector3.UnitX;
        private float _cameraFov = MathHelper.DegreesToRadians(78);
        private bool _leftMouse = false;
        private int _activeCutscene = -1;
        private Vector3 _priorCameraPos = Vector3.Zero;
        private Vector3 _priorCameraFacing = -Vector3.UnitZ;
        private float _priorCameraFov = MathHelper.DegreesToRadians(78);
        public FrustumInfo FrustumInfo { get; } = new FrustumInfo();

        private bool _showTextures = true;
        private bool _showColors = true;
        private bool _wireframe = false;
        // 0 - lines + fill, 1 - lines only, 2 - fill only
        private int _volumeEdges = 0;
        private bool _faceCulling = true;
        private bool _textureFiltering = false;
        private bool _lighting = false;
        private bool _scanVisor = false;
        private int _showInvisible = 0;
        private bool _showNodeData = false;
        private VolumeDisplay _showVolumes = VolumeDisplay.None;
        private bool _showCollision = false;
        private bool _showAllNodes = false;
        private bool _transformRoomNodes = false;
        private bool _outputCameraPos = false;

        private readonly List<EntityBase> _entities = new List<EntityBase>();
        private readonly List<EntityBase> _destroyedEntities = new List<EntityBase>();
        private readonly Dictionary<int, EntityBase> _entityMap = new Dictionary<int, EntityBase>();
        // map each model's texture ID/palette ID combinations to the bound OpenGL texture ID and "onlyOpaque" boolean
        private int _textureCount = 0;
        private readonly Dictionary<int, TextureMap> _texPalMap = new Dictionary<int, TextureMap>();

        private int _shaderProgramId = 0;
        private readonly ShaderLocations _shaderLocations = new ShaderLocations();

        private Vector3 _light1Vector = Vector3.Zero;
        private Vector3 _light1Color = Vector3.Zero;
        private Vector3 _light2Vector = Vector3.Zero;
        private Vector3 _light2Color = Vector3.Zero;
        private bool _hasFog = false;
        private bool _showFog = true;
        private Vector4 _fogColor = Vector4.Zero;
        private int _fogOffset = 0;
        private int _fogSlope = 0;
        private Color4 _clearColor = new Color4(0f, 0f, 0f, 1f);
        private float _farClip = 0;
        private bool _useClip = false;
        private float _killHeight = 0f;

        private float _frameTime = 0;
        private float _elapsedTime = 0;
        private ulong _frameCount = 0;
        private bool _frameAdvanceOn = false;
        public bool FrameAdvance => _frameAdvanceOn;
        private bool _advanceOneFrame = false;
        private bool _recording = false;
        private int _framesRecorded = 0;
        private bool ProcessFrame => _frameCount == 0 || !_frameAdvanceOn || _advanceOneFrame;
        private bool _roomLoaded = false;
        private RoomEntity? _room = null;
        private int _roomId = -1;
        public GameMode GameMode { get; set; } = GameMode.SinglePlayer;
        public int PlayerCount { get; private set; } = 1;
        public bool Multiplayer => GameMode != GameMode.SinglePlayer;
        public int RoomId => _roomId;
        public int AreaId { get; set; }

        public Matrix4 ViewMatrix => _viewMatrix;
        public Matrix4 ViewInvRotMatrix => _viewInvRotMatrix;
        public Matrix4 ViewInvRotYMatrix => _viewInvRotYMatrix;
        public Vector3 CameraPosition => _cameraPosition;
        public bool ShowNodeData => _showNodeData;
        public bool ShowInvisibleEntities => _showInvisible != 0;
        public bool ShowAllEntities => _showInvisible == 2;
        public bool TransformRoomNodes => _transformRoomNodes;
        public bool ShowAllNodes => _showAllNodes;
        public float FrameTime => _frameTime;
        public ulong FrameCount => _frameCount;
        public VolumeDisplay ShowVolumes => _showVolumes;
        public bool ShowForceFields => _showVolumes != VolumeDisplay.Portal;
        public float KillHeight => _killHeight;
        public bool ScanVisor => _scanVisor;
        public Vector3 Light1Vector => _light1Vector;
        public Vector3 Light1Color => _light1Color;
        public Vector3 Light2Vector => _light2Vector;
        public Vector3 Light2Color => _light2Color;
        public IReadOnlyList<EntityBase> Entities => _entities;
        public RoomEntity? Room => _room;
        public int ActiveCutscene => _activeCutscene;
        // todo: disallow if camera roll is not zero?
        public bool AllowCameraMovement => _activeCutscene == -1 || (_frameAdvanceOn && !_advanceOneFrame);

        public const int DisplaySphereStacks = 16;
        public const int DisplaySphereSectors = 24;

        private readonly KeyboardState _keyboardState;
        private readonly MouseState _mouseState;
        private readonly Action<string> _setTitle;

        public Scene(Vector2i size, KeyboardState keyboardState, MouseState mouseState, Action<string> setTitle)
        {
            Size = size;
            _keyboardState = keyboardState;
            _mouseState = mouseState;
            _setTitle = setTitle;
            PlayerEntity.Construct(this);
        }

        // called before load
        public void AddRoom(string name, GameMode mode = GameMode.None, int playerCount = 0,
            BossFlags bossFlags = BossFlags.None, int nodeLayerMask = 0, int entityLayerId = -1)
        {
            if (_roomLoaded)
            {
                throw new ProgramException("Cannot load more than one room in a scene.");
            }
            _roomLoaded = true;
            GameMode = mode;
            (RoomEntity room, RoomMetadata meta, CollisionInstance collision, IReadOnlyList<EntityBase> entities)
                = SceneSetup.LoadRoom(name, this, playerCount, bossFlags, nodeLayerMask, entityLayerId);
            if (GameMode == GameMode.None)
            {
                GameMode = meta.Multiplayer ? GameMode.Battle : GameMode.SinglePlayer;
            }
            _entities.Add(room);
            InitEntity(room);
            _room = room;
            if (meta.InGameName != null)
            {
                _setTitle.Invoke(meta.InGameName);
            }
            foreach (EntityBase entity in entities)
            {
                _entities.Add(entity);
                Debug.Assert(entity.Id != -1);
                _entityMap.Add(entity.Id, entity);
                InitEntity(entity);
            }
            SceneSetup.LoadItemResources(this);
            SceneSetup.LoadEnemyResources(this);
            _light1Vector = meta.Light1Vector;
            _light1Color = new Vector3(
                meta.Light1Color.Red / 31.0f,
                meta.Light1Color.Green / 31.0f,
                meta.Light1Color.Blue / 31.0f
            );
            _light2Vector = meta.Light2Vector;
            _light2Color = new Vector3(
                meta.Light2Color.Red / 31.0f,
                meta.Light2Color.Green / 31.0f,
                meta.Light2Color.Blue / 31.0f
            );
            _lighting = true;
            _hasFog = meta.FogEnabled;
            _fogColor = new Vector4(
                meta.FogColor.Red / 31f,
                meta.FogColor.Green / 31f,
                meta.FogColor.Blue / 31f,
                1.0f
            );
            _fogOffset = meta.FogOffset;
            _fogSlope = meta.FogSlope;
            if (meta.ClearFog && meta.FirstHunt)
            {
                _clearColor = new Color4(_fogColor.X, _fogColor.Y, _fogColor.Z, _fogColor.W);
            }
            _killHeight = meta.KillHeight;
            _farClip = meta.FarClip;
            _cameraMode = CameraMode.Roam;
            _roomId = room.RoomId;
        }

        // called before load
        public EntityBase AddModel(string name, int recolor = 0, bool firstHunt = false, MetaDir dir = MetaDir.Models, Vector3? pos = null)
        {
            ModelInstance model = Read.GetModelInstance(name, firstHunt, dir);
            var entity = new ModelEntity(model, this, recolor);
            _entities.Add(entity);
            if (entity.Id != -1)
            {
                _entityMap.Add(entity.Id, entity);
            }
            InitEntity(entity);
            if (pos.HasValue)
            {
                entity.Position = pos.Value;
            }
            return entity;
        }

        // called after load -- entity needs init
        public void AddEntity(EntityBase entity)
        {
            _entities.Add(entity);
            if (entity.Id != -1)
            {
                _entityMap.Add(entity.Id, entity);
            }
            // important to call in this order because the entity may add models (at least in development)
            entity.Initialize();
            InitEntity(entity);
        }

        public void AddPlayer(Hunter hunter, int recolor = 0, Vector3? position = null)
        {
            if (_roomLoaded)
            {
                // todo?: add this functionality back
            }
            else
            {
                var player = PlayerEntity.Create(hunter, recolor);
                if (player != null)
                {
                    player.ForcedSpawnPos = position;
                    // todo: revisit flags
                    player.LoadFlags |= LoadFlags.SlotActive;
                    player.LoadFlags |= LoadFlags.Active;
                    PlayerEntity.PlayerCount++;
                }
            }
        }

        public bool TryGetEntity(int id, [NotNullWhen(true)] out EntityBase? entity)
        {
            return _entityMap.TryGetValue(id, out entity);
        }

        public void RemoveEntity(EntityBase entity)
        {
            _entityMap.Remove(entity.Id);
            _entities.Remove(entity);
        }

        public void OnLoad()
        {
            GL.ClearColor(_clearColor);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Texture2D);
            GL.DepthFunc(DepthFunction.Lequal);
            InitShaders();
            AllocateEffects();
            CollisionDetection.Init();
            for (int i = 0; i < _renderItemAlloc; i++)
            {
                _freeRenderItems.Enqueue(new RenderItem());
            }
            // entities added during initialization of other entities will already be initialized
            int count = _entities.Count;
            for (int i = 0; i < count; i++)
            {
                _entities[i].Initialize();
            }
            // todo: probably revisit this
            foreach (PlayerEntity player in PlayerEntity.Players)
            {
                if (player.LoadFlags.TestFlag(LoadFlags.SlotActive))
                {
                    _entities.Add(player);
                    player.Initialize();
                    InitEntity(player);
                    InitEntity(player.Halfturret);
                }
            }
            OutputStart();
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
            _shaderLocations.ViewInvMatrix = GL.GetUniformLocation(_shaderProgramId, "view_inv_mtx");
            _shaderLocations.ProjectionMatrix = GL.GetUniformLocation(_shaderProgramId, "proj_mtx");
            _shaderLocations.TextureMatrix = GL.GetUniformLocation(_shaderProgramId, "tex_mtx");
            _shaderLocations.TexgenMode = GL.GetUniformLocation(_shaderProgramId, "texgen_mode");
            _shaderLocations.MatrixStack = GL.GetUniformLocation(_shaderProgramId, "mtx_stack");
            _shaderLocations.ToonTable = GL.GetUniformLocation(_shaderProgramId, "toon_table");
            _shaderLocations.FadeColor = GL.GetUniformLocation(_shaderProgramId, "fade_color");

            GL.UseProgram(_shaderProgramId);

            var floats = new List<float>(Metadata.ToonTable.Count * 3);
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

        public void InitEntity(EntityBase entity)
        {
            foreach (ModelInstance inst in entity.GetModels())
            {
                InitTextures(inst.Model);
                GenerateLists(inst.Model, isRoom: entity.Type == EntityType.Room);
            }
        }

        private void GenerateLists(Model model, bool isRoom)
        {
            var tempListIds = new Dictionary<int, int>();
            foreach (Mesh mesh in model.Meshes)
            {
                if (mesh.ListId != 0)
                {
                    continue;
                }
                if (!tempListIds.TryGetValue(mesh.DlistId, out int listId))
                {
                    int textureWidth = 0;
                    int textureHeight = 0;
                    Material material = model.Materials[mesh.MaterialId];
                    if (material.TextureId != -1)
                    {
                        Texture texture = model.Recolors[0].Textures[material.TextureId];
                        textureWidth = texture.Width;
                        textureHeight = texture.Height;
                    }
                    listId = GL.GenLists(1);
                    GL.NewList(listId, ListMode.Compile);
                    bool texgen = material.TexgenMode == TexgenMode.Normal;
                    DoDlist(model, mesh, textureWidth, textureHeight, texgen, isRoom);
                    GL.EndList();
                }
                mesh.ListId = listId;
            }
        }

        private void DoDlist(Model model, Mesh mesh, int textureWidth, int textureHeight, bool texgen, bool isRoom)
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
                    if (!isRoom)
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

        public void LoadModel(string name, bool firstHunt = false)
        {
            LoadModel(Read.GetModelInstance(name, firstHunt).Model);
        }

        public void LoadModel(Model model)
        {
            InitTextures(model);
            GenerateLists(model, isRoom: false);
        }

        private void InitTextures(Model model)
        {
            if (_texPalMap.ContainsKey(model.Id))
            {
                return;
            }
            var combos = new HashSet<(int, int, int)>();
            foreach (Material material in model.Materials)
            {
                if (material.TextureId == -1)
                {
                    continue;
                }
                if (material.RenderMode == RenderMode.Unknown3 || material.RenderMode == RenderMode.Unknown4)
                {
                    material.RenderMode = RenderMode.Normal;
                }
                for (int i = 0; i < model.Recolors.Count; i++)
                {
                    combos.Add((material.TextureId, material.PaletteId, i));
                }
            }
            foreach (TextureAnimationGroup group in model.AnimationGroups.Texture)
            {
                foreach (TextureAnimation animation in group.Animations.Values)
                {
                    for (int i = animation.StartIndex; i < animation.StartIndex + animation.Count; i++)
                    {
                        for (int j = 0; j < model.Recolors.Count; j++)
                        {
                            combos.Add((group.TextureIds[i], group.PaletteIds[i], j));
                        }
                    }
                }
            }
            if (combos.Count > 0)
            {
                var map = new TextureMap();
                foreach ((int textureId, int paletteId, int recolorId) in combos)
                {
                    bool onlyOpaque = BindTexture(model, textureId, paletteId, recolorId);
                    map.Add(textureId, paletteId, recolorId, _textureCount, onlyOpaque);
                }
                _texPalMap.Add(model.Id, map);
            }
        }

        public int BindGetTexture(Model model, int textureId, int paletteId, int recolorId)
        {
            if (_texPalMap.TryGetValue(model.Id, out TextureMap? value))
            {
                return value.Get(textureId, paletteId, recolorId).BindingId;
            }
            BindTexture(model, textureId, paletteId, recolorId);
            return _textureCount;
        }

        private bool BindTexture(Model model, int textureId, int paletteId, int recolorId)
        {
            _textureCount++;
            bool onlyOpaque = true;
            var pixels = new List<uint>();
            foreach (ColorRgba pixel in model.GetPixels(textureId, paletteId, recolorId))
            {
                pixels.Add(pixel.ToUint());
                onlyOpaque &= pixel.Alpha == 255;
            }
            Texture texture = model.Recolors[recolorId].Textures[textureId];
            GL.BindTexture(TextureTarget.Texture2D, _textureCount);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, texture.Width, texture.Height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, pixels.ToArray());
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return onlyOpaque;
        }

        public void UpdateMaterials(Model model, int recolorId)
        {
            for (int i = 0; i < model.Materials.Count; i++)
            {
                Material material = model.Materials[i];
                int textureId = material.CurrentTextureId;
                if (textureId == -1)
                {
                    continue;
                }
                int paletteId = material.CurrentPaletteId;
                (int bindingId, bool onlyOpaque) = _texPalMap[model.Id].Get(textureId, paletteId, recolorId);
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

        public void OnUpdateFrame(double frameTime)
        {
            if (_recording || Debugger.IsAttached)
            {
                _frameTime = 1 / 60f; // todo: FPS stuff
            }
            else
            {
                _frameTime = (float)frameTime;
            }
            LoadAndUnload();
            if (ProcessFrame)
            {
                PlayerEntity.ProcessInput(_keyboardState, _mouseState);
            }
            OnKeyHeld();
            _singleParticleCount = 0;
            _decalItems.Clear();
            _nonDecalItems.Clear();
            _translucentItems.Clear();
            if (ProcessFrame)
            {
                UpdateScene();
                ProcessMessageQueue();
                _elapsedTime += 1 / 60f; // todo: FPS stuff
                _frameCount++;
            }
            if (ProcessFrame || CameraMode != CameraMode.Player)
            {
                TransformCamera();
                UpdateCameraPosition();
            }
            UpdateProjection();
            GetDrawItems();
        }

        private void UpdateProjection()
        {
            // todo: update this only when the viewport or camera values change
            GL.GetFloat(GetPName.Viewport, out Vector4 viewport);
            float aspect = (viewport.Z - viewport.X) / (viewport.W - viewport.Y);
            float near = 0.0625f;
            _perspectiveMatrix = Matrix4.CreatePerspectiveFieldOfView(_cameraFov, aspect, near, _useClip ? _farClip : 10000f);
            GL.UniformMatrix4(_shaderLocations.ProjectionMatrix, transpose: false, ref _perspectiveMatrix);
            CameraInfo camInfo = PlayerEntity.Main.CameraInfo;
            float far = _farClip;
            Vector3 pos = camInfo.Position;
            Vector3 target = camInfo.Target;

            Vector3 up = camInfo.TrueUp;
            Vector3 facing = (pos - target).Normalized();
            Vector3 right = Vector3.Cross(up, facing).Normalized();
            up = Vector3.Cross(facing, right);

            float tan = MathF.Tan(_cameraFov / 2);
            float nearHeight = tan * near;
            float nearWidth = nearHeight * aspect;
            float farHeight = tan * far;
            float farWidth = farHeight * aspect;
            Vector3 nearPos = pos - facing * near;
            Vector3 farPos = pos - facing * far;
            Vector3 nearTopLeft = nearPos + up * nearHeight - right * nearWidth;
            Vector3 nearTopRight = nearPos + up * nearHeight + right * nearWidth;
            Vector3 nearBottomLeft = nearPos - up * nearHeight - right * nearWidth;
            Vector3 nearBottomRight = nearPos - up * nearHeight + right * nearWidth;
            Vector3 farTopLeft = farPos + up * farHeight - right * farWidth;
            Vector3 farTopRight = farPos + up * farHeight + right * farWidth;
            Vector3 farBottomLeft = farPos - up * farHeight - right * farWidth;
            Vector3 farBottomRight = farPos - up * farHeight + right * farWidth;
            FrustumInfo.NearPlane = PlaneFromPoints(nearTopLeft, nearTopRight, nearBottomRight);
            FrustumInfo.FarPlane = PlaneFromPoints(farTopRight, farTopLeft, farBottomLeft);
            FrustumInfo.TopPlane = PlaneFromPoints(nearTopRight, nearTopLeft, farTopLeft);
            FrustumInfo.BottomPlane = PlaneFromPoints(nearBottomLeft, nearBottomRight, farBottomRight);
            FrustumInfo.LeftPlane = PlaneFromPoints(nearTopLeft, nearBottomLeft, farBottomLeft);
            FrustumInfo.RightPlane = PlaneFromPoints(nearBottomRight, nearTopRight, farBottomRight);
            FrustumInfo.Position = pos;
            FrustumInfo.FarTopLeft = farTopLeft;
            FrustumInfo.FarTopRight = farTopRight;
            FrustumInfo.FarBottomLeft = farBottomLeft;
            FrustumInfo.FarBottomRight = farBottomRight;
        }

        public Vector4 PlaneFromPoints(Vector3 one, Vector3 two, Vector3 three)
        {
            Vector3 normal = Vector3.Cross(two - one, three - one).Normalized();
            return new Vector4(normal.X, normal.Y, normal.Z, -Vector3.Dot(normal, one));
        }

        public void AfterRenderFrame()
        {
            if (_recording)
            {
                Images.Record(Size.X, Size.Y, $"frame{_framesRecorded:0000}");
                _framesRecorded++;
            }
            _advanceOneFrame = false;
        }

        public void OnRenderFrame()
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
            GL.ClearStencil(0);

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
            for (int i = 0; i < _nonDecalItems.Count; i++)
            {
                RenderItem item = _nonDecalItems[i];
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
            for (int i = 0; i < _decalItems.Count; i++)
            {
                RenderItem item = _decalItems[i];
                RenderItem(item);
            }
            GL.PolygonOffset(0, 0);
            GL.Disable(EnableCap.PolygonOffsetFill);
            // pass 3: mark transparent faces in stencil
            GL.Enable(EnableCap.AlphaTest);
            GL.AlphaFunc(AlphaFunction.Less, 1.0f);
            GL.ColorMask(false, false, false, false);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
            for (int i = 0; i < _translucentItems.Count; i++)
            {
                RenderItem item = _translucentItems[i];
                GL.StencilFunc(StencilFunction.Greater, item.PolygonId, 0xFF);
                RenderItem(item);
            }
            // pass 4: rebuild depth buffer
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
            GL.StencilFunc(StencilFunction.Always, 0, 0xFF);
            GL.AlphaFunc(AlphaFunction.Equal, 1.0f);
            for (int i = 0; i < _nonDecalItems.Count; i++)
            {
                RenderItem item = _nonDecalItems[i];
                RenderItem(item);
            }
            // pass 5: translucent (behind)
            GL.AlphaFunc(AlphaFunction.Less, 1.0f);
            GL.ColorMask(true, true, true, true);
            GL.DepthMask(false);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
            for (int i = 0; i < _translucentItems.Count; i++)
            {
                RenderItem item = _translucentItems[i];
                GL.StencilFunc(StencilFunction.Notequal, item.PolygonId, 0xFF);
                RenderItem(item);
            }
            // pass 6: translucent (before)
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
            for (int i = 0; i < _translucentItems.Count; i++)
            {
                RenderItem item = _translucentItems[i];
                GL.StencilFunc(StencilFunction.Equal, item.PolygonId, 0xFF);
                RenderItem(item);
            }
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.AlphaTest);
            GL.Disable(EnableCap.StencilTest);
        }

        private void LoadAndUnload()
        {
            if (_loadQueue.Count > 0)
            {
                while (_loadQueue.TryDequeue(out (string Name, int Recolor, bool FirstHunt) item))
                {
                    try
                    {
                        // called after load -- entity needs init
                        EntityBase entity = AddModel(item.Name, item.Recolor, item.FirstHunt);
                        entity.Initialize();
                    }
                    catch (ProgramException) { }
                }
            }
            if (_unloadQueue.Count > 0)
            {
                Selection.Clear();
                while (_unloadQueue.TryDequeue(out EntityBase? entity))
                {
                    UnloadEntity(entity);
                }
            }
        }

        private void UnloadEntity(EntityBase entity)
        {
            if (entity.Type == EntityType.Room)
            {
                return;
            }
            entity.Destroy();
            RemoveEntity(entity);
            foreach (ModelInstance inst in entity.GetModels())
            {
                Model model = inst.Model;
                if (Metadata.PreloadResources.ContainsKey(model.Name))
                {
                    continue;
                }
                if (!_entities.Any(e => e.GetModels().Any(m => m.Model == model)))
                {
                    UnloadModel(model);
                }
            }
        }

        private void UnloadModel(Model model)
        {
            if (_texPalMap.TryGetValue(model.Id, out TextureMap? map))
            {
                foreach (KeyValuePair<int, (int BindingId, bool OnlyOpaque)> kvp in map)
                {
                    GL.DeleteTexture(kvp.Value.BindingId);
                }
                _texPalMap.Remove(model.Id);
            }
            foreach (Mesh mesh in model.Meshes)
            {
                GL.DeleteLists(mesh.ListId, 1);
            }
            Read.RemoveModel(model.Name, model.FirstHunt);
        }

        private void TransformCamera()
        {
            // todo: only update this when the camera values change
            _viewMatrix = Matrix4.Identity;
            _viewInvRotMatrix = Matrix4.Identity;
            _viewInvRotYMatrix = Matrix4.Identity;
            if (_cameraMode == CameraMode.Pivot)
            {
                _viewMatrix.Row3.Xyz = new Vector3(0, 0, _pivotDistance * -1);
                _viewMatrix = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(_pivotAngleX)) * _viewMatrix;
                _viewMatrix = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_pivotAngleY)) * _viewMatrix;
                _viewInvRotMatrix = _viewInvRotYMatrix = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(-1 * _pivotAngleY));
                _viewInvRotMatrix = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-1 * _pivotAngleX)) * _viewInvRotMatrix;
            }
            else if (_cameraMode == CameraMode.Roam || _cameraMode == CameraMode.Player)
            {
                if (_cameraMode == CameraMode.Player)
                {
                    _viewMatrix = PlayerEntity.Main.CameraInfo.ViewMatrix; // sktodo
                    float fov = PlayerEntity.Main.CameraInfo.Fov > 0 ? PlayerEntity.Main.CameraInfo.Fov : 78;
                    _cameraFov = MathHelper.DegreesToRadians(fov);
                }
                else
                {
                    _viewMatrix = Matrix4.LookAt(_cameraPosition, _cameraPosition + _cameraFacing, _cameraUp);
                }
                _viewInvRotMatrix = Matrix4.Transpose(_viewMatrix.ClearTranslation());
                if (_viewInvRotMatrix.Row0.X != 0 || _viewInvRotMatrix.Row0.Z != 0)
                {
                    _viewInvRotYMatrix.Row0.Xyz = new Vector3(_viewInvRotMatrix.Row0.X, 0, _viewInvRotMatrix.Row0.Z).Normalized();
                    _viewInvRotYMatrix.Row2.Xyz = new Vector3(_viewInvRotMatrix.Row2.X, 0, _viewInvRotMatrix.Row2.Z).Normalized();
                }
            }
            GL.UniformMatrix4(_shaderLocations.ViewMatrix, transpose: false, ref _viewMatrix);
        }

        private void UpdateCameraPosition()
        {
            if (_cameraMode == CameraMode.Pivot)
            {
                float angleY = _pivotAngleY + 90;
                if (angleY > 360)
                {
                    angleY -= 360;
                }
                float angleX = _pivotAngleX + 90;
                if (angleX > 360)
                {
                    angleX -= 360;
                }
                float theta = MathHelper.DegreesToRadians(angleY);
                float phi = MathHelper.DegreesToRadians(angleX);
                float x = MathF.Round(_pivotDistance * MathF.Cos(theta), 4);
                float y = MathF.Round(_pivotDistance * MathF.Sin(theta) * MathF.Cos(phi), 4) * -1;
                float z = MathF.Round(_pivotDistance * MathF.Sin(theta) * MathF.Sin(phi), 4);
                _cameraPosition = new Vector3(x, y, z);
            }
            else if (_cameraMode == CameraMode.Player)
            {
                _cameraPosition = PlayerEntity.Main.CameraInfo.Position; // sktodo
            }
        }

        private void ResetCamera()
        {
            if (_cameraMode == CameraMode.Roam)
            {
                _cameraPosition = Vector3.Zero;
                _cameraFacing = -Vector3.UnitZ;
                _cameraUp = Vector3.UnitY;
                _cameraRight = Vector3.UnitX;
            }
            else if (_cameraMode == CameraMode.Pivot)
            {
                _pivotAngleX = 0;
                _pivotAngleY = 0;
                _pivotDistance = 5.0f;
            }
        }

        public void SetCamera(Vector3 position, Vector3 target, float fov = 0, float roll = 0)
        {
            _cameraMode = CameraMode.Roam;
            _cameraPosition = position;
            _cameraFacing = target;
            _cameraUp = Vector3.UnitY;
            if (roll > 0)
            {
                _cameraUp = (Matrix3.CreateRotationZ(roll) * _cameraUp).Normalized();
            }
            _cameraRight = Vector3.Cross(_cameraFacing, _cameraUp);
            _cameraUp = Vector3.Cross(_cameraRight, _cameraFacing);
            if (fov > 0)
            {
                _cameraFov = fov;
            }
        }

        private const float _almostHalfPi = MathF.PI / 2 - 0.000001f;

        private void UpdateCameraRotation(float stepH, float stepV)
        {
            float angleH = MathF.Atan2(_cameraFacing.X, -_cameraFacing.Z) + stepH;
            float angleV = MathF.Asin(_cameraFacing.Y) + stepV;
            angleV = Math.Clamp(angleV, -_almostHalfPi, _almostHalfPi);
            _cameraFacing = new Vector3(
                MathF.Cos(angleV) * MathF.Sin(angleH),
                MathF.Sin(angleV),
                -(MathF.Cos(angleV) * MathF.Cos(angleH))
            ).Normalized();
            _cameraRight = Vector3.Cross(_cameraFacing, Vector3.UnitY);
            _cameraUp = Vector3.Cross(_cameraRight, _cameraFacing);
        }

        public void StartCutscene(int id)
        {
            if (_activeCutscene == -1)
            {
                _activeCutscene = id;
                _priorCameraPos = _cameraPosition;
                _priorCameraFacing = _cameraFacing;
                _priorCameraFov = _cameraFov;
            }
        }

        public void EndCutscene(bool resetFade = false)
        {
            if (_activeCutscene != -1)
            {
                _activeCutscene = -1;
                _cameraPosition = _priorCameraPos;
                _cameraFacing = _priorCameraFacing;
                _cameraRight = Vector3.Cross(_cameraFacing, Vector3.UnitY);
                _cameraUp = Vector3.Cross(_cameraRight, _cameraFacing);
                _cameraFov = _priorCameraFov;
            }
            if (resetFade)
            {
                SetFade(FadeType.None, length: 0, overwrite: true);
            }
        }

        // todo: effect limits for beam effects
        // in-game: 64 effects, 96 elements, 200 particles
        private static readonly int _effectEntryMax = 100;
        private static readonly int _effectElementMax = 200;
        private static readonly int _effectParticleMax = 3000;
        private static readonly int _singleParticleMax = 1000;
        private static readonly int _beamEffectMax = 100;
        private static readonly int _bombMax = 32;

        private readonly Queue<EffectEntry> _inactiveEffects = new Queue<EffectEntry>(_effectEntryMax);
        private readonly Queue<EffectElementEntry> _inactiveElements = new Queue<EffectElementEntry>(_effectElementMax);
        private readonly List<EffectElementEntry> _activeElements = new List<EffectElementEntry>(_effectElementMax);
        private readonly Queue<EffectParticle> _inactiveParticles = new Queue<EffectParticle>(_effectParticleMax);
        private int _singleParticleCount = 0;
        private readonly List<SingleParticle> _singleParticles = new List<SingleParticle>(_singleParticleMax);
        private readonly Queue<BeamEffectEntity> _inactiveBeamEffects = new Queue<BeamEffectEntity>(_beamEffectMax);
        private readonly List<BeamEffectEntity> _activeBeamEffects = new List<BeamEffectEntity>(_beamEffectMax);
        private readonly Queue<BombEntity> _inactiveBombs = new Queue<BombEntity>(_bombMax);
        private readonly List<BombEntity> _activeBombs = new List<BombEntity>(_bombMax);

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
            for (int i = 0; i < _singleParticleMax; i++)
            {
                _singleParticles.Add(new SingleParticle());
            }
            for (int i = 0; i < _beamEffectMax; i++)
            {
                _inactiveBeamEffects.Enqueue(new BeamEffectEntity(this));
            }
            for (int i = 0; i < _bombMax; i++)
            {
                _inactiveBombs.Enqueue(new BombEntity(this));
            }
        }

        public BeamEffectEntity InitBeamEffect(BeamEffectEntityData data)
        {
            BeamEffectEntity entry = _inactiveBeamEffects.Dequeue();
            entry.Spawn(data);
            return entry;
        }

        public void UnlinkBeamEffect(BeamEffectEntity entry)
        {
            _activeBeamEffects.Remove(entry);
            _inactiveBeamEffects.Enqueue(entry);
        }

        public BombEntity? InitBomb()
        {
            if (_inactiveBombs.Count == 0)
            {
                return null;
            }
            return _inactiveBombs.Dequeue();
        }

        public void UnlinkBomb(BombEntity entry)
        {
            _activeBombs.Remove(entry);
            _inactiveBombs.Enqueue(entry);
        }

        public void AddSingleParticle(SingleType type, Vector3 position, Vector3 color, float alpha, float scale)
        {
            // note: skipping the room size limit check; singles get cleared every frame anyway
            if (_singleParticleCount < _singleParticleMax)
            {
                SingleParticle entry = _singleParticles[_singleParticleCount++];
                entry.ParticleDefinition = Read.GetSingleParticle(type);
                entry.Position = position;
                entry.Color = color;
                entry.Alpha = alpha;
                entry.Scale = scale;
                if (!_texPalMap.ContainsKey(entry.ParticleDefinition.Model.Id))
                {
                    InitTextures(entry.ParticleDefinition.Model);
                }
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
            entry.Elements.Clear();
            _inactiveEffects.Enqueue(entry);
        }

        public void DetachEffectEntry(EffectEntry entry, bool setExpired)
        {
            for (int i = 0; i < entry.Elements.Count; i++)
            {
                EffectElementEntry element = entry.Elements[i];
                if (element.Flags.TestFlag(EffElemFlags.DestroyOnDetach))
                {
                    UnlinkEffectElement(element);
                }
                else
                {
                    element.Flags &= ~EffElemFlags.ElementExtension;
                    element.Flags |= EffElemFlags.KeepAlive; // keep alive until particles expire
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

        private EffectElementEntry InitEffectElement(Effect effect, EffectElement element, EntityCollision? entCol, bool child)
        {
            EffectElementEntry entry = _inactiveElements.Dequeue();
            entry.EffectName = effect.Name;
            entry.ElementName = element.Name;
            entry.BufferTime = element.BufferTime;
            // todo: FPS stuff
            entry.CreationTime = _elapsedTime + (child ? (1 / 60f) : 0);
            entry.DrainTime = element.DrainTime;
            entry.DrawType = element.DrawType;
            entry.Lifespan = element.Lifespan;
            entry.ExpirationTime = entry.CreationTime + entry.Lifespan;
            entry.Flags = element.Flags;
            entry.Flags |= EffElemFlags.DrawEnabled;
            entry.Func39Called = false;
            entry.Funcs = element.Funcs;
            entry.Actions = element.Actions;
            entry.OwnTransform = Matrix4.Identity;
            entry.Transform = Matrix4.Identity;
            entry.ParticleAmount = 0;
            entry.Expired = false;
            entry.ChildEffectId = (int)element.ChildEffectId;
            entry.Acceleration = element.Acceleration;
            entry.ParticleDefinitions.AddRange(element.Particles);
            entry.Parity = (int)(_frameCount % 2);
            entry.EffectEntry = null;
            entry.EntityCollision = entCol;
            entry.RoField1 = 0;
            entry.RoField2 = 0;
            entry.RoField3 = 0;
            entry.RoField4 = 0;
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
            element.EntityCollision = null;
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

        public void LoadEffect(int effectId)
        {
            Effect effect = Read.LoadEffect(effectId);
            foreach (EffectElement element in effect.Elements)
            {
                // the model may already be loaded; meshes with a ListId will be skipped
                Model model = Read.GetModelInstance(element.ModelName).Model;
                InitTextures(model);
                GenerateLists(model, isRoom: false);
            }
        }

        public EffectEntry SpawnEffectGetEntry(int effectId, Vector3 facing, Vector3 up, Vector3 position, EntityCollision? entCol = null)
        {
            Matrix4 transform = EntityBase.GetTransformMatrix(facing, up, position);
            return SpawnEffectGetEntry(effectId, transform, entCol);
        }

        public EffectEntry SpawnEffectGetEntry(int effectId, Matrix4 transform, EntityCollision? entCol = null)
        {
            EffectEntry entry = InitEffectEntry();
            entry.EffectId = effectId;
            SpawnEffect(effectId, transform, child: false, entry, entCol);
            return entry;
        }

        public void SpawnEffect(int effectId, Vector3 facing, Vector3 up, Vector3 position, bool child = false, EntityCollision? entCol = null)
        {
            Matrix4 transform = EntityBase.GetTransformMatrix(facing, up, position);
            SpawnEffect(effectId, transform, child, entry: null, entCol);
        }

        public void SpawnEffect(int effectId, Matrix4 transform, bool child = false, EntityCollision? entCol = null)
        {
            SpawnEffect(effectId, transform, child, entry: null, entCol);
        }

        private void SpawnEffect(int effectId, Matrix4 transform, bool child, EffectEntry? entry, EntityCollision? entCol)
        {
            Effect? effect = Read.GetEffect(effectId);
            if (effect == null)
            {
                Debug.Assert(effectId == 162); // skdebug - unintended Omega Cannon damage effect
                return;
            }
            for (int i = 0; i < effect.Elements.Count; i++)
            {
                EffectElement elementDef = effect.Elements[i];
                EffectElementEntry element = InitEffectElement(effect, elementDef, entCol, child);
                if (entry != null)
                {
                    element.EffectEntry = entry;
                    entry.Elements.Add(element);
                }
                if (element.Flags.TestFlag(EffElemFlags.SpawnUnitVecs))
                {
                    Vector3 vec1 = Vector3.UnitY;
                    Vector3 vec2 = Vector3.UnitX;
                    transform = Matrix.GetTransform4(vec2, vec1, transform.Row3.Xyz);
                }
                element.Transform = element.OwnTransform = transform;
                for (int j = 0; j < elementDef.Particles.Count; j++)
                {
                    Particle particleDef = elementDef.Particles[j];
                    if (j == 0)
                    {
                        if (!_texPalMap.ContainsKey(particleDef.Model.Id))
                        {
                            InitTextures(particleDef.Model);
                        }
                        element.Model = particleDef.Model;
                    }
                    element.Nodes.Add(particleDef.Node);
                    Material material = particleDef.Model.Materials[particleDef.MaterialId];
                    material.TextureBindingId = _texPalMap[particleDef.Model.Id].Get(material.TextureId, material.PaletteId, 0).BindingId;
                    element.TextureBindingIds.Add(material.TextureBindingId);
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
                    if (element.EffectEntry == null && !element.Flags.TestFlag(EffElemFlags.KeepAlive))
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
                    if (element.Flags.TestFlag(EffElemFlags.ElementExtension))
                    {
                        if (_elapsedTime - element.CreationTime > element.BufferTime)
                        {
                            element.CreationTime += element.BufferTime - element.DrainTime;
                            element.ExpirationTime += element.BufferTime - element.DrainTime;
                        }
                    }
                    if (element.EntityCollision != null)
                    {
                        element.Transform = element.OwnTransform * element.EntityCollision.Transform;
                    }
                    else
                    {
                        element.Transform = element.OwnTransform;
                    }
                    var times = new TimeValues(_elapsedTime, _elapsedTime - element.CreationTime, element.Lifespan);
                    if (_frameCount % 2 == (ulong)element.Parity
                        && element.Actions.TryGetValue(FuncAction.IncreaseParticleAmount, out FxFuncInfo? info))
                    {
                        // todo: maybe revisit this frame time hack
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
                        if (!element.Flags.TestFlag(EffElemFlags.UseTransform))
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
                        else
                        {
                            particle.RoField1 = element.RoField1;
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRoField2, out info))
                        {
                            particle.RoField2 = particle.InvokeFloatFunc(info, times);
                        }
                        else
                        {
                            particle.RoField2 = element.RoField2;
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRoField3, out info))
                        {
                            particle.RoField3 = particle.InvokeFloatFunc(info, times);
                        }
                        else
                        {
                            particle.RoField3 = element.RoField3;
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRoField4, out info))
                        {
                            particle.RoField4 = particle.InvokeFloatFunc(info, times);
                        }
                        else
                        {
                            particle.RoField4 = element.RoField4;
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
                        // in-game, if these one-time functions are called here, they are unset so they don't get called again
                        if (element.Actions.TryGetValue(FuncAction.UpdateParticleSpeed, out info) && info.FuncId == 4)
                        {
                            Vector3 temp2 = particle.Speed;
                            particle.InvokeVecFunc(info, times, ref temp2);
                            particle.Speed = temp2;
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRed, out info) && info.FuncId == 42)
                        {
                            particle.Red = particle.InvokeFloatFunc(info, times);
                        }
                        else
                        {
                            particle.Red = 1;
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleGreen, out info) && info.FuncId == 42)
                        {
                            particle.Green = particle.InvokeFloatFunc(info, times);
                        }
                        else
                        {
                            particle.Green = 1;
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleBlue, out info) && info.FuncId == 42)
                        {
                            particle.Blue = particle.InvokeFloatFunc(info, times);
                        }
                        else
                        {
                            particle.Blue = 1;
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleAlpha, out info) && info.FuncId == 42)
                        {
                            particle.Alpha = particle.InvokeFloatFunc(info, times);
                            if (particle.Alpha < 0)
                            {
                                particle.Alpha = 0;
                            }
                        }
                        else
                        {
                            particle.Alpha = 1;
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleScale, out info) && info.FuncId == 42)
                        {
                            particle.Scale = particle.InvokeFloatFunc(info, times);
                        }
                        else
                        {
                            particle.Scale = 0;
                        }
                        if (element.Actions.TryGetValue(FuncAction.SetParticleRotation, out info) && info.FuncId == 42)
                        {
                            particle.Rotation = particle.InvokeFloatFunc(info, times);
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
                        portionTotal += 1f / spawnCount;
                    }
                }
                for (int j = 0; j < element.Particles.Count; j++)
                {
                    EffectParticle particle = element.Particles[j];
                    if (element.Flags.TestFlag(EffElemFlags.ElementExtension) && element.Flags.TestFlag(EffElemFlags.ParticleExtension))
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
                        // todo: frame time scaling for speed/accel
                        if (element.Flags.TestFlag(EffElemFlags.UseAcceleration))
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
                        if (element.Flags.TestFlag(EffElemFlags.CheckCollision))
                        {
                            CollisionResult res = default;
                            if (CollisionDetection.CheckBetweenPoints(prevPos, particle.Position, TestFlags.None, this, ref res))
                            {
                                particle.Position = res.Position;
                                particle.ExpirationTime = _elapsedTime;
                            }
                        }
                    }
                    else
                    {
                        if (element.Flags.TestFlag(EffElemFlags.SpawnChildEffect) && element.ChildEffectId != 0)
                        {
                            Vector3 vec1 = (-particle.Speed).Normalized();
                            Vector3 vec2;
                            if (vec1.Z <= Fixed.ToFloat(-3686) || vec1.Z >= Fixed.ToFloat(3686))
                            {
                                vec2 = Vector3.UnitX;
                            }
                            else
                            {
                                vec2 = Vector3.UnitZ;
                            }
                            vec2 = Vector3.Cross(vec1, vec2).Normalized();
                            Matrix4 transform = Matrix.GetTransform4(vec2, vec1, particle.Position);
                            SpawnEffect(element.ChildEffectId, transform);
                        }
                        element.Particles.Remove(particle);
                        UnlinkEffectParticle(particle);
                        j--;
                    }
                }
            }
        }

        private const int _renderItemAlloc = 200; // todo: revisit this (could allocate based on number of meshes on load)
        private readonly Queue<RenderItem> _freeRenderItems = new Queue<RenderItem>(_renderItemAlloc);
        private readonly Queue<RenderItem> _usedRenderItems = new Queue<RenderItem>(_renderItemAlloc);
        // avoiding overhead by duplicating things in these lists
        private readonly List<RenderItem> _decalItems = new List<RenderItem>();
        private readonly List<RenderItem> _nonDecalItems = new List<RenderItem>();
        private readonly List<RenderItem> _translucentItems = new List<RenderItem>();

        private RenderItem GetRenderItem()
        {
            if (_freeRenderItems.Count > 0)
            {
                return _freeRenderItems.Dequeue();
            }
            return new RenderItem();
        }

        private readonly float[] _scaleFactors = new float[16];

        // for meshes
        public void AddRenderItem(Material material, int polygonId, float alphaScale, Vector3 emission, LightInfo lightInfo, Matrix4 texcoordMatrix,
            Matrix4 transform, int listId, int matrixStackCount, IReadOnlyList<float> matrixStack, Vector4? overrideColor, Vector4? paletteOverride,
            SelectionType selectionType, BillboardMode billboardMode, float scaleFactor = 1, int? bindingOverride = null)
        {
            transform.Row0.X *= scaleFactor;
            transform.Row0.Y *= scaleFactor;
            transform.Row0.Z *= scaleFactor;
            transform.Row1.X *= scaleFactor;
            transform.Row1.Y *= scaleFactor;
            transform.Row1.Z *= scaleFactor;
            transform.Row2.X *= scaleFactor;
            transform.Row2.Y *= scaleFactor;
            transform.Row2.Z *= scaleFactor;
            _scaleFactors[0] = scaleFactor;
            _scaleFactors[1] = scaleFactor;
            _scaleFactors[2] = scaleFactor;
            _scaleFactors[3] = 1;
            _scaleFactors[4] = scaleFactor;
            _scaleFactors[5] = scaleFactor;
            _scaleFactors[6] = scaleFactor;
            _scaleFactors[7] = 1;
            _scaleFactors[8] = scaleFactor;
            _scaleFactors[9] = scaleFactor;
            _scaleFactors[10] = scaleFactor;
            _scaleFactors[11] = 1;
            _scaleFactors[12] = 1;
            _scaleFactors[13] = 1;
            _scaleFactors[14] = 1;
            _scaleFactors[15] = 1;
            RenderItem item = GetRenderItem();
            item.Type = RenderItemType.Mesh;
            item.PolygonId = polygonId;
            item.Alpha = material.CurrentAlpha * alphaScale;
            item.PolygonMode = material.PolygonMode;
            item.RenderMode = material.RenderMode;
            item.CullingMode = material.Culling;
            item.BillboardMode = billboardMode;
            item.Wireframe = material.Wireframe != 0;
            item.Lighting = material.Lighting != 0;
            item.NoLines = false;
            item.Diffuse = material.CurrentDiffuse;
            item.Ambient = material.CurrentAmbient;
            item.Specular = material.CurrentSpecular;
            item.Emission = emission;
            item.LightInfo = lightInfo;
            if (bindingOverride.HasValue)
            {
                // double damage
                item.TexgenMode = TexgenMode.Normal;
                item.XRepeat = RepeatMode.Mirror;
                item.YRepeat = RepeatMode.Mirror;
                item.HasTexture = true;
                item.TextureBindingId = bindingOverride.Value;
            }
            else
            {
                item.TexgenMode = material.TexgenMode;
                item.XRepeat = material.XRepeat;
                item.YRepeat = material.YRepeat;
                item.HasTexture = material.TextureId != -1;
                item.TextureBindingId = material.TextureBindingId;
            }
            item.TexcoordMatrix = texcoordMatrix;
            item.Transform = transform;
            item.ListId = listId;
            Debug.Assert(matrixStack.Count == 16 * matrixStackCount);
            item.MatrixStackCount = matrixStackCount;
            for (int i = 0; i < matrixStack.Count; i++)
            {
                float value = matrixStack[i];
                item.MatrixStack[i] = value * _scaleFactors[i - (i / 16) * 16];
            }
            item.OverrideColor = overrideColor;
            item.PaletteOverride = paletteOverride;
            item.Points = Array.Empty<Vector3>();
            item.ScaleS = 1;
            item.ScaleT = 1;
            if (selectionType != SelectionType.None)
            {
                overrideColor = Selection.GetSelectionColor(selectionType);
                if (overrideColor != null)
                {
                    item.OverrideColor = overrideColor;
                    item.PaletteOverride = null;
                }
            }
            AddRenderItem(item);
        }

        // for volumes/planes
        public void AddRenderItem(CullingMode cullingMode, int polygonId, Vector4 overrideColor, RenderItemType type,
            Vector3[] vertices, int vertexCount = 0, bool noLines = false)
        {
            RenderItem item = GetRenderItem();
            item.Type = type;
            item.PolygonId = polygonId;
            item.Alpha = 1;
            item.PolygonMode = PolygonMode.Modulate;
            item.RenderMode = RenderMode.Translucent;
            item.CullingMode = cullingMode;
            item.BillboardMode = BillboardMode.None;
            item.Wireframe = false;
            item.Lighting = false;
            item.NoLines = noLines;
            item.Diffuse = Vector3.Zero;
            item.Ambient = Vector3.Zero;
            item.Specular = Vector3.Zero;
            item.Emission = Vector3.Zero;
            item.LightInfo = LightInfo.Zero;
            item.TexgenMode = TexgenMode.None;
            item.XRepeat = RepeatMode.Clamp;
            item.YRepeat = RepeatMode.Clamp;
            item.HasTexture = false;
            item.TextureBindingId = 0;
            item.TexcoordMatrix = Matrix4.Identity;
            item.Transform = Matrix4.Identity;
            item.ListId = 0;
            item.MatrixStackCount = 0;
            item.OverrideColor = overrideColor;
            item.PaletteOverride = null;
            item.Points = vertices;
            item.ScaleS = 1;
            item.ScaleT = 1;
            Debug.Assert(type != RenderItemType.Ngon || vertexCount >= 3);
            item.ItemCount = vertexCount;
            AddRenderItem(item);
        }

        // for effects/trails
        public void AddRenderItem(RenderItemType type, float alpha, int polygonId, Vector3 color,
            RepeatMode xRepeat, RepeatMode yRepeat, float scaleS, float scaleT, Matrix4 transform, Vector3[] uvsAndVerts,
            int bindingId, BillboardMode billboardMode = BillboardMode.None, int trailCount = 8)
        {
            RenderItem item = GetRenderItem();
            item.Type = type;
            item.PolygonId = polygonId;
            item.Alpha = alpha;
            item.PolygonMode = PolygonMode.Modulate;
            item.RenderMode = RenderMode.Translucent;
            item.CullingMode = CullingMode.Neither;
            item.BillboardMode = billboardMode;
            item.Wireframe = false;
            item.Lighting = false;
            item.NoLines = false;
            item.Diffuse = color;
            item.Ambient = Vector3.Zero;
            item.Specular = Vector3.Zero;
            item.Emission = Vector3.Zero;
            item.LightInfo = LightInfo.Zero;
            item.TexgenMode = TexgenMode.None;
            item.XRepeat = xRepeat;
            item.YRepeat = yRepeat;
            item.HasTexture = true;
            item.TextureBindingId = bindingId;
            item.TexcoordMatrix = Matrix4.Identity;
            item.Transform = transform;
            item.ListId = 0;
            item.MatrixStackCount = 0;
            item.OverrideColor = null;
            item.PaletteOverride = null;
            item.Points = uvsAndVerts;
            item.ScaleS = scaleS;
            item.ScaleT = scaleT;
            item.ItemCount = trailCount;
            AddRenderItem(item);
        }

        // for Morph Ball trails
        public void AddRenderItem(RenderItemType type, int polygonId, Vector3 color, RepeatMode xRepeat, RepeatMode yRepeat, float scaleS,
            float scaleT, int matrixStackCount, IReadOnlyList<float> matrixStack, Vector3[] uvsAndVerts, int segmentCount, int bindingId)
        {
            RenderItem item = GetRenderItem();
            item.Type = type;
            item.PolygonId = polygonId;
            item.Alpha = 1;
            item.PolygonMode = PolygonMode.Modulate;
            item.RenderMode = RenderMode.Translucent;
            item.CullingMode = CullingMode.Neither;
            item.BillboardMode = BillboardMode.None;
            item.Wireframe = false;
            item.Lighting = false;
            item.NoLines = false;
            item.Diffuse = color;
            item.Ambient = Vector3.Zero;
            item.Specular = Vector3.Zero;
            item.Emission = Vector3.Zero;
            item.LightInfo = LightInfo.Zero;
            item.TexgenMode = TexgenMode.None;
            item.XRepeat = xRepeat;
            item.YRepeat = yRepeat;
            item.HasTexture = true;
            item.TextureBindingId = bindingId;
            item.TexcoordMatrix = Matrix4.Identity;
            item.Transform = Matrix4.Identity;
            item.ListId = 0;
            Debug.Assert(matrixStack.Count >= 16 * matrixStackCount);
            item.MatrixStackCount = matrixStackCount;
            for (int i = 0; i < 16 * matrixStackCount; i++)
            {
                item.MatrixStack[i] = matrixStack[i];
            }
            item.OverrideColor = null;
            item.PaletteOverride = null;
            item.Points = uvsAndVerts;
            item.ScaleS = scaleS;
            item.ScaleT = scaleT;
            item.ItemCount = segmentCount;
            AddRenderItem(item);
        }

        private void AddRenderItem(RenderItem item)
        {
            if (item.RenderMode == RenderMode.Decal)
            {
                _decalItems.Add(item);
            }
            else
            {
                _nonDecalItems.Add(item);
            }
            if (item.RenderMode == RenderMode.Translucent || item.Alpha < 1)
            {
                _translucentItems.Add(item);
            }
            _usedRenderItems.Enqueue(item);
        }

        private int _nextPolygonId = 1;

        public int GetNextPolygonId()
        {
            return _nextPolygonId++;
        }

        private void UpdateScene()
        {
            while (_usedRenderItems.Count > 0)
            {
                RenderItem item = _usedRenderItems.Dequeue();
                if (item.Type != RenderItemType.Mesh)
                {
                    ArrayPool<Vector3>.Shared.Return(item.Points);
                }
                _freeRenderItems.Enqueue(item);
            }
            _nextPolygonId = 1;
            _destroyedEntities.Clear();

            for (int i = 0; i < _entities.Count; i++)
            {
                EntityBase entity = _entities[i];
                if (!entity.Process())
                {
                    SendMessage(Message.Destroyed, entity, null, 0, 0, delay: 1);
                    // todo: need to handle destroying vs. unloading etc.
                    entity.Destroy();
                    _destroyedEntities.Add(entity);
                }
            }

            ProcessEffects();
            for (int i = 0; i < _singleParticleCount; i++)
            {
                SingleParticle single = _singleParticles[i];
                single.Process();
            }
        }

        private void GetDrawItems()
        {
            if (_room != null)
            {
                _room.GetDrawInfo();
                _room.GetDisplayVolumes();
            }
            for (int i = 0; i < _entities.Count; i++)
            {
                EntityBase entity = _entities[i];
                if (entity.Type != EntityType.Player || _destroyedEntities.Contains(entity))
                {
                    continue;
                }
                var player = (PlayerEntity)entity;
                if (player.LoadFlags.TestFlag(LoadFlags.Active))
                {
                    player.Draw();
                    // skdebug
                    entity.GetDisplayVolumes();
                }
            }
            for (int i = 0; i < _entities.Count; i++)
            {
                EntityBase entity = _entities[i];
                if (entity.Type == EntityType.Player || entity.Type == EntityType.Room || _destroyedEntities.Contains(entity))
                {
                    continue;
                }
                if (entity.ShouldDraw)
                {
                    entity.GetDrawInfo();
                }
                if (_showVolumes != VolumeDisplay.None)
                {
                    entity.GetDisplayVolumes();
                }
            }

            for (int i = 0; i < _destroyedEntities.Count; i++)
            {
                EntityBase entity = _destroyedEntities[i];
                RemoveEntity(entity);
            }

            for (int i = 0; i < _activeElements.Count; i++)
            {
                EffectElementEntry element = _activeElements[i];
                if (element.Flags.TestFlag(EffElemFlags.DrawEnabled))
                {
                    for (int j = 0; j < element.Particles.Count; j++)
                    {
                        EffectParticle particle = element.Particles[j];
                        Matrix4 matrix = _viewMatrix;
                        if (particle.Owner.Flags.TestFlag(EffElemFlags.UseTransform) && !particle.Owner.Flags.TestFlag(EffElemFlags.UseMesh))
                        {
                            matrix = particle.Owner.Transform * matrix;
                        }
                        particle.InvokeSetVecsFunc(matrix);
                        particle.InvokeDrawFunc(1);
                        if (particle.ShouldDraw)
                        {
                            particle.AddRenderItem(this);
                        }
                    }
                }
            }
            for (int i = 0; i < _singleParticleCount; i++)
            {
                SingleParticle single = _singleParticles[i];
                if (single.ShouldDraw)
                {
                    single.AddRenderItem(this);
                }
            }
        }

        private void UpdateUniforms()
        {
            UseRoomLights();
            GL.Uniform1(_shaderLocations.UseFog, _hasFog && _showFog ? 1 : 0);
            GL.Uniform1(_shaderLocations.ShowColors, _showColors ? 1 : 0);
            UpdateFade();
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

        private FadeType _fadeType = FadeType.None;
        private float _fadeColor = 0;
        private float _fadeTarget = 0;
        private float _fadeStart = 0;
        private float _fadeLength = 0;
        private float _currentFade = 0;

        public void SetFade(FadeType type, float length, bool overwrite)
        {
            if (!overwrite && _fadeType != FadeType.None)
            {
                return;
            }
            _fadeType = type;
            if (type == FadeType.None)
            {
                _fadeType = type;
                _fadeColor = 0;
                _fadeTarget = 0;
                _currentFade = 0;
                _fadeStart = 0;
                _fadeLength = 0;
            }
            else if (type == FadeType.FadeInWhite)
            {
                _fadeColor = 1;
                _fadeTarget = 0;
            }
            else if (type == FadeType.FadeInBlack)
            {
                _fadeColor = -1;
                _fadeTarget = 0;
            }
            else if (type == FadeType.FadeOutWhite || type == FadeType.FadeOutInWhite)
            {
                _fadeColor = 0;
                _fadeTarget = 1;
            }
            else if (type == FadeType.FadeOutBlack || type == FadeType.FadeOutInBlack)
            {
                _fadeColor = 0;
                _fadeTarget = -1;
            }
            _fadeStart = _elapsedTime;
            _fadeLength = length;
        }

        private void UpdateFade()
        {
            Color4 clearColor = _clearColor;
            if (_fadeType != FadeType.None)
            {
                float percent = (_elapsedTime - _fadeStart) / _fadeLength;
                if (percent > 1)
                {
                    percent = 1;
                }
                _currentFade = _fadeColor + (_fadeTarget - _fadeColor) * percent;
                clearColor = new Color4(_clearColor.R + _currentFade, _clearColor.G + _currentFade,
                    _clearColor.B + _currentFade, _clearColor.A);
                if (percent == 1)
                {
                    EndFade();
                }
            }
            GL.Uniform1(_shaderLocations.FadeColor, _currentFade);
            GL.ClearColor(clearColor);
        }

        private void EndFade()
        {
            if (_fadeType == FadeType.FadeOutBlack)
            {
                _currentFade = -1;
            }
            else if (_fadeType == FadeType.FadeOutWhite)
            {
                _currentFade = 1;
            }
            else if (_fadeType == FadeType.FadeOutInBlack)
            {
                SetFade(FadeType.FadeInBlack, _fadeLength, overwrite: true);
            }
            else if (_fadeType == FadeType.FadeOutInWhite)
            {
                SetFade(FadeType.FadeInWhite, _fadeLength, overwrite: true);
            }
            else
            {
                _fadeType = FadeType.None;
                _fadeColor = 0;
                _fadeTarget = 0;
                _currentFade = 0;
                _fadeStart = 0;
                _fadeLength = 0;
            }
        }

        private void RenderItem(RenderItem item)
        {
            UseLight1(item.LightInfo.Light1Vector, item.LightInfo.Light1Color);
            UseLight2(item.LightInfo.Light2Vector, item.LightInfo.Light2Color);

            if (item.MatrixStackCount > 0)
            {
                GL.UniformMatrix4(_shaderLocations.MatrixStack, item.MatrixStackCount, transpose: false, item.MatrixStack);
            }
            else
            {
                Matrix4 transform = item.Transform;
                GL.UniformMatrix4(_shaderLocations.MatrixStack, transpose: false, ref transform);
            }
            Matrix4 viewInv = Matrix4.Identity;
            if (item.BillboardMode == BillboardMode.Sphere)
            {
                viewInv = _viewInvRotMatrix;
            }
            else if (item.BillboardMode == BillboardMode.Cylinder)
            {
                viewInv = _viewInvRotYMatrix;
            }
            GL.UniformMatrix4(_shaderLocations.ViewInvMatrix, transpose: false, ref viewInv);

            DoMaterial(item);
            // texgen actually uses the transform from the current node, not the matrix stack
            DoTexture(item);
            if (_faceCulling)
            {
                GL.Enable(EnableCap.CullFace);
                if (item.CullingMode == CullingMode.Neither)
                {
                    GL.Disable(EnableCap.CullFace);
                }
                else if (item.CullingMode == CullingMode.Back)
                {
                    GL.CullFace(CullFaceMode.Back);
                }
                else if (item.CullingMode == CullingMode.Front)
                {
                    GL.CullFace(CullFaceMode.Front);
                }
            }
            GL.PolygonMode(MaterialFace.FrontAndBack,
                _wireframe || item.Wireframe
                ? OpenTK.Graphics.OpenGL.PolygonMode.Line
                : OpenTK.Graphics.OpenGL.PolygonMode.Fill);
            if (item.Type == RenderItemType.Mesh)
            {
                GL.CallList(item.ListId);
            }
            else if (item.Type == RenderItemType.Box)
            {
                RenderBox(item.Points);
            }
            else if (item.Type == RenderItemType.Cylinder)
            {
                RenderCylinder(item.Points);
            }
            else if (item.Type == RenderItemType.Sphere)
            {
                RenderSphere(item.Points);
            }
            else if (item.Type == RenderItemType.Quad)
            {
                RenderQuad(item.Points);
            }
            else if (item.Type == RenderItemType.Ngon)
            {
                if (_volumeEdges != 1)
                {
                    RenderNgon(item.Points, item.ItemCount);
                }
                if (_volumeEdges != 2 && !item.NoLines)
                {
                    // todo: implement this for volumes as well
                    RenderNgonLines(item.Points, item.ItemCount);
                }
            }
            else if (item.Type == RenderItemType.Particle)
            {
                RenderParticle(item);
            }
            else if (item.Type == RenderItemType.TrailSingle)
            {
                RenderTrailSingle(item);
            }
            else if (item.Type == RenderItemType.TrailMulti)
            {
                RenderTrailMulti(item);
            }
            else if (item.Type == RenderItemType.TrailStack)
            {
                RenderTrailStack(item);
            }
        }

        private void RenderBox(Vector3[] verts)
        {
            // sides
            GL.Begin(PrimitiveType.TriangleStrip);
            GL.Vertex3(verts[2]);
            GL.Vertex3(verts[6]);
            GL.Vertex3(verts[0]);
            GL.Vertex3(verts[4]);
            GL.Vertex3(verts[1]);
            GL.Vertex3(verts[5]);
            GL.Vertex3(verts[3]);
            GL.Vertex3(verts[7]);
            GL.Vertex3(verts[2]);
            GL.Vertex3(verts[6]);
            GL.End();
            // top
            GL.Begin(PrimitiveType.TriangleStrip);
            GL.Vertex3(verts[5]);
            GL.Vertex3(verts[4]);
            GL.Vertex3(verts[7]);
            GL.Vertex3(verts[6]);
            GL.End();
            // bottom
            GL.Begin(PrimitiveType.TriangleStrip);
            GL.Vertex3(verts[3]);
            GL.Vertex3(verts[2]);
            GL.Vertex3(verts[1]);
            GL.Vertex3(verts[0]);
            GL.End();
        }

        private void RenderCylinder(Vector3[] verts)
        {
            // bottom
            GL.Begin(PrimitiveType.TriangleFan);
            GL.Vertex3(verts[32]);
            GL.Vertex3(verts[0]);
            GL.Vertex3(verts[1]);
            GL.Vertex3(verts[2]);
            GL.Vertex3(verts[3]);
            GL.Vertex3(verts[4]);
            GL.Vertex3(verts[5]);
            GL.Vertex3(verts[6]);
            GL.Vertex3(verts[7]);
            GL.Vertex3(verts[8]);
            GL.Vertex3(verts[9]);
            GL.Vertex3(verts[10]);
            GL.Vertex3(verts[11]);
            GL.Vertex3(verts[12]);
            GL.Vertex3(verts[13]);
            GL.Vertex3(verts[14]);
            GL.Vertex3(verts[15]);
            GL.Vertex3(verts[0]);
            GL.End();
            // top
            GL.Begin(PrimitiveType.TriangleFan);
            GL.Vertex3(verts[33]);
            GL.Vertex3(verts[31]);
            GL.Vertex3(verts[30]);
            GL.Vertex3(verts[29]);
            GL.Vertex3(verts[28]);
            GL.Vertex3(verts[27]);
            GL.Vertex3(verts[26]);
            GL.Vertex3(verts[25]);
            GL.Vertex3(verts[24]);
            GL.Vertex3(verts[23]);
            GL.Vertex3(verts[22]);
            GL.Vertex3(verts[21]);
            GL.Vertex3(verts[20]);
            GL.Vertex3(verts[19]);
            GL.Vertex3(verts[18]);
            GL.Vertex3(verts[17]);
            GL.Vertex3(verts[16]);
            GL.Vertex3(verts[31]);
            GL.End();
            // sides
            GL.Begin(PrimitiveType.TriangleStrip);
            GL.Vertex3(verts[0]);
            GL.Vertex3(verts[16]);
            GL.Vertex3(verts[1]);
            GL.Vertex3(verts[17]);
            GL.Vertex3(verts[2]);
            GL.Vertex3(verts[18]);
            GL.Vertex3(verts[3]);
            GL.Vertex3(verts[19]);
            GL.Vertex3(verts[4]);
            GL.Vertex3(verts[20]);
            GL.Vertex3(verts[5]);
            GL.Vertex3(verts[21]);
            GL.Vertex3(verts[6]);
            GL.Vertex3(verts[22]);
            GL.Vertex3(verts[7]);
            GL.Vertex3(verts[23]);
            GL.Vertex3(verts[8]);
            GL.Vertex3(verts[24]);
            GL.Vertex3(verts[9]);
            GL.Vertex3(verts[25]);
            GL.Vertex3(verts[10]);
            GL.Vertex3(verts[26]);
            GL.Vertex3(verts[11]);
            GL.Vertex3(verts[27]);
            GL.Vertex3(verts[12]);
            GL.Vertex3(verts[28]);
            GL.Vertex3(verts[13]);
            GL.Vertex3(verts[29]);
            GL.Vertex3(verts[14]);
            GL.Vertex3(verts[30]);
            GL.Vertex3(verts[15]);
            GL.Vertex3(verts[31]);
            GL.Vertex3(verts[0]);
            GL.Vertex3(verts[16]);
            GL.End();
        }

        private void RenderSphere(Vector3[] verts)
        {
            int stackCount = DisplaySphereStacks;
            int sectorCount = DisplaySphereSectors;
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
                        GL.Vertex3(verts[k1 + 1]);
                        GL.Vertex3(verts[k2]);
                        GL.Vertex3(verts[k1]);
                    }
                    if (i != (stackCount - 1))
                    {
                        GL.Vertex3(verts[k2 + 1]);
                        GL.Vertex3(verts[k2]);
                        GL.Vertex3(verts[k1 + 1]);
                    }
                }
            }
            GL.End();
        }

        private void RenderQuad(Vector3[] verts)
        {
            GL.Begin(PrimitiveType.TriangleStrip);
            GL.Vertex3(verts[0]);
            GL.Vertex3(verts[3]);
            GL.Vertex3(verts[1]);
            GL.Vertex3(verts[2]);
            GL.End();
        }

        private void RenderNgon(Vector3[] verts, int count)
        {
            GL.Begin(PrimitiveType.TriangleFan);
            for (int i = 0; i < count; i++)
            {
                GL.Vertex3(verts[i]);
            }
            GL.End();
        }

        private void RenderNgonLines(Vector3[] verts, int count)
        {
            Vector4 color = _showCollision && ColDisplayColor == CollisionColor.None && ColDisplayAlpha == 1
                ? new Vector4(0f, 0f, 1f, 1f)
                : new Vector4(1f, 0f, 0f, 1f);
            GL.Uniform4(_shaderLocations.OverrideColor, color);
            GL.Begin(PrimitiveType.LineLoop);
            for (int i = 0; i < count; i++)
            {
                GL.Vertex3(verts[i]);
            }
            GL.End();
        }

        private void RenderParticle(RenderItem item)
        {
            Vector3 texcoord0 = item.Points[0];
            Vector3 vertex0 = item.Points[1];
            Vector3 texcoord1 = item.Points[2];
            Vector3 vertex1 = item.Points[3];
            Vector3 texcoord2 = item.Points[4];
            Vector3 vertex2 = item.Points[5];
            Vector3 texcoord3 = item.Points[6];
            Vector3 vertex3 = item.Points[7];
            GL.Begin(PrimitiveType.Quads);
            GL.TexCoord3(texcoord0.X * item.ScaleS, texcoord0.Y * item.ScaleT, 0f);
            GL.Vertex3(vertex0);
            GL.TexCoord3(texcoord1.X * item.ScaleS, texcoord1.Y * item.ScaleT, 0f);
            GL.Vertex3(vertex1);
            GL.TexCoord3(texcoord2.X * item.ScaleS, texcoord2.Y * item.ScaleT, 0f);
            GL.Vertex3(vertex2);
            GL.TexCoord3(texcoord3.X * item.ScaleS, texcoord3.Y * item.ScaleT, 0f);
            GL.Vertex3(vertex3);
            GL.End();
        }

        private void RenderTrailSingle(RenderItem item)
        {
            Vector3 texcoord0 = item.Points[0];
            Vector3 vertex0 = item.Points[1];
            Vector3 texcoord1 = item.Points[2];
            Vector3 vertex1 = item.Points[3];
            Vector3 texcoord2 = item.Points[4];
            Vector3 vertex2 = item.Points[5];
            Vector3 texcoord3 = item.Points[6];
            Vector3 vertex3 = item.Points[7];
            GL.Begin(PrimitiveType.QuadStrip);
            GL.TexCoord3(texcoord0);
            GL.Vertex3(vertex0);
            GL.TexCoord3(texcoord1);
            GL.Vertex3(vertex1);
            GL.TexCoord3(texcoord2);
            GL.Vertex3(vertex2);
            GL.TexCoord3(texcoord3);
            GL.Vertex3(vertex3);
            GL.End();
        }

        private void RenderTrailMulti(RenderItem item)
        {
            Debug.Assert(item.ItemCount >= 4 && item.ItemCount % 2 == 0);
            GL.Begin(PrimitiveType.QuadStrip);
            for (int i = 0; i < item.ItemCount; i += 2)
            {
                Vector3 texcoord = item.Points[i];
                Vector3 vertex = item.Points[i + 1];
                GL.TexCoord3(texcoord);
                GL.Vertex3(vertex);
            }
            GL.End();
        }

        private void RenderTrailStack(RenderItem item)
        {
            for (int i = 0; i < item.ItemCount; i++)
            {
                Vector3 texcoord0 = item.Points[i * 8];
                Vector3 vertex0 = item.Points[i * 8 + 1];
                Vector3 texcoord1 = item.Points[i * 8 + 2];
                Vector3 vertex1 = item.Points[i * 8 + 3];
                Vector3 texcoord2 = item.Points[i * 8 + 4];
                Vector3 vertex2 = item.Points[i * 8 + 5];
                Vector3 texcoord3 = item.Points[i * 8 + 6];
                Vector3 vertex3 = item.Points[i * 8 + 7];
                GL.Begin(PrimitiveType.Quads);
                GL.TexCoord3(texcoord0);
                GL.Vertex3(vertex0);
                GL.TexCoord3(texcoord1);
                GL.Vertex3(vertex1);
                GL.TexCoord3(texcoord2);
                GL.Vertex3(vertex2);
                GL.TexCoord3(texcoord3);
                GL.Vertex3(vertex3);
                GL.End();
            }
        }

        private void DoMaterial(RenderItem item)
        {
            GL.Uniform1(_shaderLocations.UseLight, _lighting && item.Lighting ? 1 : 0);
            // MPH applies the material colors initially by calling DIF_AMB with bit 15 set,
            // so the diffuse color is always set as the vertex color to start
            // (the emission color is set to white if lighting is disabled or black if lighting is enabled; we can just ignore that)
            // --> ...except for hunter models with teams enabled or with double damage
            GL.Color3(item.Diffuse);
            GL.Uniform3(_shaderLocations.Diffuse, item.Diffuse);
            GL.Uniform3(_shaderLocations.Ambient, item.Ambient);
            GL.Uniform3(_shaderLocations.Specular, item.Specular);
            GL.Uniform3(_shaderLocations.Emission, item.Emission);
            GL.Uniform1(_shaderLocations.MaterialAlpha, item.Alpha);
            GL.Uniform1(_shaderLocations.MaterialMode, (int)item.PolygonMode);
        }

        private void DoTexture(RenderItem item)
        {
            if (item.HasTexture)
            {
                GL.BindTexture(TextureTarget.Texture2D, item.TextureBindingId);
                int minParameter = _textureFiltering ? (int)TextureMinFilter.Linear : (int)TextureMinFilter.Nearest;
                int magParameter = _textureFiltering ? (int)TextureMagFilter.Linear : (int)TextureMagFilter.Nearest;
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, minParameter);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, magParameter);
                switch (item.XRepeat)
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
                switch (item.YRepeat)
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
                Matrix4 texcoordMatrix = item.TexcoordMatrix;
                GL.Uniform1(_shaderLocations.TexgenMode, (int)item.TexgenMode);
                GL.UniformMatrix4(_shaderLocations.TextureMatrix, transpose: false, ref texcoordMatrix);
            }
            GL.Uniform1(_shaderLocations.UseTexture, item.HasTexture && _showTextures ? 1 : 0);
            Vector4? overrideColor = item.OverrideColor;
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
            if (item.PaletteOverride != null)
            {
                Vector4 overrideColorValue = item.PaletteOverride.Value;
                GL.Uniform1(_shaderLocations.UsePaletteOverride, 1);
                GL.Uniform4(_shaderLocations.PaletteOverrideColor, ref overrideColorValue);
            }
            else
            {
                GL.Uniform1(_shaderLocations.UsePaletteOverride, 0);
            }
        }

        public void LookAt(Vector3 target)
        {
            _cameraMode = CameraMode.Roam;
            _cameraPosition = target.AddZ(5);
            _cameraFacing = -Vector3.UnitZ;
            _cameraUp = Vector3.UnitY;
            _cameraRight = Vector3.UnitX;
        }

        public void OnMouseClick(bool down)
        {
            _leftMouse = down;
        }

        public void OnMouseMove(float deltaX, float deltaY)
        {
            if (_leftMouse && AllowCameraMovement)
            {
                if (_cameraMode == CameraMode.Pivot)
                {
                    _pivotAngleX += deltaY / 1.5f;
                    _pivotAngleX = Math.Clamp(_pivotAngleX, -90.0f, 90.0f);
                    _pivotAngleY += deltaX / 1.5f;
                    _pivotAngleY %= 360f;
                }
                else if (_cameraMode == CameraMode.Roam)
                {
                    UpdateCameraRotation(MathHelper.DegreesToRadians(deltaX / 1.5f), MathHelper.DegreesToRadians(-deltaY / 1.5f));
                }
            }
        }

        public void OnMouseWheel(float offsetY)
        {
            if (_cameraMode == CameraMode.Pivot && AllowCameraMovement)
            {
                _pivotDistance += offsetY / -1.5f;
                if (_pivotDistance < 0)
                {
                    _pivotDistance = 0;
                }
                else if (_pivotDistance > 1000)
                {
                    _pivotDistance = 1000;
                }
            }
        }

        public bool ShowCollision => _showCollision;
        public EntityType ColEntDisplay { get; private set; } = EntityType.Room;
        public Terrain ColTerDisplay { get; private set; } = Terrain.All;
        public CollisionType ColTypeDisplay { get; private set; } = CollisionType.Any;
        public CollisionColor ColDisplayColor { get; private set; } = CollisionColor.None;
        public float ColDisplayAlpha { get; private set; } = 0.5f;
        private int _colMenuSelect = 0; // 0-4

        public void OnKeyDown(KeyboardKeyEventArgs e)
        {
            if (Selection.OnKeyDown(e, this))
            {
                return;
            }
            if (e.Key == Keys.J && _showCollision)
            {
                if (_colMenuSelect == 0)
                {
                    if (e.Control)
                    {
                        ColEntDisplay = EntityType.All;
                    }
                    else if (e.Shift)
                    {
                        if (ColEntDisplay == EntityType.Room)
                        {
                            ColEntDisplay = EntityType.All;
                        }
                        else if (ColEntDisplay == EntityType.Object)
                        {
                            ColEntDisplay = EntityType.Platform;
                        }
                        else if (ColEntDisplay == EntityType.All)
                        {
                            ColEntDisplay = EntityType.Object;
                        }
                        else
                        {
                            ColEntDisplay = EntityType.Room;
                        }
                    }
                    else
                    {
                        if (ColEntDisplay == EntityType.Room)
                        {
                            ColEntDisplay = EntityType.Platform;
                        }
                        else if (ColEntDisplay == EntityType.Platform)
                        {
                            ColEntDisplay = EntityType.Object;
                        }
                        else if (ColEntDisplay == EntityType.Object)
                        {
                            ColEntDisplay = EntityType.All;
                        }
                        else
                        {
                            ColEntDisplay = EntityType.Room;
                        }
                    }
                }
                else if (_colMenuSelect == 1)
                {
                    if (e.Control)
                    {
                        ColTerDisplay = Terrain.All;
                    }
                    else if (e.Shift)
                    {
                        ColTerDisplay--;
                        if ((byte)ColTerDisplay == 255)
                        {
                            ColTerDisplay = Terrain.All;
                        }
                    }
                    else
                    {
                        ColTerDisplay++;
                        if (ColTerDisplay > Terrain.All)
                        {
                            ColTerDisplay = Terrain.Metal;
                        }
                    }
                }
                else if (_colMenuSelect == 2)
                {
                    if (e.Control)
                    {
                        ColTypeDisplay = CollisionType.Any;
                    }
                    else if (e.Shift)
                    {
                        ColTypeDisplay--;
                        if (ColTypeDisplay < 0)
                        {
                            ColTypeDisplay = CollisionType.Both;
                        }
                    }
                    else
                    {
                        ColTypeDisplay++;
                        if (ColTypeDisplay > CollisionType.Both)
                        {
                            ColTypeDisplay = CollisionType.Any;
                        }
                    }
                }
                else if (_colMenuSelect == 3)
                {
                    if (e.Control)
                    {
                        ColDisplayColor = CollisionColor.None;
                    }
                    else if (e.Shift)
                    {
                        ColDisplayColor--;
                        if (ColDisplayColor < 0)
                        {
                            ColDisplayColor = CollisionColor.Type;
                        }
                    }
                    else
                    {
                        ColDisplayColor++;
                        if (ColDisplayColor > CollisionColor.Type)
                        {
                            ColDisplayColor = CollisionColor.None;
                        }
                    }
                }
                else if (_colMenuSelect == 4)
                {
                    if (ColDisplayAlpha == 1)
                    {
                        ColDisplayAlpha = 0.5f;
                    }
                    else
                    {
                        ColDisplayAlpha = 1;
                    }
                }
            }
            else if (e.Key == Keys.K)
            {
                if (e.Alt)
                {
                    _showCollision = !_showCollision;
                }
                else if (_showCollision)
                {
                    if (e.Control)
                    {
                        _colMenuSelect = 0;
                        ColEntDisplay = EntityType.Room;
                        ColTerDisplay = Terrain.All;
                        ColTypeDisplay = CollisionType.Any;
                        ColDisplayColor = CollisionColor.None;
                        ColDisplayAlpha = 0.5f;
                    }
                    else if (e.Shift)
                    {
                        _colMenuSelect--;
                        if (_colMenuSelect < 0)
                        {
                            _colMenuSelect = 4;
                        }
                    }
                    else
                    {
                        _colMenuSelect++;
                        if (_colMenuSelect > 4)
                        {
                            _colMenuSelect = 0;
                        }
                    }
                }
            }
            else if (e.Key == Keys.D5)
            {
                if (!_recording)
                {
                    Images.Screenshot(Size.X, Size.Y);
                }
            }
            else if (e.Key == Keys.T)
            {
                _showTextures = !_showTextures;
            }
            else if (e.Key == Keys.C)
            {
                if (e.Alt)
                {
                    if (e.Shift)
                    {
                        _promptState = PromptState.CameraPos;
                    }
                    else
                    {
                        _outputCameraPos = !_outputCameraPos;
                    }
                }
                else if (_cameraMode != CameraMode.Player)
                {
                    _showColors = !_showColors;
                }
            }
            else if (e.Key == Keys.Q)
            {
                if (e.Alt)
                {
                    if (e.Shift)
                    {
                        _volumeEdges--;
                        if (_volumeEdges < 0)
                        {
                            _volumeEdges = 2;
                        }
                    }
                    else
                    {
                        _volumeEdges++;
                        if (_volumeEdges > 2)
                        {
                            _volumeEdges = 0;
                        }
                    }
                }
                else if (_cameraMode != CameraMode.Player)
                {
                    _wireframe = !_wireframe;
                }
            }
            else if (e.Key == Keys.B && !e.Alt)
            {
                _faceCulling = !_faceCulling;
                if (!_faceCulling)
                {
                    GL.Disable(EnableCap.CullFace);
                }
            }
            else if (e.Key == Keys.F)
            {
                _textureFiltering = !_textureFiltering;
            }
            else if (e.Key == Keys.L)
            {
                _lighting = !_lighting;
            }
            else if (e.Key == Keys.Z)
            {
                if (e.Control)
                {
                    _showVolumes = VolumeDisplay.None;
                }
                else if (e.Shift)
                {
                    _showVolumes--;
                    if (_showVolumes < VolumeDisplay.None)
                    {
                        _showVolumes = VolumeDisplay.Portal;
                    }
                }
                else
                {
                    _showVolumes++;
                    if (_showVolumes > VolumeDisplay.Portal)
                    {
                        _showVolumes = VolumeDisplay.None;
                    }
                }
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
            }
            else if (e.Key == Keys.N)
            {
                if (e.Alt)
                {
                    _showAllNodes = !_showAllNodes;
                }
                else
                {
                    _transformRoomNodes = !_transformRoomNodes;
                }
            }
            else if (e.Key == Keys.H)
            {
                if (e.Alt)
                {
                    Selection.ToggleUnselectedVolumes();
                }
                else
                {
                    Selection.ToggleShowSelection();
                }
            }
            else if (e.Key == Keys.I)
            {
                if (e.Alt)
                {
                    if (_showInvisible == 2)
                    {
                        _showInvisible = 0;
                    }
                    else
                    {
                        _showInvisible = 2;
                    }
                }
                else if (_showInvisible == 0)
                {
                    _showInvisible = 1;
                }
                else
                {
                    _showInvisible = 0;
                }
            }
            else if (e.Key == Keys.Y)
            {
                _showNodeData = !_showNodeData;
            }
            else if (e.Key == Keys.E && !e.Alt)
            {
                _scanVisor = !_scanVisor;
            }
            else if (e.Key == Keys.R)
            {
                if (e.Control && e.Shift)
                {
                    _recording = !_recording;
                    _framesRecorded = 0;
                }
                else if (AllowCameraMovement)
                {
                    ResetCamera();
                }
            }
            else if (e.Key == Keys.P)
            {
                if (e.Alt)
                {
                    UpdatePointModule();
                }
                else
                {
                    if (_cameraMode == CameraMode.Pivot)
                    {
                        _cameraMode = CameraMode.Roam;
                    }
                    else if (_cameraMode == CameraMode.Roam)
                    {
                        _cameraMode = CameraMode.Player;
                    }
                    else
                    {
                        _cameraMode = CameraMode.Pivot;
                    }
                    ResetCamera();
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
                _promptState = PromptState.Load;
            }
            else if (e.Control && e.Key == Keys.U)
            {
                if (Selection.Entity != null)
                {
                    _unloadQueue.Enqueue(Selection.Entity);
                }
            }
        }

        private void OnKeyHeld()
        {
            if (_keyboardState.IsKeyDown(Keys.LeftAlt) || _keyboardState.IsKeyDown(Keys.RightAlt))
            {
                Selection.OnKeyHeld(_keyboardState);
                return;
            }
            if (!AllowCameraMovement)
            {
                return;
            }
            if (_cameraMode == CameraMode.Roam)
            {
                float moveStep = _keyboardState.IsKeyDown(Keys.LeftShift) || _keyboardState.IsKeyDown(Keys.RightShift) ? 0.5f : 0.1f;
                float rotStepDeg = _keyboardState.IsKeyDown(Keys.LeftShift) || _keyboardState.IsKeyDown(Keys.RightShift) ? 3 : 1.5f;
                float rotStep = MathHelper.DegreesToRadians(rotStepDeg);
                if (_keyboardState.IsKeyDown(Keys.W)) // move forward
                {
                    _cameraPosition += _cameraFacing * moveStep;
                }
                else if (_keyboardState.IsKeyDown(Keys.S)) // move backward
                {
                    _cameraPosition -= _cameraFacing * moveStep;
                }
                if (_keyboardState.IsKeyDown(Keys.Space)) // move up
                {
                    _cameraPosition = _cameraPosition.WithY(_cameraPosition.Y + moveStep);
                }
                else if (_keyboardState.IsKeyDown(Keys.V)) // move down
                {
                    _cameraPosition = _cameraPosition.WithY(_cameraPosition.Y - moveStep);
                }
                if (_keyboardState.IsKeyDown(Keys.A)) // move left
                {
                    _cameraPosition -= _cameraRight * moveStep;
                }
                else if (_keyboardState.IsKeyDown(Keys.D)) // move right
                {
                    _cameraPosition += _cameraRight * moveStep;
                }
                if (_keyboardState.IsKeyDown(Keys.Left) || _keyboardState.IsKeyDown(Keys.Right)
                    || _keyboardState.IsKeyDown(Keys.Up) || _keyboardState.IsKeyDown(Keys.Down))
                {
                    float stepH = 0;
                    float stepV = 0;
                    if (_keyboardState.IsKeyDown(Keys.Left)) // rotate left
                    {
                        stepH = -rotStep;
                    }
                    else if (_keyboardState.IsKeyDown(Keys.Right)) // rotate right
                    {
                        stepH = rotStep;
                    }
                    if (_keyboardState.IsKeyDown(Keys.Up)) // rotate up
                    {
                        stepV = rotStep;
                    }
                    else if (_keyboardState.IsKeyDown(Keys.Down)) // rotate down
                    {
                        stepV = -rotStep;
                    }
                    UpdateCameraRotation(stepH, stepV);
                }
            }
            else if (_cameraMode == CameraMode.Pivot)
            {
                float rotStep = _keyboardState.IsKeyDown(Keys.LeftShift) || _keyboardState.IsKeyDown(Keys.RightShift) ? -3 : -1.5f;
                if (_keyboardState.IsKeyDown(Keys.Up)) // rotate up
                {
                    _pivotAngleX += rotStep;
                    _pivotAngleX = Math.Clamp(_pivotAngleX, -90.0f, 90.0f);
                }
                else if (_keyboardState.IsKeyDown(Keys.Down)) // rotate down
                {
                    _pivotAngleX -= rotStep;
                    _pivotAngleX = Math.Clamp(_pivotAngleX, -90.0f, 90.0f);
                }
                if (_keyboardState.IsKeyDown(Keys.Left)) // rotate left
                {
                    _pivotAngleY += rotStep;
                    _pivotAngleY %= 360f;
                }
                else if (_keyboardState.IsKeyDown(Keys.Right)) // rotate right
                {
                    _pivotAngleY -= rotStep;
                    _pivotAngleY %= 360f;
                }
            }
        }

        private void UpdatePointModule()
        {
            if (PointModuleEntity.Current == null)
            {
                if (TryGetEntity(PointModuleEntity.StartId, out EntityBase? entity) && entity is PointModuleEntity module)
                {
                    module.SetCurrent();
                }
            }
            else
            {
                PointModuleEntity? next = PointModuleEntity.Current.Next ?? PointModuleEntity.Current.Prev;
                if (next != null && next != PointModuleEntity.Current)
                {
                    next.SetCurrent();
                }
                else
                {
                    if (TryGetEntity(PointModuleEntity.StartId, out EntityBase? entity) && entity is PointModuleEntity module)
                    {
                        module.SetCurrent();
                    }
                }
            }
        }

        private enum PromptState
        {
            None,
            Load,
            CameraPos
        }

        private PromptState _promptState = PromptState.None;
        private readonly ConcurrentQueue<(string Name, int Recolor, bool FirstHunt)> _loadQueue = new ConcurrentQueue<(string, int, bool)>();
        private readonly ConcurrentQueue<EntityBase> _unloadQueue = new ConcurrentQueue<EntityBase>();

        private readonly CancellationTokenSource _outputCts = new CancellationTokenSource();
        private string _currentOutput = "";
        private readonly StringBuilder _sb = new StringBuilder();

        private void OutputStart()
        {
            Task.Run(async () => await OutputUpdate(_outputCts.Token), _outputCts.Token);
        }

        private async Task OutputUpdate(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_promptState == PromptState.Load)
                {
                    OutputLoadPrompt();
                    _promptState = PromptState.None;
                    _currentOutput = "";
                }
                else if (_promptState == PromptState.CameraPos)
                {
                    OutputCameraPrompt();
                    _promptState = PromptState.None;
                    _currentOutput = "";
                }
                string output = OutputGetAll();
                if (output != _currentOutput)
                {
                    Console.Clear(); // todo: this causes flickering
                    Console.WriteLine(output);
                    _currentOutput = output;
                }
                await Task.Delay(100, token);
            }
        }

        private void OutputLoadPrompt()
        {
            Console.Clear();
            Console.Write("Enter model name: ");
            string[] input = (Console.ReadLine() ?? "").Trim().Split(' ');
            string name = input[0].Trim();
            if (name.Length > 0)
            {
                int recolor = 0;
                bool firstHunt = false;
                if (input.Length > 1)
                {
                    if (UInt32.TryParse(input[1].Trim(), out uint value))
                    {
                        recolor = (int)value;
                    }
                    if (input.Length > 2)
                    {
                        firstHunt = input[2].Trim() == "-fh";
                    }
                }
                _loadQueue.Enqueue((name, recolor, firstHunt));
            }
        }

        private void OutputCameraPrompt()
        {
            Console.Clear();
            Console.Write("Enter camera position: ");
            string[] input = (Console.ReadLine() ?? "").Trim().Replace(",", "").Split(' ');
            float x = 0;
            float y = 0;
            float z = 0;
            for (int i = 0; i < input.Length && i < 3; i++)
            {
                string item = input[i];
                float coord = 0;
                if (item.StartsWith("0x"))
                {
                    if (Int32.TryParse(item.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out int value))
                    {
                        coord = value / 4096f;
                    }
                }
                else if (Single.TryParse(item, out float value))
                {
                    coord = value;
                }
                if (i == 0)
                {
                    x = coord;
                }
                else if (i == 1)
                {
                    y = coord;
                }
                else if (i == 2)
                {
                    z = coord;
                }
            }
            _cameraPosition = new Vector3(x, y, z);
        }

        private string OutputGetAll()
        {
            _sb.Clear();
            string recording = _recording ? " - Recording" : "";
            string frameAdvance = _frameAdvanceOn ? " - Frame Advance" : "";
            _sb.AppendLine($"MphRead Version {Program.Version}{recording}{frameAdvance}");
            if (_showCollision)
            {
                OutputGetCollisionMenu();
            }
            if (Selection.Entity != null)
            {
                OutputGetEntityInfo();
                if (Selection.Instance != null)
                {
                    OutputGetModel();
                    if (Selection.Node != null)
                    {
                        OutputGetNode();
                        if (Selection.Mesh != null)
                        {
                            OutputGetMesh();
                        }
                    }
                }
            }
            else if (!_showCollision)
            {
                OutputGetMenu();
            }
            return _sb.ToString();
        }

        private void OutputGetCollisionMenu()
        {
            _sb.AppendLine();
            _sb.AppendLine($"[{(_colMenuSelect == 0 ? "x" : " ")}] Entities ({ColEntDisplay})");
            _sb.AppendLine($"[{(_colMenuSelect == 1 ? "x" : " ")}] Terrain ({ColTerDisplay})");
            _sb.AppendLine($"[{(_colMenuSelect == 2 ? "x" : " ")}] Interaction ({ColTypeDisplay})");
            _sb.AppendLine($"[{(_colMenuSelect == 3 ? "x" : " ")}] Color mode ({ColDisplayColor})");
            _sb.AppendLine($"[{(_colMenuSelect == 4 ? "x" : " ")}] Opacity ({ColDisplayAlpha})");
            _sb.AppendLine();
            _sb.AppendLine("K: Next option, Shift+K: Previous option, Alt+K: Hide collision");
            _sb.AppendLine("J: Next value, Shift+J: Previous value, Ctrl+J: Reset value, Ctrl+K: Reset all");
        }

        private void OutputGetMenu()
        {
            _sb.AppendLine();
            if (_cameraMode == CameraMode.Pivot)
            {
                _sb.AppendLine(" - Scroll mouse wheel to zoom");
            }
            else if (_cameraMode == CameraMode.Roam)
            {
                _sb.AppendLine(" - Use WASD, Space, and V to move");
            }
            string volume = _showVolumes switch
            {
                VolumeDisplay.LightColor1 => "light sources, color 1",
                VolumeDisplay.LightColor2 => "light sources, color 2",
                VolumeDisplay.TriggerParent => "trigger volumes, parent event",
                VolumeDisplay.TriggerChild => "trigger volumes, child event",
                VolumeDisplay.AreaInside => "area volumes, inside event",
                VolumeDisplay.AreaExit => "area volumes, exit event",
                VolumeDisplay.MorphCamera => "morph cameras",
                VolumeDisplay.JumpPad => "jump pads",
                VolumeDisplay.Teleporter => "teleporters",
                VolumeDisplay.EnemyHurt => "enemy hurtboxes",
                VolumeDisplay.Object => "objects",
                VolumeDisplay.FlagBase => "flag bases",
                VolumeDisplay.DefenseNode => "defense nodes",
                VolumeDisplay.KillPlane => "kill plane",
                VolumeDisplay.PlayerLimit => "room limits (player)",
                VolumeDisplay.CameraLimit => "room limits (camera)",
                VolumeDisplay.Portal => "portals",
                _ => "off"
            };
            string invisible = _showInvisible switch
            {
                2 => "all",
                1 => "placeholders",
                _ => "off"
            };
            _sb.AppendLine(" - Hold left mouse button or use arrow keys to rotate");
            _sb.AppendLine(" - Hold Shift to move the camera faster");
            _sb.AppendLine($" - T toggles texturing ({OnOff(_showTextures)})");
            _sb.AppendLine($" - C toggles vertex colours ({OnOff(_showColors)})");
            _sb.AppendLine($" - Q toggles wireframe ({OnOff(_wireframe)})");
            _sb.AppendLine($" - B toggles face culling ({OnOff(_faceCulling)})");
            _sb.AppendLine($" - F toggles texture filtering ({OnOff(_textureFiltering)})");
            _sb.AppendLine($" - L toggles lighting ({OnOff(_lighting)})");
            _sb.AppendLine($" - G toggles fog ({OnOff(_showFog)})");
            _sb.AppendLine($" - E toggles Scan Visor ({OnOff(_scanVisor)})");
            _sb.AppendLine($" - I toggles invisible entities ({invisible})");
            _sb.AppendLine($" - Z toggles volume display ({volume})");
            _sb.AppendLine($" - P switches camera mode ({(_cameraMode == CameraMode.Pivot ? "pivot" : "roam")})");
            _sb.AppendLine(" - R resets the camera");
            _sb.AppendLine(" - Ctrl+O then enter \"model_name [recolor]\" to load");
            _sb.AppendLine(" - Ctrl+U then enter \"model_id\" to unload");
            _sb.AppendLine(" - Esc closes the viewer");
        }

        private void OutputGetEntityInfo()
        {
            EntityBase? entity = Selection.Entity;
            Debug.Assert(entity != null);
            _sb.AppendLine();
            if (_roomLoaded)
            {
                string string1 = $"{(int)(_light1Color.X * 255)};{(int)(_light1Color.Y * 255)};{(int)(_light1Color.Z * 255)}";
                string string2 = $"{(int)(_light2Color.X * 255)};{(int)(_light2Color.Y * 255)};{(int)(_light2Color.Z * 255)}";
                _sb.Append($"Room \u001b[38;2;{string1}m████\u001b[0m \u001b[38;2;{string2}m████\u001b[0m");
                _sb.AppendLine($" ({_light1Vector.X}, {_light1Vector.Y}, {_light1Vector.Z}) ({_light2Vector.X}, {_light2Vector.Y}, {_light2Vector.Z})");
            }
            else
            {
                _sb.AppendLine("No room loaded");
            }
            if (_outputCameraPos)
            {
                _sb.AppendLine($"Camera ({_cameraPosition.X}, {_cameraPosition.Y}, {_cameraPosition.Z})");
            }
            else
            {
                _sb.AppendLine($"Camera (?, ?, ?)");
            }
            _sb.AppendLine();
            _sb.Append($"Entity: {entity.Type}");
            IReadOnlyList<ModelInstance> models = entity.GetModels();
            if (entity.Type == EntityType.Model)
            {
                Debug.Assert(models.Count > 0);
                _sb.Append($" ({models[0].Model.Name})");
            }
            string color = "";
            if (models.Count > 0 && !models[0].IsPlaceholder)
            {
                color = $" - Color {entity.Recolor}";
            }
            _sb.Append($" [{entity.Id}] {(entity.Active ? "On " : "Off")}{color}");
            if (entity.Type == EntityType.Room)
            {
                _sb.Append($" ({entity.GetModels()[0].Model.Nodes.Count(n => n.IsRoomPartNode)})");
            }
            else if (entity is LightSourceEntity light)
            {
                Vector3 color1 = light.Light1Color;
                Vector3 color2 = light.Light2Color;
                string string1 = $"{(int)(color1.X * 255)};{(int)(color1.Y * 255)};{(int)(color1.Z * 255)}";
                string string2 = $"{(int)(color2.X * 255)};{(int)(color2.Y * 255)};{(int)(color2.Z * 255)}";
                _sb.Append($" \u001b[38;2;{string1}m████\u001b[0m \u001b[38;2;{string2}m████\u001b[0m");
                _sb.Append($" {light.Light1Enabled} / {light.Light2Enabled}");
                Vector3 vector1 = light.Light1Vector;
                Vector3 vector2 = light.Light2Vector;
                _sb.Append($" ({vector1.X}, {vector1.Y}, {vector1.Z}) ({vector2.X}, {vector2.Y}, {vector2.Z})");
            }
            else if (entity is AreaVolumeEntity area)
            {
                EntityBase? parent = area.GetParent();
                EntityBase? child = area.GetChild();
                _sb.Append($" ({area.Data.TriggerFlags})");
                _sb.AppendLine();
                _sb.Append($"Entry: {area.Data.InsideMessage}");
                _sb.Append($", Param1: {area.Data.InsideMsgParam1}, Param2: {area.Data.InsideMsgParam2}");
                _sb.Append($", Target: {parent?.Type.ToString() ?? "None"} ({area.Data.ParentId})");
                _sb.AppendLine();
                _sb.Append($" Exit: {area.Data.ExitMessage}");
                _sb.Append($", Param1: {area.Data.ExitMsgParam1}, Param2: {area.Data.ExitMsgParam2}");
                _sb.Append($", Target: {child?.Type.ToString() ?? "None"} ({area.Data.ChildId})");
            }
            else if (entity is FhAreaVolumeEntity fhArea)
            {
                _sb.Append($" ({fhArea.Data.TriggerFlags})");
                _sb.AppendLine();
                _sb.Append($"Entry: {fhArea.Data.InsideMessage}");
                _sb.Append($", Param1: {fhArea.Data.InsideMsgParam1}, Param2: 0");
                _sb.AppendLine();
                _sb.Append($" Exit: {fhArea.Data.ExitMessage}");
                _sb.Append($", Param1: {fhArea.Data.ExitMsgParam1}, Param2: 0");
            }
            else if (entity is TriggerVolumeEntity trigger)
            {
                EntityBase? parent = trigger.GetParent();
                EntityBase? child = trigger.GetChild();
                _sb.Append($" ({trigger.Data.Subtype}");
                if (trigger.Data.Subtype == TriggerType.Threshold)
                {
                    _sb.Append($" x{trigger.Data.TriggerThreshold}");
                }
                _sb.Append(')');
                _sb.Append($" ({trigger.Data.TriggerFlags})");
                _sb.AppendLine();
                _sb.Append($"Parent: {trigger.Data.ParentMessage}");
                _sb.Append($", Param1: {trigger.Data.ParentMsgParam1}, Param2: {trigger.Data.ParentMsgParam2}");
                _sb.Append($", Target: {parent?.Type.ToString() ?? "None"} ({trigger.Data.ParentId})");
                _sb.AppendLine();
                _sb.Append($" Child: {trigger.Data.ChildMessage}");
                _sb.Append($", Param1: {trigger.Data.ChildMsgParam1}, Param2: {trigger.Data.ChildMsgParam2}");
                _sb.Append($", Target: {child?.Type.ToString() ?? "None"} ({trigger.Data.ChildId})");
            }
            else if (entity is FhTriggerVolumeEntity fhTrigger)
            {
                if (fhTrigger.Data.Subtype == FhTriggerType.Threshold)
                {
                    _sb.Append($" x{fhTrigger.Data.Threshold}");
                }
                _sb.Append($" ({fhTrigger.Data.TriggerFlags})");
                _sb.AppendLine();
                _sb.Append($"Parent: {fhTrigger.Data.ParentMessage}");
                _sb.Append($", Param1: {fhTrigger.Data.ParentMsgParam1}, Param2: 0");
                // rtodo: use entity fields for parent/child
                if (fhTrigger.Data.ParentMessage != FhMessage.None && TryGetEntity(fhTrigger.Data.ParentId, out EntityBase? parent))
                {
                    _sb.Append($", Target: {parent.Type} ({fhTrigger.Data.ParentId})");
                }
                else
                {
                    _sb.Append(", Target: None");
                }
                _sb.AppendLine();
                _sb.Append($" Child: {fhTrigger.Data.ChildMessage}");
                _sb.Append($", Param1: {fhTrigger.Data.ChildMsgParam1}, Param2: 0");
                if (fhTrigger.Data.ChildMessage != FhMessage.None && TryGetEntity(fhTrigger.Data.ChildId, out EntityBase? child))
                {
                    _sb.Append($", Target: {child.Type} ({fhTrigger.Data.ChildId})");
                }
                else
                {
                    _sb.Append(", Target: None");
                }
            }
            else if (entity is EnemySpawnEntity enemySpawn)
            {
                _sb.Append($" ({enemySpawn.Data.EnemyType})");
            }
            else if (entity is EnemyInstanceEntity enemyInst)
            {
                _sb.Append($" ({enemyInst.EnemyType})");
            }
            else if (entity is ItemSpawnEntity itemSpawn)
            {
                _sb.Append($" ({itemSpawn.Data.ItemType})");
            }
            else if (entity is ObjectEntity obj)
            {
                if (obj.Data.EffectId > 0)
                {
                    _sb.Append($" ({obj.Data.EffectId}, {Metadata.Effects[obj.Data.EffectId].Name})");
                }
            }
            else if (entity is CameraSequenceEntity cam)
            {
                _sb.Append($" (ID {cam.Data.SequenceId})");
            }
            _sb.AppendLine();
            _sb.AppendLine($"Position ({entity.Position.X}, {entity.Position.Y}, {entity.Position.Z})");
            _sb.AppendLine($"Rotation ({entity.Rotation.X}, {entity.Rotation.Y}, {entity.Rotation.Z})");
            _sb.AppendLine($"   Scale ({entity.Scale.X}, {entity.Scale.Y}, {entity.Scale.Z})");
        }

        private void OutputGetModel()
        {
            ModelInstance? inst = Selection.Instance;
            Debug.Assert(inst != null);
            _sb.AppendLine();
            _sb.AppendLine($"Model: {inst.Model.Name}, Scale: {inst.Model.Scale.X}, Active: {YesNo(inst.Active)}" +
                $"{(inst.IsPlaceholder ? ", Placeholder" : "")}");
            _sb.AppendLine($"Nodes {inst.Model.Nodes.Count}, Meshes {inst.Model.Meshes.Count}, Materials {inst.Model.Materials.Count}," +
                $" Textures {inst.Model.Recolors[0].Textures.Count}, Palettes {inst.Model.Recolors[0].Palettes.Count}");
            AnimationInfo a = inst.AnimInfo;
            AnimationGroups g = inst.Model.AnimationGroups;
            _sb.AppendLine($"Anim: {a.Index[0]}, {a.Frame[1]}" +
                $" (Node {(a.Node.Group?.Count > 0 ? a.NodeIndex : -1)} / {g.Node.Count}," +
                $" Mat {(a.Material.Group?.Count > 0 ? a.MaterialIndex : -1)} / {g.Material.Count}," +
                $" UV {(a.Texcoord.Group?.Count > 0 ? a.TexcoordIndex : -1)} / {g.Texcoord.Count}," +
                $" Tex {(a.Texture.Group?.Count > 0 ? a.TextureIndex : -1)} / {g.Texture.Count})");
        }

        private void OutputGetNode()
        {
            static string FormatNode(Model model, int otherId)
            {
                if (otherId == -1)
                {
                    return "None";
                }
                return $"{model.Nodes[otherId].Name} [{otherId}]";
            }
            Node? node = Selection.Node;
            ModelInstance? inst = Selection.Instance;
            Debug.Assert(node != null && inst != null);
            _sb.AppendLine();
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
            int index = inst.Model.Nodes.IndexOf(n => n == node);
            string enabled = node.Enabled ? (inst.Model.NodeParentsEnabled(node) ? "On " : "On*") : "Off";
            string billboard = node.BillboardMode != BillboardMode.None ? $" - {node.BillboardMode} Billboard" : "";
            _sb.AppendLine($"Node: {node.Name} [{index}] {enabled}{mesh}{billboard}");
            _sb.AppendLine($"Parent {FormatNode(inst.Model, node.ParentIndex)}");
            _sb.AppendLine($" Child {FormatNode(inst.Model, node.ChildIndex)}");
            _sb.AppendLine($"  Next {FormatNode(inst.Model, node.NextIndex)}");
            _sb.AppendLine($"Position ({node.Position.X}, {node.Position.Y}, {node.Position.Z})");
            _sb.AppendLine($"Rotation ({node.Angle.X}, {node.Angle.Y}, {node.Angle.Z})");
            _sb.AppendLine($"   Scale ({node.Scale.X}, {node.Scale.Y}, {node.Scale.Z})");
        }

        private void OutputGetMesh()
        {
            Mesh? mesh = Selection.Mesh;
            ModelInstance? inst = Selection.Instance;
            Debug.Assert(mesh != null && inst != null);
            int index = inst.Model.Meshes.IndexOf(n => n == mesh);
            _sb.AppendLine();
            _sb.AppendLine($"Mesh: [{index}] {(mesh.Visible ? "On " : "Off")} - " +
                $"Material ID {mesh.MaterialId}, DList ID {mesh.DlistId}");
            _sb.AppendLine();
            Material material = inst.Model.Materials[mesh.MaterialId];
            _sb.AppendLine($"Material: {material.Name} [{mesh.MaterialId}] - {material.RenderMode}, {material.PolygonMode}" +
                $" - {material.TexgenMode}");
            _sb.AppendLine($"Lighting {material.Lighting}, Alpha {material.Alpha}, " +
                $"XRepeat {material.XRepeat}, YRepeat {material.YRepeat}");
            _sb.AppendLine($"Texture ID {material.CurrentTextureId}, Palette ID {material.CurrentPaletteId}");
            _sb.AppendLine($"Diffuse ({material.Diffuse.Red}, {material.Diffuse.Green}, {material.Diffuse.Blue})" +
                $" Ambient ({material.Ambient.Red}, {material.Ambient.Green}, {material.Ambient.Blue})" +
                $" Specular ({ material.Specular.Red}, { material.Specular.Green}, { material.Specular.Blue})");
        }

        private string OnOff(bool setting)
        {
            return setting ? "on" : "off";
        }

        private string YesNo(bool setting)
        {
            return setting ? "yes" : "no ";
        }
    }

    public class RenderWindow : GameWindow
    {
        private static readonly GameWindowSettings _gameWindowSettings = new GameWindowSettings()
        {
            RenderFrequency = 60,
            UpdateFrequency = 60
        };

        private static readonly NativeWindowSettings _nativeWindowSettings = new NativeWindowSettings()
        {
            Size = new Vector2i(800, 600),
            Title = "MphRead",
            Profile = ContextProfile.Compatability,
            APIVersion = new Version(3, 2)
        };

        public Scene Scene { get; }

        public RenderWindow() : base(_gameWindowSettings, _nativeWindowSettings)
        {
            CursorGrabbed = true;
            Scene = new Scene(Size, KeyboardState, MouseState, (string title) =>
            {
                Title = title;
            });
        }

        public void AddRoom(int id, GameMode mode = GameMode.None, int playerCount = 0,
            BossFlags bossFlags = BossFlags.None, int nodeLayerMask = 0, int entityLayerId = -1)
        {
            RoomMetadata? meta = Metadata.GetRoomById(id);
            if (meta == null)
            {
                throw new ProgramException("No room with this ID is known.");
            }
            Scene.AddRoom(meta.Name, mode, playerCount, bossFlags, nodeLayerMask, entityLayerId);
        }

        public void AddRoom(string name, GameMode mode = GameMode.None, int playerCount = 0,
            BossFlags bossFlags = BossFlags.None, int nodeLayerMask = 0, int entityLayerId = -1)
        {
            Scene.AddRoom(name, mode, playerCount, bossFlags, nodeLayerMask, entityLayerId);
        }

        public void AddModel(string name, int recolor = 0, bool firstHunt = false, MetaDir dir = MetaDir.Models, Vector3? pos = null)
        {
            Scene.AddModel(name, recolor, firstHunt, dir, pos);
        }

        public void AddPlayer(Hunter hunter, int recolor = 0, Vector3? position = null)
        {
            Scene.AddPlayer(hunter, recolor, position);
        }

        protected override void OnLoad()
        {
            Scene.OnLoad();
            base.OnLoad();
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            Scene.OnUpdateFrame(args.Time);
            Scene.OnRenderFrame();
            SwapBuffers();
            Scene.AfterRenderFrame();
            base.OnRenderFrame(args);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            GL.Viewport(0, 0, Size.X, Size.Y);
            Scene.Size = Size;
            base.OnResize(e);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.Button == MouseButton.Button1)
            {
                Scene.OnMouseClick(down: true);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (e.Button == MouseButton.Button1)
            {
                Scene.OnMouseClick(down: false);
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            Scene.OnMouseMove(e.DeltaX, e.DeltaY);
            base.OnMouseMove(e);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            Scene.OnMouseWheel(e.OffsetY);
            base.OnMouseWheel(e);
        }

        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            if (e.Key == Keys.Escape)
            {
                Close();
            }
            else
            {
                Scene.OnKeyDown(e);
            }
            base.OnKeyDown(e);
        }
    }

    public class TextureMap : Dictionary<int, (int BindingId, bool OnlyOpaque)>
    {
        private int GetKey(int textureId, int paletteId, int recolorId)
        {
            if (paletteId == -1)
            {
                paletteId = 4095;
            }
            Debug.Assert(textureId >= 0 && textureId < 4096);
            Debug.Assert(paletteId >= 0 && paletteId < 4096);
            Debug.Assert(recolorId >= 0 && recolorId < 255);
            return textureId | (paletteId << 12) | (recolorId << 24);
        }

        public (int BindingId, bool OnlyOpaque) Get(int textureId, int paletteId, int recolorId)
        {
            return this[GetKey(textureId, paletteId, recolorId)];
        }

        public void Add(int textureId, int paletteId, int recolorId, int bindingId, bool onlyOpaque)
        {
            this[GetKey(textureId, paletteId, recolorId)] = (bindingId, onlyOpaque);
        }
    }
}
