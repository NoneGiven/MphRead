using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MphRead.Entities
{
    public partial class PlayerEntity
    {
        private void ProcessInput()
        {
            if (_health > 0)
            {
                if (Flags1.TestFlag(PlayerFlags1.FreeLook))
                {
                    Flags1 |= PlayerFlags1.FreeLookPrevious;
                }
                else
                {
                    Flags1 &= ~PlayerFlags1.FreeLookPrevious;
                }
                Flags1 &= ~PlayerFlags1.FreeLook;
                Flags1 &= ~PlayerFlags1.Walking;
                Flags1 &= ~PlayerFlags1.Strafing;
                if (!IsBot)
                {
                    ProcessTouchInput();
                }
                if (_frozenTimer > 0)
                {
                    _frozenTimer--;
                    _timeSinceFrozen = 0;
                    if (_frozenTimer == 0)
                    {
                        // todo: play SFX
                        if (IsAltForm)
                        {
                            CreateIceBreakEffectAlt();
                        }
                        else if (IsMainPlayer)
                        {
                            CreateIceBreakEffectGun();
                        }
                        else if (Flags2.TestFlag(PlayerFlags2.DrawnThirdPerson))
                        {
                            int lod = Flags2.TestFlag(PlayerFlags2.Lod1) ? 1 : 0;
                            CreateIceBreakEffectBiped(_bipedModelLods[lod].Model, _modelTransform);
                        }
                    }
                }
                if (_frozenGfxTimer > 0)
                {
                    _frozenGfxTimer--;
                    if (IsMainPlayer && _frozenGfxTimer == 0)
                    {
                        // todo: update HUD
                    }
                }
                if (_timeSinceFrozen != UInt16.MaxValue)
                {
                    _timeSinceFrozen++;
                }
            }
            if (IsAltForm || IsMorphing)
            {
                ProcessAlt();
            }
            else
            {
                ProcessBiped();
            }
        }

        private void ProcessTouchInput()
        {
            // todo: touch input
        }

        private void ProcessBiped()
        {
            // todo: set a field if cam seq, main player, and 1P mode
            if (EquipInfo.SmokeLevel < EquipInfo.Weapon.SmokeDrain)
            {
                EquipInfo.SmokeLevel = 0;
            }
            else
            {
                EquipInfo.SmokeLevel -= EquipInfo.Weapon.SmokeDrain;
            }
            if (_frozenTimer == 0 && _health > 0 && _field6D0 == 0)
            {
                if (Biped1Anim == PlayerAnimation.Turn)
                {
                    if (Biped1Frame <= Biped1FrameCount / 2)
                    {
                        Biped1Flags |= AnimFlags.Reverse;
                    }
                    else
                    {
                        Biped1Flags &= ~AnimFlags.Reverse;
                    }
                    Biped1Flags |= AnimFlags.NoLoop;
                }
                if (Biped2Anim == PlayerAnimation.Turn)
                {
                    if (Biped2Frame <= Biped2FrameCount / 2)
                    {
                        Biped2Flags |= AnimFlags.Reverse;
                    }
                    else
                    {
                        Biped2Flags &= ~AnimFlags.Reverse;
                    }
                    Biped2Flags |= AnimFlags.NoLoop;
                }
            }
            ProcessMovement();
            UpdateCamera();
            // skhere
        }

        private void ProcessAlt()
        {
            // sktodo
        }

        private void ProcessMovement()
        {
            // sktodo
        }

        private void UpdateCamera()
        {
            // sktodo
        }
    }
}
