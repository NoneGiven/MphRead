using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Memory;
using OpenTK.Mathematics;

namespace MphRead.Testing
{
    public static class TestLogic
    {
        private class SaveBlock
        {
            public int Field0 { get; set; }
            public int Field4 { get; set; }
            public int Field8 { get; set; }
            public int FieldC { get; set; }
        }

        private class SaveBufBlock
        {
            public int Field0 { get; set; }
            public int Field4 { get; set; }
            public int Field8 { get; set; }
            public int FieldC { get; set; }
            public int Field10 { get; set; }
            public int Field14 { get; set; }
            public int Field18 { get; set; }
            public int Field1C { get; set; }
        }

        public static void TestSaveBlocks()
        {
            var blocks = new List<SaveBlock>();
            for (int i = 0; i < 3; i++)
            {
                blocks.Add(new SaveBlock()
                {
                    Field0 = 0,
                    Field4 = 128,
                    Field8 = 4276,
                    FieldC = -23
                });
            }
            blocks.Add(new SaveBlock()
            {
                Field0 = 0,
                Field4 = 128,
                Field8 = 64,
                FieldC = 2
            });
            blocks.Add(new SaveBlock()
            {
                Field0 = 0,
                Field4 = 128,
                Field8 = 164,
                FieldC = -1
            });
            blocks.Add(new SaveBlock()
            {
                Field0 = 0,
                Field4 = 128,
                Field8 = 488,
                FieldC = 9
            });
            blocks.Add(new SaveBlock()
            {
                Field0 = 0,
                Field4 = 128,
                Field8 = 8400,
                FieldC = 0
            });
            int dword20EEFF4 = 4 * (blocks.Count - 1) + 36; // 60
            int dword20EEFC0 = 0x100;
            int dword20EEFBC = 0x40000;
            var bufBlocks = new List<SaveBufBlock>();
            for (int i = 0; i < blocks.Count; i++)
            {
                bufBlocks.Add(new SaveBufBlock());
            }
            int size = 0;
            for (int i = 0; i < blocks.Count; i++)
            {
                SaveBlock block = blocks[i];
                if (block.FieldC != 0)
                {
                    int v11 = (block.Field8 + 8 + dword20EEFC0 - 1) / dword20EEFC0 * dword20EEFC0;
                    if (block.Field0 == 0 && block.FieldC < 0)
                    {
                        block.FieldC = dword20EEFBC * -block.FieldC / 100 / v11;
                    }
                    SaveBufBlock newBuf = bufBlocks[i];
                    newBuf.Field0 = block.Field0;
                    newBuf.Field4 = block.Field8;
                    newBuf.Field8 = v11;
                    newBuf.FieldC = 0;
                    newBuf.Field14 = block.FieldC;
                    newBuf.Field10 = block.FieldC * v11;
                    size += newBuf.Field10;
                    if (size > dword20EEFBC)
                    {
                        Debug.Assert(false);
                    }
                }
            }
            int v19 = dword20EEFC0 * (dword20EEFF4 + dword20EEFC0 - 1) / dword20EEFC0;
            size += v19;
            for (int i = 0; i < blocks.Count; i++)
            {
                SaveBlock block = blocks[i];
                if (block.FieldC == 0)
                {
                    int v11 = (block.Field8 + 8 + dword20EEFC0 - 1) / dword20EEFC0 * dword20EEFC0;
                    SaveBufBlock newBuf = bufBlocks[i];
                    newBuf.Field0 = block.Field0;
                    newBuf.Field4 = block.Field8;
                    newBuf.Field8 = v11;
                    newBuf.FieldC = 0;
                    newBuf.Field14 = (dword20EEFBC - size) / v11;
                    newBuf.Field10 = newBuf.Field14 * v11;
                    size += newBuf.Field10;
                    if (size > dword20EEFBC)
                    {
                        Debug.Assert(false);
                    }
                }
            }
            foreach (SaveBufBlock bufBlock in bufBlocks)
            {
                bufBlock.FieldC = v19;
                v19 += bufBlock.Field10;
            }
        }

