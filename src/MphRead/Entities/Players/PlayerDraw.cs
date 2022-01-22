using System;
using System.Buffers;
using System.Diagnostics;
using MphRead.Formats;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public partial class PlayerEntity
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
                UpdateSpireAltAttack();
            }
            int lod = 0;
            Flags2 &= ~PlayerFlags2.Lod1;
            // todo: make this configurable
            if (!IsMainPlayer && (Position - CameraInfo.Position).LengthSquared >= 3 * 3)
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
                drawBiped = !IsMainPlayer || CameraType != CameraType.First || CameraSequence.Current != null
                    || _camSwitchTimer < Values.CamSwitchTime * 2; // todo: FPS stuff
                if (IsAltForm)
                {
                    _modelTransform.Row3.Xyz = Position;
                    if (_timeSinceDamage < Values.DamageFlashTime * 2) // todo: FPS stuff
                    {
                        PaletteOverride = Metadata.RedPalette;
                    }
                    if (Hunter == Hunter.Kanden)
                    {
                        DrawKandenAlt();
                    }
                    else if (Hunter == Hunter.Spire && Flags2.TestFlag(PlayerFlags2.AltAttack))
                    {
                        DrawSpireAltAttack();
                    }
                    else
                    {
                        UpdateTransforms(_altModel, _modelTransform, Recolor);
                        GetDrawItems(_altModel, _altModel.Model.Nodes[0], _curAlpha);
                    }
                    PaletteOverride = null;
                    if (_frozenGfxTimer > 0)
                    {
                        float radius = _volume.SphereRadius + 0.2f;
                        Matrix4 transform = Matrix4.CreateScale(radius) * _modelTransform;
                        transform.Row3.Y += Fixed.ToFloat(Values.AltColYPos);
                        UpdateTransforms(_altIceModel, transform, recolor: 0);
                        GetDrawItems(_altIceModel, _altIceModel.Model.Nodes[0], alpha: 1, recolor: 0);
                    }
                    if (Hunter == Hunter.Samus && !Flags2.TestFlag(PlayerFlags2.Cloaking))
                    {
                        DrawMorphBallTrail();
                    }
                    _modelTransform.Row3.Xyz = Vector3.Zero;
                    Flags2 |= PlayerFlags2.DrawnThirdPerson;
                }
                else if (drawBiped)
                {
                    // we want to animate first up to the spine with _bipedModel1's animation info, then the rest with _bipedModel2's info
                    Node spineNode = _spineNodes[lod]!;
                    spineNode.AnimIgnoreChild = true;
                    // todo: we can just figure out the angle directly from the facing vector
                    Vector3 facing = _facingVector;
                    float limit = Fixed.ToFloat(2896);
                    float cos = MathF.Sqrt(1 - facing.Y * facing.Y);
                    float sin = facing.Y;
                    if (MathF.Abs(facing.Y) > limit)
                    {
                        cos = limit;
                        sin = facing.Y <= 0 ? -limit : limit;
                    }
                    float angle = MathF.Atan2(sin, cos);
                    spineNode.AfterTransform = Matrix4.CreateRotationZ(angle);
                    Model model = _bipedModel1.Model;
                    model.AnimateNodes(index: 0, false, Matrix4.Identity, Vector3.One, _bipedModel1.AnimInfo);
                    spineNode.AnimIgnoreChild = false;
                    model.AnimateNodes(spineNode.ChildIndex, false, Matrix4.Identity, Vector3.One, _bipedModel2.AnimInfo);
                    spineNode.AfterTransform = null;
                    float scale = Metadata.HunterScales[Hunter];
                    float bottom = Fixed.ToFloat(Values.MinPickupHeight);
                    var lateral = new Vector3(_field70, 0, _field74);
                    Matrix4 transform = Matrix4.Identity;
                    transform.Row0.Xyz = -_gunVec2;
                    transform.Row1.Xyz = Vector3.Cross(lateral, _gunVec2);
                    transform.Row2.Xyz = -lateral;
                    transform.Row3.Xyz = Position;
                    transform.Row3.Y += bottom + bottom * (1 - scale);
                    transform.Row0.Xyz *= scale;
                    transform.Row1.Xyz *= scale;
                    transform.Row2.Xyz *= scale;
                    for (int i = 0; i < model.Nodes.Count; i++)
                    {
                        Node node = model.Nodes[i];
                        node.Animation *= transform; // todo?: could do this in the shader
                    }
                    model.UpdateMatrixStack();
                    if (_health > 0)
                    {
                        if (_timeSinceDamage < Values.DamageFlashTime * 2) // todo: FPS stuff
                        {
                            PaletteOverride = Metadata.RedPalette;
                        }
                        float alpha = _curAlpha;
                        if (IsMainPlayer && CameraSequence.Current == null
                            && _bipedModel1.AnimInfo.Index[0] == (int)PlayerAnimation.Unmorph)
                        {
                            alpha -= alpha * _bipedModel1.AnimInfo.Frame[0] / _bipedModel1.AnimInfo.FrameCount[0];
                            alpha = Math.Clamp(alpha, 0, 1);
                        }
                        UpdateMaterials(_bipedModel2, Recolor);
                        GetDrawItems(_bipedModel2, _bipedModel2.Model.Nodes[0], alpha);
                        PaletteOverride = null;
                        if (_chargeEffect != null || _muzzleEffect != null)
                        {
                            Vector3 muzzlePos = Metadata.MuzzleOffests[(int)Hunter];
                            muzzlePos = Matrix.Vec3MultMtx4(muzzlePos, _shootNodes[lod]!.Animation);
                            if (_chargeEffect != null)
                            {
                                _chargeEffect.SetDrawEnabled(true);
                                _chargeEffect.Transform(_gunVec2, _gunVec1, muzzlePos);
                            }
                            if (_muzzleEffect != null)
                            {
                                _muzzleEffect.SetDrawEnabled(true);
                                _muzzleEffect.Transform(_gunVec2, _gunVec1, muzzlePos);
                            }
                        }
                        if (_frozenGfxTimer > 0)
                        {
                            for (int i = 0; i < _bipedIceModel.Model.Nodes.Count; i++)
                            {
                                _bipedIceModel.Model.Nodes[i].Animation = _bipedModel1.Model.Nodes[i].Animation;
                                _bipedIceTransforms[i] = _bipedModel1.Model.Nodes[i].Animation;
                            }
                            _bipedIceModel.Model.UpdateMatrixStack();
                            UpdateMaterials(_bipedIceModel, recolor: 0);
                            GetDrawItems(_bipedIceModel, _bipedIceModel.Model.Nodes[0], alpha: 1, recolor: 0);
                        }
                    }
                    _modelTransform = transform;
                    if (_health == 0)
                    {
                        DrawDeathParticles();
                    }
                    Flags2 |= PlayerFlags2.DrawnThirdPerson;
                }
                else if (AttachedEnemy == null && !_field6D0 && Hunter != Hunter.Guardian)
                {
                    Matrix4 transform = GetTransformMatrix(_aimVec, _upVector, _gunDrawPos);
                    UpdateTransforms(_gunModel, transform, Recolor);
                    GetDrawItems(_gunModel, _gunModel.Model.Nodes[0], _curAlpha);
                    if (Flags1.TestFlag(PlayerFlags1.DrawGunSmoke))
                    {
                        // todo?: the game uses an alternate projection matrix to draw this
                        var drawPos = new Vector3(0, 0, Fixed.ToFloat(Values.MuzzleOffset));
                        drawPos = Matrix.Vec3MultMtx4(drawPos, transform);
                        transform.Row3.Xyz = drawPos;
                        UpdateTransforms(_gunSmokeModel, transform, recolor: 0);
                        GetDrawItems(_gunSmokeModel, _gunSmokeModel.Model.Nodes[0], _smokeAlpha, recolor: 0);
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

        private void DrawKandenAlt()
        {
            for (int i = 0; i < _kandenSegMtx.Length; i++)
            {
                _altModel.Model.Nodes[i].Animation = _kandenSegMtx[i];
            }
            _altModel.Model.UpdateMatrixStack();
            UpdateMaterials(_altModel, Recolor);
            GetDrawItems(_altModel, _altModel.Model.Nodes[0], _curAlpha);
        }

        private void UpdateSpireAltAttack()
        {
            Matrix4 transform = GetTransformMatrix(_spireAltFacing, _spireAltUp);
            _altModel.Model.AnimateNodes(index: 0, useNodeTransform: false, transform, Vector3.One, _altModel.AnimInfo);
            _spireRockPosL = _spireAltNodes[0]!.Animation.Row3.Xyz + Position;
            _spireRockPosR = _spireAltNodes[1]!.Animation.Row3.Xyz + Position;
        }

        private void DrawSpireAltAttack()
        {
            _altModel.Model.Nodes[0].Animation = _modelTransform;
            for (int i = 1; i < _altModel.Model.Nodes.Count; i++)
            {
                Node node = _altModel.Model.Nodes[i];
                Matrix4 animation = node.Animation;
                animation.Row3.Xyz += _modelTransform.Row3.Xyz;
                node.Animation = animation;
            }
            _altModel.Model.UpdateMatrixStack();
            UpdateMaterials(_altModel, Recolor);
            GetDrawItems(_altModel, _altModel.Model.Nodes[0], _curAlpha);
        }

        private void GetDrawItems(ModelInstance inst, Node node, float alpha, int polygonId = -1, int recolor = -1)
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
                for (int i = 0; i < node.MeshCount; i++)
                {
                    Mesh mesh = model.Meshes[start + i];
                    if (!mesh.Visible)
                    {
                        continue;
                    }
                    Material material = model.Materials[mesh.MaterialId];
                    Vector3 emission = GetEmission(inst, material, mesh.MaterialId);
                    Matrix4 texcoordMatrix = GetTexcoordMatrix(inst, material, mesh.MaterialId, node, recolor);
                    Vector4? color = null;
                    SelectionType selectionType = SelectionType.None;
                    int? bindingOverride = GetBindingOverride(inst, material, mesh.MaterialId);
                    _scene.AddRenderItem(material, polygonId, alpha, emission, GetLightInfo(), texcoordMatrix,
                        node.Animation, mesh.ListId, model.NodeMatrixIds.Count, model.MatrixStackValues, color,
                        PaletteOverride, selectionType, node.BillboardMode, _drawScale, bindingOverride);
                }
                if (node.ChildIndex != -1)
                {
                    GetDrawItems(inst, model.Nodes[node.ChildIndex], alpha, polygonId, recolor);
                }
            }
            if (node.NextIndex != -1)
            {
                GetDrawItems(inst, model.Nodes[node.NextIndex], alpha, polygonId, recolor);
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

        protected override Matrix4 GetTexcoordMatrix(ModelInstance inst, Material material, int materialId,
            Node node, int recolor)
        {
            if (_doubleDmgTimer > 0 && (Hunter != Hunter.Spire || !(inst == _gunModel && materialId == 0))
                && material.Lighting > 0 && node.BillboardMode == BillboardMode.None)
            {
                Texture texture = _doubleDmgModel.Model.Recolors[0].Textures[0];
                // product should start with the upper 3x3 of the node animation result,
                // texgenMatrix should be multiplied with the view matrix if lighting is enabled,
                // and the result should be transposed.
                // these steps are done in the shader so the view matrix can be updated when frame advance is on.
                // strictly speaking, the use_light check in the shader is not the same as what the game does,
                // since the game checks if *any* material in the model uses lighting, but the result is the same.
                Matrix4 texgenMatrix = Matrix4.Identity;
                // in-game, there's only one uniform scale factor for models
                if (inst.Model.Scale.X != 1 || inst.Model.Scale.Y != 1 || inst.Model.Scale.Z != 1)
                {
                    texgenMatrix = Matrix4.CreateScale(inst.Model.Scale) * texgenMatrix;
                }
                Matrix4 product = texgenMatrix;
                product.M12 *= -1;
                product.M13 *= -1;
                product.M22 *= -1;
                product.M23 *= -1;
                product.M32 *= -1;
                product.M33 *= -1;
                ulong frame = _scene.FrameCount / 2;
                float rotZ = ((int)(16 * ((781874935307L * (53248 * frame) >> 32) + 2048)) >> 20) * (360 / 4096f);
                float rotY = ((int)(16 * ((781874935307L * (26624 * frame) + 0x80000000000) >> 32)) >> 20) * (360 / 4096f);
                var rot = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(rotZ));
                rot *= Matrix4.CreateRotationY(MathHelper.DegreesToRadians(rotY));
                product = rot * product;
                product *= 1.0f / (texture.Width / 2);
                product = new Matrix4(
                    product.Row0 * 16.0f,
                    product.Row1 * 16.0f,
                    product.Row2 * 16.0f,
                    product.Row3
                );
                return product;
            }
            return base.GetTexcoordMatrix(inst, material, materialId, node, recolor);
        }

        private void DrawShadow()
        {
            if (IsMainPlayer && CameraType == CameraType.First)
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

        private void DrawMorphBallTrail()
        {
            Debug.Assert(_trailModel != null);
            Material material = _trailModel.Model.Materials[0];
            Debug.Assert(_trailModel.Model.Recolors[0].Textures[material.TextureId].Width == 32);
            float[] matrixStack = ArrayPool<float>.Shared.Rent(16 * _mbTrailSegments);
            for (int i = 0; i < _mbTrailSegments; i++)
            {
                Matrix4 matrix = _mbTrailMatrices[SlotIndex, i];
                matrixStack[i * 16] = matrix.Row0.X;
                matrixStack[i * 16 + 1] = matrix.Row0.Y;
                matrixStack[i * 16 + 2] = matrix.Row0.Z;
                matrixStack[i * 16 + 3] = matrix.Row0.W;
                matrixStack[i * 16 + 4] = matrix.Row1.X;
                matrixStack[i * 16 + 5] = matrix.Row1.Y;
                matrixStack[i * 16 + 6] = matrix.Row1.Z;
                matrixStack[i * 16 + 7] = matrix.Row1.W;
                matrixStack[i * 16 + 8] = matrix.Row2.X;
                matrixStack[i * 16 + 9] = matrix.Row2.Y;
                matrixStack[i * 16 + 10] = matrix.Row2.Z;
                matrixStack[i * 16 + 11] = matrix.Row2.W;
                matrixStack[i * 16 + 12] = matrix.Row3.X;
                matrixStack[i * 16 + 13] = matrix.Row3.Y;
                matrixStack[i * 16 + 14] = matrix.Row3.Z;
                matrixStack[i * 16 + 15] = matrix.Row3.W;
            }
            int count = 0;
            int index = _mbTrailIndices[SlotIndex];
            Vector3[] uvsAndVerts = ArrayPool<Vector3>.Shared.Rent(8 * _mbTrailSegments);
            for (int i = 0; i < _mbTrailSegments; i++)
            {
                // going backwards with wrap-around
                int mtxId1 = index - 1 - i + (index - 1 - i < 0 ? _mbTrailSegments : 0);
                int mtxId2 = mtxId1 - 1 + (mtxId1 - 1 < 0 ? _mbTrailSegments : 0);
                float alpha1 = _mbTrailAlphas[SlotIndex, mtxId1];
                float alpha2 = _mbTrailAlphas[SlotIndex, mtxId2];
                if (alpha1 > 0 && alpha2 > 0)
                {
                    float uvS1 = (31 - (int)(alpha1 * 31)) / 32f;
                    float uvS2 = (31 - (int)(alpha2 * 31)) / 32f;
                    uvsAndVerts[i * 8] = new Vector3(uvS1, 0, mtxId1);
                    uvsAndVerts[i * 8 + 1] = new Vector3(0, 0.375f, 0);
                    uvsAndVerts[i * 8 + 2] = new Vector3(uvS1, 1, mtxId1);
                    uvsAndVerts[i * 8 + 3] = new Vector3(0, -0.375f, 0);
                    uvsAndVerts[i * 8 + 4] = new Vector3(uvS2, 1, mtxId2);
                    uvsAndVerts[i * 8 + 5] = new Vector3(0, -0.375f, 0);
                    uvsAndVerts[i * 8 + 6] = new Vector3(uvS2, 0, mtxId2);
                    uvsAndVerts[i * 8 + 7] = new Vector3(0, 0.375f, 0);
                    count++;
                }
            }
            if (count > 0)
            {
                var color = new Vector3(1, 27 / 31f, 11 / 31f);
                _scene.AddRenderItem(RenderItemType.TrailStack, _scene.GetNextPolygonId(), color, material.XRepeat, material.YRepeat,
                    material.ScaleS, material.ScaleT, _mbTrailSegments, matrixStack, uvsAndVerts, count, _trailBindingId1);
            }
            ArrayPool<float>.Shared.Return(matrixStack);
        }

        // todo: FPS stuff
        private void DrawDeathParticles()
        {
            // get current percentage through the first 1/3 of the respawn cooldown
            float timePct = 1 - ((_respawnTimer - (2 / 3f * _respawnTime)) / (1 / 3f * _respawnTime));
            if (timePct < 0 || timePct > 1)
            {
                return;
            }
            float scale = timePct / 2 + 0.1f;
            // todo: the angle stuff could be removed
            float angle = MathF.Sin(MathHelper.DegreesToRadians(270 - 90 * timePct));
            float sin270 = MathF.Sin(MathHelper.DegreesToRadians(270));
            float sin180 = MathF.Sin(MathHelper.DegreesToRadians(180));
            float offset = (angle - sin270) / (sin180 - sin270);
            for (int i = 1; i < _bipedModel1.Model.Nodes.Count; i++)
            {
                Node node = _bipedModel1.Model.Nodes[i];
                var nodePos = new Vector3(node.Animation.Row3);
                nodePos.Y += offset;
                if (node.ChildIndex != -1)
                {
                    Debug.Assert(node.ChildIndex > 0);
                    var childPos = new Vector3(_bipedModel1.Model.Nodes[node.ChildIndex].Animation.Row3);
                    childPos.Y += offset;
                    for (int j = 1; j < 5; j++)
                    {
                        var segPos = new Vector3(
                            nodePos.X + j * (childPos.X - nodePos.X) / 5,
                            nodePos.Y + j * (childPos.Y - nodePos.Y) / 5,
                            nodePos.Z + j * (childPos.Z - nodePos.Z) / 5
                        );
                        segPos += (segPos - Position).Normalized() * offset;
                        _scene.AddSingleParticle(SingleType.Death, segPos, Vector3.One, 1 - timePct, scale);
                    }
                }
                if (node.NextIndex != -1)
                {
                    Debug.Assert(node.NextIndex > 0);
                    var nextPos = new Vector3(_bipedModel1.Model.Nodes[node.NextIndex].Animation.Row3);
                    nextPos.Y += offset;
                    for (int j = 1; j < 5; j++)
                    {
                        var segPos = new Vector3(
                            nodePos.X + j * (nextPos.X - nodePos.X) / 5,
                            nodePos.Y + j * (nextPos.Y - nodePos.Y) / 5,
                            nodePos.Z + j * (nextPos.Z - nodePos.Z) / 5
                        );
                        segPos += (segPos - Position).Normalized() * offset;
                        _scene.AddSingleParticle(SingleType.Death, segPos, Vector3.One, 1 - timePct, scale);
                    }
                }
                nodePos += (nodePos - Position).Normalized() * offset;
                _scene.AddSingleParticle(SingleType.Death, nodePos, Vector3.One, 1 - timePct, scale);
            }
        }

        public override void GetDrawInfo()
        {
        }
    }
}
