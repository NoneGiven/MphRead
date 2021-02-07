using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Entities;
using MphRead.Export;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace MphRead
{
    public class NewScene
    {
        public Vector2i Size { get; set; }
        private Matrix4 _viewMatrix = Matrix4.Identity;
        private Matrix4 _viewInvRotMatrix = Matrix4.Identity;
        private Matrix4 _viewInvRotYMatrix = Matrix4.Identity;
        public Matrix4 ViewMatrix => _viewMatrix;
        public Matrix4 ViewInvRotMatrix => _viewInvRotMatrix;
        public Matrix4 ViewInvRotYMatrix => _viewInvRotYMatrix;

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
        private bool _volumeEdges = false;
        private bool _faceCulling = true;
        private bool _textureFiltering = false;
        private bool _lighting = false;
        private bool _scanVisor = false;
        private bool _showInvisible = false;
        private int _showVolumes = 0;
        private bool _showKillPlane = false;
        private bool _transformRoomNodes = false;

        public bool ShowInvisible => _showInvisible;
        public bool TransformRoomNodes => _transformRoomNodes;

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

        private float _elapsedTime = 0;
        private long _frameCount = -1;
        public long FrameCount => _frameCount;
        private bool _frameAdvanceOn = false;
        private bool _advanceOneFrame = false;
        private bool _recording = false;
        private int _framesRecorded = 0;

        private readonly KeyboardState _keyboardState;

        public NewScene(Vector2i size, KeyboardState keyboardState)
        {
            Size = size;
            _keyboardState = keyboardState;
        }

        public void AddRoom(string name)
        {
        }

        public void AddEntity(string name)
        {
            var entity = new ModelEntity(Read.GetNewModel(name));
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
                UpdateMaterials(model, renderable.Recolor);
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
                onlyOpaque &= pixel.Alpha < 255;
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

        public void OnRenderFrame()
        {
            _frameCount++;
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

        // avoiding overhead by duplicating things in these lists
        private readonly List<RenderItem> _decalMeshes = new List<RenderItem>();
        private readonly List<RenderItem> _nonDecalMeshes = new List<RenderItem>();
        private readonly List<RenderItem> _translucentMeshes = new List<RenderItem>();

        private int _nextPolygonId = 1;

        public int GetNextPolygonId()
        {
            return _nextPolygonId++;
        }

        public void AddRenderItem(RenderItem item)
        {
            if (item.RenderMode == RenderMode.Decal)
            {
                _decalMeshes.Add(item);
            }
            else
            {
                _nonDecalMeshes.Add(item);
            }
            if (item.RenderMode == RenderMode.Translucent || item.Alpha < 1)
            {
                _translucentMeshes.Add(item);
            }
        }

        private void RenderScene()
        {
            //if (!_effectSetupDone)
            //{
            //    AllocateEffects();
            //    _effectSetupDone = true;
            //}
            if (!_frameAdvanceOn || _advanceOneFrame)
            {
                _elapsedTime += 1 / 60f;
            }
            //if (!_frameAdvanceOn || _advanceOneFrame)
            //{
            //    _singleParticleCount = 0;
            //}
            _decalMeshes.Clear();
            _nonDecalMeshes.Clear();
            _translucentMeshes.Clear();
            _entities.Sort(CompareEntities);

            for (int i = 0; i < _entities.Count; i++)
            {
                EntityBase entity = _entities[i];
                if (!_frameAdvanceOn || _advanceOneFrame)
                {
                    entity.Process(this);
                }
                if (!entity.ShouldDraw)
                {
                    continue;
                }
                entity.GetDrawInfo(this);
            }

            //if (!_frameAdvanceOn || _advanceOneFrame)
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
            for (int i = 0; i < _nonDecalMeshes.Count; i++)
            {
                RenderItem item = _nonDecalMeshes[i];
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
                RenderItem item = _decalMeshes[i];
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
                RenderItem item = _translucentMeshes[i];
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
                RenderItem item = _nonDecalMeshes[i];
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
                RenderItem item = _translucentMeshes[i];
                GL.StencilFunc(StencilFunction.Notequal, item.PolygonId, 0xFF);
                RenderItem(item);
            }
            // pass 6: translucent (before)
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
            for (int i = 0; i < _translucentMeshes.Count; i++)
            {
                RenderItem item = _translucentMeshes[i];
                GL.StencilFunc(StencilFunction.Equal, item.PolygonId, 0xFF);
                RenderItem(item);
            }
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.AlphaTest);
            GL.Disable(EnableCap.StencilTest);
            //RenderDisplayVolumes();
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

        private void RenderItem(RenderItem item)
        {
            RenderMesh(item);
        }

        private void RenderMesh(RenderItem item)
        {
            UseRoomLights();
            //if (model.UseLightSources || model.UseLightOverride)
            //{
            //    UseLight1(model.Light1Vector, model.Light1Color);
            //    UseLight2(model.Light2Vector, model.Light2Color);
            //}

            // mtodo: need to handle matrix stack values
            Matrix4 transform = item.Transform;
            GL.UniformMatrix4(_shaderLocations.MatrixStack, transpose: false, ref transform);

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
            GL.CallList(item.ListId);
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
            //Vector4? overrideColor = null;
            //if (_showSelection)
            //{
            //    overrideColor = mesh.OverrideColor;
            //}
            //if (overrideColor == null)
            //{
            //    overrideColor = mesh.PlaceholderColor;
            //}
            //if (overrideColor != null)
            //{
            //    Vector4 overrideColorValue = overrideColor.Value;
            //    GL.Uniform1(_shaderLocations.UseOverride, 1);
            //    GL.Uniform4(_shaderLocations.OverrideColor, ref overrideColorValue);
            //}
            //else
            //{
            //    GL.Uniform1(_shaderLocations.UseOverride, 0);
            //}
            GL.Uniform1(_shaderLocations.UseOverride, 0);
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
                if (_showVolumes > 0)
                {
                    _volumeEdges = !_volumeEdges;
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
                    _showVolumes = 0;
                }
                else if (e.Shift)
                {
                    _showVolumes--;
                    if (_showVolumes < 0)
                    {
                        _showVolumes = 12;
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
                    if (_showVolumes > 12)
                    {
                        _showVolumes = 0;
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
            Scene = new NewScene(Size, KeyboardState);
        }

        public void AddEntity(string name)
        {
            Scene.AddEntity(name);
        }

        protected override void OnLoad()
        {
            Scene.OnLoad();
            base.OnLoad();
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
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

    public class NewTextureMap : Dictionary<int, (int BindingId, bool OnlyOpaque)>
    {
        private int GetKey(int textureId, int paletteId, int recolorId)
        {
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
