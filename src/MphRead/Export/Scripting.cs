using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenTK.Mathematics;

namespace MphRead.Export
{
    public class Scripting
    {
        public static string GenerateScript(Model model, Dictionary<string, IReadOnlyList<Collada.Vertex>> lists)
        {
            var sb = new StringBuilder();
            void AppendIndent()
            {
                sb!.Append(' ', 4);
            }

            sb.AppendLine(@"
import bpy
import math
import mathutils

bpy.ops.object.armature_add(enter_editmode=True, align='WORLD', location=(0, 0, 0))
bpy.ops.armature.select_all(action='SELECT')
bpy.ops.armature.delete()");

            foreach (Node node in model.Nodes)
            {
                sb.AppendLine($"bpy.ops.armature.bone_primitive_add(name='{node.Name}')");
            }
            sb.AppendLine("bpy.ops.armature.select_all(action='DESELECT')");
            sb.AppendLine("bones = bpy.data.armatures[0].edit_bones");

            foreach (Node child in model.Nodes.Where(n => n.ParentIndex != UInt16.MaxValue))
            {
                Node parent = model.Nodes[child.ParentIndex];
                sb.AppendLine($"bones.get('{child.Name}').parent = bones.get('{parent.Name}')");
            }

            sb.AppendLine(@"
bpy.ops.object.editmode_toggle()
bpy.ops.object.select_all(action='DESELECT')
for obj in bpy.data.objects:
    if obj.type == 'MESH':
        obj.select_set(True)
bpy.data.objects['Armature'].select_set(True)
bpy.ops.object.parent_set(type='ARMATURE_NAME')");

            foreach (KeyValuePair<string, IReadOnlyList<Collada.Vertex>> obj in lists)
            {
                sb.AppendLine("bpy.ops.object.select_all(action='DESELECT')");
                sb.AppendLine($"obj = bpy.data.objects['{obj.Key}']");
                sb.AppendLine("obj.select_set(True)");
                var vertices = new Dictionary<string, List<int>>();
                int i = 0;
                foreach (Collada.Vertex vertex in obj.Value)
                {
                    Node node = model.Nodes[model.NodeMatrixIds[vertex.MatrixId]];
                    if (!vertices.ContainsKey(node.Name))
                    {
                        vertices.Add(node.Name, new List<int>() { i });
                    }
                    else
                    {
                        vertices[node.Name].Add(i);
                    }
                    i++;
                }
                foreach (KeyValuePair<string, List<int>> kvp in vertices)
                {
                    sb.AppendLine($"group = obj.vertex_groups['{kvp.Key}']");
                    sb.AppendLine($"group.add([{String.Join(", ", kvp.Value)}], 1.0, 'ADD')");
                }
            }

            foreach (Node node in model.Nodes.Where(n => n.Scale != Vector3.One || n.Angle != Vector3.Zero || n.Position != Vector3.Zero))
            {
                sb.AppendLine($"bone = bpy.data.objects['Armature'].pose.bones['{node.Name}']");
                if (node.Scale != Vector3.One)
                {
                    sb.AppendLine($"bone.scale = mathutils.Vector(({node.Scale.X}, {node.Scale.Y}, {node.Scale.Z}))");
                }
                if (node.Angle != Vector3.Zero)
                {
                    sb.AppendLine("bone.rotation_mode = 'XYZ'");
                    //float x = MathHelper.DegreesToRadians(node.Angle.X);
                    //float y = MathHelper.DegreesToRadians(node.Angle.Y);
                    //float z = MathHelper.DegreesToRadians(node.Angle.Z);
                    float x = node.Angle.X;
                    float y = node.Angle.Y;
                    float z = node.Angle.Z;
                    sb.AppendLine($"bone.rotation_euler = mathutils.Vector(({x}, {y}, {z}))");
                    sb.AppendLine("bone.rotation_mode = 'QUATERNION'");
                }
                if (node.Position != Vector3.Zero)
                {
                    sb.AppendLine($"bone.location = mathutils.Vector(({node.Position.X}, {node.Position.Y}, {node.Position.Z}))");
                }
            }

            sb.AppendLine("import bpy");
            sb.AppendLine("from mph_common import *");
            sb.AppendLine();
            sb.AppendLine("def import_dae(recolor):");
            AppendIndent();
            sb.AppendLine("cleanup()");
            AppendIndent();
            string daePath = Path.Combine(Paths.Export, model.Name, $"{model.Name}_{{recolor}}.dae");
            sb.AppendLine("bpy.ops.wm.collada_import(filepath =");
            AppendIndent();
            AppendIndent();
            sb.AppendLine($"fr'{daePath}')");
            AppendIndent();
            sb.AppendLine("set_common()");
            var invertMeshIds = new HashSet<int>();
            for (int i = 0; i < model.Materials.Count; i++)
            {
                Material material = model.Materials[i];
                if (material.TextureId != UInt16.MaxValue)
                {
                    AppendIndent();
                    // this assumes this is the same between all recolors, which is the case in MPH
                    bool alphaPixels = model.Recolors.First()
                        .GetPixels(material.TextureId, material.PaletteId).Any(p => p.Alpha < 255);
                    sb.AppendLine($"set_texture_alpha('{material.Name}_mat', {material.Alpha}, {(alphaPixels ? "True" : "False")})");
                    bool mirrorX = material.XRepeat == RepeatMode.Mirror;
                    bool mirrorY = material.YRepeat == RepeatMode.Mirror;
                    if (mirrorX || mirrorY)
                    {
                        AppendIndent();
                        sb.AppendLine($"set_mirror('{material.Name}_mat', {(mirrorX ? "True" : "False")}, {(mirrorY ? "True" : "False")})");
                    }
                }
                else if (material.Alpha < 31)
                {
                    AppendIndent();
                    sb.AppendLine($"set_material_alpha('{material.Name}_mat', {material.Alpha})");
                }
                if (material.Culling == CullingMode.Back || material.Culling == CullingMode.Front)
                {
                    AppendIndent();
                    sb.AppendLine($"set_back_culling('{material.Name}_mat')");
                    if (material.Culling == CullingMode.Front)
                    {
                        for (int j = 0; j < model.Meshes.Count; j++)
                        {
                            Mesh mesh = model.Meshes[j];
                            if (mesh.MaterialId == i)
                            {
                                invertMeshIds.Add(j);
                            }
                        }
                    }
                }
            }
            foreach (Node node in model.Nodes)
            {
                // todo: implement this for cylinders
                if (node.BillboardMode == BillboardMode.Sphere)
                {
                    AppendIndent();
                    sb.AppendLine($"set_billboard('{node.Name}')");
                }
                foreach (int meshId in node.GetMeshIds())
                {
                    if (invertMeshIds.Contains(meshId))
                    {
                        AppendIndent();
                        if (node.MeshCount == 1)
                        {
                            sb.AppendLine($"invert_normals('{node.Name}')");
                        }
                        else
                        {
                            sb.AppendLine($"invert_normals('geom{meshId + 1}_obj')");
                        }
                    }
                }
            }
            sb.AppendLine();
            sb.AppendLine("if __name__ == '__main__':");
            AppendIndent();
            sb.AppendLine($"# recolors: {String.Join(", ", model.Recolors.Select(r => r.Name))}");
            AppendIndent();
            sb.AppendLine($"import_dae('{model.Recolors.First().Name}')");
            return sb.ToString();
        }
    }
}
