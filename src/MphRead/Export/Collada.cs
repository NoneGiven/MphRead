using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OpenTK.Mathematics;

namespace MphRead.Export
{
    public static class Collada
    {
        public readonly struct Vertex
        {
            public readonly Vector3 Position;
            public readonly Vector3 Normal;
            public readonly Vector3 Color;
            public readonly Vector2 Uv;
            public readonly int MatrixId;

            public Vertex(Vector3 position, Vector3 normal, Vector3 color, Vector2 uv, int matrixId)
            {
                Position = position;
                Normal = normal;
                Color = color;
                Uv = uv;
                MatrixId = matrixId;
            }
        }

        private static string FloatFormat(Vector3 vector)
        {
            return FormattableString.Invariant($"{FloatFormat(vector.X)} {FloatFormat(vector.Y)} {FloatFormat(vector.Z)}");
        }

        private static string FloatFormat(float input)
        {
            // this is slightly more accurate than the %f format specifier used in MetroidModelViewer
            // 0.454875469 rounds to 0.454875, but MetroidModelViewer outputs 0.454876
            return MathF.Round(input, 6, MidpointRounding.AwayFromZero).ToString("F6", CultureInfo.InvariantCulture);
        }

        public static void ExportModel(Model model, bool transformRoom = false)
        {
            string exportPath = Paths.Combine(Paths.Export, model.Name);
            Directory.CreateDirectory(exportPath);
            var lists = new List<Dictionary<string, IReadOnlyList<Vertex>>>();
            for (int i = 0; i < model.Recolors.Count; i++)
            {
                lists.Add(ExportRecolor(model, transformRoom, i));
            }
            if (lists.Count == 0)
            {
                throw new ProgramException("Export failed due to missing recolor.");
            }
            for (int i = 1; i < lists.Count; i++)
            {
                Dictionary<string, IReadOnlyList<Vertex>> current = lists[i];
                Dictionary<string, IReadOnlyList<Vertex>> previous = lists[i - 1];
                if (!Enumerable.SequenceEqual(current.Keys, previous.Keys))
                {
                    throw new ProgramException("Export failed due to mismatching objects.");
                }
                foreach (KeyValuePair<string, IReadOnlyList<Vertex>> kvp in current)
                {
                    if (!Enumerable.SequenceEqual(kvp.Value, previous[kvp.Key]))
                    {
                        throw new ProgramException("Export failed due to mismatching vertices.");
                    }
                }
            }
            File.WriteAllText(Paths.Combine(exportPath, $"import_{model.Name}.py"), Scripting.GenerateScript(model, lists.First()));
        }

