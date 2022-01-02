using System;
using System.Collections.Generic;
using System.Diagnostics;
using MphRead.Formats;
using MphRead.Formats.Collision;
using OpenTK.Mathematics;

namespace MphRead.Entities
{
    public struct DamageResult
    {
        public bool TakeDamage;
        public uint Damage;
    }

    public partial class PlayerEntity
    {
        private EntityCollision? _collidedEntCol = null;
        private EntityCollision? _standingEntCol = null;

        // todo: visualize EVERYTHING
        private void CheckPlayerCollision()
        {
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.Player)
                {
                    continue;
                }
                var other = (PlayerEntity)entity;
                if (!other.LoadFlags.TestFlag(LoadFlags.Active) || other._health == 0)
                {
                    continue;
                }
                if (Flags2.TestFlag(PlayerFlags2.Halfturret))
                {
                    // todo: halfturret collision
                }
                if (other == this)
                {
                    continue;
                }
                // sktodo
            }
        }

        private void CheckCollision()
        {
            _standingEntCol = null;
            _collidedEntCol = null;
            _terrainDamage = false;
            var results = new CollisionResult[40];
            CollisionVolume altVolume = PlayerVolumes[(int)Hunter, 2];
            Vector3 point1;
            Vector3 point2;
            Vector3 limitMin;
            Vector3 limitMax;
            float margin;
            if (IsAltForm)
            {
                point1 = PrevPosition + altVolume.SpherePosition;
                point2 = Position + altVolume.SpherePosition;
                margin = altVolume.SphereRadius + 0.4f;
                limitMin = new Vector3(
                    MathF.Min(MathF.Min(Single.MaxValue, point1.X), point2.X) - margin,
                    MathF.Min(MathF.Min(Single.MaxValue, point1.Y), point2.Y) - margin,
                    MathF.Min(MathF.Min(Single.MaxValue, point1.Z), point2.Z) - margin
                );
                limitMax = new Vector3(
                    MathF.Max(MathF.Max(Single.MinValue, point1.X), point2.X) + margin,
                    MathF.Max(MathF.Max(Single.MinValue, point1.Y), point2.Y) + margin,
                    MathF.Max(MathF.Max(Single.MinValue, point1.Z), point2.Z) + margin
                );
                limitMin.Y = MathF.Min(limitMin.Y, Position.Y + Fixed.ToFloat(Values.MaxPickupHeight));
            }
            else
            {
                float midpoint = (Fixed.ToFloat(Values.MaxPickupHeight) + Fixed.ToFloat(Values.MinPickupHeight)) / 2;
                point1 = PrevPosition.AddY(midpoint);
                point2 = Position.AddY(midpoint);
                margin = (Fixed.ToFloat(Values.MaxPickupHeight) - Fixed.ToFloat(Values.MinPickupHeight)) / 2 + 0.4f;
                limitMin = new Vector3(
                    MathF.Min(MathF.Min(Single.MaxValue, point1.X), point2.X) - margin,
                    MathF.Min(MathF.Min(Single.MaxValue, point1.Y), point2.Y) - margin,
                    MathF.Min(MathF.Min(Single.MaxValue, point1.Z), point2.Z) - margin
                );
                limitMax = new Vector3(
                    MathF.Max(MathF.Max(Single.MinValue, point1.X), point2.X) + margin,
                    MathF.Max(MathF.Max(Single.MinValue, point1.Y), point2.Y) + margin,
                    MathF.Max(MathF.Max(Single.MinValue, point1.Z), point2.Z) + margin
                );
            }
            // point1/point2/margin aren't used, but point1 has to be passed in order to include entities
            IReadOnlyList<CollisionCandidate> candidates
                = CollisionDetection.GetCandidatesForLimits(point1, point2, margin, limitMin, limitMax, includeEntities: true, _scene);
            if (IsAltForm)
            {
                float radius = altVolume.SphereRadius + (Hunter == Hunter.Spire || Hunter == Hunter.Sylux ? 0.5f : 0.35f);
                int count = CollisionDetection.CheckSphereBetweenPoints(candidates, point1, point2, radius,
                    limit: 40, includeOffset: true, TestFlags.AffectsPlayers, _scene, results);
                for (int i = 0; i < count; i++)
                {
                    HandleCollision(results[i]);
                }
                radius = Fixed.ToFloat(Values.BipedColRadius) - 0.15f;
                point1 = Position.AddY(radius);
                float yOffset = Fixed.ToFloat(Values.AltColYPos) + Fixed.ToFloat(Values.MaxPickupHeight) - Fixed.ToFloat(Values.MinPickupHeight)
                    - altVolume.SphereRadius - radius;
                point2 = Position.AddY(yOffset);
                count = CollisionDetection.CheckSphereBetweenPoints(candidates, point1, point2, radius,
                    limit: 1, includeOffset: false, TestFlags.AffectsPlayers, _scene, results);
                if (count > 0)
                {
                    Flags1 |= PlayerFlags1.NoUnmorph;
                }
                if (Hunter == Hunter.Kanden)
                {
                    float altRadius = Fixed.ToFloat(Values.AltColRadius);
                    for (int i = 1; i < _kandenSegPos.Length; i++)
                    {
                        point2 = _kandenSegPos[i].AddY(Fixed.ToFloat(Values.AltColYPos));
                        count = CollisionDetection.CheckSphereBetweenPoints(candidates, point2, point2, altRadius,
                            limit: 40, includeOffset: true, TestFlags.AffectsPlayers, _scene, results);
                        for (int j = 0; j < count; j++)
                        {
                            CollisionResult result = results[j];
                            if (result.Field0 == 1)
                            {
                                Vector3 edge = result.EdgePoint2 - result.EdgePoint1;
                                Debug.Assert(edge != Vector3.Zero);
                                Vector3 between = point2 - result.EdgePoint1;
                                float dot = Vector3.Dot(between, edge);
                                float div = Math.Clamp(dot / edge.LengthSquared, 0, 1);
                                between = result.EdgePoint1 + edge * div;
                                between = point2 - between;
                                float magSqr = between.LengthSquared;
                                if (magSqr > 0 && magSqr < altRadius * altRadius)
                                {
                                    float mag = MathF.Sqrt(magSqr);
                                    if (altRadius - mag > 0)
                                    {
                                        float yInc = between.Y / mag * (altRadius - mag);
                                        _kandenSegPos[i].Y += yInc;
                                    }
                                }
                            }
                            else if (result.Field0 == 0)
                            {
                                float dot = altRadius + result.Plane.W - Vector3.Dot(point2, result.Plane.Xyz);
                                if (dot > 0)
                                {
                                    _kandenSegPos[i] += result.Plane.Xyz * dot;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                float radius = (Fixed.ToFloat(Values.MaxPickupHeight) - Fixed.ToFloat(Values.MinPickupHeight)) / 2;
                int count = CollisionDetection.CheckSphereBetweenPoints(candidates, point1, point2, radius,
                    limit: 40, includeOffset: true, TestFlags.AffectsPlayers, _scene, results);
                for (int i = 0; i < count; i++)
                {
                    HandleCollision(results[i]);
                }
            }
            // todo: collide with doors and force fields
            DamageResult dmgRes = default;
            dmgRes.Damage = 1;
            dmgRes.TakeDamage = _terrainDamage;
            if (_collidedEntCol != null)
            {
                // todo: query collision damage from entity
            }
            if (dmgRes.TakeDamage)
            {
                TakeDamage(dmgRes.Damage, DamageFlags.IgnoreInvuln, direction: null, source: null);
            }
            _volume = CollisionVolume.Move(_volumeUnxf, Position);
        }

        private void HandleCollision(CollisionResult result)
        {
            bool v163 = false;
            bool v164 = false;
            bool v165 = false;
            bool v166 = false;
            bool isPlatform = false;
            bool isCrusher = false;
            PlatformEntity? platform = null;
            if (result.EntityCollision?.Entity?.Type == EntityType.Platform)
            {
                isPlatform = true;
                platform = (PlatformEntity)result.EntityCollision.Entity;
                if (platform.Flags.TestFlag(PlatformFlags.Hazard))
                {
                    isCrusher = true;
                }
            }
            if (isCrusher && _health == 0)
            {
                return;
            }
            float v2;
            if (IsAltForm)
            {
                float altRad = Fixed.ToFloat(Values.AltColRadius);
                Vector3 altPos = Position.AddY(Fixed.ToFloat(Values.AltColYPos));
                if (result.Field0 == 0)
                {
                    v2 = altRad + result.Plane.W - Vector3.Dot(altPos, result.Plane.Xyz);
                }
                else
                {
                    Debug.Assert(result.Field0 == 1);
                    Vector3 edge = result.EdgePoint2 - result.EdgePoint1;
                    Debug.Assert(edge != Vector3.Zero);
                    Vector3 between = altPos - result.EdgePoint1;
                    float dot = Vector3.Dot(between, edge);
                    float div = Math.Clamp(dot / edge.LengthSquared, 0, 1);
                    between = altPos - (result.EdgePoint1 + edge * div);
                    float magSqr = between.LengthSquared;
                    if (magSqr >= altRad * altRad || magSqr <= 0)
                    {
                        return;
                    }
                    float mag = MathF.Sqrt(magSqr);
                    between /= mag;
                    dot = Vector3.Dot(between, result.Plane.Xyz) * (altRad - mag);
                    v2 = dot * dot;
                }
            }
            else if (result.Field0 == 0)
            {
                float radius = Fixed.ToFloat(Values.BipedColRadius);
                Vector3 vec1 = Position.AddY(Fixed.ToFloat(Values.MaxPickupHeight) - radius);
                Vector3 vec2 = Position.AddY(Fixed.ToFloat(Values.MinPickupHeight) + radius);
                float dot1 = radius + result.Plane.W - Vector3.Dot(vec1, result.Plane.Xyz);
                float dot2 = radius + result.Plane.W - Vector3.Dot(vec2, result.Plane.Xyz);
                if (dot2 <= dot1)
                {
                    v2 = dot1;
                    v163 = true;
                }
                else
                {
                    v2 = dot2;
                }
            }
            else
            {
                Debug.Assert(result.Field0 == 1);
                float v11 = 0;
                float v162 = 1;
                bool v169 = false;
                Vector3 edge = result.EdgePoint2 - result.EdgePoint1;
                Debug.Assert(edge != Vector3.Zero);
                float yTop = Position.Y + Fixed.ToFloat(Values.MaxPickupHeight);
                float yBot = Position.Y + Fixed.ToFloat(Values.MinPickupHeight);
                float yBotAdd = yBot + 0.5f;
                if (result.EdgePoint1.Y >= yBot)
                {
                    if (result.EdgePoint1.Y > yTop)
                    {
                        if (edge.Y == 0)
                        {
                            return;
                        }
                        v11 = (yTop - result.EdgePoint1.Y) / edge.Y;
                    }
                    v2 = 0; // gets assigned later
                }
                else if (edge.Y != 0)
                {
                    v11 = (yTop - result.EdgePoint1.Y) / edge.Y;
                    v2 = 0; // gets assigned later
                }
                else
                {
                    if (result.EdgePoint1.Y <= yBotAdd)
                    {
                        return;
                    }
                    float betweenX = Position.X - result.EdgePoint1.X;
                    float betweenZ = Position.Z - result.EdgePoint1.Z;
                    float div = (betweenX * edge.X + betweenZ * edge.Z) / (edge.X * edge.X + edge.Z * edge.Z);
                    div = Math.Clamp(div, 0, 1);
                    var between = new Vector3(
                        Position.X - result.EdgePoint1.X + edge.X * div,
                        yBotAdd - result.EdgePoint1.Y,
                        Position.Z - result.EdgePoint1.Z + edge.Z * div
                    );
                    float magSqr = between.LengthSquared;
                    if (magSqr >= 0.25f)
                    {
                        return;
                    }
                    float mag = MathF.Sqrt(magSqr);
                    result.Plane.Xyz = between / mag;
                    v2 = 0.5f - mag;
                    v169 = true;
                }
                if (!v169)
                {
                    if (result.EdgePoint2.Y >= yBot)
                    {
                        if (result.EdgePoint2.Y > yTop)
                        {
                            v162 = (yTop - result.EdgePoint1.Y) / edge.Y;
                        }
                    }
                    else
                    {
                        v162 = (yBot - result.EdgePoint1.Y) / edge.Y;
                    }
                    if (Fixed.ToInt(v11) == Fixed.ToInt(v162))
                    {
                        return;
                    }
                    var between = new Vector3(
                        Position.X - result.EdgePoint1.X,
                        yTop - result.EdgePoint1.Y,
                        Position.Z - result.EdgePoint1.Z
                    );
                    float dot1 = Vector3.Dot(between, edge);
                    float dot2 = Vector3.Dot(edge, edge);
                    float div = dot1 / dot2;
                    if (div >= v11)
                    {
                        if (div <= v162)
                        {
                            v11 = div;
                        }
                        else
                        {
                            v11 = v162;
                        }
                    }
                    float betweenY = result.EdgePoint1.Y + edge.Y * v11;
                    if (betweenY > yTop + Fixed.ToFloat(2) || betweenY < yBot - Fixed.ToFloat(2))
                    {
                        return;
                    }
                    if (betweenY <= yBotAdd)
                    {
                        between = new Vector3(
                            Position.X - (result.EdgePoint1.X + edge.X * v11),
                            yBotAdd - betweenY,
                            Position.Z - (result.EdgePoint1.Z + edge.Z * v11)
                        );
                        float magSqr = between.LengthSquared;
                        if (magSqr >= 0.25f)
                        {
                            return;
                        }
                        float mag = MathF.Sqrt(magSqr);
                        result.Plane.Xyz = between / mag;
                        v2 = 0.5f - mag;
                    }
                    else
                    {
                        float radius = Fixed.ToFloat(Values.BipedColRadius);
                        float betweenX = Position.X - (result.EdgePoint1.X + edge.X * v11);
                        float betweenZ = Position.Z - (result.EdgePoint1.Z + edge.Z * v11);
                        float v31 = betweenX * betweenX + betweenZ * betweenZ;
                        if (v31 >= radius * radius)
                        {
                            return;
                        }
                        float v32 = MathF.Sqrt(v31);
                        result.Plane.Xyz = new Vector3(betweenX / v32, 0, betweenZ / v32);
                        v2 = radius - v32;
                        if (betweenY > Position.Y)
                        {
                            v163 = true;
                        }
                    }
                }
            }
            Vector3 position = Position;
            if (v2 >= Fixed.ToFloat(-5))
            {
                v164 = true;
            }
            if (v2 > 0)
            {
                if (result.Plane.Y < 0.1f && result.Plane.Y > -0.1f)
                {
                    v165 = true;
                    Flags1 |= PlayerFlags1.CollidingLateral;
                }
                // walking into a wall, jumping into a wall/ceiling, or landing/slopes/etc. -- update x/z
                position.X += result.Plane.X * v2;
                position.Z += result.Plane.Z * v2;
                if (!v163 || !Flags1.TestFlag(PlayerFlags1.StandingPrevious))
                {
                    // jumping into a wall/ceiling or landing/slopes/etc. -- update y
                    // hack: mimic the collision response for 1 frame at 30 FPS in the game
                    // --> move downward when hitting ceiling to avoid getting stuck in corners
                    // --> compensate for halved gravity so we can't go up steeper slopes
                    // todo?: the response when moving into walls laterally is also not accurate ("wall sliding")
                    // --> needs a hack; just doubling it results in jittering
                    position.Y += result.Plane.Y * v2 * (result.Plane.Y > 0 ? 0.5f / 2 : 2 * 2); // todo: FPS stuff
                }
                float dot = Vector3.Dot(Speed, result.Plane.Xyz);
                if (dot < 0)
                {
                    // floor collision
                    float damageSpeed = Fixed.ToFloat(Values.FallDamageSpeed);
                    if (Speed.Y <= -damageSpeed && !v163 && !v165 && !IsAltForm && !IsMorphing
                        && !IsUnmorphing && !Flags1.TestFlag(PlayerFlags1.Standing) && _timeSinceJumpPad > 5 * 2) // todo: FPS stuff
                    {
                        // fall damage
                        int damage = (int)(Fixed.ToFloat(Values.FallDamageMax) * -(Speed.Y + damageSpeed) / 0.8f);
                        Debug.Assert(damage >= 0);
                        if (damage == 0)
                        {
                            damage = 1;
                        }
                        TakeDamage((uint)damage, DamageFlags.NoDmgInvuln, direction: null, source: null);
                    }
                    if (Hunter == Hunter.Noxus && IsAltForm && v165)
                    {
                        float magSqr = PrevSpeed.X * result.Plane.X + PrevSpeed.Z * result.Plane.Z;
                        if (magSqr < 0)
                        {
                            float tilt = Fixed.ToFloat(Values.AltBounceTilt) * -magSqr;
                            _altTiltX += result.Plane.X * tilt;
                            _altTiltZ += result.Plane.Z * tilt;
                            _altWobble += Fixed.ToFloat(Values.AltBounceWobble) * -magSqr;
                            _altSpinSpeed -= Fixed.ToFloat(Values.AltBounceSpin) * -magSqr;
                            var rotMtx = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(40));
                            Vector3 axis = Matrix.Vec3MultMtx3(result.Plane.Xyz, rotMtx);
                            Speed = Speed.AddX(axis.X * (-magSqr / 2)).AddZ(axis.Z * (-magSqr / 2));
                        }
                    }
                    Speed += result.Plane.Xyz * -dot;
                    if (!v163 && !IsAltForm && result.Field0 != 1)
                    {
                        float hMagSqr = Speed.X * Speed.X + Speed.Z * Speed.Z;
                        if (hMagSqr > 0)
                        {
                            float div = (result.Plane.X * Speed.X + result.Plane.Z * Speed.Z) / MathF.Sqrt(hMagSqr);
                            if (div < 0)
                            {
                                Speed *= div + 1;
                            }
                        }
                    }
                }
            }
            if (IsAltForm)
            {
                // sktodo: handle alt form
            }
            if (result.EntityCollision != null)
            {
                if (result.Plane.Y > 0.25f)
                {
                    _standingEntCol = result.EntityCollision;
                }
                Flags1 |= PlayerFlags1.CollidingEntity;
                _scene.SendMessage(Message.PlayerCollideWith, this, result.EntityCollision.Entity, 0, _standingEntCol == null ? 0 : 1);
                _collidedEntCol = result.EntityCollision;
                if (isCrusher)
                {
                    Debug.Assert(platform != null);
                    if (result.Field0 == 0 && Vector3.Dot(result.Plane.Xyz, platform.Velocity) >= 0)
                    {
                        float value = Fixed.ToFloat(4080);
                        if (result.Plane.X <= value)
                        {
                            if (result.Plane.X >= -value)
                            {
                                if (result.Plane.Y <= value)
                                {
                                    if (result.Plane.Y >= -value)
                                    {
                                        if (result.Plane.Z <= value)
                                        {
                                            if (result.Plane.Z < -value)
                                            {
                                                _crushBits |= 0x20;
                                            }
                                        }
                                        else
                                        {
                                            _crushBits |= 0x10;
                                        }
                                    }
                                    else
                                    {
                                        _crushBits |= 8;
                                    }
                                }
                                else
                                {
                                    _crushBits |= 4;
                                }
                            }
                            else
                            {
                                _crushBits |= 2;
                            }
                        }
                        else
                        {
                            _crushBits |= 1;
                        }
                        if ((_crushBits & 3) == 3 || (_crushBits & 0xC) == 0xC || (_crushBits & 0x30) == 0x30)
                        {
                            // caught between one crusher moving +X and one moving -X, or one +Z and one -Z, or one +Y and one -Y
                            TakeDamage((uint)_health, DamageFlags.Death | DamageFlags.IgnoreInvuln | DamageFlags.NoDmgInvuln,
                                direction: null, source: null);
                        }
                    }
                }
                else if (isPlatform)
                {
                    Debug.Assert(platform != null);
                    if (platform.Velocity.Y <= 0 && (Flags1.TestFlag(PlayerFlags1.Standing) || Flags1.TestFlag(PlayerFlags1.StandingPrevious))
                        && (v163 || result.Plane.Y < Fixed.ToFloat(-3849) && IsAltForm))
                    {
                        // reverse platform Y movement when hitting player
                        platform.Recoil();
                    }
                }
            }
            _terrainDamage = result.Flags.TestFlag(CollisionFlags.Damaging);
            if (v164)
            {
                _field449 = 0;
                if (result.Plane.Y >= Fixed.ToFloat(1401))
                {
                    _fieldC0 += result.Plane.Xyz;
                }
                if (!v163)
                {
                    _slipperiness = result.Slipperiness;
                    _standTerrain = result.Terrain;
                    if (_terrainDamage)
                    {
                        Flags1 |= PlayerFlags1.OnAcid;
                    }
                    if (_standTerrain == Terrain.Lava)
                    {
                        Flags1 |= PlayerFlags1.OnLava;
                    }
                    // solid landing
                    if (result.Field0 == 0 && result.Plane.Y > 0.5f)
                    {
                        Flags1 |= PlayerFlags1.Standing;
                        _timeSinceStanding = 0;
                        if (!Flags1.TestFlag(PlayerFlags1.StandingPrevious) || Flags1.TestFlag(PlayerFlags1.AltFormPrevious))
                        {
                            Flags1 &= ~PlayerFlags1.UsedJump;
                        }
                        // if ( (some_flags & PSF_GROUNDED) == 0 && (some_flags & PSF_ALT_FORM) == 0 && player->energy )
                        if (_health > 0 && !IsAltForm && !Flags1.TestFlag(PlayerFlags1.Grounded))
                        {
                            if (Biped1Anim == PlayerAnimation.JumpLeft)
                            {
                                SetBiped1Animation(PlayerAnimation.LandLeft, AnimFlags.NoLoop);
                            }
                            else if (Biped1Anim == PlayerAnimation.JumpRight)
                            {
                                SetBiped1Animation(PlayerAnimation.LandRight, AnimFlags.NoLoop);
                            }
                            else
                            {
                                SetBiped1Animation(PlayerAnimation.LandNeutral, AnimFlags.NoLoop);
                            }
                        }
                    }
                }
            }
            if (v166 && !Flags1.TestFlag(PlayerFlags1.Standing))
            {
                Flags2 |= PlayerFlags2.SpireClimbing;
            }
            Position = position;
            _volume = CollisionVolume.Move(_volumeUnxf, Position);
        }
    }
}