        public class CompletionValues
        {
            public int Completion { get; set; }
            public int Octolith { get; set; }
            public int EnergyTanks { get; set; }
            public int UaExpansions { get; set; }
            public int MissileExpansions { get; set; }
        }

        public static CompletionValues GetCompletionValues(StorySaveData save)
        {
            int octolithCount = 0;
            for (int i = 0; i < 8; i++)
            {
                if ((save.FoundOctos & (1 << i)) != 0)
                {
                    octolithCount++;
                }
            }
            return new CompletionValues()
            {
                Completion = GetCompletionPercentage(save),
                Octolith = 100 * octolithCount / 8,
                EnergyTanks = save.EnergyCap / 100,
                UaExpansions = (save.AmmoCaps[0] - 400) / 300,
                MissileExpansions = (save.AmmoCaps[1] - 50) / 100
            };
        }

        public static int GetCompletionPercentage(StorySaveData save)
        {
            if (save.MaxScanCount == 0)
            {
                return 0;
            }
            int counts = 0;
            // some scan count?
            counts += save.ScanCount;
            // weapons other than PB, Ms, OC
            for (int i = 1; i < 8; i++)
            {
                if (i != 2 && (save.Weapons & (1 << i)) != 0)
                {
                    counts++;
                }
            }
            // octoliths obtained
            for (int i = 0; i < 8; i++)
            {
                if ((save.FoundOctos & (1 << i)) != 0)
                {
                    counts++;
                }
            }
            // artifacts obtained
            for (int i = 0; i < 24; i++)
            {
                if ((save.Artifacts & (1 << i)) != 0)
                {
                    counts++;
                }
            }
            // energy tanks
            counts += save.EnergyCap / 100;
            // UA expansions
            counts += (save.AmmoCaps[0] - 400) / 300;
            // missile expansions
            counts += (save.AmmoCaps[1] - 50) / 100;
            // 66 = weapons + octoliths + artifacts + energy + UA + missiles
            return 100 * counts / ((int)save.MaxScanCount + 66);
        }

        // 4F4 update for alt forms in sub_201DCE4
        public static void TestLogic1()
        {
            Hunter hunter = 0;
            int flags = 0;
            int v45 = 0;

            if (hunter == Hunter.Noxus)
            {

            }
            else if (hunter > Hunter.Samus && hunter != Hunter.Spire)
            {
                if (hunter == Hunter.Kanden)
                {
                    /* call sub_202657C */
                }
                else // Trace, Sylux, Weavel, Guardian
                {

                }
            }
            else // Samus, Spire
            {
                // the "404 + 64" used in the vector setup seems to point to fx32 0.5
                // might be a modifier for movement speed, or terrain angle?
                if (hunter > Hunter.Samus || (flags & 0x80) > 0) // Spire OR colliding with platform
                {
                    /* v45 vector setup 1 */
                    // (?) calculate vector based on speed
                    v45 = 1;
                }
                else // Samus AND !(colliding with platform)
                {
                    /* v45 vector setup 2 */
                    // calculate vector based on current and previous position
                    v45 = 2;
                }
                if (v45 > 0)
                {
                    /* matrix setup */
                    if (hunter == Hunter.Samus)
                    {
                        /* 4F4 matrix concat */
                    }
                    /* 4F4 cross product and normalize */
                    if (hunter == Hunter.Spire)
                    {
                        /* 4F4 matrix multiplication */
                    }
                }
            }
        }

        [Flags]
        public enum SomeFlags : uint
        {
            None = 0x0,
            SurfaceCollision = 0x10,
            PlatformCollision = 0x80,
            UsedJump = 0x100,
            AltForm = 0x200,
            DrawAltForm = 0x400,
            BlockAiming = 0x1000000,
            WeaponMenu = 0x2000000,
            DrawGunSmoke = 0x80000000
        }

        [Flags]
        public enum MoreFlags : uint
        {
            None = 0x0,
            FullCharge = 0x1,
            HideModel = 0x2,
            WeaponFiring = 0x4,
            AltFormAttack = 0x8
        }

