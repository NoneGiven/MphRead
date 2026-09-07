#if ANDROID
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ES = OpenTK.Graphics.ES30;

namespace MphRead.Mods.Render
{
    /// <summary>
    /// The engine's <c>GL</c>, on OpenGL ES 3.0.
    ///
    /// The Android head compiles the shared sources with a global using alias
    /// pointing <c>GL</c> at this class, so every <c>GL.Something</c> in the
    /// renderer, the movie decoder and the capture code lands here instead of
    /// in OpenTK's desktop bindings, with no edit to any of them. The arguments
    /// stay the desktop enum types -- they are the same GL constants, and
    /// keeping them means the call sites do not change.
    ///
    /// Four things the desktop renderer uses do not exist on ES, and this class
    /// is where each of them is answered:
    ///
    /// - **Immediate mode.** <c>Begin</c>/<c>Vertex3</c>/<c>End</c> accumulate
    ///   into a vertex buffer instead of a command stream. Quads, quad strips,
    ///   triangle strips and fans are turned into indexed triangles, since ES
    ///   draws neither quads nor anything else the DS geometry engine emitted.
    /// - **Display lists.** <c>NewList</c>/<c>EndList</c> bake that buffer into
    ///   a VBO, an index buffer and a VAO; <c>CallList</c> is one
    ///   <c>glDrawElements</c>. This is what the display list was for and it is
    ///   the same trade -- build once at load, draw cheaply forever.
    /// - **The current colour.** A vertex with no colour of its own takes the
    ///   colour current at *execution* time, which for a display list is the
    ///   <c>GL.Color3</c> the engine issues per render item. Vertices carry a
    ///   flag for whether they had their own; the ones that did not read the
    ///   <c>imm_color</c> uniform. See <see cref="EsShaders"/>.
    /// - **The alpha test.** <c>glAlphaFunc</c> is fixed-function. The engine
    ///   asks for two comparisons and the fragment shader discards on them.
    ///
    /// And one thing is bookkeeping rather than emulation: the engine binds
    /// texture names it made up itself (<c>_textureCount</c>), which a
    /// compatibility profile allows and ES does not. Those names are kept as a
    /// map to real ones, created on first bind.
    ///
    /// What is lost: <c>glPolygonMode</c>, so the wireframe and collision-volume
    /// debug views draw solid. Nothing a player sees uses it.
    /// </summary>
    internal static class GlEs
    {
        // 0..2 position, 3..6 colour, 7..9 normal, 10..12 texcoord + matrix id, 13 "had its own colour"
        private const int FloatsPerVertex = 14;
        private const int Stride = FloatsPerVertex * sizeof(float);

        private sealed class Batch
        {
            public readonly List<float> Vertices = new List<float>(4096);
            public readonly List<int> TriIndices = new List<int>(4096);
            public readonly List<int> LineIndices = new List<int>();
            public int VertexCount;

            public void Clear()
            {
                Vertices.Clear();
                TriIndices.Clear();
                LineIndices.Clear();
                VertexCount = 0;
            }
        }

        private sealed class CompiledList
        {
            public int Vao;
            public int Vbo;
            public int Ibo;
            public int TriCount;
            public int LineCount;
        }

        // ---- current vertex state, the same state machine fixed-function GL keeps ----
        private static Vector4 _curColor = new Vector4(1, 1, 1, 1);
        private static Vector3 _curNormal = new Vector3(0, 0, 1);
        private static Vector3 _curTexCoord = Vector3.Zero;
        private static bool _colorSet = false;

        private static OpenTK.Graphics.OpenGL.PrimitiveType _primMode;
        private static int _primStart;

        private static readonly Batch _batch = new Batch();
        private static bool _recording;
        private static int _recordListId;

        private static readonly Dictionary<int, CompiledList> _lists = new Dictionary<int, CompiledList>();
        private static int _nextListId = 1;

        // ---- the buffers the non-list Begin/End pairs draw through ----
        private static int _dynVao;
        private static int _dynVbo;
        private static int _dynIbo;
        private static int _dynVboSize;
        private static int _dynIboSize;