        private static Dictionary<string, IReadOnlyList<Vertex>> ExportRecolor(Model model, bool transformRoom, int recolorIndex)
        {
            var results = new Dictionary<string, IReadOnlyList<Vertex>>();
            Recolor recolor = model.Recolors[recolorIndex];
            var sb = new StringBuilder();

            // collada header
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.Append("\n<COLLADA xmlns=\"http://www.collada.org/2005/11/COLLADASchema\" version=\"1.4.1\">");

            // assets
            sb.Append("\n\t<asset>");
            sb.Append("\n\t\t<up_axis>Y_UP</up_axis>");
            sb.Append("\n\t\t<unit name=\"meter\" meter=\"1\" />");
            sb.Append("\n\t</asset>");

            // images
            sb.Append("\n\t<library_images>");
            int id = 0;
            var imagesInLibrary = new HashSet<(int, int)>();
            void AddLibraryImage(int textureId, int paletteId, string name)
            {
                if (textureId != -1 && !imagesInLibrary.Contains((textureId, paletteId)))
                {
                    imagesInLibrary.Add((textureId, paletteId));
                    sb.Append($"\n\t\t<image id=\"{name}\" name=\"{name}\">");
                    sb.Append("\n\t\t\t<init_from>");
                    if (id <= 0)
                    {
                        sb.Append($"{recolor.Name}/{textureId}-{paletteId}.png");
                    }
                    else
                    {
                        sb.Append($"{recolor.Name}/anim__{id.ToString().PadLeft(3, '0')}.png");
                    }
                    sb.Append("</init_from>\n\t\t</image>");
                }
            }

            foreach (Material material in model.Materials)
            {
                AddLibraryImage(material.TextureId, material.PaletteId, material.Name);
            }
            id = 1;
            imagesInLibrary.Clear();
            foreach (TextureAnimationGroup group in model.AnimationGroups.Texture)
            {
                foreach (TextureAnimation animation in group.Animations.Values)
                {
                    for (int i = animation.StartIndex; i < animation.StartIndex + animation.Count; i++)
                    {
                        AddLibraryImage(group.TextureIds[i], group.PaletteIds[i], $"anim__{group.TextureIds[i]}-{group.PaletteIds[i]}");
                        id++;
                    }
                }
            }
            sb.Append("\n\t</library_images>");

            // materials
            sb.Append("\n\t<library_materials>");
            foreach (Material material in model.Materials)
            {
                string textureName = String.IsNullOrEmpty(material.Name) ? "null" : material.Name;
                string materialTag = "\n\t\t<material id=\"";
                materialTag += textureName;
                materialTag += "-material\" name=\"";
                materialTag += textureName;
                materialTag += "_mat\">";
                sb.Append(materialTag);
                string instanceEffectTag = "\n\t\t\t<instance_effect url=\"#";
                instanceEffectTag += textureName;
                instanceEffectTag += "-effect\" />";
                sb.Append(instanceEffectTag);
                sb.Append("\n\t\t</material>");
            }
            sb.Append("\n\t</library_materials>");

            // geometries
            sb.Append("\n\t<library_geometries>");
            // mesh vars
            var meshVerts = new List<Vertex>();
            var tempMeshVerts = new List<Vertex>();

            // go through every mesh
            int meshCounter = 0;

            foreach (Mesh mesh in model.Meshes)
            {
                meshCounter++;
                // reset
                meshVerts.Clear();
                tempMeshVerts.Clear();

                DisplayList dlist = model.DisplayLists[mesh.DlistId];
                Material material = model.Materials[mesh.MaterialId];
                Texture tex = default;
                if (material.TextureId != -1)
                {
                    tex = recolor.Textures[material.TextureId];
                }

                ExportDlist(model, mesh.DlistId, tempMeshVerts, meshVerts);

                int numVertices = meshVerts.Count;
                string textureName = String.IsNullOrEmpty(material.Name) ? "null" : material.Name;
                string geometryID = $"geometry{meshCounter}";
                string geometryTag = $"\n\t\t<geometry id=\"geometry{meshCounter}\" name=\"geom{meshCounter}\">";
                sb.Append(geometryTag);
                sb.Append("\n\t\t\t<mesh>");
                // positions
                string positionsTag = "\n\t\t\t\t<source id=\"";
                positionsTag += geometryID;
                positionsTag += "-positions\">";
                sb.Append(positionsTag);
                // float array
                string positionsArrayID = geometryID;
                positionsArrayID += "-positions-array";

                string floatarrayTag = "\n\t\t\t\t\t<float_array id=\"";
                floatarrayTag += positionsArrayID;
                floatarrayTag += "\" count=\"";
                floatarrayTag += (numVertices * 3).ToString();
                floatarrayTag += "\">";
                sb.Append(floatarrayTag);
                foreach (Vertex vert in meshVerts)
                {
                    string vertPos = $"{FloatFormat(vert.Position.X)} {FloatFormat(vert.Position.Y)} {FloatFormat(vert.Position.Z)} ";
                    sb.Append(vertPos);
                }
                sb.Append("</float_array>");

                // technique common (position x,y,z)
                sb.Append("\n\t\t\t\t\t<technique_common>");
                string accessorTag = "\n\t\t\t\t\t\t<accessor source=\"#";
                accessorTag += positionsArrayID;
                accessorTag += "\" count=\"";
                accessorTag += numVertices.ToString();
                accessorTag += "\" stride=\"3\">";
                sb.Append(accessorTag);
                sb.Append("\n\t\t\t\t\t\t\t<param name=\"X\" type=\"float\" />");
                sb.Append("\n\t\t\t\t\t\t\t<param name=\"Y\" type=\"float\" />");
                sb.Append("\n\t\t\t\t\t\t\t<param name=\"Z\" type=\"float\" />");
                sb.Append("\n\t\t\t\t\t\t</accessor>");
                sb.Append("\n\t\t\t\t\t</technique_common>");
                sb.Append("\n\t\t\t\t</source>");

                // normals
                string normalsTag = "\n\t\t\t\t<source id=\"";
                normalsTag += geometryID;
                normalsTag += "-normals\">";
                sb.Append(normalsTag);
                // float array
                string normalsArrayID = geometryID;
                normalsArrayID += "-normals-array";

                floatarrayTag = "\n\t\t\t\t\t<float_array id=\"";
                floatarrayTag += normalsArrayID;
                floatarrayTag += "\" count=\"";
                floatarrayTag += (numVertices * 3).ToString();
                floatarrayTag += "\">";
                sb.Append(floatarrayTag);
                for (int i = 0; i < meshVerts.Count; i += 3)
                {
                    var normal = Vector3.Cross(
                        meshVerts[i + 1].Position - meshVerts[i].Position,
                        meshVerts[i + 2].Position - meshVerts[i].Position);
                    if (normal != Vector3.Zero)
                    {
                        normal.Normalize();
                    }
                    string vertNormal = $"{FloatFormat(normal.X)} {FloatFormat(normal.Y)} {FloatFormat(normal.Z)} ";
                    sb.Append(vertNormal);
                    sb.Append(vertNormal);
                    sb.Append(vertNormal);
                }
                sb.Append("</float_array>");

                // technique common (normal x,y,z)
                sb.Append("\n\t\t\t\t\t<technique_common>");
                accessorTag = "\n\t\t\t\t\t\t<accessor source=\"#";
                accessorTag += normalsArrayID;
                accessorTag += "\" count=\"";
                accessorTag += numVertices.ToString();
                accessorTag += "\" stride=\"3\">";
                sb.Append(accessorTag);
                sb.Append("\n\t\t\t\t\t\t\t<param name=\"X\" type=\"float\" />");
                sb.Append("\n\t\t\t\t\t\t\t<param name=\"Y\" type=\"float\" />");
                sb.Append("\n\t\t\t\t\t\t\t<param name=\"Z\" type=\"float\" />");
                sb.Append("\n\t\t\t\t\t\t</accessor>");
                sb.Append("\n\t\t\t\t\t</technique_common>");
                sb.Append("\n\t\t\t\t</source>");

                // vertex colors
                string vertexcolorsTag = "\n\t\t\t\t<source id=\"";
                vertexcolorsTag += geometryID;
                vertexcolorsTag += "-colors\">";
                sb.Append(vertexcolorsTag);
                // float array
                string vertexcolorsArrayID = geometryID;
                vertexcolorsArrayID += "-colors-array";

                floatarrayTag = "\n\t\t\t\t\t<float_array id=\"";
                floatarrayTag += vertexcolorsArrayID;
                floatarrayTag += "\" count=\"";
                floatarrayTag += (numVertices * 3).ToString();
                floatarrayTag += "\">";
                sb.Append(floatarrayTag);
                foreach (Vertex vert in meshVerts)
                {
                    string vertColor = $"{FloatFormat(vert.Color.X)} {FloatFormat(vert.Color.Y)} {FloatFormat(vert.Color.Z)} ";
                    sb.Append(vertColor);
                }
                sb.Append("</float_array>");

                // technique common (color r,g,b)
                sb.Append("\n\t\t\t\t\t<technique_common>");
                accessorTag = "\n\t\t\t\t\t\t<accessor source=\"#";
                accessorTag += vertexcolorsArrayID;
                accessorTag += "\" count=\"";
                accessorTag += numVertices.ToString();
                accessorTag += "\" stride=\"3\">";
                sb.Append(accessorTag);
                sb.Append("\n\t\t\t\t\t\t\t<param name=\"R\" type=\"float\" />");
                sb.Append("\n\t\t\t\t\t\t\t<param name=\"G\" type=\"float\" />");
                sb.Append("\n\t\t\t\t\t\t\t<param name=\"B\" type=\"float\" />");
                sb.Append("\n\t\t\t\t\t\t</accessor>");
                sb.Append("\n\t\t\t\t\t</technique_common>");
                sb.Append("\n\t\t\t\t</source>");

                // texcoords
                string texcoordsTag = "\n\t\t\t\t<source id=\"";
                texcoordsTag += geometryID;
                texcoordsTag += "-texcoords\">";
                sb.Append(texcoordsTag);
                // float array
                string texcoordsArrayID = geometryID;
                texcoordsArrayID += "-texcoords-array";

                floatarrayTag = "\n\t\t\t\t\t<float_array id=\"";
                floatarrayTag += texcoordsArrayID;
                floatarrayTag += "\" count=\"";
                floatarrayTag += (numVertices * 2).ToString();
                floatarrayTag += "\">";
                sb.Append(floatarrayTag);
                for (int i = 0; i < meshVerts.Count; i++)
                {
                    Vertex vert = meshVerts[i];
                    // compensate for glScalef(1.0f / tex->texture->getWidth()) of texture space which is applied during rendering
                    if (material.TextureId != -1)
                    {
                        float factorS = 1;
                        float factorT = 1;
                        // todo: handle material translation (find an example where it's nonzero)
                        // todo: handle texture matrices?
                        if (material.TexgenMode != TexgenMode.None && model.TextureMatrices.Count == 0)
                        {
                            factorS = material.ScaleS;
                            factorT = material.ScaleT;
                        }
                        var newUv = new Vector2(
                            vert.Uv.X * factorS * (1.0f / tex.Width),
                            vert.Uv.Y * factorT * (1.0f / tex.Height));
                        if (material.XRepeat == RepeatMode.Clamp)
                        {
                            newUv = new Vector2(MathHelper.Clamp(newUv.X, 0f, 1f), newUv.Y);
                        }
                        if (material.YRepeat == RepeatMode.Clamp)
                        {
                            newUv = new Vector2(newUv.X, MathHelper.Clamp(newUv.Y, 0f, 1f));
                        }
                        var newVert = new Vertex(vert.Position, vert.Color, vert.Normal, newUv, vert.MatrixId);
                        string texCoord = $"{FloatFormat(newVert.Uv.X)} {FloatFormat(1 - newVert.Uv.Y)} ";
                        sb.Append(texCoord);
                        meshVerts.RemoveAt(i);
                        meshVerts.Insert(i, newVert);
                    }
                }
                sb.Append("</float_array>");

                // technique common (uv x,y)
                sb.Append("\n\t\t\t\t\t<technique_common>");
                accessorTag = "\n\t\t\t\t\t\t<accessor source=\"#";
                accessorTag += texcoordsArrayID;
                accessorTag += "\" count=\"";
                accessorTag += numVertices.ToString();
                accessorTag += "\" stride=\"2\">";
                sb.Append(accessorTag);
                sb.Append("\n\t\t\t\t\t\t\t<param name=\"S\" type=\"float\" />");
                sb.Append("\n\t\t\t\t\t\t\t<param name=\"T\" type=\"float\" />");
                sb.Append("\n\t\t\t\t\t\t</accessor>");
                sb.Append("\n\t\t\t\t\t</technique_common>");
                sb.Append("\n\t\t\t\t</source>");

                // position
                string verticesTag = "\n\t\t\t\t<vertices id=\"";
                verticesTag += geometryID;
                verticesTag += "-vertices\">";
                sb.Append(verticesTag);
                string inputTag = "\n\t\t\t\t\t<input semantic=\"POSITION\" source=\"#";
                inputTag += geometryID;
                inputTag += "-positions\" />";
                sb.Append(inputTag);
                sb.Append("\n\t\t\t\t</vertices>");

                // triangles
                string trianglesTag = "\n\t\t\t\t<triangles material=\"#";
                trianglesTag += textureName;
                trianglesTag += "-material\" count=\"";
                trianglesTag += (numVertices / 3).ToString();
                trianglesTag += "\">";
                sb.Append(trianglesTag);
                // vertex
                string vertexTag = "\n\t\t\t\t\t<input semantic=\"VERTEX\" source=\"#";
                vertexTag += geometryID;
                vertexTag += "-vertices\" offset=\"0\" />";
                sb.Append(vertexTag);

                // normal
                string normalTag = "\n\t\t\t\t\t<input semantic=\"NORMAL\" source=\"#";
                normalTag += geometryID;
                normalTag += "-normals\" offset=\"1\" />";
                sb.Append(normalTag);

                // texcoord
                string texcoordTag = "\n\t\t\t\t\t<input semantic=\"TEXCOORD\" source=\"#";
                texcoordTag += geometryID;
                texcoordTag += "-texcoords\" offset=\"2\" set=\"1\" />";
                sb.Append(texcoordTag);

                // vertex color
                string vertexcolorTag = "\n\t\t\t\t\t<input semantic=\"COLOR\" source=\"#";
                vertexcolorTag += geometryID;
                vertexcolorTag += "-colors\" offset=\"3\" set=\"0\" />";
                sb.Append(vertexcolorTag);

                sb.Append("\n\t\t\t\t\t<p>");
                for (int i = 0; i < meshVerts.Count; i++)
                {
                    string singleVertexP = $"{i} {i} {i} {i} ";
                    sb.Append(singleVertexP);
                }
                sb.Append("</p>");
                sb.Append("\n\t\t\t\t</triangles>");
                sb.Append("\n\t\t\t</mesh>");
                sb.Append("\n\t\t</geometry>");

                results.Add($"geom{meshCounter}_obj", meshVerts.ToList());
            }
            sb.Append("\n\t</library_geometries>");

            // effects (= texturing)
            sb.Append("\n\t<library_effects>");
            var effectsInLibrary = new Dictionary<int, string>();
            foreach (Material material in model.Materials)
            {
                string textureName = String.IsNullOrEmpty(material.Name) ? "null" : material.Name;
                string effectTag = "\n\t\t<effect id=\"";
                effectTag += textureName;
                effectTag += "-effect\">";
                sb.Append(effectTag);
                sb.Append("\n\t\t\t<profile_COMMON>");
                // surface
                string newparamSurfaceTag = "\n\t\t\t\t<newparam sid=\"";
                newparamSurfaceTag += textureName;
                newparamSurfaceTag += "-surface\">";
                sb.Append(newparamSurfaceTag);
                sb.Append("\n\t\t\t\t\t<surface type=\"2D\">");
                string initfromTag = "\n\t\t\t\t\t\t<init_from>";
                if (effectsInLibrary.TryGetValue(material.TextureId, out string? existingName))
                {
                    initfromTag += existingName;
                }
                else
                {
                    effectsInLibrary.Add(material.TextureId, textureName);
                    initfromTag += textureName;
                }
                initfromTag += "</init_from>";
                sb.Append(initfromTag);
                sb.Append("\n\t\t\t\t\t</surface>");
                sb.Append("\n\t\t\t\t</newparam>");

                // sampler
                string newparamSamplerTag = "\n\t\t\t\t<newparam sid=\"";
                newparamSamplerTag += textureName;
                newparamSamplerTag += "-sampler\">";
                sb.Append(newparamSamplerTag);
                sb.Append("\n\t\t\t\t\t<sampler2D>");
                string sourceTag = "\n\t\t\t\t\t\t<source>";
                sourceTag += textureName;
                sourceTag += "-surface</source>";
                sb.Append(sourceTag);
                sb.Append("\n\t\t\t\t\t</sampler2D>");
                sb.Append("\n\t\t\t\t</newparam>");

                // technique
                sb.Append("\n\t\t\t\t<technique sid=\"common\">");
                sb.Append("\n\t\t\t\t\t<phong>");
                // emission
                sb.Append("\n\t\t\t\t\t\t<emission>");
                sb.Append("\n\t\t\t\t\t\t\t<color sid=\"emission\">0 0 0 1</color>");
                sb.Append("\n\t\t\t\t\t\t</emission>");

                // ambient
                sb.Append("\n\t\t\t\t\t\t<ambient>");
                sb.Append("\n\t\t\t\t\t\t\t<color sid=\"ambient\">0.5 0.5 0.5 1</color>");
                sb.Append("\n\t\t\t\t\t\t</ambient>");

                // diffuse
                sb.Append("\n\t\t\t\t\t\t<diffuse>");
                string diffuseTag = "\n\t\t\t\t\t\t\t<texture texture=\"";
                diffuseTag += textureName;
                diffuseTag += "-sampler\" texcoord=\"UVMap\" />";
                sb.Append(diffuseTag);
                sb.Append("\n\t\t\t\t\t\t</diffuse>");

                // specular
                sb.Append("\n\t\t\t\t\t\t<specular>");
                sb.Append("\n\t\t\t\t\t\t\t<color sid=\"specular\">0.0 0.0 0.0 1</color>");
                sb.Append("\n\t\t\t\t\t\t</specular>");

                // shininess
                sb.Append("\n\t\t\t\t\t\t<shininess>");
                sb.Append("\n\t\t\t\t\t\t\t<float sid=\"shininess\">5</float>");
                sb.Append("\n\t\t\t\t\t\t</shininess>");

                // index of refraction
                sb.Append("\n\t\t\t\t\t\t<index_of_refraction>");
                sb.Append("\n\t\t\t\t\t\t\t<float sid=\"index_of_refraction\">1</float>");
                sb.Append("\n\t\t\t\t\t\t</index_of_refraction>");
                sb.Append("\n\t\t\t\t\t</phong>");
                sb.Append("\n\t\t\t\t</technique>");
                sb.Append("\n\t\t\t</profile_COMMON>");
                sb.Append("\n\t\t</effect>");
            }
            sb.Append("\n\t</library_effects>");

            // scene
            sb.Append("\n\t<library_visual_scenes>");
            sb.Append("\n\t\t<visual_scene id=\"Scene\" name=\"Scene\">\n");
            if (Metadata.RoomMetadata.ContainsKey(model.Name))
            {
                ExportRoomNodes(model, -1, sb, 3, transformRoom);
            }
            else
            {
                ExportMeshes(model, sb, 3);
            }
            sb.Append("\t\t</visual_scene>");
            sb.Append("\n\t</library_visual_scenes>");

            // end
            sb.Append("\n</COLLADA>");

            string exportPath = Paths.Combine(Paths.Export, model.Name);
            File.WriteAllText(Paths.Combine(exportPath, $"{model.Name}_{recolor.Name}.dae"), sb.ToString());
            return results;
        }

