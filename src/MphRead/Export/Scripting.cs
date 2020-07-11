using System;
using System.Linq;
using System.Text;

namespace MphRead.Export
{
    public class Scripting
    {
        // todo: mph_common, auto-import + script running
        public static string GenerateScript(Model model)
        {
            var sb = new StringBuilder();
            sb.AppendLine("import mph_common");
            sb.AppendLine("set_common()");
            for (int i = 0; i < model.Materials.Count; i++)
            {
                Material material = model.Materials[i];
                if (material.TextureId != UInt16.MaxValue)
                {
                    sb.AppendLine($"set_texture_alpha('{material.Name}_mat', {material.Alpha})");
                }
                else if (material.Alpha < 31)
                {
                    sb.AppendLine($"set_material_alpha('{material.Name}_mat', {material.Alpha})");
                }
                if (material.Culling == CullingMode.Back || material.Culling == CullingMode.Front)
                {
                    sb.AppendLine($"set_back_culling('{material.Name}_mat')");
                    if (material.Culling == CullingMode.Front)
                    {
                        for (int j = 0; j < model.Meshes.Count; j++)
                        {
                            Mesh mesh = model.Meshes[j];
                            if (mesh.MaterialId == i)
                            {
                                sb.AppendLine($"invert_normals('geom{j + 1}_obj')");
                            }
                        }
                    }
                }
            }
            foreach (Node node in model.Nodes.Where(n => n.Billboard))
            {
                sb.AppendLine($"set_billboard('{node.Name}')");
            }
            return sb.ToString();
        }
    }
}