        // ---- state the shaders have to be told about ----
        private static bool _alphaTestEnabled;
        private static AlphaFunction _alphaFunc = AlphaFunction.Always;
        private static int _program;
        private static int _immColorLoc = -1;
        private static int _alphaTestLoc = -1;
        private static readonly Dictionary<int, (int ImmColor, int AlphaTest)> _programLocs
            = new Dictionary<int, (int, int)>();

        // ---- the engine's own texture names, mapped to real ones ----
        private static readonly Dictionary<int, int> _textures = new Dictionary<int, int>();
        private static int _textureHighWater;

        /// <summary>Drop every GL object this class owns. For a lost context.</summary>
        public static void Reset()
        {
            _lists.Clear();
            _textures.Clear();
            _programLocs.Clear();
            _nextListId = 1;
            _textureHighWater = 0;
            _dynVao = _dynVbo = _dynIbo = 0;
            _dynVboSize = _dynIboSize = 0;
            _program = 0;
            _immColorLoc = -1;
            _alphaTestLoc = -1;
            _batch.Clear();
            _recording = false;
        }

        #region immediate mode

        public static void Begin(OpenTK.Graphics.OpenGL.PrimitiveType mode)
        {
            if (!_recording)
            {
                _batch.Clear();
                // Outside a list there is nothing to inherit from: a vertex with
                // no colour of its own reads the uniform, which is this colour.
                _colorSet = false;
            }
            _primMode = mode;
            _primStart = _batch.VertexCount;
        }

        public static void End()
        {
            int count = _batch.VertexCount - _primStart;
            EmitIndices(_primMode, _primStart, count);
            if (!_recording)
            {
                FlushDynamic();
            }
        }

        public static void Vertex3(float x, float y, float z)
        {
            List<float> v = _batch.Vertices;
            v.Add(x);
            v.Add(y);
            v.Add(z);
            v.Add(_curColor.X);
            v.Add(_curColor.Y);
            v.Add(_curColor.Z);
            v.Add(_curColor.W);
            v.Add(_curNormal.X);
            v.Add(_curNormal.Y);
            v.Add(_curNormal.Z);
            v.Add(_curTexCoord.X);
            v.Add(_curTexCoord.Y);
            v.Add(_curTexCoord.Z);
            v.Add(_colorSet ? 1f : 0f);
            _batch.VertexCount++;
        }

        public static void Vertex3(Vector3 vector)
        {
            Vertex3(vector.X, vector.Y, vector.Z);
        }

        public static void Color3(float r, float g, float b)
        {
            _curColor = new Vector4(r, g, b, 1f);
            _colorSet = true;
        }

        public static void Color3(Vector3 color)
        {
            Color3(color.X, color.Y, color.Z);
        }

        public static void Color4(float r, float g, float b, float a)
        {
            _curColor = new Vector4(r, g, b, a);
            _colorSet = true;
        }

        public static void Normal3(float x, float y, float z)
        {
            _curNormal = new Vector3(x, y, z);
        }

        public static void TexCoord3(float s, float t, float matrixId)
        {
            _curTexCoord = new Vector3(s, t, matrixId);
        }

        public static void TexCoord3(Vector3 texcoord)
        {
            _curTexCoord = texcoord;
        }

