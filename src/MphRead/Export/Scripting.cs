using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using OpenTK.Mathematics;

namespace MphRead.Export
{
    public class Scripting
    {
        private static void PrintAnimations(Model model, StringBuilder sb)
        {
            int indent = 1;
            sb.AppendIndent("uv_anims = [", indent);
            foreach (TexcoordAnimationGroup group in model.Animations.TexcoordGroups)
            {
                sb.AppendIndent("{", indent + 1);
                foreach (KeyValuePair<string, TexcoordAnimation> kvp in group.Animations)
                {
                    TexcoordAnimation anim = kvp.Value;
                    sb.AppendIndent($"'{kvp.Key}':", indent + 2);
                    sb.AppendIndent("[", indent + 2);
                    for (int frame = 0; frame < group.FrameCount; frame++)
                    {
                        float scaleS = RenderWindow.InterpolateAnimation(group.Scales, anim.ScaleLutIndexS, frame,
                            anim.ScaleBlendS, anim.ScaleLutLengthS, group.FrameCount);
                        float scaleT = RenderWindow.InterpolateAnimation(group.Scales, anim.ScaleLutIndexT, frame,
                            anim.ScaleBlendT, anim.ScaleLutLengthT, group.FrameCount);
                        float rotate = RenderWindow.InterpolateAnimation(group.Rotations, anim.RotateLutIndexZ, frame,
                            anim.RotateBlendZ, anim.RotateLutLengthZ, group.FrameCount, isRotation: true);
                        float translateS = RenderWindow.InterpolateAnimation(group.Translations, anim.TranslateLutIndexS, frame,
                            anim.TranslateBlendS, anim.TranslateLutLengthS, group.FrameCount);
                        float translateT = RenderWindow.InterpolateAnimation(group.Translations, anim.TranslateLutIndexT, frame,
                            anim.TranslateBlendT, anim.TranslateLutLengthT, group.FrameCount);
                        sb.AppendIndent($"[{scaleS}, {scaleT}, {rotate}, {translateS}, {translateT}],", indent + 3);
                    }
                    sb.AppendIndent("],", indent + 2);
                }
                sb.AppendIndent("},", indent + 1);
            }
            sb.AppendIndent("]", indent);
            sb.AppendIndent("mat_anims = [", indent);
            foreach (MaterialAnimationGroup group in model.Animations.MaterialGroups)
            {
                sb.AppendIndent("{", indent + 1);
                foreach (KeyValuePair<string, MaterialAnimation> kvp in group.Animations)
                {
                    MaterialAnimation anim = kvp.Value;
                    sb.AppendIndent($"'{kvp.Key}_mat':", indent + 2);
                    sb.AppendIndent("[", indent + 2);
                    for (int frame = 0; frame < group.FrameCount; frame++)
                    {
                        // todo: animate diffuse/ambient/specular light colors
                        float red = RenderWindow.InterpolateAnimation(group.Colors, anim.DiffuseLutIndexR, frame,
                            anim.DiffuseBlendR, anim.DiffuseLutLengthR, group.FrameCount);
                        float green = RenderWindow.InterpolateAnimation(group.Colors, anim.DiffuseLutIndexG, frame,
                            anim.DiffuseBlendG, anim.DiffuseLutLengthG, group.FrameCount);
                        float blue = RenderWindow.InterpolateAnimation(group.Colors, anim.DiffuseLutIndexB, frame,
                            anim.DiffuseBlendB, anim.DiffuseLutLengthB, group.FrameCount);
                        float alpha = RenderWindow.InterpolateAnimation(group.Colors, anim.AlphaLutIndex, frame,
                            anim.AlphaBlend, anim.AlphaLutLength, group.FrameCount);
                        sb.AppendIndent($"[{red / 31.0f}, {green / 31.0f}, {blue / 31.0f}, {alpha / 31.0f}],", indent + 3);
                    }
                    sb.AppendIndent("],", indent + 2);
                }
                sb.AppendIndent("},", indent + 1);
            }
            sb.AppendIndent("]", indent);

            sb.AppendIndent("tex_anims = [", indent);
            var combos = new List<(int, int)>();
            foreach (TextureAnimationGroup group in model.Animations.TextureGroups)
            {
                foreach (KeyValuePair<string, TextureAnimation> kvp in group.Animations)
                {
                    for (int i = kvp.Value.StartIndex; i < kvp.Value.StartIndex + kvp.Value.Count; i++)
                    {
                        (int, int) pair = (group.TextureIds[i], group.PaletteIds[i]);
                        if (!combos.Contains(pair))
                        {
                            combos.Add(pair);
                        }
                    }
                }
            }
            foreach (TextureAnimationGroup group in model.Animations.TextureGroups)
            {
                sb.AppendIndent("{", indent + 1);
                foreach (KeyValuePair<string, TextureAnimation> kvp in group.Animations)
                {
                    TextureAnimation anim = kvp.Value;
                    sb.AppendIndent($"'{kvp.Key}_mat':", indent + 2);
                    sb.AppendIndent("[", indent + 2);
                    for (int i = kvp.Value.StartIndex; i < kvp.Value.StartIndex + kvp.Value.Count; i++)
                    {
                        int frame = group.FrameIndices[i];
                        int index = combos.IndexOf((group.TextureIds[i], group.PaletteIds[i]));
                        Debug.Assert(index != -1);
                        sb.AppendIndent($"[{frame}, {index}],", indent + 3);
                    }
                    sb.AppendIndent("],", indent + 2);
                }
                sb.AppendIndent("},", indent + 1);
            }
            sb.AppendIndent("]", indent);


            sb.AppendIndent("node_anims = [", indent);
            foreach (NodeAnimationGroup group in model.Animations.NodeGroups)
            {
                sb.AppendIndent("{", indent + 1);
                foreach (KeyValuePair<string, NodeAnimation> kvp in group.Animations)
                {
                    NodeAnimation anim = kvp.Value;
                    sb.AppendIndent($"'{kvp.Key}':", indent + 2);
                    sb.AppendIndent("[", indent + 2);
                    for (int frame = 0; frame < group.FrameCount; frame++)
                    {
                        float scaleX = RenderWindow.InterpolateAnimation(group.Scales, anim.ScaleLutIndexX, frame,
                            anim.ScaleBlendX, anim.ScaleLutLengthX, group.FrameCount);
                        float scaleY = RenderWindow.InterpolateAnimation(group.Scales, anim.ScaleLutIndexY, frame,
                            anim.ScaleBlendY, anim.ScaleLutLengthY, group.FrameCount);
                        float scaleZ = RenderWindow.InterpolateAnimation(group.Scales, anim.ScaleLutIndexZ, frame,
                            anim.ScaleBlendZ, anim.ScaleLutLengthZ, group.FrameCount);
                        float rotateX = RenderWindow.InterpolateAnimation(group.Rotations, anim.RotateLutIndexX, frame,
                            anim.RotateBlendX, anim.RotateLutLengthX, group.FrameCount, isRotation: true);
                        float rotateY = RenderWindow.InterpolateAnimation(group.Rotations, anim.RotateLutIndexY, frame,
                            anim.RotateBlendY, anim.RotateLutLengthY, group.FrameCount, isRotation: true);
                        float rotateZ = RenderWindow.InterpolateAnimation(group.Rotations, anim.RotateLutIndexZ, frame,
                            anim.RotateBlendZ, anim.RotateLutLengthZ, group.FrameCount, isRotation: true);
                        float translateX = RenderWindow.InterpolateAnimation(group.Translations, anim.TranslateLutIndexX, frame,
                            anim.TranslateBlendX, anim.TranslateLutLengthX, group.FrameCount);
                        float translateY = RenderWindow.InterpolateAnimation(group.Translations, anim.TranslateLutIndexY, frame,
                            anim.TranslateBlendY, anim.TranslateLutLengthY, group.FrameCount);
                        float translateZ = RenderWindow.InterpolateAnimation(group.Translations, anim.TranslateLutIndexZ, frame,
                            anim.TranslateBlendZ, anim.TranslateLutLengthZ, group.FrameCount);
                        sb.AppendIndent($"[{scaleX}, {scaleY}, {scaleZ}, {rotateX}, {rotateY}, {rotateZ}," +
                            $" {translateX}, {translateY}, {translateZ}],", indent + 3);
                    }
                    sb.AppendIndent("],", indent + 2);
                }
                sb.AppendIndent("},", indent + 1);
            }
            sb.AppendIndent("]", indent);
        }

