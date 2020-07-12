using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MphRead.Export
{
    public class Scripting
    {
        // todo: auto-import + script running
        public static string GenerateScript(Model model)
        {
            var sb = new StringBuilder();
            void AppendIndent()
            {
                sb!.Append(' ', 4);
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
            sb.AppendLine($"f'{daePath}')");
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
                if (node.Billboard)
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