        private static void EmitIndices(OpenTK.Graphics.OpenGL.PrimitiveType mode, int b, int n)
        {
            List<int> tris = _batch.TriIndices;
            switch (mode)
            {
            case OpenTK.Graphics.OpenGL.PrimitiveType.Triangles:
                for (int i = 0; i + 2 < n; i += 3)
                {
                    tris.Add(b + i);
                    tris.Add(b + i + 1);
                    tris.Add(b + i + 2);
                }
                break;
            case OpenTK.Graphics.OpenGL.PrimitiveType.Quads:
                for (int i = 0; i + 3 < n; i += 4)
                {
                    tris.Add(b + i);
                    tris.Add(b + i + 1);
                    tris.Add(b + i + 2);
                    tris.Add(b + i);
                    tris.Add(b + i + 2);
                    tris.Add(b + i + 3);
                }
                break;
            case OpenTK.Graphics.OpenGL.PrimitiveType.TriangleStrip:
                // every other triangle is wound the other way, which the strip
                // primitive does for you and independent triangles do not
                for (int i = 0; i + 2 < n; i++)
                {
                    if ((i & 1) == 0)
                    {
                        tris.Add(b + i);
                        tris.Add(b + i + 1);
                        tris.Add(b + i + 2);
                    }
                    else
                    {
                        tris.Add(b + i + 1);
                        tris.Add(b + i);
                        tris.Add(b + i + 2);
                    }
                }
                break;
            case OpenTK.Graphics.OpenGL.PrimitiveType.QuadStrip:
                // vertices arrive in pairs: quad k is (2k, 2k+1, 2k+3, 2k+2)
                for (int i = 0; i + 3 < n; i += 2)
                {
                    tris.Add(b + i);
                    tris.Add(b + i + 1);
                    tris.Add(b + i + 3);
                    tris.Add(b + i);
                    tris.Add(b + i + 3);
                    tris.Add(b + i + 2);
                }
                break;
            case OpenTK.Graphics.OpenGL.PrimitiveType.TriangleFan:
                for (int i = 1; i + 1 < n; i++)
                {
                    tris.Add(b);
                    tris.Add(b + i);
                    tris.Add(b + i + 1);
                }
                break;
            case OpenTK.Graphics.OpenGL.PrimitiveType.LineLoop:
                {
                    List<int> lines = _batch.LineIndices;
                    for (int i = 0; i < n; i++)
                    {
                        lines.Add(b + i);
                        lines.Add(b + (i + 1) % n);
                    }
                }
                break;
            default:
                throw new ProgramException($"No ES translation for primitive type {mode}.");
            }
        }

        #endregion

        #region display lists

        public static int GenLists(int range)
        {
            int id = _nextListId;
            _nextListId += range;
            return id;
        }

        public static void NewList(int list, ListMode mode)
        {
            _batch.Clear();
            _recording = true;
            _recordListId = list;
            // A list does not inherit the colour current when it was compiled;
            // it inherits the one current when it is called.
            _colorSet = false;
        }

        public static unsafe void EndList()
        {
            _recording = false;
            var compiled = new CompiledList
            {
                TriCount = _batch.TriIndices.Count,
                LineCount = _batch.LineIndices.Count
            };
            if (compiled.TriCount == 0 && compiled.LineCount == 0)
            {
                _lists[_recordListId] = compiled;
                _batch.Clear();
                return;
            }
            compiled.Vao = ES.GL.GenVertexArray();
            compiled.Vbo = ES.GL.GenBuffer();
            compiled.Ibo = ES.GL.GenBuffer();
            ES.GL.BindVertexArray(compiled.Vao);
            ES.GL.BindBuffer(ES.BufferTarget.ArrayBuffer, compiled.Vbo);
            fixed (float* verts = CollectionsMarshal.AsSpan(_batch.Vertices))
            {
                ES.GL.BufferData(ES.BufferTarget.ArrayBuffer, _batch.Vertices.Count * sizeof(float),
                    (IntPtr)verts, ES.BufferUsageHint.StaticDraw);
            }
            ES.GL.BindBuffer(ES.BufferTarget.ElementArrayBuffer, compiled.Ibo);
            int[] indices = BuildIndexArray();
            fixed (int* idx = indices)
            {
                ES.GL.BufferData(ES.BufferTarget.ElementArrayBuffer, indices.Length * sizeof(int),
                    (IntPtr)idx, ES.BufferUsageHint.StaticDraw);
            }
            SetupAttributes();
            ES.GL.BindVertexArray(0);
            ES.GL.BindBuffer(ES.BufferTarget.ArrayBuffer, 0);
            ES.GL.BindBuffer(ES.BufferTarget.ElementArrayBuffer, 0);
            _lists[_recordListId] = compiled;
            _batch.Clear();
        }

        public static void CallList(int list)
        {
            if (!_lists.TryGetValue(list, out CompiledList? compiled) || compiled.Vao == 0)
            {
                return;
            }
            ApplyDrawState();
            ES.GL.BindVertexArray(compiled.Vao);
            if (compiled.TriCount > 0)
            {
                ES.GL.DrawElements(ES.PrimitiveType.Triangles, compiled.TriCount,
                    ES.DrawElementsType.UnsignedInt, IntPtr.Zero);
            }
            if (compiled.LineCount > 0)
            {
                ES.GL.DrawElements(ES.PrimitiveType.Lines, compiled.LineCount,
                    ES.DrawElementsType.UnsignedInt, (IntPtr)(compiled.TriCount * sizeof(int)));
            }
            ES.GL.BindVertexArray(0);
        }

