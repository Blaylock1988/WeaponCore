﻿using System;
using System.Collections.Concurrent;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.AI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using static WeaponCore.Support.WeaponDefinition;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;
using static WeaponCore.Support.WeaponDefinition.TargetingDef.BlockTypes;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.TrajectoryDef;
using static WeaponCore.WeaponRandomGenerator.RandomType;
using static WeaponCore.WeaponRandomGenerator;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal static void AcquireTarget(Weapon w, bool attemptReset, MyEntity targetGrid = null)
        {
            w.HitOther = false;
            var tick = w.Comp.Session.Tick;
            w.Target.CheckTick = tick;
            var targetType = TargetType.None;
            if (w.PosChangedTick != w.Comp.Session.Tick) w.UpdatePivotPos();

            if (!w.Comp.TrackReticle)
            {
                w.AimCone.ConeDir = w.MyPivotDir;
                w.AimCone.ConeTip = w.MyPivotPos;
                var pCount = w.Comp.Ai.LiveProjectile.Count;
                var shootProjectile = pCount > 0 && w.System.TrackProjectile;
                var projectilesFirst = !attemptReset && shootProjectile && w.System.Values.Targeting.Threats.Length > 0 && w.System.Values.Targeting.Threats[0] == Threat.Projectiles;
                var onlyCheckProjectile = w.ProjectilesNear && !w.Target.TargetChanged && w.Comp.Session.Count != w.LoadId && !attemptReset;

                if (!projectilesFirst && w.System.TrackOther && !onlyCheckProjectile) AcquireOther(w, out targetType, attemptReset, targetGrid);
                else if (!attemptReset && targetType == TargetType.None && shootProjectile) AcquireProjectile(w, out targetType);
                if (projectilesFirst && targetType == TargetType.None && !onlyCheckProjectile) AcquireOther(w, out targetType, false, targetGrid);
            }
            else
            {
                Vector3D predictedPos;
                if (Weapon.CanShootTarget(w, w.Comp.Ai.DummyTarget.Position, w.Comp.Ai.DummyTarget.LinearVelocity, w.Comp.Ai.DummyTarget.Acceleration, out predictedPos))
                {
                    w.Target.SetFake(w.Comp.Session.Tick, predictedPos);
                    if (w.ActiveAmmoDef.AmmoDef.Trajectory.Guidance != GuidanceType.None || !w.MuzzleHitSelf())
                        targetType = TargetType.Other;
                }
            }

            if (targetType == TargetType.None)
            {
                if (w.NewTarget.CurrentState != Target.States.NoTargetsSeen) w.NewTarget.Reset(w.Comp.Session.Tick, Target.States.NoTargetsSeen);
                w.LastBlockCount = w.Comp.Ai.BlockCount;
                if (w.Target.CurrentState != Target.States.NoTargetsSeen) w.Target.Reset(w.Comp.Session.Tick, Target.States.NoTargetsSeen, !w.Comp.TrackReticle);
            }
            else w.WakeTargets();
        }

        internal static bool ReacquireTarget(Projectile p)
        {
            p.ChaseAge = p.Info.Age;
            var s = p.Info.System;
            var ai = p.Info.Ai;
            var weaponPos = p.Position;
            var overRides = p.Info.Overrides;
            var overActive = overRides.Activate;
            var attackNeutrals = overActive && overRides.Neutrals;
            var attackFriends = overActive && overRides.Friendly;
            var attackNoOwner = overActive && overRides.Unowned;
            var forceFoci = overActive && overRides.FocusTargets;

            TargetInfo alphaInfo = null;
            TargetInfo betaInfo = null;
            int offset = 0;

            if (ai.Focus.Target[0] != null)
                if (ai.Targets.TryGetValue(ai.Focus.Target[0], out alphaInfo)) offset++;
            if (ai.Focus.Target[1] != null)
                if (ai.Targets.TryGetValue(ai.Focus.Target[1], out betaInfo)) offset++;


            var numOfTargets = ai.SortedTargets.Count;
            var hasOffset = offset > 0;
            var adjTargetCount = forceFoci && hasOffset ? offset : numOfTargets + offset;
            var deck = GetDeck(ref p.Info.Target.TargetDeck, ref p.Info.Target.TargetPrevDeckLen, 0, numOfTargets, p.Info.System.Values.Targeting.TopTargets, p.Info.WeaponRng, ReAcquire);

            for (int i = 0; i < adjTargetCount; i++)
            {
                var focusTarget = hasOffset && i < offset;
                var lastOffset = offset - 1;

                TargetInfo info;
                if (i == 0 && alphaInfo != null) info = alphaInfo;
                else if (i <= lastOffset && betaInfo != null) info = betaInfo;
                else info = ai.SortedTargets[deck[i - offset]];

                if (info.Target == null || info.Target.MarkedForClose || hasOffset && i > lastOffset && (info.Target == alphaInfo?.Target || info.Target == betaInfo?.Target)) continue;
                var targetRadius = info.Target.PositionComp.LocalVolume.Radius;
                var targetPos = info.Target.PositionComp.WorldAABB.Center;

                if (targetRadius < s.MinTargetRadius || targetRadius > s.MaxTargetRadius || Vector3D.DistanceSquared(targetPos, p.Position) >= p.DistanceToTravelSqr) continue;

                if (!focusTarget && info.OffenseRating <= 0 || Obstruction(ref info, ref targetPos, p))
                    continue;

                if (focusTarget && !attackFriends && info.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Friends) continue;

                if (!attackNeutrals && info.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral || !attackNoOwner && info.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.NoOwnership) continue;

                if (info.IsGrid && s.TrackGrids)
                {
                    if (!focusTarget && info.FatCount < 2) continue;

                    if (!AcquireBlock(p.Info.System, p.Info.Ai, p.Info.Target, info, weaponPos, p.Info.WeaponRng, ReAcquire, null, !focusTarget)) continue;
                    return true;
                }

                var character = info.Target as IMyCharacter;
                if (character != null && !s.TrackCharacters) continue;

                var meteor = info.Target as MyMeteor;
                if (meteor != null && !s.TrackMeteors) continue;

                double rayDist;
                Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                var shortDist = rayDist;
                var origDist = rayDist;
                var topEntId = info.Target.GetTopMostParent().EntityId;
                p.Info.Target.Set(info.Target, targetPos, shortDist, origDist, topEntId);
                return true;
            }
            p.Info.Target.Reset(ai.Session.Tick, Target.States.NoTargetsSeen);
            return false;
        }

        private static void AcquireOther(Weapon w, out TargetType targetType, bool attemptReset = false, MyEntity targetGrid = null)
        {
            var comp = w.Comp;
            var overRides = comp.Set.Value.Overrides;
            var overActive = overRides.Activate;
            var attackNeutrals = overActive && overRides.Neutrals;
            var attackFriends = overActive && overRides.Friendly;
            var attackNoOwner = overActive && overRides.Unowned;
            var forceFoci = overActive && overRides.FocusTargets;
            var session = w.Comp.Session;
            var ai = comp.Ai;
            session.TargetRequests++;
            var physics = session.Physics;
            var weaponPos = w.MyPivotPos;
            var target = w.NewTarget;
            var s = w.System;
            var accelPrediction = (int) s.Values.HardPoint.AimLeadingPrediction > 1;
            TargetInfo alphaInfo = null;
            TargetInfo betaInfo = null;
            int offset = 0;

            if (ai.Focus.Target[0] != null)
                if (ai.Targets.TryGetValue(ai.Focus.Target[0], out alphaInfo)) offset++;
            if (ai.Focus.Target[1] != null)
                if (ai.Targets.TryGetValue(ai.Focus.Target[1], out betaInfo)) offset++;

            TargetInfo gridInfo = null;
            var forceTarget = false;
            if (targetGrid != null)
                if(ai.Targets.TryGetValue(targetGrid, out gridInfo))
                    forceTarget = true;

            var hasOffset = offset > 0;
            var numOfTargets = ai.SortedTargets.Count;
            var adjTargetCount = forceFoci && hasOffset ? offset : numOfTargets + offset;
            var deck = GetDeck(ref target.TargetDeck, ref target.TargetPrevDeckLen, 0, numOfTargets, w.System.Values.Targeting.TopTargets, w.Comp.WeaponValues.WeaponRandom[w.WeaponId], Acquire);
            try
            {
                for (int x = 0; x < adjTargetCount; x++)
                {
                    var focusTarget = hasOffset && x < offset;
                    var lastOffset = offset - 1;
                    if (attemptReset && !focusTarget) break;
                    TargetInfo info = null;
                    if (forceTarget && !focusTarget) info = gridInfo;
                    else
                    {
                        if (focusTarget)
                        {
                            if (x == 0 && alphaInfo != null) info = alphaInfo;
                            else if (x == 0 && betaInfo != null) info = betaInfo;
                            else if (x == 1) info = betaInfo;
                            if (!attackFriends && info.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Friends) continue;
                        }
                        else info = ai.SortedTargets[deck[x - offset]];
                    }

                    if (info?.Target == null || info.Target.MarkedForClose || hasOffset && x > lastOffset && (info.Target == alphaInfo?.Target || info.Target == betaInfo?.Target) || !attackNeutrals && info.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral || !attackNoOwner && info.EntInfo.Relationship == MyRelationsBetweenPlayerAndBlock.NoOwnership) continue;

                    if (info.TargetRadius < s.MinTargetRadius || info.TargetRadius > s.MaxTargetRadius || !focusTarget && info.OffenseRating <= 0) continue;
                    var targetCenter = info.Target.PositionComp.WorldAABB.Center;
                    var targetDistSqr = Vector3D.DistanceSquared(targetCenter, w.MyPivotPos);
                    if (targetDistSqr > (w.MaxTargetDistance + info.TargetRadius) * (w.MaxTargetDistance + info.TargetRadius) || targetDistSqr < w.MinTargetDistanceSqr) continue;
                    session.TargetChecks++;
                    Vector3D targetLinVel = info.Target.Physics?.LinearVelocity ?? Vector3D.Zero;
                    Vector3D targetAccel = accelPrediction ? info.Target.Physics?.LinearAcceleration ?? Vector3D.Zero : Vector3.Zero;
                    if (info.IsGrid)
                    {
                        if (!s.TrackGrids || !focusTarget && info.FatCount < 2) continue;
                        session.CanShoot++;
                        if (!w.AiEnabled)
                        {
                            var newCenter = w.System.Prediction != HardPointDef.Prediction.Off && (!w.ActiveAmmoDef.AmmoDef.Const.IsBeamWeapon && w.ActiveAmmoDef.AmmoDef.Const.DesiredProjectileSpeed > 0) ? w.GetPredictedTargetPosition(targetCenter, targetLinVel, targetAccel) : targetCenter;
                            var targetSphere = info.Target.PositionComp.WorldVolume;
                            targetSphere.Center = newCenter;
                            if (!MathFuncs.TargetSphereInCone(ref targetSphere, ref w.AimCone)) continue;
                        }
                        else if (!Weapon.CanShootTargetObb(w, info.Target, targetLinVel, targetAccel)) continue;

                        if (!AcquireBlock(s, w.Comp.Ai, target, info, weaponPos, w.Comp.WeaponValues.WeaponRandom[w.WeaponId], Acquire, w, true)) continue;

                        targetType = TargetType.Other;
                        target.TransferTo(w.Target, w.Comp.Session.Tick);

                        return;
                    }
                    var meteor = info.Target as MyMeteor;
                    if (meteor != null && !s.TrackMeteors) continue;

                    var character = info.Target as IMyCharacter;

                    if (character != null && !s.TrackCharacters || character.IsDead || character.Integrity <= 0 || session.AdminMap.ContainsKey(character)) continue;
                    Vector3D predictedPos;

                    if (!Weapon.CanShootTarget(w, targetCenter, targetLinVel, targetAccel, out predictedPos)) continue;
                    var targetPos = info.Target.PositionComp.WorldAABB.Center;
                    session.TopRayCasts++;
                    IHitInfo hitInfo;
                    physics.CastRay(weaponPos, targetPos, out hitInfo, 15, true);

                    if (hitInfo != null && hitInfo.HitEntity == info.Target && (!w.System.Values.HardPoint.Other.MuzzleCheck || !w.MuzzleHitSelf()))
                    {
                        double rayDist;
                        Vector3D.Distance(ref weaponPos, ref targetPos, out rayDist);
                        var shortDist = rayDist * (1 - hitInfo.Fraction);
                        var origDist = rayDist * hitInfo.Fraction;
                        var topEntId = info.Target.GetTopMostParent().EntityId;
                        target.Set(info.Target, hitInfo.Position, shortDist, origDist, topEntId);
                        targetType = TargetType.Other;
                        target.TransferTo(w.Target, w.Comp.Session.Tick);
                        return;
                    }
                    if (forceTarget) break;
                }
                if (!attemptReset || !w.Target.HasTarget) targetType = TargetType.None;
                else targetType = w.Target.IsProjectile ? TargetType.Projectile : TargetType.Other;
            }
            catch (Exception ex) { Log.Line($"Exception in AcquireOther: {ex}"); targetType = TargetType.None;}
        }

        private static bool AcquireBlock(WeaponSystem system, GridAi ai, Target target, TargetInfo info, Vector3D weaponPos, WeaponRandomGenerator wRng, RandomType type, Weapon w = null, bool checkPower = true)
        {
            if (system.TargetSubSystems)
            {
                var subSystems = system.Values.Targeting.SubSystems;
                var targetLinVel = info.Target.Physics?.LinearVelocity ?? Vector3D.Zero;
                var targetAccel = (int)system.Values.HardPoint.AimLeadingPrediction > 1 ? info.Target.Physics?.LinearAcceleration ?? Vector3D.Zero : Vector3.Zero;
                var focusSubSystem = w != null && w.Comp.Set.Value.Overrides.FocusSubSystem;
                
                foreach (var blockType in subSystems)
                {
                    var bt = focusSubSystem ? w.Comp.Set.Value.Overrides.SubSystem : blockType;

                    ConcurrentDictionary<BlockTypes, ConcurrentCachingList<MyCubeBlock>> blockTypeMap;
                    ai.Session.GridToBlockTypeMap.TryGetValue((MyCubeGrid) info.Target, out blockTypeMap);
                    if (bt != Any && blockTypeMap != null && blockTypeMap[bt].Count > 0)
                    {
                        var subSystemList = blockTypeMap[bt];
                        if (system.ClosestFirst)
                        {
                            if (target.Top5.Count > 0 && (bt != target.LastBlockType || target.Top5[0].CubeGrid != subSystemList[0].CubeGrid))
                                target.Top5.Clear();

                            target.LastBlockType = bt;
                            if (GetClosestHitableBlockOfType(subSystemList, ai, target, weaponPos, targetLinVel, targetAccel, system, w, checkPower)) return true;
                        }
                        else if (FindRandomBlock(system, ai, target, weaponPos, info, subSystemList, w, wRng, type, checkPower)) return true;
                    }

                    if (focusSubSystem) break;
                }

                if (system.OnlySubSystems || focusSubSystem && w.Comp.Set.Value.Overrides.SubSystem != Any) return false;
            }
            FatMap fatMap;
            return ai.Session.GridToFatMap.TryGetValue((MyCubeGrid)info.Target, out fatMap) && fatMap.MyCubeBocks != null && FindRandomBlock(system, ai, target, weaponPos, info, fatMap.MyCubeBocks, w, wRng, type, checkPower);
        }

        private static bool FindRandomBlock(WeaponSystem system, GridAi ai, Target target, Vector3D weaponPos, TargetInfo info, ConcurrentCachingList<MyCubeBlock> subSystemList, Weapon w, WeaponRandomGenerator wRng, RandomType type, bool checkPower = true)
        {
            var totalBlocks = subSystemList.Count;

            var topEnt = info.Target.GetTopMostParent();

            var entSphere = topEnt.PositionComp.WorldVolume;
            var distToEnt = MyUtils.GetSmallestDistanceToSphere(ref weaponPos, ref entSphere);
            var turretCheck = w != null;
            var topBlocks = system.Values.Targeting.TopBlocks;
            var lastBlocks = topBlocks > 10 && distToEnt < 1000 ? topBlocks : 10;
            var isPriroity = false;
            if (lastBlocks < 250)
            {
                TargetInfo priorityInfo;
                if (ai.Focus.Target[0] != null && ai.Targets.TryGetValue(ai.Focus.Target[0], out priorityInfo) && priorityInfo.Target?.GetTopMostParent() == topEnt)
                {
                    isPriroity = true;
                    lastBlocks = totalBlocks < 250 ? totalBlocks : 250;
                }
                else if (ai.Focus.Target[1] != null && ai.Targets.TryGetValue(ai.Focus.Target[1], out priorityInfo) && priorityInfo.Target?.GetTopMostParent() == topEnt)
                {
                    isPriroity = true;
                    lastBlocks = totalBlocks < 250 ? totalBlocks : 250;
                }
            }

            if (totalBlocks < lastBlocks) lastBlocks = totalBlocks;
            var deck = GetDeck(ref target.BlockDeck, ref target.BlockPrevDeckLen, 0, totalBlocks, topBlocks, wRng, type);
            var physics = ai.Session.Physics;
            var iGrid = topEnt as IMyCubeGrid;
            var gridPhysics = iGrid?.Physics;
            Vector3D targetLinVel = gridPhysics?.LinearVelocity ?? Vector3D.Zero;
            Vector3D targetAccel = (int)system.Values.HardPoint.AimLeadingPrediction > 1 ? info.Target.Physics?.LinearAcceleration ?? Vector3D.Zero : Vector3.Zero;
            var notSelfHit = false;
            var foundBlock = false;
            var blocksChecked = 0;
            var blocksSighted = 0;

            for (int i = 0; i < totalBlocks; i++)
            {
                if (turretCheck && (blocksChecked > lastBlocks || isPriroity && (blocksSighted > 100 || blocksChecked > 50 && ai.Session.RandomRayCasts > 500 || blocksChecked > 25 && ai.Session.RandomRayCasts > 1000) ))
                    break;

                var card = deck[i];
                var block = subSystemList[card];

                if (!(block is IMyTerminalBlock) || block.MarkedForClose || checkPower && !block.IsWorking) continue;

                ai.Session.BlockChecks++;

                var blockPos = block.CubeGrid.GridIntegerToWorld(block.Position);
                double rayDist;
                if (turretCheck)
                {
                    double distSqr;
                    Vector3D.DistanceSquared(ref blockPos, ref weaponPos, out distSqr);
                    if (distSqr > w.MaxTargetDistanceSqr || distSqr < w.MinTargetDistanceSqr)
                        continue;

                    blocksChecked++;
                    ai.Session.CanShoot++;
                    Vector3D predictedPos;
                    if (!Weapon.CanShootTarget(w, blockPos, targetLinVel, targetAccel, out predictedPos)) continue;

                    blocksSighted++;
                    Vector3D? hitPos;
                    if (!w.HitOther && GridIntersection.BresenhamGridIntersection(ai.MyGrid, ref weaponPos, ref blockPos, out hitPos, w.Comp.MyCube, w.Comp.Ai))
                        continue;

                    ai.Session.RandomRayCasts++;
                    IHitInfo hitInfo;
                    physics.CastRay(weaponPos, blockPos, out hitInfo, 15, true);

                    if (hitInfo == null || hitInfo.HitEntity != ai.MyGrid && (!w.System.Values.HardPoint.Other.MuzzleCheck || !w.MuzzleHitSelf()))
                        notSelfHit = true;

                    if (hitInfo?.HitEntity == null || hitInfo.HitEntity is MyVoxelBase ||
                        hitInfo.HitEntity == ai.MyGrid)
                        continue;

                    var hitGrid = hitInfo.HitEntity as MyCubeGrid;
                    if (hitGrid != null)
                    {
                        if (hitGrid.MarkedForClose) continue;
                        bool enemy;

                        var bigOwners = hitGrid.BigOwners;
                        if (bigOwners.Count == 0) enemy = true;
                        else
                        {
                            var relationship = target.FiringCube.GetUserRelationToOwner(hitGrid.BigOwners[0]);
                            enemy = relationship != MyRelationsBetweenPlayerAndBlock.Owner &&
                                    relationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
                        }

                        if (!enemy)
                            continue;
                    }

                    Vector3D.Distance(ref weaponPos, ref blockPos, out rayDist);
                    var shortDist = rayDist * (1 - hitInfo.Fraction);
                    var origDist = rayDist * hitInfo.Fraction;
                    var topEntId = block.GetTopMostParent().EntityId;
                    target.Set(block, hitInfo.Position, shortDist, origDist, topEntId);
                    foundBlock = true;
                    break;
                }

                Vector3D.Distance(ref weaponPos, ref blockPos, out rayDist);
                target.Set(block, block.PositionComp.WorldAABB.Center, rayDist, rayDist, block.GetTopMostParent().EntityId);
                foundBlock = true;
                break;
            }
            if (turretCheck && !notSelfHit) w.HitOther = true;
            return foundBlock;
        }

        internal static bool GetClosestHitableBlockOfType(ConcurrentCachingList<MyCubeBlock> cubes, GridAi ai, Target target, Vector3D currentPos, Vector3D targetLinVel, Vector3D targetAccel, WeaponSystem system, Weapon w = null, bool checkPower = true)
        {
            var minValue = double.MaxValue;
            var minValue0 = double.MaxValue;
            var minValue1 = double.MaxValue;
            var minValue2 = double.MaxValue;
            var minValue3 = double.MaxValue;

            MyCubeBlock newEntity = null;
            MyCubeBlock newEntity0 = null;
            MyCubeBlock newEntity1 = null;
            MyCubeBlock newEntity2 = null;
            MyCubeBlock newEntity3 = null;
            var bestCubePos = Vector3D.Zero;
            var top5Count = target.Top5.Count;
            var testPos = currentPos;
            var top5 = target.Top5;
            IHitInfo hitInfo = null;
            var notSelfHit = false;
            for (int i = 0; i < cubes.Count + top5Count; i++)
            {
                ai.Session.BlockChecks++;
                var index = i < top5Count ? i : i - top5Count;
                var cube = i < top5Count ? top5[index] : cubes[index];

                var grid = cube.CubeGrid;
                if (grid?.Physics == null || !grid.Physics.Enabled || grid.PositionComp == null) continue;

                if (cube.MarkedForClose || checkPower && !cube.IsWorking || !(cube is IMyTerminalBlock) || cube == newEntity || cube == newEntity0 || cube == newEntity1 || cube == newEntity2 || cube == newEntity3) continue;
                var cubePos = grid.GridIntegerToWorld(cube.Position);
                var range = cubePos - testPos;
                var test = (range.X * range.X) + (range.Y * range.Y) + (range.Z * range.Z);
                if (test < minValue3)
                {
                    IHitInfo hit = null;
                    var best = test < minValue;
                    var bestTest = false;
                    if (best)
                    {
                        if (w != null && !(!w.IsTurret && w.ActiveAmmoDef.AmmoDef.Trajectory.Smarts.OverideTarget))
                        {
                            ai.Session.CanShoot++;
                            var castRay = false;

                            Vector3D predictedPos;
                            Vector3D? hitPos;
                            if (Weapon.CanShootTarget(w, cubePos, targetLinVel, targetAccel, out predictedPos))
                              castRay = !w.HitOther || !GridIntersection.BresenhamGridIntersection(ai.MyGrid, ref testPos, ref cubePos, out hitPos, w.Comp.MyCube, w.Comp.Ai);

                            if (castRay)
                            {
                                ai.Session.ClosestRayCasts++;
                                bestTest = MyAPIGateway.Physics.CastRay(testPos, cubePos, out hit, 15, true) && hit?.HitEntity == cube.CubeGrid;

                                if (hit == null && (!w.System.Values.HardPoint.Other.MuzzleCheck || !w.MuzzleHitSelf()) || (hit.HitEntity != ai.MyGrid))
                                    notSelfHit = true;
                            }
                        }
                        else bestTest = true;
                    }
                    if (best && bestTest)
                    {
                        minValue3 = minValue2;
                        newEntity3 = newEntity2;
                        minValue2 = minValue1;
                        newEntity2 = newEntity1;
                        minValue1 = minValue0;
                        newEntity1 = newEntity0;
                        minValue0 = minValue;
                        newEntity0 = newEntity;
                        minValue = test;

                        newEntity = cube;
                        bestCubePos = cubePos;
                        hitInfo = hit;
                    }
                    else if (test < minValue0)
                    {
                        minValue3 = minValue2;
                        newEntity3 = newEntity2;
                        minValue2 = minValue1;
                        newEntity2 = newEntity1;
                        minValue1 = minValue0;
                        newEntity1 = newEntity0;
                        minValue0 = test;

                        newEntity0 = cube;
                    }
                    else if (test < minValue1)
                    {
                        minValue3 = minValue2;
                        newEntity3 = newEntity2;
                        minValue2 = minValue1;
                        newEntity2 = newEntity1;
                        minValue1 = test;

                        newEntity1 = cube;
                    }
                    else if (test < minValue2)
                    {
                        minValue3 = minValue2;
                        newEntity3 = newEntity2;
                        minValue2 = test;

                        newEntity2 = cube;
                    }
                    else
                    {
                        minValue3 = test;
                        newEntity3 = cube;
                    }
                }

            }
            top5.Clear();
            if (newEntity != null && hitInfo != null)
            {
                double rayDist;
                Vector3D.Distance(ref testPos, ref bestCubePos, out rayDist);
                var shortDist = rayDist * (1 - hitInfo.Fraction);
                var origDist = rayDist * hitInfo.Fraction;
                var topEntId = newEntity.GetTopMostParent().EntityId;
                target.Set(newEntity, hitInfo.Position, shortDist, origDist, topEntId);
                top5.Add(newEntity);
            }
            else if (newEntity != null)
            {
                double rayDist;
                Vector3D.Distance(ref testPos, ref bestCubePos, out rayDist);
                var shortDist = rayDist;
                var origDist = rayDist;
                var topEntId = newEntity.GetTopMostParent().EntityId;
                target.Set(newEntity, bestCubePos, shortDist, origDist, topEntId);
                top5.Add(newEntity);
            }
            else target.Reset(ai.Session.Tick, Target.States.NoTargetsSeen, w == null);

            if (newEntity0 != null) top5.Add(newEntity0);
            if (newEntity1 != null) top5.Add(newEntity1);
            if (newEntity2 != null) top5.Add(newEntity2);   
            if (newEntity3 != null) top5.Add(newEntity3);

            if (!notSelfHit && w != null) w.HitOther = true;

            return hitInfo != null;
        }


        internal static void AcquireProjectile(Weapon w, out TargetType targetType)
        {
            var ai = w.Comp.Ai;
            var s = w.System;
            var physics = ai.Session.Physics;
            var target = w.NewTarget;
            var weaponPos = w.MyPivotPos;

            var collection = ai.GetProCache();
            var numOfTargets = collection.Count;
            var lockedOnly = w.System.Values.Targeting.LockedSmartOnly;
            var smartOnly = w.System.Values.Targeting.IgnoreDumbProjectiles;
            if (s.ClosestFirst) {
                int length = collection.Count;
                for (int h = length / 2; h > 0; h /= 2) {
                    for (int i = h; i < length; i += 1) {
                        var tempValue = collection[i];
                        double temp;
                        Vector3D.DistanceSquared(ref collection[i].Position, ref weaponPos, out temp);

                        int j;
                        for (j = i; j >= h && Vector3D.DistanceSquared(collection[j - h].Position, weaponPos) > temp; j -= h)
                            collection[j] = collection[j - h];

                        collection[j] = tempValue;
                    }
                }
            }

            var numToRandomize = s.ClosestFirst ? w.System.Values.Targeting.TopTargets : numOfTargets;
            var deck = GetDeck(ref target.TargetDeck, ref target.TargetPrevDeckLen, 0, numOfTargets, numToRandomize, w.Comp.WeaponValues.WeaponRandom[w.WeaponId], Acquire);

            for (int x = 0; x < numOfTargets; x++)
            {
                var card = deck[x];
                var lp = collection[card];
                var cube = lp.Info.Target.Entity as MyCubeBlock;
                if (smartOnly && !lp.SmartsOn || lockedOnly && (!lp.SmartsOn || cube != null && cube.CubeGrid.IsSameConstructAs(w.Comp.Ai.MyGrid)) || lp.MaxSpeed > s.MaxTargetSpeed || lp.MaxSpeed <= 0 || lp.State != Projectile.ProjectileState.Alive || Vector3D.DistanceSquared(lp.Position, w.MyPivotPos) > w.MaxTargetDistanceSqr || Vector3D.DistanceSquared(lp.Position, w.MyPivotPos) < w.MinTargetDistanceSqr) continue;

                Vector3D predictedPos;
                if (Weapon.CanShootTarget(w, lp.Position, lp.Velocity, lp.AccelVelocity, out predictedPos))
                {
                    var needsCast = false;
                    for (int i = 0; i < ai.Obstructions.Count; i++)
                    {
                        var ent = ai.Obstructions[i];
                        var obsSphere = ent.PositionComp.WorldVolume;

                        var dir = lp.Position - weaponPos;
                        var beam = new RayD(ref weaponPos, ref dir);

                        if (beam.Intersects(obsSphere) != null)
                        {
                            var rotMatrix = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
                            var obb = new MyOrientedBoundingBoxD(ent.PositionComp.WorldAABB.Center, ent.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                            if (obb.Intersects(ref beam) != null)
                            {
                                needsCast = true;
                                break;
                            }
                        }
                    }

                    if (needsCast)
                    {
                        IHitInfo hitInfo;
                        physics.CastRay(weaponPos, lp.Position, out hitInfo, 15, true);
                        if (hitInfo?.HitEntity == null && (!w.System.Values.HardPoint.Other.MuzzleCheck || !w.MuzzleHitSelf()))
                        {
                            double hitDist;
                            Vector3D.Distance(ref weaponPos, ref lp.Position, out hitDist);
                            var shortDist = hitDist;
                            var origDist = hitDist;
                            const long topEntId = long.MaxValue;
                            target.Set(null, lp.Position, shortDist, origDist, topEntId, lp);
                            targetType = TargetType.Projectile;
                            target.TransferTo(w.Target, w.Comp.Session.Tick);
                            return;
                        }
                    }
                    else
                    {
                        Vector3D? hitInfo;
                        if (GridIntersection.BresenhamGridIntersection(ai.MyGrid, ref weaponPos, ref lp.Position, out hitInfo, w.Comp.MyCube, w.Comp.Ai))
                            continue;

                        double hitDist;
                        Vector3D.Distance(ref weaponPos, ref lp.Position, out hitDist);
                        var shortDist = hitDist;
                        var origDist = hitDist;
                        const long topEntId = long.MaxValue;
                        target.Set(null, lp.Position, shortDist, origDist, topEntId, lp);
                        targetType = TargetType.Projectile;
                        target.TransferTo(w.Target, w.Comp.Session.Tick);
                        return;
                    }
                }
            }
            targetType = TargetType.None;
        }

        private static bool Obstruction(ref TargetInfo info, ref Vector3D targetPos, Projectile p)
        {
            var ai = p.Info.Ai;
            var obstruction = false;
            for (int j = 0; j < ai.Obstructions.Count; j++)
            {
                var ent = ai.Obstructions[j];
                var voxel = ent as MyVoxelBase;
                var dir = (targetPos - p.Position);
                if (voxel != null)
                {
                    var voxelVolume = ent.PositionComp.WorldVolume;

                    if (voxelVolume.Contains(p.Position) != ContainmentType.Disjoint || new RayD(ref p.Position, ref dir).Intersects(voxelVolume) != null)
                    {
                        var dirNorm = Vector3D.Normalize(dir);
                        var targetDist = Vector3D.Distance(p.Position, targetPos);
                        var tRadius = info.Target.PositionComp.LocalVolume.Radius;
                        var testPos = p.Position + (dirNorm * (targetDist - tRadius));
                        var lineTest = new LineD(p.Position, testPos);
                        Vector3D? voxelHit = null;
                        using (voxel.Pin())
                            voxel.RootVoxel.GetIntersectionWithLine(ref lineTest, out voxelHit);

                        obstruction = voxelHit.HasValue;
                        if (obstruction)
                            break;
                    }
                }
                else
                {
                    if (new RayD(ref p.Position, ref dir).Intersects(ent.PositionComp.WorldVolume) != null)
                    {
                        var rotMatrix = Quaternion.CreateFromRotationMatrix(ent.WorldMatrix);
                        var obb = new MyOrientedBoundingBoxD(ent.PositionComp.WorldAABB.Center, ent.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                        var lineTest = new LineD(p.Position, targetPos);
                        if (obb.Intersects(ref lineTest) != null)
                        {
                            obstruction = true;
                            break;
                        }
                    }
                }
            }

            if (!obstruction)
            {
                var dir = (targetPos - p.Position);
                var ray = new RayD(ref p.Position, ref dir);
                foreach (var sub in ai.SubGrids)
                {
                    var subDist = sub.PositionComp.WorldVolume.Intersects(ray);
                    if (subDist.HasValue)
                    {
                        var rotMatrix = Quaternion.CreateFromRotationMatrix(ai.MyGrid.WorldMatrix);
                        var obb = new MyOrientedBoundingBoxD(ai.MyGrid.PositionComp.WorldAABB.Center, ai.MyGrid.PositionComp.LocalAABB.HalfExtents, rotMatrix);
                        if (obb.Intersects(ref ray) != null)
                            obstruction = sub.RayCastBlocks(p.Position, targetPos) != null;
                    }

                    if (obstruction) break;
                }

                if (!obstruction && ai.PlanetSurfaceInRange && ai.MyPlanet != null)
                {
                    var dirNorm = Vector3D.Normalize(dir);
                    var targetDist = Vector3D.Distance(p.Position, targetPos);
                    var tRadius = info.Target.PositionComp.LocalVolume.Radius;
                    var testPos = p.Position + (dirNorm * (targetDist - tRadius));
                    var lineTest = new LineD(p.Position, testPos);

                    using (ai.MyPlanet.Pin())
                        obstruction = VoxelIntersect.CheckSurfacePointsOnLine(ai.MyPlanet, lineTest, 2);

                    //obstruction = voxelHit.HasValue;
                }
            }
            return obstruction;
        }
    }
}