        private static void ExportRoomNodes(Model model, int parentId, StringBuilder sb, int indent, bool transformRoom)
        {
            for (int i = 0; i < model.Nodes.Count; i++)
            {
                Node node = model.Nodes[i];
                if (node.ParentIndex == parentId)
                {
                    Vector3 angle = Vector3.Zero;
                    Vector3 scale = Vector3.One;
                    Vector3 position = Vector3.Zero;
                    if (transformRoom)
                    {
                        angle = new Vector3(
                            MathHelper.RadiansToDegrees(node.Angle.X),
                            MathHelper.RadiansToDegrees(node.Angle.Y),
                            MathHelper.RadiansToDegrees(node.Angle.Z)
                        );
                        scale = node.Scale;
                        position = node.Position;
                    }
                    if (i == 0)
                    {
                        scale *= model.Scale;
                    }
                    sb.Append('\t', indent);
                    sb.Append($"<node id=\"{node.Name}\" type=\"NODE\">\n");
                    sb.Append('\t', indent + 1);
                    sb.Append($"<rotate>1.0 0.0 0.0 {angle.X}</rotate>\n");
                    sb.Append('\t', indent + 1);
                    sb.Append($"<rotate>0.0 1.0 0.0 {angle.Y}</rotate>\n");
                    sb.Append('\t', indent + 1);
                    sb.Append($"<rotate>0.0 0.0 1.0 {angle.Z}</rotate>\n");
                    sb.Append('\t', indent + 1);
                    sb.Append($"<scale>{FloatFormat(scale)}</scale>\n");
                    sb.Append('\t', indent + 1);
                    sb.Append($"<translate>{FloatFormat(position)}</translate>\n");
                    ExportNodeMeshes(model, i, sb, indent + 1);
                    if (node.ChildIndex != -1)
                    {
                        ExportRoomNodes(model, i, sb, indent + 1, transformRoom);
                    }
                    sb.Append('\t', indent);
                    sb.Append($"</node>\n");
                }
            }
        }