        public static void DeleteLists(int list, int range)
        {
            for (int i = 0; i < range; i++)
            {
                int id = list + i;
                if (_lists.Remove(id, out CompiledList? compiled) && compiled.Vao != 0)
                {
                    ES.GL.DeleteVertexArray(compiled.Vao);
                    ES.GL.DeleteBuffer(compiled.Vbo);
                    ES.GL.DeleteBuffer(compiled.Ibo);
                }
            }
        }

        private static int[] BuildIndexArray()
        {
            var indices = new int[_batch.TriIndices.Count + _batch.LineIndices.Count];
            _batch.TriIndices.CopyTo(indices, 0);
            _batch.LineIndices.CopyTo(indices, _batch.TriIndices.Count);
            return indices;
        }

        private static unsafe void FlushDynamic()
        {
            int triCount = _batch.TriIndices.Count;
            int lineCount = _batch.LineIndices.Count;
            if (triCount == 0 && lineCount == 0)
            {
                _batch.Clear();
                return;
            }
            if (_dynVao == 0)
            {
                _dynVao = ES.GL.GenVertexArray();
                _dynVbo = ES.GL.GenBuffer();
                _dynIbo = ES.GL.GenBuffer();
                ES.GL.BindVertexArray(_dynVao);
                ES.GL.BindBuffer(ES.BufferTarget.ArrayBuffer, _dynVbo);
                ES.GL.BindBuffer(ES.BufferTarget.ElementArrayBuffer, _dynIbo);
                SetupAttributes();
            }
            else
            {
                ES.GL.BindVertexArray(_dynVao);
                ES.GL.BindBuffer(ES.BufferTarget.ArrayBuffer, _dynVbo);
                ES.GL.BindBuffer(ES.BufferTarget.ElementArrayBuffer, _dynIbo);
            }
            int vertexBytes = _batch.Vertices.Count * sizeof(float);
            fixed (float* verts = CollectionsMarshal.AsSpan(_batch.Vertices))
            {
                if (vertexBytes > _dynVboSize)
                {
                    ES.GL.BufferData(ES.BufferTarget.ArrayBuffer, vertexBytes, (IntPtr)verts,
                        ES.BufferUsageHint.StreamDraw);
                    _dynVboSize = vertexBytes;
                }
                else
                {
                    // orphan first, so the driver does not stall waiting for the
                    // frame that is still reading the old contents
                    ES.GL.BufferData(ES.BufferTarget.ArrayBuffer, _dynVboSize, IntPtr.Zero,
                        ES.BufferUsageHint.StreamDraw);
                    ES.GL.BufferSubData(ES.BufferTarget.ArrayBuffer, IntPtr.Zero, vertexBytes, (IntPtr)verts);
                }
            }
            int[] indices = BuildIndexArray();
            int indexBytes = indices.Length * sizeof(int);
            fixed (int* idx = indices)
            {
                if (indexBytes > _dynIboSize)
                {
                    ES.GL.BufferData(ES.BufferTarget.ElementArrayBuffer, indexBytes, (IntPtr)idx,
                        ES.BufferUsageHint.StreamDraw);
                    _dynIboSize = indexBytes;
                }
                else
                {
                    ES.GL.BufferData(ES.BufferTarget.ElementArrayBuffer, _dynIboSize, IntPtr.Zero,
                        ES.BufferUsageHint.StreamDraw);
                    ES.GL.BufferSubData(ES.BufferTarget.ElementArrayBuffer, IntPtr.Zero, indexBytes, (IntPtr)idx);
                }
            }
            ApplyDrawState();
            if (triCount > 0)
            {
                ES.GL.DrawElements(ES.PrimitiveType.Triangles, triCount,
                    ES.DrawElementsType.UnsignedInt, IntPtr.Zero);
            }
            if (lineCount > 0)
            {
                ES.GL.DrawElements(ES.PrimitiveType.Lines, lineCount,
                    ES.DrawElementsType.UnsignedInt, (IntPtr)(triCount * sizeof(int)));
            }
            ES.GL.BindVertexArray(0);
            _batch.Clear();
        }