        public class CPlayer
        {
            public Vector3 Position { get; set; }
            public Hunter Hunter { get; set; }
            public SomeFlags SomeFlags { get; set; }
            public MoreFlags MoreFlags { get; set; }
            public CModel Model { get; set; } = null!;
            public CModel Gun { get; set; } = null!;
            public CModel GunSmoke { get; set; } = null!;
            public Matrix4x3 SomeMatrix { get; set; }
            public byte Field4BB { get; set; }
            public CModel Field1A4 { get; set; } = null!;
            public uint Health { get; set; }
            public int Field358 { get; set; }
            public int Field6D0 { get; set; }
            public Vector3 Field64 { get; set; }
            public Vector3 FieldB4 { get; set; }
            public short FieldE2 { get; set; }
            public byte Field4D6 { get; set; }
            public int Field550 { get; set; }
            public int Field46C { get; set; }
        }

        public class CModel
        {
            public MModel Model { get; set; } = null!;
            public short SomeFlag { get; set; }
            public CNodeAnimation NodeAnimation { get; set; } = null!;
        }

        public class CNodeAnimation
        {
            public UIntPtr NodeAnimation { get; set; }
        }

        public class MModel
        {
            public UIntPtr NodeAnimation { get; set; }
            public byte Flags { get; set; }
            public float Scale { get; set; }
        }

        // (?) determine if other Hunters are visible based on partial room?
        private static bool IsVisibleMaybe(CPlayer player)
        {
            return player != null;
        }

        private static readonly MModel _mdl200D960 = null!;

        private static readonly MModel _mdl200D938 = null!;

        private static readonly MModel _mdl200E490 = null!;

        private static readonly Matrix4x3 _mtx20D955C = Matrix4x3.Zero;

        private static readonly Matrix4x3 _viewMatrix = Matrix4x3.Zero;

        private static Matrix4x3 _currentTextureMatrix = Matrix4x3.Zero;

        private static readonly int _mem20E97B0 = 0;

        private static readonly int _mem20DA5D0 = 0;

        private static int _mem20E3EA0 = 0;

        private static readonly int _gameState = 2;

        // ???
        private static void Memory1FF8000()
        {
        }

        private static void CModelDraw(CModel model, Matrix4x3 someMatrix)
        {
            DrawAnimatedModel(model.Model, someMatrix, (byte)model.SomeFlag);
        }

        private static void DrawAnimatedModel(MModel model, Matrix4x3 texMatrix, byte flags)
        {
            Matrix4x3 currentTextureMatrix;
            if ((model.Flags & 1) > 0) // if any materials have lighting enabled
            {
                if (model.Scale == 1)
                {
                    currentTextureMatrix = Matrix.Concat43(texMatrix, _viewMatrix);
                }
                else
                {
                    var scaleMatrix = Matrix4x3.CreateScale(model.Scale);
                    currentTextureMatrix = Matrix.Concat43(scaleMatrix, texMatrix);
                    currentTextureMatrix = Matrix.Concat43(currentTextureMatrix, _viewMatrix);
                }
            }
            else
            {
                if (model.Scale == 1)
                {
                    currentTextureMatrix = texMatrix;
                }
                else
                {
                    var scaleMatrix = Matrix4x3.CreateScale(model.Scale);
                    currentTextureMatrix = Matrix.Concat43(scaleMatrix, texMatrix);
                }
            }
            _currentTextureMatrix = currentTextureMatrix;
            _mem20E3EA0 = 0;
            if (model.NodeAnimation != UIntPtr.Zero)
            {
                if ((flags & 1) > 0) // ???
                {
                    Memory1FF8000();
                }
                else
                {
                    Memory1FF8000();
                    _mem20E3EA0 = -2147483648;
                }
            }
            // later, in normal texgen:
            if (_mem20E3EA0 >= 0)
            {
                // node_transform * current_texture_matrix
            }
            else
            {
                // node_transform only
            }
        }

        private static void CModelInitializeAnimationData(CModel model)
        {
            CNodeAnimationSetData(model.Model, model.NodeAnimation.NodeAnimation);
        }

        private static void CNodeAnimationSetData(MModel model, UIntPtr nodeAnimation)
        {
            model.NodeAnimation = nodeAnimation;
        }