        private static void ExportNodeMeshes(Model model, int nodeId, StringBuilder sb, int indent)
        {
            foreach (int meshId in model.Nodes[nodeId].GetMeshIds())
            {
                sb.Append('\t', indent);
                sb.Append($"<node id=\"geom{meshId + 1}_obj\" type=\"NODE\">\n");
                ExportMesh(model, meshId, sb, indent + 1);
                sb.Append('\t', indent);
                sb.Append($"</node>\n");
            }
        }

        private static void ExportMeshes(Model model, StringBuilder sb, int indent)
        {
            for (int i = 0; i < model.Meshes.Count; i++)
            {
                sb.Append('\t', indent);
                sb.Append($"<node id=\"geom{i + 1}_obj\" type=\"NODE\">\n");
                ExportMesh(model, i, sb, indent + 1);
                sb.Append('\t', indent);
                sb.Append($"</node>\n");
            }
        }

        private static void ExportMesh(Model model, int meshId, StringBuilder sb, int indent)
        {
            Mesh mesh = model.Meshes[meshId];
            Material material = model.Materials[mesh.MaterialId];
            string textureName = String.IsNullOrEmpty(material.Name) ? "null" : material.Name;
            sb.Append('\t', indent);
            sb.Append($"<instance_geometry url=\"#geometry{meshId + 1}\">\n");
            sb.Append('\t', indent + 1);
            sb.Append("<bind_material>\n");
            sb.Append('\t', indent + 2);
            sb.Append("<technique_common>\n");
            sb.Append('\t', indent + 3);
            sb.Append($"<instance_material symbol=\"{textureName}-material\" target=\"#{textureName}-material\">\n");
            sb.Append('\t', indent + 4);
            sb.Append("<bind_vertex_input semantic=\"UVMap\" input_semantic=\"TEXCOORD\" input_set=\"0\" />\n");
            sb.Append('\t', indent + 3);
            sb.Append("</instance_material>\n");
            sb.Append('\t', indent + 2);
            sb.Append("</technique_common>\n");
            sb.Append('\t', indent + 1);
            sb.Append("</bind_material>\n");
            sb.Append('\t', indent);
            sb.Append("</instance_geometry>\n");
        }