        private static void SetupAttributes()
        {
            for (int i = 0; i <= 4; i++)
            {
                ES.GL.EnableVertexAttribArray(i);
            }
            ES.GL.VertexAttribPointer(0, 3, ES.VertexAttribPointerType.Float, false, Stride, 0);
            ES.GL.VertexAttribPointer(1, 4, ES.VertexAttribPointerType.Float, false, Stride, 3 * sizeof(float));
            ES.GL.VertexAttribPointer(2, 3, ES.VertexAttribPointerType.Float, false, Stride, 7 * sizeof(float));
            ES.GL.VertexAttribPointer(3, 3, ES.VertexAttribPointerType.Float, false, Stride, 10 * sizeof(float));
            ES.GL.VertexAttribPointer(4, 1, ES.VertexAttribPointerType.Float, false, Stride, 13 * sizeof(float));
        }

        private static void ApplyDrawState()
        {
            if (_immColorLoc >= 0)
            {
                ES.GL.Uniform4(_immColorLoc, _curColor.X, _curColor.Y, _curColor.Z, _curColor.W);
            }
            if (_alphaTestLoc >= 0)
            {
                int mode = 0;
                if (_alphaTestEnabled)
                {
                    mode = _alphaFunc == AlphaFunction.Equal ? 1 : _alphaFunc == AlphaFunction.Less ? 2 : 0;
                }
                ES.GL.Uniform1(_alphaTestLoc, mode);
            }
        }

        #endregion

        #region textures the engine named itself

        private static int RealTexture(int name)
        {
            if (name == 0)
            {
                return 0;
            }
            if (name > _textureHighWater)
            {
                _textureHighWater = name;
            }
            if (!_textures.TryGetValue(name, out int real))
            {
                real = ES.GL.GenTexture();
                _textures[name] = real;
            }
            return real;
        }

        public static int GenTexture()
        {
            // The engine counts texture names itself and expects GenTexture to
            // hand out the next one in the same sequence, so keep one counter.
            int name = ++_textureHighWater;
            RealTexture(name);
            return name;
        }

        public static void DeleteTexture(int name)
        {
            if (_textures.Remove(name, out int real))
            {
                ES.GL.DeleteTexture(real);
            }
        }

        public static void BindTexture(TextureTarget target, int name)
        {
            ES.GL.BindTexture((ES.TextureTarget)(int)target, RealTexture(name));
        }

        #endregion

        #region shaders

        public static int CreateShader(ShaderType type)
        {
            return ES.GL.CreateShader((ES.ShaderType)(int)type);
        }

        public static void ShaderSource(int shader, string source)
        {
            EsShaders.CheckInSync();
            string translated = EsShaders.Translate(source) ?? source;
            ES.GL.ShaderSource(shader, 1, new string[] { translated }, new int[] { translated.Length });
        }

        public static void CompileShader(int shader)
        {
            ES.GL.CompileShader(shader);
            ES.GL.GetShader(shader, ES.ShaderParameter.CompileStatus, out int status);
            if (status == 0)
            {
                // The engine only reads the log under a debugger, and a shader
                // that will not compile is otherwise a black screen with no
                // explanation anywhere.
                Console.WriteLine($"[gles] shader {shader} failed to compile: {ES.GL.GetShaderInfoLog(shader)}");
            }
        }

        public static void GetShader(int shader, ShaderParameter pname, out int value)
        {
            ES.GL.GetShader(shader, (ES.ShaderParameter)(int)pname, out value);
        }

        public static string GetShaderInfoLog(int shader)
        {
            return ES.GL.GetShaderInfoLog(shader);
        }

        public static void DeleteShader(int shader)
        {
            ES.GL.DeleteShader(shader);
        }

        public static int CreateProgram()
        {
            return ES.GL.CreateProgram();
        }

        public static void AttachShader(int program, int shader)
        {
            ES.GL.AttachShader(program, shader);
        }

        public static void DetachShader(int program, int shader)
        {
            ES.GL.DetachShader(program, shader);
        }

        public static void LinkProgram(int program)
        {
            ES.GL.LinkProgram(program);
            ES.GL.GetProgram(program, ES.GetProgramParameterName.LinkStatus, out int status);
            if (status == 0)
            {
                throw new ProgramException(
                    $"Failed to link program {program}: {ES.GL.GetProgramInfoLog(program)}");
            }
        }