        // model draw calls in draw_player
        public static void TestLogic2(CPlayer player, int playerId)
        {
            if (!player.MoreFlags.TestFlag(MoreFlags.HideModel))
            {
                if (player.Hunter == Hunter.Spire && player.MoreFlags.TestFlag(MoreFlags.AltFormAttack))
                {
                    CModelInitializeAnimationData(player.Model);
                }
                if (playerId == 0 || IsVisibleMaybe(player))
                {
                    // one of these must be checking if the player is P1 but the camera is third person
                    bool v10 = (
                        playerId != 0
                        || player.Field4D6 != 0
                        // (unsigned __int8)tmp_player->field_550 < (signed int)*(unsigned __int16 *)(tmp_player->field_404 + 104)
                        || player.Field550 < player.Field46C
                        || _mem20DA5D0 != 0
                    );
                    if (player.SomeFlags.TestFlag(SomeFlags.AltForm))
                    {
                        if (player.Hunter == Hunter.Kanden)
                        {
                            CNodeAnimationSetData(player.Model.Model, UIntPtr.Zero);
                            CModelDraw(player.Model, _mtx20D955C);
                            CNodeAnimationSetData(player.Model.Model, player.Model.NodeAnimation.NodeAnimation);
                        }
                        else if (player.Hunter == Hunter.Spire)
                        {
                            if (player.MoreFlags.TestFlag(MoreFlags.AltFormAttack))
                            {
                                CModelInitializeAnimationData(player.Model);
                                CNodeAnimationSetData(player.Model.Model, UIntPtr.Zero);
                                var matrix = Matrix4x3.CreateTranslation(player.Position);
                                DrawAnimatedModel(_mdl200D960, matrix, (byte)player.Model.SomeFlag);
                            }
                            else
                            {
                                CModelDraw(player.Model, player.SomeMatrix);
                            }
                        }
                        else
                        {
                            CModelDraw(player.Model, player.SomeMatrix);
                        }
                        if (player.Field4BB != 0)
                        {
                            // v52 = sub_20AC718(tmp_player->field_108.data.sphere.radius);
                            // v54 = sub_20AC190(dword_200D970, dword_200D974, v52, v53);
                            // v55 = sub_20AC5AC(v54);
                            int v55 = 1;
                            var scaleMatrix = Matrix4x3.CreateScale(v55);
                            Matrix4x3 matrix = Matrix.Concat43(scaleMatrix, player.SomeMatrix);
                            CModelDraw(player.Field1A4, matrix);
                        }
                    }
                    else if (v10)
                    {
                        if (player.Health > 0)
                        {
                            // v171 (???)
                            Matrix4x3 matrix = Matrix4x3.Zero;
                            DrawAnimatedModel(_mdl200D938, matrix, flags: 0);
                            if (player.Field4BB != 0)
                            {
                                CModelDraw(player.Field1A4, matrix);
                            }
                        }
                    }
                    else
                    {
                        if (player.Field358 != 0 || player.Field6D0 != 0)
                        {

                        }
                        else
                        {
                            Matrix3 transform = Matrix.GetTransform3(player.Field64, player.FieldB4);
                            var matrix = new Matrix4x3(transform.Row0, transform.Row1, transform.Row2, new Vector3());
                            CModelDraw(player.Gun, matrix);
                            if (player.SomeFlags.TestFlag(SomeFlags.DrawGunSmoke))
                            {
                                CModelDraw(player.GunSmoke, matrix);
                            }
                        }
                    }
                }
                // if ( *((_BYTE *)off_200E484 + 36) )
                // if ( LOBYTE(tmp_player->field_E2) )
                // if ( LOBYTE(tmp_player->field_E2) <= 0x77 )
                if (_gameState == 2 && playerId == 0 && _mem20E97B0 != 0 && player.FieldE2 <= 0x77)
                {
                    //v155 = off_200D924;
                    //v156 = off_200E48C[0][1];
                    //v157 = off_200E48C[0][2];
                    //off_200D924->m[9] = *off_200E48C[0];
                    //v155->m[10] = v156;
                    //v155->m[11] = v157;
                    Matrix4x3 matrix = Matrix4x3.Zero;
                    DrawAnimatedModel(_mdl200E490, matrix, flags: 0);
                }
            }
        }
    }
}
