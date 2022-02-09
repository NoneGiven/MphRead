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
                if (!other.LoadFlags.TestFlag(LoadFlags.Active) || other.Health == 0)
                {
                    continue;
                }
                if (Flags2.TestFlag(PlayerFlags2.Halfturret))
                {
                    Vector3 toTurret = other.Volume.SpherePosition - _halfturret.Position;
                    float radius = other.Volume.SphereRadius + 0.45f + 0.1f;
                    if (toTurret.LengthSquared <= radius * radius)
                    {
                        CollisionResult turretRes = default;
                        if (toTurret.Y < 0)
                        {
                            toTurret.Y = 0;
                        }
                        Debug.Assert(toTurret != Vector3.Zero);
                        toTurret = toTurret.Normalized();
                        turretRes.Field0 = 0;
                        turretRes.Plane = new Vector4(toTurret);
                        toTurret *= 0.45f;
                        toTurret += _halfturret.Position;
                        turretRes.Plane.W = Vector3.Dot(toTurret, turretRes.Plane.Xyz);
                        other.HandleCollision(turretRes);
                        if (other != this)
                        {
                            if (other.Flags1.TestFlag(PlayerFlags1.Boosting))
                            {
                                TakeDamage(other._boostDamage, DamageFlags.NoDmgInvuln | DamageFlags.Halfturret, other.Speed, other);
                                other.EndAltAttack();
                            }
                            if (other._deathaltTimer > 0)
                            {
                                TakeDamage(200, DamageFlags.Deathalt | DamageFlags.NoDmgInvuln | DamageFlags.Halfturret, other.Speed, other);
                            }
                            CheckAltAttackHit2(other, this, halfturret: true);
                        }
                    }
                    CheckAltAttackHit1(other, this, halfturret: true);
                }
                if (other == this)
                {
                    continue;
                }
                Vector3 between = _volume.SpherePosition - other.Volume.SpherePosition;
                float distSqr = between.LengthFast;
                if (distSqr == 0)
                {
                    between = Vector3.UnitX;
                    distSqr = 1;
                }
                float radii = _volume.SphereRadius + other.Volume.SphereRadius;
                if (distSqr < radii * radii)
                {
                    float dist = MathF.Sqrt(distSqr);
                    between /= dist;
                    float dot = Vector3.Dot(Speed, between);
                    Speed -= between * dot;
                    Vector3 posAdd = between * (radii - dist);
                    Position += posAdd;
                    _volume = CollisionVolume.Move(_volume, _volume.SpherePosition + posAdd);
                    float kbAccel = Fixed.ToFloat(Values.AltAttackKnockbackAccel);
                    if (Hunter == Hunter.Noxus && IsAltForm)
                    {
                        other.Acceleration = new Vector3(between.X * -kbAccel, 0, between.Z * -kbAccel);
                        other._accelerationTimer = (ushort)(Values.AltAttackKnockbackTime * 2); // todo: FPS stuff
                    }
                    if (other.Hunter == Hunter.Noxus && other.IsAltForm)
                    {
                        Acceleration = new Vector3(between.X * kbAccel, 0, between.Z * kbAccel);
                        _accelerationTimer = (ushort)(Values.AltAttackKnockbackTime * 2); // todo: FPS stuff
                    }
                    if (Flags1.TestFlag(PlayerFlags1.Boosting))
                    {
                        other.TakeDamage(_boostDamage, DamageFlags.NoDmgInvuln, Speed, this);
                        EndAltAttack();
                    }
                    if (other.Flags1.TestFlag(PlayerFlags1.Boosting))
                    {
                        TakeDamage(other._boostDamage, DamageFlags.NoDmgInvuln, other.Speed, other);
                        other.EndAltAttack();
                    }
                    if (_deathaltTimer > 0)
                    {
                        other.TakeDamage(200, DamageFlags.Deathalt | DamageFlags.NoDmgInvuln, Speed, this);
                    }
                    if (other._deathaltTimer > 0)
                    {
                        TakeDamage(200, DamageFlags.Deathalt | DamageFlags.NoDmgInvuln, other.Speed, other);
                    }
                    CheckAltAttackHit2(this, other, halfturret: false);
                    CheckAltAttackHit2(other, this, halfturret: false);
                }
                CheckAltAttackHit1(this, other, halfturret: false);
            }
        }

        // todo: more visualization
        private static void CheckAltAttackHit1(PlayerEntity attacker, PlayerEntity target, bool halfturret)
        {
            // the game assumes the hunter is noxus based on the alt attack timer
            if (attacker.Hunter == Hunter.Spire && attacker.Flags2.TestFlag(PlayerFlags2.AltAttack))
            {
                CollisionResult unused = default;
                bool hit = false;
                if (halfturret)
                {
                    var otherVolume = new CollisionVolume(target.Halfturret.Position, 0.45f);
                    hit = CollisionDetection.CheckSphereOverlapVolume(otherVolume, attacker._spireRockPosL, 0.5f, ref unused)
                        || CollisionDetection.CheckSphereOverlapVolume(otherVolume, attacker._spireRockPosR, 0.5f, ref unused);
                }
                else
                {
                    hit = CollisionDetection.CheckSphereOverlapVolume(target.Volume, attacker._spireRockPosL, 0.5f, ref unused)
                        || CollisionDetection.CheckSphereOverlapVolume(target.Volume, attacker._spireRockPosR, 0.5f, ref unused);
                }
                if (hit)
                {
                    Vector3 dir = Vector3.Zero;
                    if (!halfturret)
                    {
                        float x = target.Position.X - attacker.Position.X;
                        float z = target.Position.Z - attacker.Position.Z;
                        float factor = MathF.Sqrt(x * x + z * z) * 4;
                        dir.X = x / factor;
                        dir.Z = z / factor;
                    }
                    ushort damage = attacker.Values.AltAttackDamage;
                    // todo: if attacker is bot with encounter state, uses alternate damage value
                    DamageFlags flags = DamageFlags.NoSfx | DamageFlags.NoDmgInvuln;
                    if (halfturret)
                    {
                        flags |= DamageFlags.Halfturret;
                    }
                    target.TakeDamage(damage, flags, dir, attacker);
                    attacker._soundSource.PlaySfx(SfxId.SPIRE_ALT_ATTACK_HIT);
                }
            }
            else if (attacker.Hunter == Hunter.Noxus && attacker._altAttackTime >= attacker.Values.AltAttackStartup * 2) // todo: FPS stuff
            {
                Vector3 between;
                if (halfturret)
                {
                    between = target.Halfturret.Position - attacker.Volume.SpherePosition;
                }
                else
                {
                    between = target.Volume.SpherePosition - attacker.Volume.SpherePosition;
                }
                float radius = target.Volume.SphereRadius;
                if (between.Y > -radius && between.Y < radius)
                {
                    float hMagSqr = between.X * between.X + between.Z * between.Z;
                    float radAddSqr = radius + 1.8f;
                    radAddSqr *= radAddSqr;
                    if (hMagSqr < radAddSqr)
                    {
                        float factor = MathF.Sqrt(hMagSqr) * 8;
                        var dir = new Vector3(between.X / factor, 0, between.Z / factor);
                        target.Acceleration = dir;
                        target._accelerationTimer = 8 * 2; // todo: FPS stuff
                        ushort damage = attacker.Values.AltAttackDamage;
                        // todo: if attacker is bot with encounter state, uses alternate damage value
                        DamageFlags flags = DamageFlags.NoSfx | DamageFlags.NoDmgInvuln;
                        if (halfturret)
                        {
                            flags |= DamageFlags.Halfturret;
                        }
                        target.TakeDamage(damage, flags, dir, attacker);
                        attacker._soundSource.PlaySfx(SfxId.NOX_ALT_ATTACK_HIT);
                        target._scene.SpawnEffect(235, Vector3.UnitX, Vector3.UnitY, target.Position); // noxHit
                        attacker.EndAltAttack();
                    }
                }
            }
        }

        private static void CheckAltAttackHit2(PlayerEntity attacker, PlayerEntity target, bool halfturret)
        {
            if (attacker.Hunter != Hunter.Trace && attacker.Hunter != Hunter.Weavel
                || !attacker.Flags2.TestFlag(PlayerFlags2.AltAttack))
            {
                return;
            }
            // the game has a calculation + condition which is unnecessary since it also passes if Trace or Weavel (always true)
            // --> possibly meant to account for usages where this is called outside the already passed collision check
            Vector3 dir = Vector3.Zero;
            if (attacker.Hunter == Hunter.Trace)
            {
                dir.X = attacker.Speed.X;
                dir.Z = attacker.Speed.Z;
            }
            else
            {
                dir.X = attacker._field70;
                dir.Z = attacker._field74;
            }
            if (!halfturret)
            {
                float kbAccel = Fixed.ToFloat(attacker.Values.AltAttackKnockbackAccel);
                target.Acceleration = new Vector3(dir.X * kbAccel, -0.1f, dir.Z * kbAccel);
                target._accelerationTimer = (ushort)(attacker.Values.AltAttackKnockbackTime * 2); // todo: FPS stuff
            }
            ushort damage = attacker.Values.AltAttackDamage;
            // todo: if attacker is bot with encounter state, uses alternate damage value
            DamageFlags flags = DamageFlags.NoSfx | DamageFlags.NoDmgInvuln;
            if (halfturret)
            {
                flags |= DamageFlags.Halfturret;
            }
            target.TakeDamage(damage, flags, dir, attacker);
            SfxId sfx = attacker.Hunter == Hunter.Weavel ? SfxId.WEAVEL_ALT_ATTACK_HIT : SfxId.TRACE_ALT_ATTACK_HIT;
            attacker._soundSource.PlaySfx(sfx);
            attacker.EndAltAttack();
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
                    limit: 40, includeOffset: true, TestFlags.Players, _scene, results);
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
                    limit: 1, includeOffset: false, TestFlags.Players, _scene, results);
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
                            limit: 40, includeOffset: true, TestFlags.Players, _scene, results);
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
                    limit: 40, includeOffset: true, TestFlags.Players, _scene, results);
                for (int i = 0; i < count; i++)
                {
                    HandleCollision(results[i]);
                }
            }
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.Door)
                {
                    continue;
                }
                var door = (DoorEntity)entity;
                if (door.Flags.TestFlag(DoorFlags.Open))
                {
                    continue;
                }
                Vector3 lockPos = door.LockPosition;
                Vector3 doorFacing = door.FacingVector;
                Vector3 between = Position - lockPos;
                float dot = Vector3.Dot(between, doorFacing);
                if (dot <= 1.25f && dot >= -1.25f)
                {
                    between -= doorFacing * dot;
                    if (between.LengthSquared < door.RadiusSquared)
                    {
                        CollisionResult doorResult = default;
                        doorResult.Field0 = 0;
                        doorResult.Plane = new Vector4(doorFacing);
                        doorResult.EntityCollision = null;
                        doorResult.Flags = CollisionFlags.None;
                        if (Vector3.Dot(PrevPosition - lockPos, doorFacing) < 0)
                        {
                            doorResult.Plane.Xyz *= -1;
                        }
                        doorResult.Plane.W = doorResult.Plane.X * (lockPos.X + 0.4f * doorResult.Plane.X)
                            + doorResult.Plane.Y * (lockPos.Y + 0.4f * doorResult.Plane.Y)
                            + doorResult.Plane.Z * (lockPos.Z + 0.4f * doorResult.Plane.Z);
                        HandleCollision(doorResult);
                    }
                }
            }
            for (int i = 0; i < _scene.Entities.Count; i++)
            {
                EntityBase entity = _scene.Entities[i];
                if (entity.Type != EntityType.ForceField)
                {
                    continue;
                }
                var forceField = (ForceFieldEntity)entity;
                if (!forceField.Active)
                {
                    continue;
                }
                float dot1 = Vector3.Dot(Position, forceField.Plane.Xyz) - forceField.Plane.W;
                float dot2 = Vector3.Dot(PrevPosition, forceField.Plane.Xyz) - forceField.Plane.W;
                if (dot1 < 1 && dot1 > -1 || dot2 < 1 && dot2 > -1)
                {
                    Vector3 between = Volume.SpherePosition - forceField.Position;
                    float dotH = Vector3.Dot(between, forceField.UpVector);
                    float dotW = Vector3.Dot(between, forceField.RightVector);
                    if (dotH <= forceField.Height && dotH >= -forceField.Height
                        && dotW <= forceField.Width && dotW >= -forceField.Width)
                    {
                        CollisionResult ffResult = default;
                        ffResult.Field0 = 0;
                        ffResult.EntityCollision = null;
                        ffResult.Flags = CollisionFlags.None;
                        ffResult.Plane = forceField.Plane;
                        if (dot2 < 0)
                        {
                            ffResult.Plane *= -1;
                        }
                        HandleCollision(ffResult);
                    }
                }
            }
            DamageResult dmgRes = default;
            dmgRes.Damage = 1;
            dmgRes.TakeDamage = _terrainDamage;
            if (_collidedEntCol != null)
            {
                _collidedEntCol.Entity.CheckContactDamage(ref dmgRes);
            }
            if (dmgRes.TakeDamage)
            {
                TakeDamage(dmgRes.Damage, DamageFlags.IgnoreInvuln, direction: null, source: null);
            }
            _volume = CollisionVolume.Move(_volumeUnxf, Position);
        }

        public void HandleCollision(CollisionResult result)
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
                    if (result.Field0 != 1)
                    {
                        return;
                    }
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
                if (result.Field0 != 1)
                {
                    return;
                }
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
                    v11 = (yBot - result.EdgePoint1.Y) / edge.Y;
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
                    if (MathF.Abs(v11 - v162) < 1 / 4096f)
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
                    float factor = 1;
                    if (result.Plane.Y > 0 && result.Plane.Y < 0.9f)
                    {
                        factor = 0.5f / 2; // todo: FPS stuff
                    }
                    else if (result.Plane.Y < 0)
                    {
                        factor = 2 * 2; // todo: FPS stuff
                    }
                    position.Y += result.Plane.Y * v2 * factor;
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
                if (Hunter == Hunter.Spire && result.Field0 == 0)
                {
                    for (int i = 0; i < _spireAltVecs.Length; i++)
                    {
                        Vector3 vec = Position + _spireAltVecs[i];
                        float dot = result.Plane.W - Vector3.Dot(vec, result.Plane.Xyz);
                        if (dot >= 0)
                        {
                            Debug.Assert(vec != Position);
                            v164 = true;
                            // todo: revisit these calculations and improve the wall climbing "stickiness" issue
                            // --> related to the need for a hack to get pushed out more by horizontal collision (without jittering)
                            Position += result.Plane.Xyz * dot;
                            vec = new Vector3(Position.X - vec.X, 0, Position.Z - vec.Z).Normalized();
                            vec.X *= dot / 4;
                            vec.Z *= dot / 4;
                            vec.X *= _hSpeedMag + 0.1f;
                            vec.Z *= _hSpeedMag + 0.1f;
                            Speed += vec / 2;
                            if (result.Plane.Y > Fixed.ToFloat(-357) && Speed.Y < 0.15f)
                            {
                                v166 = true;
                                float yFactor = _hSpeedMag / 2;
                                if (Speed.Y < 0.01f)
                                {
                                    yFactor += 0.3f;
                                }
                                Speed = Speed.AddY(4 * dot * yFactor / 2);
                                Speed = Speed.WithY(Math.Min(Speed.Y, 0.15f));
                            }
                        }
                    }
                }
                else if (Hunter == Hunter.Sylux && result.Field0 == 0 && result.Plane.Y > Fixed.ToFloat(3138)
                    && !Flags1.TestFlag(PlayerFlags1.NoUnmorphPrevious))
                {
                    Vector3 pos = Position.AddY(Fixed.ToFloat(Values.AltColYPos) - Fixed.ToFloat(Values.AltColRadius) - 0.3f);
                    float dot = result.Plane.W - Vector3.Dot(pos, result.Plane.Xyz);
                    if (dot >= 0)
                    {
                        v164 = true;
                        Speed = Speed.AddY(Fixed.ToFloat(Values.AltAirGravity));
                        if (Speed.Y < 0.25f)
                        {
                            if (Speed.Y < 0)
                            {
                                Speed = Speed.WithY(Speed.Y * Fixed.ToFloat(4034));
                            }
                            Speed = Speed.AddY(dot * 0.2f);
                            Speed = Speed.WithY(Math.Min(Speed.Y, 0.25f));
                        }
                    }
                }
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