        public static void UseProgram(int program)
        {
            ES.GL.UseProgram(program);
            _program = program;
            if (!_programLocs.TryGetValue(program, out (int ImmColor, int AlphaTest) locs))
            {
                locs = (ES.GL.GetUniformLocation(program, "imm_color"),
                    ES.GL.GetUniformLocation(program, "alpha_test"));
                _programLocs[program] = locs;
            }
            _immColorLoc = locs.ImmColor;
            _alphaTestLoc = locs.AlphaTest;
        }

        public static int GetUniformLocation(int program, string name)
        {
            return ES.GL.GetUniformLocation(program, name);
        }

        #endregion

        #region state

        public static void Enable(EnableCap cap)
        {
            if (cap == EnableCap.AlphaTest)
            {
                _alphaTestEnabled = true;
                return;
            }
            if (IgnoredCap(cap))
            {
                return;
            }
            ES.GL.Enable((ES.EnableCap)(int)cap);
        }

        public static void Disable(EnableCap cap)
        {
            if (cap == EnableCap.AlphaTest)
            {
                _alphaTestEnabled = false;
                return;
            }
            if (IgnoredCap(cap))
            {
                return;
            }
            ES.GL.Disable((ES.EnableCap)(int)cap);
        }

        private static bool IgnoredCap(EnableCap cap)
        {
            // Texture2D is fixed-function; the debug output extension is not
            // part of ES 3.0 and the capture code that asks for it is a desktop
            // path anyway.
            return cap == EnableCap.Texture2D || cap == EnableCap.DebugOutput
                || (int)cap == 0x8242 /* DebugOutputSynchronous */;
        }

        public static void AlphaFunc(AlphaFunction func, float reference)
        {
            _alphaFunc = func;
        }

        public static void PolygonMode(TriangleFace face, OpenTK.Graphics.OpenGL.PolygonMode mode)
        {
            // ES has no glPolygonMode. Only the debug views ask for Line.
        }

        public static void DebugMessageCallback(DebugProc callback, IntPtr userParam)
        {
        }

        public static void Clear(ClearBufferMask mask)
        {
            ES.GL.Clear((ES.ClearBufferMask)(int)mask);
        }

        public static void ClearColor(Color4 color)
        {
            ES.GL.ClearColor(color.R, color.G, color.B, color.A);
        }

        public static void ClearStencil(int value)
        {
            ES.GL.ClearStencil(value);
        }

        public static void ColorMask(bool red, bool green, bool blue, bool alpha)
        {
            ES.GL.ColorMask(red, green, blue, alpha);
        }

        public static void DepthMask(bool flag)
        {
            ES.GL.DepthMask(flag);
        }

        public static void DepthFunc(DepthFunction func)
        {
            ES.GL.DepthFunc((ES.DepthFunction)(int)func);
        }

        public static void CullFace(TriangleFace mode)
        {
            // The ES enum has no TriangleFace overload; the values are the same.
#pragma warning disable CS0618
            ES.GL.CullFace((ES.CullFaceMode)(int)mode);
#pragma warning restore CS0618
        }

        public static void BlendFunc(BlendingFactor src, BlendingFactor dst)
        {
            ES.GL.BlendFunc((ES.BlendingFactorSrc)(int)src, (ES.BlendingFactorDest)(int)dst);
        }

        public static void StencilFunc(StencilFunction func, int reference, int mask)
        {
            ES.GL.StencilFunc((ES.StencilFunction)(int)func, reference, mask);
        }

        public static void StencilOp(OpenTK.Graphics.OpenGL.StencilOp fail,
            OpenTK.Graphics.OpenGL.StencilOp zfail, OpenTK.Graphics.OpenGL.StencilOp zpass)
        {
            ES.GL.StencilOp((ES.StencilOp)(int)fail, (ES.StencilOp)(int)zfail, (ES.StencilOp)(int)zpass);
        }

        public static void StencilMask(int mask)
        {
            ES.GL.StencilMask(mask);
        }

        public static void PolygonOffset(float factor, float units)
        {
            ES.GL.PolygonOffset(factor, units);
        }

        public static void Viewport(int x, int y, int width, int height)
        {
            ES.GL.Viewport(x, y, width, height);
        }