        public static string GenerateScript(Model model, Dictionary<string, IReadOnlyList<Collada.Vertex>> lists)
        {
            var sb = new StringBuilder();
            sb.AppendLine("import bpy");
            sb.AppendLine("import math");
            sb.AppendLine("import mathutils");
            sb.AppendLine("from mph_common import *");
            sb.AppendLine();
            sb.AppendLine($"expected_version = '{Program.Version}'");
            sb.AppendLine($"# recolors: {String.Join(", ", model.Recolors.Select(r => r.Name))}");
            sb.AppendLine($"recolor = '{model.Recolors.First().Name}'");
            sb.AppendLine($"# uv anims: {model.Animations.TexcoordGroups.Count}, mat anims: {model.Animations.MaterialGroups.Count}," +
                $" node anims: {model.Animations.NodeGroups.Count}, tex anims: {model.Animations.TextureGroups.Count}");
            int texcoordId = model.Animations.TexcoordGroups.Count > 0 ? 0 : -1;
            int materialId = model.Animations.MaterialGroups.Count > 0 ? 0 : -1;
            int nodeId = model.Animations.NodeGroups.Count > 0 ? 0 : -1;
            int textureId = model.Animations.TextureGroups.Count > 0 ? 0 : -1;
            sb.AppendLine($"uv_index = {texcoordId}");
            sb.AppendLine($"mat_index = {materialId}");
            sb.AppendLine($"node_index = {nodeId}");
            sb.AppendLine($"tex_index = {textureId}");
            sb.AppendLine();
            sb.AppendLine("def import_dae(suffix):");
            sb.AppendIndent();
            sb.AppendLine("cleanup()");
            sb.AppendIndent();
            string daePath = Path.Combine(Paths.Export, model.Name, $"{model.Name}_{{suffix}}.dae");
            sb.AppendLine("bpy.ops.wm.collada_import(filepath =");
            sb.AppendIndent();
            sb.AppendIndent();
            sb.AppendLine($"fr'{daePath}')");
            sb.AppendIndent();
            sb.AppendLine("set_common()");
            var invertMeshIds = new HashSet<int>();
            for (int i = 0; i < model.Materials.Count; i++)
            {
                Material material = model.Materials[i];
                if (material.TextureId != UInt16.MaxValue)
                {
                    sb.AppendIndent();
                    // this assumes this is the same between all recolors, which is the case in MPH
                    bool alphaPixels = model.Recolors.First()
                        .GetPixels(material.TextureId, material.PaletteId).Any(p => p.Alpha < 255);
                    sb.AppendLine($"set_texture_alpha('{material.Name}_mat', {material.Alpha}, {(alphaPixels ? "True" : "False")})");
                    bool mirrorX = material.XRepeat == RepeatMode.Mirror;
                    bool mirrorY = material.YRepeat == RepeatMode.Mirror;
                    if (mirrorX || mirrorY)
                    {
                        sb.AppendIndent();
                        sb.AppendLine($"set_mirror('{material.Name}_mat', {(mirrorX ? "True" : "False")}, {(mirrorY ? "True" : "False")})");
                    }
                }
                else
                {
                    sb.AppendIndent();
                    sb.AppendLine($"set_material_alpha('{material.Name}_mat', {material.Alpha})");
                }
                if (material.Culling == CullingMode.Back || material.Culling == CullingMode.Front)
                {
                    sb.AppendIndent();
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
                // if any meshes using this material don't call COLOR in their dlist, we need to set the material diffuse as the base color
                // (and might need to duplicate the material, if other meshes sharing the material do call COLOR)
                // --> also, when dlists contain COLOR commands, one always precedes the VTX commands, so we don't have to worry about that
                var withColor = new List<int>();
                var noColor = new List<int>();
                for (int j = 0; j < model.Meshes.Count; j++)
                {
                    Mesh mesh = model.Meshes[j];
                    if (mesh.MaterialId == i)
                    {
                        if (model.RenderInstructionLists[mesh.DlistId].Any(i => i.Code == InstructionCode.COLOR))
                        {
                            withColor.Add(j);
                        }
                        else
                        {
                            noColor.Add(j);
                        }
                    }
                }
                if (noColor.Count > 0)
                {
                    string color = $"{material.Diffuse.Red / 31.0f}, {material.Diffuse.Green / 31.0f}, {material.Diffuse.Blue / 31.0f}";
                    if (withColor.Count > 0)
                    {
                        string objects = String.Join("', '", noColor.Select(c => $"geom{c}_obj"));
                        sb.AppendIndent($"set_mat_color('{material.Name}_mat', {color}, True, ['{objects}'])");
                    }
                    else
                    {
                        sb.AppendIndent($"set_mat_color('{material.Name}_mat', {color}, False, [])");
                    }
                }
            }
            foreach (Node node in model.Nodes)
            {
                foreach (int meshId in node.GetMeshIds().Where(m => invertMeshIds.Contains(m)))
                {
                    sb.AppendIndent();
                    sb.AppendLine($"invert_normals('geom{meshId + 1}_obj')");
                }
            }
            if (model.NodeMatrixIds.Count > 0)
            {
                sb.AppendIndent("bone_setup()");
            }
            sb.AppendIndent("anim_setup()");
            foreach (Node node in model.Nodes.Where(n => n.BillboardMode != BillboardMode.None))
            {
                foreach (int meshId in node.GetMeshIds())
                {
                    sb.AppendIndent();
                    sb.AppendLine($"set_billboard('geom{meshId + 1}_obj', {(int)node.BillboardMode})");
                }
            }
            if (model.NodeMatrixIds.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("def bone_setup():");
                sb.AppendIndent("bpy.ops.object.mode_set(mode = 'OBJECT')");
                sb.AppendIndent(@"
bpy.ops.object.armature_add(enter_editmode=True, align='WORLD', location=(0, 0, 0))
bpy.ops.armature.select_all(action='SELECT')
bpy.ops.armature.delete()");

                foreach (Node node in model.Nodes)
                {
                    sb.AppendIndent($"bpy.ops.armature.bone_primitive_add(name='{node.Name}')");
                }
                sb.AppendIndent("bpy.ops.armature.select_all(action='DESELECT')");
                sb.AppendIndent("bones = bpy.data.armatures[0].edit_bones");

                foreach (Node child in model.Nodes.Where(n => n.ParentIndex != UInt16.MaxValue))
                {
                    Node parent = model.Nodes[child.ParentIndex];
                    sb.AppendIndent($"bones.get('{child.Name}').parent = bones.get('{parent.Name}')");
                }

                sb.AppendIndent(@"
bpy.ops.object.editmode_toggle()
bpy.ops.object.select_all(action='DESELECT')
for obj in bpy.data.objects:
    if obj.type == 'MESH':
        obj.select_set(True)
bpy.data.objects['Armature'].select_set(True)
bpy.ops.object.parent_set(type='ARMATURE_NAME')");

                foreach (KeyValuePair<string, IReadOnlyList<Collada.Vertex>> obj in lists)
                {
                    sb.AppendIndent("bpy.ops.object.select_all(action='DESELECT')");
                    sb.AppendIndent($"obj = bpy.data.objects['{obj.Key}']");
                    sb.AppendIndent("obj.select_set(True)");
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
                        sb.AppendIndent($"group = obj.vertex_groups['{kvp.Key}']");
                        sb.AppendIndent($"group.add([{String.Join(", ", kvp.Value)}], 1.0, 'ADD')");
                    }
                }

                foreach (Node node in model.Nodes)
                {
                    sb.AppendIndent($"bone = bpy.data.objects['Armature'].pose.bones['{node.Name}']");
                    sb.AppendIndent("bone.rotation_mode = 'XYZ'");
                    if (node.Scale != Vector3.One)
                    {
                        sb.AppendIndent($"bone.scale = mathutils.Vector(({node.Scale.X}, {node.Scale.Y}, {node.Scale.Z}))");
                    }
                    if (node.Angle != Vector3.Zero)
                    {
                        float x = node.Angle.X;
                        float y = node.Angle.Y;
                        float z = node.Angle.Z;
                        sb.AppendIndent($"bone.rotation_euler = mathutils.Vector(({x}, {y}, {z}))");
                    }
                    if (node.Position != Vector3.Zero)
                    {
                        sb.AppendIndent($"bone.location = mathutils.Vector(({node.Position.X}, {node.Position.Y}, {node.Position.Z}))");
                    }
                }
            }
            sb.AppendLine();
            // todo: make sure active_material doesn't cause problems with untextured meshes
            sb.AppendLine("def anim_setup():");
            PrintAnimations(model, sb);
            sb.AppendIndent("bpy.context.scene.render.fps = 30");
            sb.AppendIndent("if uv_index >= 0:");
            sb.AppendIndent();
            sb.AppendIndent("set_uv_anims(uv_anims[uv_index])");
            sb.AppendIndent("if tex_index >= 0:");
            sb.AppendIndent();
            sb.AppendIndent("set_tex_anims(tex_anims[tex_index])");
            sb.AppendIndent("if node_index >= 0:");
            sb.AppendIndent();
            sb.AppendIndent("set_node_anims(node_anims[node_index])");
            sb.AppendIndent("if mat_index >= 0:");
            sb.AppendIndent();
            sb.AppendIndent("set_mat_anims(mat_anims[mat_index])");
            sb.AppendLine();
            sb.AppendLine("if __name__ == '__main__':");
            sb.AppendIndent("version = get_common_version()");
            sb.AppendIndent("if expected_version != version:");
            sb.AppendIndent();
            sb.AppendIndent("raise Exception(f'Expected mph_common version {expected_version} but found {version} instead.')");
            sb.AppendIndent($"import_dae(recolor)");
            return sb.ToString();
        }
    }

    public static class StringBuilderExtensions
    {
        public static void AppendIndent(this StringBuilder sb)
        {
            sb.Append(' ', 4);
        }

        public static void AppendIndent(this StringBuilder sb, string text, int indent = 1)
        {
            foreach (string part in text.Trim().Split("\r\n"))
            {
                for (int i = 0; i < indent; i++)
                {
                    sb.Append(' ', 4);
                }
                sb.AppendLine(part);
            }
        }
    }
}
