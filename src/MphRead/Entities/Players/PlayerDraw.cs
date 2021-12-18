using System.Buffers;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public partial class PlayerEntityNew
    {
        public void Draw()
        {
            DrawShadow();
            if (Flags2.TestFlag(PlayerFlags2.HideModel))
            {
                return;
            }
            if (Hunter == Hunter.Spire && Flags2.TestFlag(PlayerFlags2.AltAttack))
            {
                // sktodo: update spire alt animation
            }
            int lod = 0;
            Flags2 &= ~PlayerFlags2.Lod1;
            // todo: use main player's cam info pos, and make this configurable
            if (!IsMainPlayer && (Position - _scene.CameraPosition).LengthSquared >= 3)
            {
                lod = 1;
                Flags2 |= PlayerFlags2.Lod1;
            }
            _bipedModel1.SetModel(_bipedModelLods[lod].Model);
            _bipedModel2.SetModel(_bipedModelLods[lod].Model);
            Flags2 &= ~PlayerFlags2.DrawnThirdPerson;
            bool drawBiped = false;
            if (IsMainPlayer || IsVisible())
            {
                // todo: or if cam seq
                drawBiped = !IsMainPlayer || _viewType != 0 || _viewSwayTimer < Values.ViewSwayTime * 2; // todo: FPS stuff
                if (IsAltForm)
                {
                    // skhere
                }
                else if (drawBiped)
                {

                }
                else if (AttachedEnemy == null && _field6D0 == 0)
                {
                    Matrix4 transform = GetTransformMatrix(_aimVec, UpVector, _gunDrawPos);
                    UpdateTransforms(_gunModel, transform, Recolor);
                    GetDrawItems(_gunModel, _gunModel.Model.Nodes[0], _curAlpha);
                    if (Flags1.TestFlag(PlayerFlags1.DrawGunSmoke))
                    {
                        var drawPos = new Vector3(0, 0, Fixed.ToFloat(Values.MuzzleOffset));
                        drawPos = Matrix.Vec3MultMtx4(drawPos, transform);
                        transform.Row3.Xyz = drawPos;
                        UpdateTransforms(_gunSmokeModel, transform, Recolor);
                        GetDrawItems(_gunSmokeModel, _gunSmokeModel.Model.Nodes[0], _smokeAlpha);
                    }
                }
            }
            if (!IsMainPlayer && !drawBiped)
            {
                if (_chargeEffect != null)
                {
                    _chargeEffect.SetDrawEnabled(false);
                }
                if (_muzzleEffect != null)
                {
                    _muzzleEffect.SetDrawEnabled(false);
                }
            }
            // todo: draw lost octolith
        }

        private void UpdateTransforms(ModelInstance inst, Matrix4 transform, int recolor)
        {
            Model model = inst.Model;
            model.AnimateMaterials(inst.AnimInfo);
            model.AnimateTextures(inst.AnimInfo);
            model.ComputeNodeMatrices(index: 0);
            model.AnimateNodes(index: 0, UseNodeTransform || _scene.TransformRoomNodes, transform, model.Scale, inst.AnimInfo);
            model.UpdateMatrixStack(_scene.ViewInvRotMatrix, _scene.ViewInvRotYMatrix);
            _scene.UpdateMaterials(model, recolor);
        }

        private void GetDrawItems(ModelInstance inst, Node node, float alpha, int polygonId = -1)
        {
            if (alpha <= 0)
            {
                return;
            }
            if (polygonId == -1)
            {
                polygonId = _scene.GetNextPolygonId();
            }
            Model model = inst.Model;
            if (node.Enabled)
            {
                int start = node.MeshId / 2;
                for (int k = 0; k < node.MeshCount; k++)
                {
                    Mesh mesh = model.Meshes[start + k];
                    if (!mesh.Visible)
                    {
                        continue;
                    }
                    Material material = model.Materials[mesh.MaterialId];
                    Vector3 emission = GetEmission(inst, material, mesh.MaterialId);
                    Matrix4 texcoordMatrix = GetTexcoordMatrix(inst, material, mesh.MaterialId, node, _scene);
                    Vector4? color = null;
                    SelectionType selectionType = SelectionType.None;
                    int? bindingOverride = GetBindingOverride(inst, material, mesh.MaterialId);
                    _scene.AddRenderItem(material, polygonId, alpha, emission, GetLightInfo(_scene), texcoordMatrix, node.Animation, mesh.ListId,
                        model.NodeMatrixIds.Count, model.MatrixStackValues, color, PaletteOverride, selectionType, _drawScale, bindingOverride);
                }
                if (node.ChildIndex != -1)
                {
                    GetDrawItems(inst, model.Nodes[node.ChildIndex], alpha, polygonId);
                }
            }
            if (node.NextIndex != -1)
            {
                GetDrawItems(inst, model.Nodes[node.NextIndex], alpha, polygonId);
            }
        }

        protected override int? GetBindingOverride(ModelInstance inst, Material material, int index)
        {
            if (_doubleDmgTimer > 0 && (Hunter != Hunter.Spire || !(inst == _gunModel && index == 0)) && material.Lighting > 0)
            {
                return _doubleDmgBindingId;
            }
            return base.GetBindingOverride(inst, material, index);
        }

        protected override Vector3 GetEmission(ModelInstance inst, Material material, int index)
        {
            if (_doubleDmgTimer > 0 && (Hunter != Hunter.Spire || !(inst == _gunModel && index == 0)) && material.Lighting > 0)
            {
                return Metadata.EmissionGray;
            }
            if (Team == Team.Orange)
            {
                return Metadata.EmissionOrange;
            }
            if (Team == Team.Green)
            {
                return Metadata.EmissionGreen;
            }
            return base.GetEmission(inst, material, index);
        }

        protected override Matrix4 GetTexcoordMatrix(ModelInstance inst, Material material, int materialId, Node node, Scene scene)
        {
            if (_doubleDmgTimer > 0 && (Hunter != Hunter.Spire || !(inst == _gunModel && materialId == 0))
                && material.Lighting > 0 && node.BillboardMode == BillboardMode.None)
            {
                Texture texture = _doubleDmgModel.Model.Recolors[0].Textures[0];
                Matrix4 product = node.Animation.Keep3x3();
                Matrix4 texgenMatrix = Matrix4.Identity;
                // in-game, there's only one uniform scale factor for models
                if (inst.Model.Scale.X != 1 || inst.Model.Scale.Y != 1 || inst.Model.Scale.Z != 1)
                {
                    texgenMatrix = Matrix4.CreateScale(inst.Model.Scale) * texgenMatrix;
                }
                // in-game, bit 0 is set on creation if any materials have lighting enabled
                if (_anyLighting || (inst.Model.Header.Flags & 1) > 0)
                {
                    texgenMatrix = scene.ViewMatrix * texgenMatrix;
                }
                product *= texgenMatrix;
                product.M12 *= -1;
                product.M13 *= -1;
                product.M22 *= -1;
                product.M23 *= -1;
                product.M32 *= -1;
                product.M33 *= -1;
                ulong frame = scene.FrameCount / 2;
                float rotZ = ((int)(16 * ((781874935307L * (53248 * frame) >> 32) + 2048)) >> 20) * (360 / 4096f);
                float rotY = ((int)(16 * ((781874935307L * (26624 * frame) + 0x80000000000) >> 32)) >> 20) * (360 / 4096f);
                var rot = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(rotZ));
                rot *= Matrix4.CreateRotationY(MathHelper.DegreesToRadians(rotY));
                product = rot * product;
                product *= (1.0f / (texture.Width / 2));
                product = new Matrix4(
                    product.Row0 * 16.0f,
                    product.Row1 * 16.0f,
                    product.Row2 * 16.0f,
                    product.Row3
                );
                product.Transpose();
                return product;
            }
            return base.GetTexcoordMatrix(inst, material, materialId, node, scene);
        }

        private void DrawShadow()
        {
            if (IsMainPlayer && _viewType == 0)
            {
                return;
            }
            Material material = _trailModel.Model.Materials[1];
            Vector3 point1 = _volume.SpherePosition;
            Vector3 point2 = _volume.SpherePosition.AddY(-10);
            CollisionResult colRes = default;
            if (CollisionDetection.CheckBetweenPoints(point1, point2, TestFlags.None, _scene, ref colRes)
                && colRes.Plane.Y >= Fixed.ToFloat(4))
            {
                float height = point1.Y - colRes.Position.Y;
                if (height < 10)
                {
                    float pct = 1 - height / 10;
                    float alpha = _curAlpha * pct;
                    if (_health == 0)
                    {
                        float decrease = 2 * (_respawnTime - _respawnTimer) / 2f; // todo: FPS stuff
                        alpha -= decrease;
                    }
                    if (alpha > 0)
                    {
                        Vector3 row1 = Vector3.Cross(colRes.Plane.Xyz, Vector3.UnitZ).Normalized();
                        Vector3 row2 = colRes.Plane.Xyz;
                        var row3 = Vector3.Cross(row1, colRes.Plane.Xyz);
                        row1 *= pct;
                        row2 *= pct;
                        row3 *= pct;
                        float factor = Fixed.ToFloat(100);
                        var row4 = new Vector3(
                            colRes.Position.X + colRes.Plane.X * factor,
                            colRes.Position.Y + colRes.Plane.Y * factor,
                            colRes.Position.Z + colRes.Plane.Z * factor
                        );
                        var transform = new Matrix4(
                            row1.X, row1.Y, row1.Z, 0,
                            row2.X, row2.Y, row2.Z, 0,
                            row3.X, row3.Y, row3.Z, 0,
                            row4.X, row4.Y, row4.Z, 1
                        );
                        Vector3[] uvsAndVerts = ArrayPool<Vector3>.Shared.Rent(8);
                        uvsAndVerts[0] = new Vector3(0, 0, 0);
                        uvsAndVerts[1] = new Vector3(-0.75f, 0.03125f, -0.75f);
                        uvsAndVerts[2] = new Vector3(0, 1, 0);
                        uvsAndVerts[3] = new Vector3(-0.75f, 0.03125f, 0.75f);
                        uvsAndVerts[4] = new Vector3(1, 1, 0);
                        uvsAndVerts[5] = new Vector3(0.75f, 0.03125f, 0.75f);
                        uvsAndVerts[6] = new Vector3(1, 0, 0);
                        uvsAndVerts[7] = new Vector3(0.75f, 0.03125f, -0.75f);
                        int polygonId = _scene.GetNextPolygonId();
                        var color = new Vector3(0, 0, 0);
                        _scene.AddRenderItem(RenderItemType.Particle, alpha, polygonId, color, material.XRepeat, material.YRepeat,
                            material.ScaleS, material.ScaleT, transform, uvsAndVerts, _trailBindingId2);
                    }
                }
            }
        }

        public override void GetDrawInfo(Scene scene)
        {
        }
    }
}