        /// <summary>
        /// Scissor, which ES has and the desktop overload list here did not.
        /// The results screen's hunter preview clears and draws inside one
        /// rectangle of the frame; see <c>Scene.ModDrawPreview</c>.
        /// </summary>
        public static void Scissor(int x, int y, int width, int height)
        {
            ES.GL.Scissor(x, y, width, height);
        }

        /// <summary>
        /// The four-component form. The desktop GL takes a Color4 or four
        /// floats; only the first was mirrored here, and the preview pass
        /// wants to put the clear colour back the way it found it.
        /// </summary>
        public static void ClearColor(float red, float green, float blue, float alpha)
        {
            ES.GL.ClearColor(red, green, blue, alpha);
        }

        public static void PixelStore(PixelStoreParameter pname, int param)
        {
            ES.GL.PixelStore((ES.PixelStoreParameter)(int)pname, param);
        }

        public static void ReadBuffer(ReadBufferMode mode)
        {
            ES.GL.ReadBuffer((ES.ReadBufferMode)(int)mode);
        }

        public static void ActiveTexture(TextureUnit texture)
        {
            ES.GL.ActiveTexture((ES.TextureUnit)(int)texture);
        }

        public static ErrorCode GetError()
        {
            return (ErrorCode)(int)ES.GL.GetError();
        }

        public static string GetString(StringName name)
        {
            return ES.GL.GetString((ES.StringName)(int)name);
        }

        public static int GetInteger(GetPName pname)
        {
            // The two the capture code asks for are context flags and the
            // profile mask, neither of which ES has; answering zero is what a
            // core context without them would report anyway.
            if ((int)pname == 0x821E || (int)pname == 0x9126)
            {
                return 0;
            }
            return ES.GL.GetInteger((ES.GetPName)(int)pname);
        }

        #endregion

        #region textures and framebuffers

        public static void TexParameter(TextureTarget target, TextureParameterName pname, int param)
        {
            ES.GL.TexParameter((ES.TextureTarget)(int)target, (ES.TextureParameterName)(int)pname, param);
        }

        public static void TexImage2D(TextureTarget target, int level, PixelInternalFormat internalFormat,
            int width, int height, int border, PixelFormat format, PixelType type, IntPtr pixels)
        {
            ES.GL.TexImage2D((ES.TextureTarget2d)(int)target, level,
                (ES.TextureComponentCount)(int)internalFormat, width, height, border,
                (ES.PixelFormat)(int)format, (ES.PixelType)(int)type, pixels);
        }

        public static void TexImage2D<T>(TextureTarget target, int level, PixelInternalFormat internalFormat,
            int width, int height, int border, PixelFormat format, PixelType type, T[] pixels) where T : struct
        {
            ES.GL.TexImage2D((ES.TextureTarget2d)(int)target, level,
                (ES.TextureComponentCount)(int)internalFormat, width, height, border,
                (ES.PixelFormat)(int)format, (ES.PixelType)(int)type, pixels);
        }

        public static void TexSubImage2D<T>(TextureTarget target, int level, int xoffset, int yoffset,
            int width, int height, PixelFormat format, PixelType type, T[] pixels) where T : struct
        {
            ES.GL.TexSubImage2D((ES.TextureTarget2d)(int)target, level, xoffset, yoffset, width, height,
                (ES.PixelFormat)(int)format, (ES.PixelType)(int)type, pixels);
        }

        /// <summary>
        /// The framebuffer that is bound for reading, into the texture that is
        /// bound. Cel shading's ink pass needs a copy of the scene to sample,
        /// since a pass cannot read the target it is drawing into, and this is
        /// that copy -- on the GPU, with no round trip through the CPU.
        /// </summary>
        public static void CopyTexSubImage2D(TextureTarget target, int level, int xoffset, int yoffset,
            int x, int y, int width, int height)
        {
            ES.GL.CopyTexSubImage2D((ES.TextureTarget2d)(int)target, level, xoffset, yoffset,
                x, y, width, height);
        }

        public static void ReadPixels<T>(int x, int y, int width, int height, PixelFormat format,
            PixelType type, T[] pixels) where T : struct
        {
            ES.GL.ReadPixels(x, y, width, height, (ES.PixelFormat)(int)format, (ES.PixelType)(int)type, pixels);
        }

