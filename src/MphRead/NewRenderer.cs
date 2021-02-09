using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Entities;
using MphRead.Export;
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
        Object,
        FlagBase,
        DefenseNode,
        Portal
    }

    public class NewScene
    {
        public Vector2i Size { get; set; }
        private Matrix4 _viewMatrix = Matrix4.Identity;
        private Matrix4 _viewInvRotMatrix = Matrix4.Identity;
        private Matrix4 _viewInvRotYMatrix = Matrix4.Identity;

        private CameraMode _cameraMode = CameraMode.Pivot;
        private float _angleY = 0.0f;
        private float _angleX = 0.0f;
        private float _distance = 5.0f;
        // note: the axes are reversed from the model coordinates
        private Vector3 _cameraPosition = new Vector3(0, 0, 0);
        private bool _leftMouse = false;
        private float _wheelOffset = 0;

        private bool _showTextures = true;
        private bool _showColors = true;
        private bool _wireframe = false;
        private bool _portalEdges = false;
        private bool _faceCulling = true;
        private bool _textureFiltering = false;
        private bool _lighting = false;
        private bool _scanVisor = false;
        private bool _showInvisible = false;
        private VolumeDisplay _showVolumes = VolumeDisplay.None;
        private bool _showKillPlane = false;
        private bool _transformRoomNodes = false;

        private readonly List<IRenderable> _renderables = new List<IRenderable>();
        private readonly List<EntityBase> _entities = new List<EntityBase>();
        private readonly Dictionary<int, EntityBase> _entityMap = new Dictionary<int, EntityBase>();
        // map each model's texture ID/palette ID combinations to the bound OpenGL texture ID and "onlyOpaque" boolean
        private int _textureCount = 0;
        private readonly Dictionary<int, NewTextureMap> _texPalMap = new Dictionary<int, NewTextureMap>();

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
        private long _frameCount = -1;
        private bool _frameAdvanceOn = false;
        private bool _advanceOneFrame = false;
        private bool _recording = false;
        private int _framesRecorded = 0;
        private bool _roomLoaded = false;

        public Matrix4 ViewMatrix => _viewMatrix;
        public Matrix4 ViewInvRotMatrix => _viewInvRotMatrix;
        public Matrix4 ViewInvRotYMatrix => _viewInvRotYMatrix;
        public Vector3 CameraPosition => _cameraPosition * (_cameraMode == CameraMode.Roam ? -1 : 1);
        public bool ShowInvisible => _showInvisible;
        public bool TransformRoomNodes => _transformRoomNodes;
        public float FrameTime => _frameTime;
        public long FrameCount => _frameCount;
        public VolumeDisplay ShowVolumes => _showVolumes;
        public bool ShowForceFields => _showVolumes != VolumeDisplay.Portal;
        public bool ScanVisor => _scanVisor;
        public Vector3 Light1Vector => _light1Vector;
        public Vector3 Light1Color => _light1Color;
        public Vector3 Light2Vector => _light2Vector;
        public Vector3 Light2Color => _light2Color;

        private readonly KeyboardState _keyboardState;
        private readonly Action<string> _setTitle;

        public NewScene(Vector2i size, KeyboardState keyboardState, Action<string> setTitle)
        {
            Size = size;
            _keyboardState = keyboardState;
            _setTitle = setTitle;
        }

        public void AddRoom(string name, GameMode mode = GameMode.None, int playerCount = 0,
            BossFlags bossFlags = BossFlags.None, int nodeLayerMask = 0, int entityLayerId = -1)
        {
            if (_roomLoaded)
            {
                throw new ProgramException("Cannot load more than one room in a scene.");
            }
            _roomLoaded = true;
            (RoomEntity room, RoomMetadata meta, CollisionInfo collision, IReadOnlyList<EntityBase> entities, int updatedMask)
                = SceneSetup.LoadNewRoom(name, mode, playerCount, bossFlags, nodeLayerMask, entityLayerId);
            _renderables.Add(room);
            _entities.Add(room);
            InitRenderable(room);
            _cameraMode = CameraMode.Roam;
            if (meta.InGameName != null)
            {
                _setTitle.Invoke(meta.InGameName);
            }
            foreach (EntityBase entity in entities)
            {
                _renderables.Add(entity);
                _entities.Add(entity);
                Debug.Assert(entity.Id != -1);
                _entityMap.Add(entity.Id, entity);
                InitRenderable(entity);
            }
            // todo: move more stuff to mutable class state
            //if (_lastPointModule != -1)
            //{
            //    ushort nextId = entities[_lastPointModule].Entity!.GetChildId();
            //    for (int i = 0; i < 5; i++)
            //    {
            //        Model model = entities[nextId];
            //        model.ScanVisorOnly = false;
            //        nextId = model.Entity!.GetChildId();
            //    }
            //}
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
            //Model dblDmgModel = Read.GetModelByName("doubleDamage_img");
            //BindTexture(dblDmgModel, 0, 0);
            //_dblDmgBindingId = _textureCount;
        }

        public void AddEntity(string name, int recolor = 0)
        {
            var entity = new ModelEntity(Read.GetNewModel(name), recolor);
            _renderables.Add(entity);
            _entities.Add(entity);
            if (entity.Id != -1)
            {
                _entityMap.Add(entity.Id, entity);
            }
            InitRenderable(entity);
        }

        public void OnLoad()
        {
            GL.ClearColor(_clearColor);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Texture2D);
            GL.DepthFunc(DepthFunction.Lequal);
            InitShaders();
            //AllocateEffects();
            for (int i = 0; i < _renderItemAlloc; i++)
            {
                _freeRenderItems.Enqueue(new RenderItem());
            }
            for (int i = 0; i < _entities.Count; i++)
            {
                _entities[i].Init(this);
            }
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

        private void InitRenderable(IRenderable renderable)
        {
            foreach (NewModel model in renderable.GetModels())
            {
                InitTextures(model);
                GenerateLists(model, isRoom: renderable.Type == NewEntityType.Room);
            }
        }

        private void GenerateLists(NewModel model, bool isRoom)
        {
            var tempListIds = new Dictionary<int, int>();
            foreach (Mesh mesh in model.Meshes)
            {
                if (!tempListIds.TryGetValue(mesh.DlistId, out int listId))
                {
                    int textureWidth = 0;
                    int textureHeight = 0;
                    Material material = model.Materials[mesh.MaterialId];
                    if (material.TextureId != UInt16.MaxValue)
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

        private void DoDlist(NewModel model, Mesh mesh, int textureWidth, int textureHeight, bool texgen, bool isRoom)
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

        private void InitTextures(NewModel model)
        {
            if (_texPalMap.ContainsKey(model.Id))
            {
                return;
            }
            var combos = new HashSet<(int, int, int)>();
            foreach (Material material in model.Materials)
            {
                if (material.TextureId == UInt16.MaxValue)
                {
                    continue;
                }
                if (material.RenderMode == RenderMode.Unknown3 || material.RenderMode == RenderMode.Unknown4)
                {
                    //_logs.Add($"mat {material.Name} of model {model.Name} has render mode {material.RenderMode}");
                    material.RenderMode = RenderMode.Normal;
                }
                for (int i = 0; i < model.Recolors.Count; i++)
                {
                    combos.Add((material.TextureId, material.PaletteId, i));
                }
            }
            foreach (TextureAnimationGroup group in model.Animations.TextureGroups)
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
                var map = new NewTextureMap();
                foreach ((int textureId, int paletteId, int recolorId) in combos)
                {
                    bool onlyOpaque = BindTexture(model, textureId, paletteId, recolorId);
                    map.Add(textureId, paletteId, recolorId, _textureCount, onlyOpaque);
                }
                _texPalMap.Add(model.Id, map);
            }
        }

        private bool BindTexture(NewModel model, int textureId, int paletteId, int recolorId)
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

        public void UpdateMaterials(NewModel model, int recolorId)
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

        public void OnRenderFrame(double frameTime)
        {
            _frameCount++;
            _frameTime = (float)frameTime;
            //LoadAndUnload();
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

            RenderScene();
        }

        public void AfterRenderFrame()
        {
            if (_recording)
            {
                Images.Screenshot(Size.X, Size.Y, $"frame{_framesRecorded:0000}");
                _framesRecorded++;
            }
            if (_advanceOneFrame)
            {
                _advanceOneFrame = false;
            }
        }

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
            //  todo: position calculation is off -- doesn't work for portal alpha
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

        private int CompareEntities(EntityBase one, EntityBase two)
        {
            if (one.Type == NewEntityType.Room)
            {
                if (two.Type != NewEntityType.Room)
                {
                    return -1;
                }
                return 0;
            }
            if (two.Type == NewEntityType.Room)
            {
                return 1;
            }
            float distanceOne = Vector3.DistanceSquared(-1 * _cameraPosition, one.Position);
            float distanceTwo = Vector3.DistanceSquared(-1 * _cameraPosition, two.Position);
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

        public void AddRenderItem(Material material, int polygonId, float alphaScale, Vector3 emission, LightInfo lightInfo,
            Matrix4 texcoordMatrix, Matrix4 transform, int listId, int matrixStackCount, IReadOnlyList<float> matrixStack,
            Vector4? overrideColor)
        {
            RenderItem item = GetRenderItem();
            item.Type = RenderItemType.Mesh;
            item.PolygonId = polygonId;
            item.Alpha = material.CurrentAlpha * alphaScale;
            item.PolygonMode = material.PolygonMode;
            item.RenderMode = material.RenderMode;
            item.CullingMode = material.Culling;
            item.Wireframe = material.Wireframe != 0;
            item.Lighting = material.Lighting != 0;
            item.Diffuse = material.CurrentDiffuse;
            item.Ambient = material.CurrentAmbient;
            item.Specular = material.CurrentSpecular;
            item.Emission = emission;
            item.LightInfo = lightInfo;
            item.TexgenMode = material.TexgenMode;
            item.XRepeat = material.XRepeat;
            item.YRepeat = material.YRepeat;
            item.HasTexture = material.TextureId != UInt16.MaxValue;
            item.TextureBindingId = material.TextureBindingId;
            item.TexcoordMatrix = texcoordMatrix;
            item.Transform = transform;
            item.ListId = listId;
            Debug.Assert(matrixStack.Count == 16 * matrixStackCount);
            item.MatrixStackCount = matrixStackCount;
            for (int i = 0; i < matrixStack.Count; i++)
            {
                item.MatrixStack[i] = matrixStack[i];
            }
            item.OverrideColor = overrideColor;
            item.Vertices = Array.Empty<Vector3>();
            AddRenderItem(item);
        }

        public void AddRenderItem(CullingMode cullingMode, int polygonId, Vector3 overrideColor, VolumeType type, Vector3[] vertices)
        {
            RenderItem item = GetRenderItem();
            item.Type = (RenderItemType)(type + 1);
            item.PolygonId = polygonId;
            item.Alpha = 1;
            item.PolygonMode = PolygonMode.Modulate;
            item.RenderMode = RenderMode.Translucent;
            item.CullingMode = cullingMode;
            item.Wireframe = false;
            item.Lighting = false;
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
            item.OverrideColor = new Vector4(overrideColor, 0.5f);
            item.Vertices = vertices;
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

        private void RenderScene()
        {
            if (_frameCount != 0 || !_frameAdvanceOn || _advanceOneFrame)
            {
                _elapsedTime += 1 / 60f;
                //_singleParticleCount = 0;
            }
            _decalItems.Clear();
            _nonDecalItems.Clear();
            _translucentItems.Clear();
            while (_usedRenderItems.Count > 0)
            {
                RenderItem item = _usedRenderItems.Dequeue();
                if (item.Type != RenderItemType.Mesh)
                {
                    ArrayPool<Vector3>.Shared.Return(item.Vertices);
                }
                _freeRenderItems.Enqueue(item);
            }
            _entities.Sort(CompareEntities);
            _nextPolygonId = 1;

            for (int i = 0; i < _entities.Count; i++)
            {
                EntityBase entity = _entities[i];
                if (_frameCount == 0 || !_frameAdvanceOn || _advanceOneFrame)
                {
                    entity.Process(this);
                }
                if (entity.ShouldDraw)
                {
                    entity.UpdateTransforms(this);
                    entity.GetDrawInfo(this);
                }
                if (_showVolumes != VolumeDisplay.None)
                {
                    entity.GetDisplayVolumes(this);
                }
            }

            //if (_frameCount == 0 || !_frameAdvanceOn || _advanceOneFrame)
            //{
            //    ProcessEffects();
            //    var camVec1 = new Vector3(_viewMatrix.M11, _viewMatrix.M12, _viewMatrix.M13 * -1);
            //    var camVec2 = new Vector3(_viewMatrix.M21, _viewMatrix.M22, _viewMatrix.M23 * -1);
            //    var camVec3 = new Vector3(_viewMatrix.M31, _viewMatrix.M32, _viewMatrix.M33 * -1);
            //    for (int i = 0; i < _singleParticleCount; i++)
            //    {
            //        SingleParticle single = _singleParticles[i];
            //        single.Process(camVec1, camVec2, camVec3, 1);
            //    }
            //}
            //for (int i = 0; i < _activeElements.Count; i++)
            //{
            //    EffectElementEntry element = _activeElements[i];
            //    for (int j = 0; j < element.Particles.Count; j++)
            //    {
            //        EffectParticle particle = element.Particles[j];
            //        Matrix4 matrix = _viewMatrix;
            //        if ((particle.Owner.Flags & 1) != 0 && (particle.Owner.Flags & 4) == 0)
            //        {
            //            matrix = particle.Owner.Transform * matrix;
            //        }
            //        particle.InvokeSetVecsFunc(matrix);
            //        particle.InvokeDrawFunc(1);
            //        if (particle.ShouldDraw)
            //        {
            //            Material material = particle.Owner.Model.Materials[particle.MaterialId];
            //            MeshInfo meshInfo;
            //            if (particle.DrawNode)
            //            {
            //                Model model = particle.Owner.Model;
            //                Node node = particle.Owner.Nodes[particle.ParticleId];
            //                Mesh mesh = particle.Owner.Model.Meshes[node.MeshId / 2];
            //                meshInfo = new MeshInfo(particle, model, node, mesh, material, polygonId++, particle.Alpha);
            //            }
            //            else
            //            {
            //                meshInfo = new MeshInfo(particle, material, polygonId++, particle.Alpha);
            //            }
            //            _nonDecalMeshes.Add(meshInfo);
            //            _translucentMeshes.Add(meshInfo);
            //        }
            //    }
            //}
            //for (int i = 0; i < _singleParticleCount; i++)
            //{
            //    SingleParticle single = _singleParticles[i];
            //    if (single.ShouldDraw)
            //    {
            //        Model model = single.ParticleDefinition.Model;
            //        Node node = single.ParticleDefinition.Node;
            //        Mesh mesh = model.Meshes[node.MeshId / 2];
            //        Material material = model.Materials[single.ParticleDefinition.MaterialId];
            //        var meshInfo = new MeshInfo(single, model, node, mesh, material, polygonId++, single.Alpha);
            //        _nonDecalMeshes.Add(meshInfo);
            //        _translucentMeshes.Add(meshInfo);
            //    }
            //}

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

        private void RenderItem(RenderItem item)
        {
            RenderMesh(item);
        }

        private void RenderMesh(RenderItem item)
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
                RenderBox(item.Vertices);
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
            Vector4? overrideColor = null;
            //if (_showSelection)
            //{
            //    overrideColor = mesh.OverrideColor;
            //}
            if (overrideColor == null)
            {
                overrideColor = item.OverrideColor;
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
            //if (model.PaletteOverride != null)
            //{
            //    Vector4 overrideColorValue = model.PaletteOverride.Value;
            //    GL.Uniform1(_shaderLocations.UsePaletteOverride, 1);
            //    GL.Uniform4(_shaderLocations.PaletteOverrideColor, ref overrideColorValue);
            //}
            //else
            //{
            //    GL.Uniform1(_shaderLocations.UsePaletteOverride, 0);
            //}
            GL.Uniform1(_shaderLocations.UsePaletteOverride, 0);
        }

        public void OnMouseClick(bool down)
        {
            _leftMouse = down;
        }

        public void OnMouseMove(float deltaX, float deltaY)
        {
            if (_leftMouse)
            {
                _angleX += deltaY / 1.5f;
                _angleX = Math.Clamp(_angleX, -90.0f, 90.0f);
                _angleY += deltaX / 1.5f;
                _angleY %= 360f;
            }
        }

        public void OnMouseWheel(float offsetY)
        {
            if (_cameraMode == CameraMode.Pivot)
            {
                float delta = _wheelOffset - offsetY;
                _distance += delta / 1.5f;
                _wheelOffset = offsetY;
            }
        }

        public void OnKeyDown(KeyboardKeyEventArgs e)
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
            }
            else if (e.Key == Keys.C)
            {
                _showColors = !_showColors;
            }
            else if (e.Key == Keys.Q)
            {
                if (_showVolumes == VolumeDisplay.Portal)
                {
                    _portalEdges = !_portalEdges;
                }
                else
                {
                    _wireframe = !_wireframe;
                }
            }
            else if (e.Key == Keys.B)
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
                // todo: this needs to be organized
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
                    //if (_showVolumes != 0 && _selectionMode == SelectionMode.Model)
                    //{
                    //    int previousSelection = _selectedModelId;
                    //    Deselect();
                    //    _selectedModelId = 0;
                    //    SelectNextModel();
                    //    if (!_modelMap.ContainsKey(_selectedModelId))
                    //    {
                    //        _selectedModelId = previousSelection;
                    //        SetSelectedModel(previousSelection);
                    //    }
                    //}
                }
                else
                {
                    _showVolumes++;
                    if (_showVolumes > VolumeDisplay.Portal)
                    {
                        _showVolumes = VolumeDisplay.None;
                    }
                    //if (_showVolumes != 0 && _selectionMode == SelectionMode.Model)
                    //{
                    //    int previousSelection = _selectedModelId;
                    //    Deselect();
                    //    _selectedModelId = 0;
                    //    SelectNextModel();
                    //    if (!_modelMap.ContainsKey(_selectedModelId))
                    //    {
                    //        _selectedModelId = previousSelection;
                    //        SetSelectedModel(previousSelection);
                    //    }
                    //}
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
                _transformRoomNodes = !_transformRoomNodes;
            }
            else if (e.Key == Keys.H)
            {
                if (e.Alt)
                {
                    _showKillPlane = !_showKillPlane;
                }
                else
                {
                    //_showSelection = !_showSelection;
                }
            }
            else if (e.Key == Keys.I)
            {
                _showInvisible = !_showInvisible;
            }
            else if (e.Key == Keys.E)
            {
                if (e.Alt)
                {
                    // undocumented -- might not be needed once we have an animation index setter
                    //if (_selectionMode == SelectionMode.Model
                    //    && SelectedModel.Entity is Entity<ObjectEntityData> obj && obj.Data.EffectId != 0)
                    //{
                    //    ((ObjectModel)SelectedModel).ForceSpawnEffect = true;
                    //}
                }
                else
                {
                    _scanVisor = !_scanVisor;
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
            }
            else if (e.Key == Keys.P)
            {
                if (e.Alt)
                {
                    //UpdatePointModule(); // undocumented
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
                //LoadModel();
            }
            else if (e.Control && e.Key == Keys.U)
            {
                //UnloadModel();
            }
            else if (e.Key == Keys.M)
            {
                //if (_models.Any(m => m.Meshes.Count > 0))
                //{
                //    if (e.Control)
                //    {
                //        if (_selectionMode == SelectionMode.Model)
                //        {
                //            InputSelectModel();
                //        }
                //        else if (_selectionMode == SelectionMode.Node)
                //        {
                //            InputSelectNode();
                //        }
                //        else if (_selectionMode == SelectionMode.Mesh)
                //        {
                //            InputSelectMesh();
                //        }
                //    }
                //    else
                //    {
                //        Deselect();
                //        if (_selectedModelId < 0)
                //        {
                //            _selectedModelId = _models[0].SceneId;
                //        }
                //        if (_selectionMode == SelectionMode.None)
                //        {
                //            _selectionMode = SelectionMode.Model;
                //            SetSelectedModel(_selectedModelId);
                //        }
                //        else if (_selectionMode == SelectionMode.Model)
                //        {
                //            _selectionMode = SelectionMode.Node;
                //            SetSelectedNode(_selectedModelId, _selectedNodeId);
                //        }
                //        else if (_selectionMode == SelectionMode.Node)
                //        {
                //            if (!SelectedModel.GetNodeMeshes(_selectedNodeId).Any())
                //            {
                //                _selectionMode = SelectionMode.None;
                //            }
                //            else
                //            {
                //                _selectionMode = SelectionMode.Mesh;
                //                SetSelectedMesh(_selectedModelId, _selectedMeshId);
                //            }
                //        }
                //        else
                //        {
                //            _selectionMode = SelectionMode.None;
                //        }
                //        PrintOutput();
                //    }
                //}
            }
            else if (e.Key == Keys.Equal || e.Key == Keys.KeyPadEqual)
            {
                //if (e.Alt)
                //{
                //    // todo: select other animation types, and enable playing in reverse
                //    if (_selectionMode == SelectionMode.Model && _modelMap.TryGetValue(_selectedModelId, out Model? model))
                //    {
                //        if (e.Control)
                //        {
                //            int id = model.Animations.MaterialGroupId + 1;
                //            if (id >= model.Animations.MaterialGroups.Count)
                //            {
                //                id = -1;
                //            }
                //            model.Animations.MaterialGroupId = id;
                //        }
                //        else
                //        {
                //            int id = model.Animations.NodeGroupId + 1;
                //            if (id >= model.Animations.NodeGroups.Count)
                //            {
                //                id = -1;
                //            }
                //            model.Animations.NodeGroupId = id;
                //        }
                //    }
                //}
                //else
                //{
                //    SelectNextModel(e.Shift);
                //}
            }
            else if (e.Key == Keys.Minus || e.Key == Keys.KeyPadSubtract)
            {
                //if (e.Alt)
                //{
                //    if (_selectionMode == SelectionMode.Model && _modelMap.TryGetValue(_selectedModelId, out Model? model))
                //    {
                //        if (e.Control)
                //        {
                //            int id = model.Animations.MaterialGroupId - 1;
                //            if (id < -1)
                //            {
                //                id = model.Animations.MaterialGroups.Count - 1;
                //            }
                //            model.Animations.MaterialGroupId = id;
                //        }
                //        else
                //        {
                //            int id = model.Animations.NodeGroupId - 1;
                //            if (id < -1)
                //            {
                //                id = model.Animations.NodeGroups.Count - 1;
                //            }
                //            model.Animations.NodeGroupId = id;
                //        }
                //    }
                //}
                //else
                //{
                //    SelectPreviousModel(e.Shift);
                //}
            }
            else if (e.Key == Keys.X)
            {
                //if (_selectionMode == SelectionMode.Model)
                //{
                //    if (e.Control)
                //    {
                //        if (SelectedModel.Entity != null)
                //        {
                //            if (e.Shift)
                //            {
                //                ushort childId = SelectedModel.Entity.GetChildId();
                //                Model? child = _models.FirstOrDefault(m => m.Entity?.EntityId == childId);
                //                if (child != null)
                //                {
                //                    LookAt(child.Position);
                //                }
                //            }
                //            else
                //            {
                //                ushort parentId = SelectedModel.Entity.GetParentId();
                //                Model? parent = _models.FirstOrDefault(m => m.Entity?.EntityId == parentId);
                //                if (parent != null)
                //                {
                //                    LookAt(parent.Position);
                //                }
                //            }
                //        }
                //    }
                //    else
                //    {
                //        LookAt(SelectedModel.Position);
                //    }
                //}
                //else if (_selectionMode == SelectionMode.Node || _selectionMode == SelectionMode.Mesh)
                //{
                //    LookAt(SelectedModel.Nodes[_selectedNodeId].Position);
                //}
            }
            else if (e.Key == Keys.D0 || e.Key == Keys.KeyPad0)
            {
                //if (_selectionMode == SelectionMode.Model)
                //{
                //    SelectedModel.Visible = !SelectedModel.Visible;
                //}
                //else if (_selectionMode == SelectionMode.Node)
                //{
                //    SelectedModel.Nodes[_selectedNodeId].Enabled = !SelectedModel.Nodes[_selectedNodeId].Enabled;
                //}
                //else if (_selectionMode == SelectionMode.Mesh)
                //{
                //    SelectedModel.Meshes[_selectedMeshId].Visible = !SelectedModel.Meshes[_selectedMeshId].Visible;
                //}
            }
            else if (e.Key == Keys.D1 || e.Key == Keys.KeyPad1)
            {
                //if (_selectionMode == SelectionMode.Model && SelectedModel.Recolors.Count > 1)
                //{
                //    int recolor = SelectedModel.CurrentRecolor - 1;
                //    if (recolor < 0)
                //    {
                //        recolor = SelectedModel.Recolors.Count - 1;
                //    }
                //    SelectedModel.CurrentRecolor = recolor;
                //    DeleteTextures(SelectedModel.SceneId);
                //    InitTextures(SelectedModel);
                //}
            }
            else if (e.Key == Keys.D2 || e.Key == Keys.KeyPad2)
            {
                //if (_selectionMode == SelectionMode.Model && SelectedModel.Recolors.Count > 1)
                //{
                //    int recolor = SelectedModel.CurrentRecolor + 1;
                //    if (recolor > SelectedModel.Recolors.Count - 1)
                //    {
                //        recolor = 0;
                //    }
                //    SelectedModel.CurrentRecolor = recolor;
                //    DeleteTextures(SelectedModel.SceneId);
                //    InitTextures(SelectedModel);
                //}
            }
        }

        private void OnKeyHeld()
        {
            //if ((_keyboardState.IsKeyDown(Keys.LeftAlt) || _keyboardState.IsKeyDown(Keys.RightAlt))
            //    && _selectionMode == SelectionMode.Model)
            //{
            //    MoveModel();
            //    return;
            //}
            // sprint
            float step = _keyboardState.IsKeyDown(Keys.LeftShift) || _keyboardState.IsKeyDown(Keys.RightShift) ? 5 : 1;
            if (_cameraMode == CameraMode.Roam)
            {
                if (_keyboardState.IsKeyDown(Keys.W)) // move forward
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
                else if (_keyboardState.IsKeyDown(Keys.S)) // move backward
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
                if (_keyboardState.IsKeyDown(Keys.Space)) // move up
                {
                    _cameraPosition = new Vector3(_cameraPosition.X, _cameraPosition.Y - step * 0.1f, _cameraPosition.Z);
                }
                else if (_keyboardState.IsKeyDown(Keys.V)) // move down
                {
                    _cameraPosition = new Vector3(_cameraPosition.X, _cameraPosition.Y + step * 0.1f, _cameraPosition.Z);
                }
                if (_keyboardState.IsKeyDown(Keys.A)) // move left
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
                else if (_keyboardState.IsKeyDown(Keys.D)) // move right
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
                step = _keyboardState.IsKeyDown(Keys.LeftShift) || _keyboardState.IsKeyDown(Keys.RightShift) ? -3 : -1.5f;
            }
            if (_keyboardState.IsKeyDown(Keys.Up)) // rotate up
            {
                _angleX += step;
                _angleX = Math.Clamp(_angleX, -90.0f, 90.0f);
            }
            else if (_keyboardState.IsKeyDown(Keys.Down)) // rotate down
            {
                _angleX -= step;
                _angleX = Math.Clamp(_angleX, -90.0f, 90.0f);
            }
            if (_keyboardState.IsKeyDown(Keys.Left)) // rotate left
            {
                _angleY += step;
                _angleY %= 360f;
            }
            else if (_keyboardState.IsKeyDown(Keys.Right)) // rotate right
            {
                _angleY -= step;
                _angleY %= 360f;
            }
        }

        private enum CameraMode
        {
            Pivot,
            Roam
        }
    }

    public class NewRenderWindow : GameWindow
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

        public NewScene Scene { get; }

        public NewRenderWindow() : base(_gameWindowSettings, _nativeWindowSettings)
        {
            Scene = new NewScene(Size, KeyboardState, (string title) =>
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

        public void AddEntity(string name, int recolor = 0)
        {
            Scene.AddEntity(name, recolor);
        }

        protected override void OnLoad()
        {
            Scene.OnLoad();
            base.OnLoad();
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            Scene.OnRenderFrame(args.Time);
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

    public class NewTextureMap : Dictionary<int, (int BindingId, bool OnlyOpaque)>
    {
        private int GetKey(int textureId, int paletteId, int recolorId)
        {
            if (paletteId == UInt16.MaxValue)
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