        private static void ExportDlist(Model model, int dlistId, List<Vertex> meshVerts, List<Vertex> tempMeshVerts)
        {
            float[] vtx_state = { 0.0f, 0.0f, 0.0f };
            float[] nrm_state = { 0.0f, 0.0f, 0.0f };
            float[] uv_state = { 0.0f, 0.0f };
            float[] col_state = { 1.0f, 1.0f, 1.0f };
            int mtx_state = 0;
            int curMeshType = 0;
            bool curMeshActive = false;
            IReadOnlyList<RenderInstruction> list = model.RenderInstructionLists[dlistId];
            // todo: support DIF_AMB somehow
            foreach (RenderInstruction instruction in list)
            {
                switch (instruction.Code)
                {
                case InstructionCode.MTX_RESTORE:
                    mtx_state = (int)instruction.Arguments[0];
                    break;
                case InstructionCode.BEGIN_VTXS:
                    if (instruction.Arguments[0] < 0 || instruction.Arguments[0] > 3)
                    {
                        throw new ProgramException("Invalid geo type");
                    }
                    curMeshType = (int)instruction.Arguments[0] + 1;
                    curMeshActive = true;
                    meshVerts.Clear();
                    break;
                case InstructionCode.COLOR:
                    {
                        uint rgb = instruction.Arguments[0];
                        uint r = (rgb >> 0) & 0x1F;
                        uint g = (rgb >> 5) & 0x1F;
                        uint b = (rgb >> 10) & 0x1F;
                        col_state[0] = r / 31.0f;
                        col_state[1] = g / 31.0f;
                        col_state[2] = b / 31.0f;
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
                        nrm_state[0] = x / 512.0f;
                        nrm_state[1] = y / 512.0f;
                        nrm_state[2] = z / 512.0f;
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
                        uv_state[0] = s / 16.0f;
                        uv_state[1] = t / 16.0f;
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
                        vtx_state[0] = Fixed.ToFloat(x);
                        vtx_state[1] = Fixed.ToFloat(y);
                        vtx_state[2] = Fixed.ToFloat(z);
                        if (curMeshActive)
                        {
                            meshVerts.Add(GetCurrentExportTri(vtx_state, nrm_state, uv_state, col_state, mtx_state));
                        }
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
                        vtx_state[0] = x / 64.0f;
                        vtx_state[1] = y / 64.0f;
                        vtx_state[2] = z / 64.0f;
                        if (curMeshActive)
                        {
                            meshVerts.Add(GetCurrentExportTri(vtx_state, nrm_state, uv_state, col_state, mtx_state));
                        }
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
                        vtx_state[0] = Fixed.ToFloat(x);
                        vtx_state[1] = Fixed.ToFloat(y);
                        if (curMeshActive)
                        {
                            meshVerts.Add(GetCurrentExportTri(vtx_state, nrm_state, uv_state, col_state, mtx_state));
                        }
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
                        vtx_state[0] = Fixed.ToFloat(x);
                        vtx_state[2] = Fixed.ToFloat(z);
                        if (curMeshActive)
                        {
                            meshVerts.Add(GetCurrentExportTri(vtx_state, nrm_state, uv_state, col_state, mtx_state));
                        }
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
                        vtx_state[1] = Fixed.ToFloat(y);
                        vtx_state[2] = Fixed.ToFloat(z);
                        if (curMeshActive)
                        {
                            meshVerts.Add(GetCurrentExportTri(vtx_state, nrm_state, uv_state, col_state, mtx_state));
                        }
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
                        vtx_state[0] += Fixed.ToFloat(x);
                        vtx_state[1] += Fixed.ToFloat(y);
                        vtx_state[2] += Fixed.ToFloat(z);
                        if (curMeshActive)
                        {
                            meshVerts.Add(GetCurrentExportTri(vtx_state, nrm_state, uv_state, col_state, mtx_state));
                        }
                    }
                    break;
                case InstructionCode.END_VTXS:
                    {
                        curMeshActive = false;

                        // triangulate everything
                        // and start new mesh, since everything seems mixed up (what if a new mesh starts after END_VTXS)
                        var triangulatedMesh = new List<Vertex>();

                        switch (curMeshType)
                        {
                        case 1:
                            // standard triangles (nothing to do here)
                            triangulatedMesh = meshVerts;
                            break;
                        case 2:
                            // quads
                            if (meshVerts.Count > 3)
                            {
                                for (int i = 0; i < meshVerts.Count; i += 4)
                                {
                                    Vertex A = meshVerts[i];
                                    Vertex B = meshVerts[i + 1];
                                    Vertex C = meshVerts[i + 2];
                                    Vertex D = meshVerts[i + 3];

                                    triangulatedMesh.Add(A);
                                    triangulatedMesh.Add(B);
                                    triangulatedMesh.Add(C);
                                    triangulatedMesh.Add(C);
                                    triangulatedMesh.Add(D);
                                    triangulatedMesh.Add(A);
                                }
                            }
                            break;
                        case 3:
                            // triangle strip
                            if (meshVerts.Count > 2)
                            {
                                for (int i = 0; i < meshVerts.Count - 2; i++)
                                {
                                    Vertex A = meshVerts[i];
                                    Vertex B = meshVerts[i + 1];
                                    Vertex C = meshVerts[i + 2];

                                    if (i % 2 > 0)
                                    {
                                        triangulatedMesh.Add(C);
                                        triangulatedMesh.Add(B);
                                        triangulatedMesh.Add(A);
                                    }
                                    else
                                    {
                                        triangulatedMesh.Add(A);
                                        triangulatedMesh.Add(B);
                                        triangulatedMesh.Add(C);
                                    }
                                }
                            }
                            break;
                        case 4:
                            // quad strip
                            if (meshVerts.Count > 3)
                            {
                                for (int i = 0; i < meshVerts.Count - 2; i += 2)
                                {
                                    Vertex A = meshVerts[i];
                                    Vertex B = meshVerts[i + 1];
                                    Vertex C = meshVerts[i + 2];
                                    Vertex D = meshVerts[i + 3];

                                    triangulatedMesh.Add(A);
                                    triangulatedMesh.Add(B);
                                    triangulatedMesh.Add(C);
                                    triangulatedMesh.Add(D);
                                    triangulatedMesh.Add(C);
                                    triangulatedMesh.Add(B);
                                }
                            }
                            break;
                        }
                        curMeshType = -1;

                        tempMeshVerts.AddRange(triangulatedMesh);
                        meshVerts.Clear();
                    }
                    break;
                case InstructionCode.DIF_AMB:
                case InstructionCode.NOP:
                    break;
                default:
                    throw new ProgramException("Unknown opcode");
                }
            }
        }

        private static Vertex GetCurrentExportTri(float[] vtx_state, float[] nrm_state, float[] uv_state, float[] col_state, int mtx_state)
        {
            return new Vertex(
                position: new Vector3(vtx_state[0], vtx_state[1], vtx_state[2]),
                normal: new Vector3(nrm_state[0], nrm_state[1], nrm_state[2]),
                color: new Vector3(col_state[0], col_state[1], col_state[2]),
                uv: new Vector2(uv_state[0], uv_state[1]),
                matrixId: mtx_state
            );
        }
    }
}