        public static int GenFramebuffer()
        {
            return ES.GL.GenFramebuffer();
        }

        public static void BindFramebuffer(FramebufferTarget target, int framebuffer)
        {
            ES.GL.BindFramebuffer((ES.FramebufferTarget)(int)target, framebuffer);
        }

        public static void FramebufferTexture2D(FramebufferTarget target, FramebufferAttachment attachment,
            TextureTarget textarget, int texture, int level)
        {
            ES.GL.FramebufferTexture2D((ES.FramebufferTarget)(int)target,
                (ES.FramebufferAttachment)(int)attachment, (ES.TextureTarget2d)(int)textarget,
                RealTexture(texture), level);
        }

        public static int GenRenderbuffer()
        {
            return ES.GL.GenRenderbuffer();
        }

        public static void BindRenderbuffer(RenderbufferTarget target, int renderbuffer)
        {
            ES.GL.BindRenderbuffer((ES.RenderbufferTarget)(int)target, renderbuffer);
        }

        public static void RenderbufferStorage(RenderbufferTarget target,
            OpenTK.Graphics.OpenGL.RenderbufferStorage internalFormat, int width, int height)
        {
            ES.GL.RenderbufferStorage((ES.RenderbufferTarget)(int)target,
                (ES.RenderbufferInternalFormat)(int)internalFormat, width, height);
        }

        public static void FramebufferRenderbuffer(FramebufferTarget target, FramebufferAttachment attachment,
            RenderbufferTarget renderbuffertarget, int renderbuffer)
        {
            ES.GL.FramebufferRenderbuffer((ES.FramebufferTarget)(int)target,
                (ES.FramebufferAttachment)(int)attachment, (ES.RenderbufferTarget)(int)renderbuffertarget,
                renderbuffer);
        }

        /// <summary>
        /// Asked once per depth attachment, so the ink pass knows how many
        /// bits it is actually differencing. A driver may answer a request for
        /// a 24-bit depth texture with fewer, and the pass is looking for a
        /// difference far below one step of a coarse one.
        /// </summary>
        public static void GetFramebufferAttachmentParameter(FramebufferTarget target,
            FramebufferAttachment attachment, FramebufferParameterName pname, out int result)
        {
            ES.GL.GetFramebufferAttachmentParameter((ES.FramebufferTarget)(int)target,
                (ES.FramebufferAttachment)(int)attachment,
                (ES.FramebufferParameterName)(int)pname, out result);
        }

        public static FramebufferErrorCode CheckFramebufferStatus(FramebufferTarget target)
        {
            return (FramebufferErrorCode)(int)ES.GL.CheckFramebufferStatus((ES.FramebufferTarget)(int)target);
        }

        #endregion

        #region uniforms

        public static void Uniform1(int location, int value)
        {
            ES.GL.Uniform1(location, value);
        }

        public static void Uniform1(int location, float value)
        {
            ES.GL.Uniform1(location, value);
        }

        public static void Uniform1(int location, int count, float[] value)
        {
            ES.GL.Uniform1(location, count, value);
        }

        public static void Uniform3(int location, Vector3 vector)
        {
            ES.GL.Uniform3(location, vector);
        }

        public static void Uniform3(int location, int count, float[] value)
        {
            ES.GL.Uniform3(location, count, value);
        }

        public static void Uniform4(int location, Vector4 vector)
        {
            ES.GL.Uniform4(location, vector);
        }

        public static void Uniform4(int location, ref Vector4 vector)
        {
            ES.GL.Uniform4(location, ref vector);
        }

        public static void Uniform4(int location, float v0, float v1, float v2, float v3)
        {
            ES.GL.Uniform4(location, v0, v1, v2, v3);
        }

        public static void Uniform4(int location, int v0, int v1, int v2, int v3)
        {
            ES.GL.Uniform4(location, v0, v1, v2, v3);
        }

        public static void UniformMatrix4(int location, bool transpose, ref Matrix4 matrix)
        {
            ES.GL.UniformMatrix4(location, transpose, ref matrix);
        }

        public static void UniformMatrix4(int location, int count, bool transpose, float[] value)
        {
            ES.GL.UniformMatrix4(location, count, transpose, value);
        }

        #endregion
    }
}
#endif
