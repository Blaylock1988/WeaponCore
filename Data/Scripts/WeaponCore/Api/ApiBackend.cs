﻿using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Projectiles;
using WeaponCore.Support;
using static WeaponCore.Support.WeaponComponent.ShootActions;
using static WeaponCore.Platform.MyWeaponPlatform.PlatformState;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

namespace WeaponCore.Api
{
    internal class ApiBackend
    {
        private readonly Session _session;
        internal readonly Dictionary<string, Delegate> ModApiMethods;
        internal Dictionary<string, Delegate> PbApiMethods;
        
        internal ApiBackend(Session session)
        {
            _session = session;

            ModApiMethods = new Dictionary<string, Delegate>()
            {
                ["GetAllWeaponDefinitions"] = new Action<IList<byte[]>>(GetAllWeaponDefinitions),
                ["GetCoreWeapons"] = new Action<ICollection<MyDefinitionId>>(GetCoreWeapons),
                ["GetCoreStaticLaunchers"] = new Action<ICollection<MyDefinitionId>>(GetCoreStaticLaunchers),
                ["GetCoreTurrets"] = new Action<ICollection<MyDefinitionId>>(GetCoreTurrets),
                ["GetBlockWeaponMap"] = new Func<IMyTerminalBlock, IDictionary<string, int>, bool>(GetBlockWeaponMap),
                ["GetProjectilesLockedOn"] = new Func<IMyEntity, MyTuple<bool, int, int>>(GetProjectilesLockedOn),
                ["GetSortedThreats"] = new Action<IMyEntity, ICollection<MyTuple<IMyEntity, float>>>(GetSortedThreats),
                ["GetObstructions"] = new Action<IMyEntity, ICollection<IMyEntity>>(GetObstructions),
                ["GetAiFocus"] = new Func<IMyEntity, int, IMyEntity>(GetAiFocus),
                ["SetAiFocus"] = new Func<IMyEntity, IMyEntity, int, bool>(SetAiFocus),
                ["GetWeaponTarget"] = new Func<IMyTerminalBlock, int, MyTuple<bool, bool, bool, IMyEntity>>(GetWeaponTarget),
                ["SetWeaponTarget"] = new Action<IMyTerminalBlock, IMyEntity, int>(SetWeaponTarget),
                ["FireWeaponOnce"] = new Action<IMyTerminalBlock, bool, int>(FireWeaponOnce),
                ["ToggleWeaponFire"] = new Action<IMyTerminalBlock, bool, bool, int>(ToggleWeaponFire),
                ["IsWeaponReadyToFire"] = new Func<IMyTerminalBlock, int, bool, bool, bool>(IsWeaponReadyToFire),
                ["GetMaxWeaponRange"] = new Func<IMyTerminalBlock,int, float>(GetMaxWeaponRange),
                ["GetTurretTargetTypes"] = new Func<IMyTerminalBlock, ICollection<string>, int, bool>(GetTurretTargetTypes),
                ["SetTurretTargetTypes"] = new Action<IMyTerminalBlock, ICollection<string>, int>(SetTurretTargetTypes),
                ["SetBlockTrackingRange"] = new Action<IMyTerminalBlock, float>(SetBlockTrackingRange),
                ["IsTargetAligned"] = new Func<IMyTerminalBlock, IMyEntity, int, bool>(IsTargetAligned),
                ["IsTargetAlignedExtended"] = new Func<IMyTerminalBlock, IMyEntity, int, MyTuple<bool, Vector3D?>>(IsTargetAlignedExtended),
                ["CanShootTarget"] = new Func<IMyTerminalBlock, IMyEntity, int, bool>(CanShootTarget),
                ["GetPredictedTargetPosition"] = new Func<IMyTerminalBlock, IMyEntity, int, Vector3D?>(GetPredictedTargetPosition),
                ["GetHeatLevel"] = new Func<IMyTerminalBlock, float>(GetHeatLevel),
                ["GetCurrentPower"] = new Func<IMyTerminalBlock, float>(GetCurrentPower),
                ["GetMaxPower"] = new Func<MyDefinitionId, float>(GetMaxPower),
                ["DisableRequiredPower"] = new Action<IMyTerminalBlock>(DisableRequiredPower),
                ["HasGridAi"] = new Func<IMyEntity, bool>(HasGridAi),
                ["HasCoreWeapon"] = new Func<IMyTerminalBlock, bool>(HasCoreWeapon),
                ["GetOptimalDps"] = new Func<IMyEntity, float>(GetOptimalDps),
                ["GetActiveAmmo"] = new Func<IMyTerminalBlock, int, string>(GetActiveAmmo),
                ["SetActiveAmmo"] = new Action<IMyTerminalBlock, int, string>(SetActiveAmmo),
                ["RegisterProjectileAdded"] = new Action<Action<Vector3, float>>(RegisterProjectileAddedCallback),
                ["UnRegisterProjectileAdded"] = new Action<Action<Vector3, float>>(UnRegisterProjectileAddedCallback),
                ["UnMonitorProjectile"] = new Action<IMyTerminalBlock, int, Action<long, int, ulong, long, Vector3D, bool>>(UnMonitorProjectileCallback),
                ["MonitorProjectile"] = new Action<IMyTerminalBlock, int, Action<long, int, ulong, long, Vector3D, bool>>(MonitorProjectileCallback),
                ["GetProjectileState"] = new Func<ulong, MyTuple<Vector3D, Vector3D, float, float, long, string>>(GetProjectileState),
                ["GetConstructEffectiveDps"] = new Func<IMyEntity, float>(GetConstructEffectiveDps),
                ["GetPlayerController"] = new Func<IMyTerminalBlock, long>(GetPlayerController),
                ["GetWeaponAzimuthMatrix"] = new Func<IMyTerminalBlock, int, Matrix>(GetWeaponAzimuthMatrix),
                ["GetWeaponElevationMatrix"] = new Func<IMyTerminalBlock, int, Matrix>(GetWeaponElevationMatrix),
                ["IsTargetValid"] = new Func<IMyTerminalBlock, IMyEntity, bool, bool, bool>(IsTargetValid),
                ["GetWeaponScope"] = new Func<IMyTerminalBlock, int, MyTuple<Vector3D, Vector3D>>(GetWeaponScope),
                ["IsInRange"] = new Func<IMyEntity, MyTuple<bool, bool>>(IsInRange),
            };
        }

        internal void PbInit()
        {
            PbApiMethods = new Dictionary<string, Delegate>
            {
                ["GetCoreWeapons"] = new Action<ICollection<MyDefinitionId>>(GetCoreWeapons),
                ["GetCoreStaticLaunchers"] = new Action<ICollection<MyDefinitionId>>(GetCoreStaticLaunchers),
                ["GetCoreTurrets"] = new Action<ICollection<MyDefinitionId>>(GetCoreTurrets),
                ["GetBlockWeaponMap"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, IDictionary<string, int>, bool>(PbGetBlockWeaponMap),
                ["GetProjectilesLockedOn"] = new Func<long, MyTuple<bool, int, int>>(PbGetProjectilesLockedOn),
                ["GetSortedThreats"] = new Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, IDictionary<Sandbox.ModAPI.Ingame.MyDetectedEntityInfo, float>>(PbGetSortedThreats),
                ["GetObstructions"] = new Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, ICollection<Sandbox.ModAPI.Ingame.MyDetectedEntityInfo>>(PbGetObstructions),
                ["GetAiFocus"] = new Func<long, int, Sandbox.ModAPI.Ingame.MyDetectedEntityInfo>(PbGetAiFocus),
                ["SetAiFocus"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, long, int, bool>(PbSetAiFocus),
                ["GetWeaponTarget"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, Sandbox.ModAPI.Ingame.MyDetectedEntityInfo>(PbGetWeaponTarget),
                ["SetWeaponTarget"] = new Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, long, int>(PbSetWeaponTarget),
                ["FireWeaponOnce"] = new Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, bool, int>(PbFireWeaponOnce),
                ["ToggleWeaponFire"] = new Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, bool, bool, int>(PbToggleWeaponFire),
                ["IsWeaponReadyToFire"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, bool, bool, bool>(PbIsWeaponReadyToFire),
                ["GetMaxWeaponRange"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, float>(PbGetMaxWeaponRange),
                ["GetTurretTargetTypes"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, ICollection<string>, int, bool>(PbGetTurretTargetTypes),
                ["SetTurretTargetTypes"] = new Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, ICollection<string>, int>(PbSetTurretTargetTypes),
                ["SetBlockTrackingRange"] = new Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(PbSetBlockTrackingRange),
                ["IsTargetAligned"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, long, int, bool>(PbIsTargetAligned),
                ["IsTargetAlignedExtended"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, long, int, MyTuple<bool, Vector3D?>>(PbIsTargetAlignedExtended),
                ["CanShootTarget"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, long, int, bool>(PbCanShootTarget),
                ["GetPredictedTargetPosition"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, long, int, Vector3D?>(PbGetPredictedTargetPosition),
                ["GetHeatLevel"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(PbGetHeatLevel),
                ["GetCurrentPower"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float>(PbGetCurrentPower),
                ["GetMaxPower"] = new Func<MyDefinitionId, float>(GetMaxPower),
                ["HasGridAi"] = new Func<long, bool>(PbHasGridAi),
                ["HasCoreWeapon"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, bool>(PbHasCoreWeapon),
                ["GetOptimalDps"] = new Func<long, float>(PbGetOptimalDps),
                ["GetActiveAmmo"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, string>(PbGetActiveAmmo),
                ["SetActiveAmmo"] = new Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, string>(PbSetActiveAmmo),
                ["RegisterProjectileAdded"] = new Action<Action<Vector3, float>>(RegisterProjectileAddedCallback),
                ["UnRegisterProjectileAdded"] = new Action<Action<Vector3, float>>(UnRegisterProjectileAddedCallback),
                ["UnMonitorProjectile"] = new Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, Action<long, int, ulong, long, Vector3D, bool>>(PbUnMonitorProjectileCallback),
                ["MonitorProjectile"] = new Action<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, Action<long, int, ulong, long, Vector3D, bool>>(PbMonitorProjectileCallback),
                ["GetProjectileState"] = new Func<ulong, MyTuple<Vector3D, Vector3D, float, float, long, string>>(GetProjectileState),
                ["GetConstructEffectiveDps"] = new Func<long, float>(PbGetConstructEffectiveDps),
                ["GetPlayerController"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, long>(PbGetPlayerController),
                ["GetWeaponAzimuthMatrix"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, Matrix>(PbGetWeaponAzimuthMatrix),
                ["GetWeaponElevationMatrix"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, Matrix>(PbGetWeaponElevationMatrix),
                ["IsTargetValid"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, long, bool, bool, bool>(PbIsTargetValid),
                ["GetWeaponScope"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, MyTuple<Vector3D, Vector3D>>(PbGetWeaponScope),
                ["IsInRange"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, MyTuple<bool, bool>>(PbIsInRange),
            };
            var pb = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Delegate>, IMyTerminalBlock>("WcPbAPI");
            pb.Getter = (b) => PbApiMethods;
            MyAPIGateway.TerminalControls.AddControl<Sandbox.ModAPI.Ingame.IMyProgrammableBlock>(pb);
            _session.PbApiInited = true;
        }


        private void GetObstructions(IMyEntity shooter, ICollection<IMyEntity> collection)
        {
            var grid = shooter?.GetTopMostParent() as MyCubeGrid;
            GridAi gridAi;
            if (grid != null && collection != null && _session.GridTargetingAIs.TryGetValue(grid, out gridAi))
            {
                for (int i = 0; i < gridAi.Obstructions.Count; i++)
                    collection.Add(gridAi.Obstructions[i]);
            }
        }

        private readonly ICollection<IMyEntity> _tmpPbGetObstructions = new List<IMyEntity>();
        private void PbGetObstructions(Sandbox.ModAPI.Ingame.IMyTerminalBlock shooter, ICollection<MyDetectedEntityInfo> collection)
        {
            if (shooter != null && collection != null)
            {
                collection.Clear();
                GetObstructions((IMyEntity)shooter, _tmpPbGetObstructions);
                foreach (var i in _tmpPbGetObstructions)
                    collection.Add(GetDetailedEntityInfo(new MyTuple<bool, bool, bool, IMyEntity>(true, false, false, i), (MyEntity)shooter));
             
                _tmpPbGetObstructions.Clear();
            }
        }

        private float PbGetConstructEffectiveDps(long arg)
        {
            return GetConstructEffectiveDps(MyEntities.GetEntityById(arg));
        }

        private void PbSetActiveAmmo(object arg1, int arg2, string arg3)
        {
            SetActiveAmmo(arg1 as IMyTerminalBlock, arg2, arg3);
        }

        private string PbGetActiveAmmo(object arg1, int arg2)
        {
            return GetActiveAmmo(arg1 as IMyTerminalBlock, arg2);
        }

        private float PbGetOptimalDps(long arg)
        {
            return GetOptimalDps(MyEntities.GetEntityById(arg));
        }

        private bool PbHasCoreWeapon(object arg)
        {
            return HasCoreWeapon(arg as IMyTerminalBlock);
        }

        private bool PbHasGridAi(long arg)
        {
            return HasGridAi(MyEntities.GetEntityById(arg));
        }

        private float PbGetCurrentPower(object arg)
        {
            return GetCurrentPower(arg as IMyTerminalBlock);
        }

        private float PbGetHeatLevel(object arg)
        {
            return GetHeatLevel(arg as IMyTerminalBlock);
        }

        private Vector3D? PbGetPredictedTargetPosition(object arg1, long arg2, int arg3)
        {
            var block = arg1 as IMyTerminalBlock;
            var target = MyEntities.GetEntityById(arg2);
            GridAi ai;
            if (block != null && target != null && _session.GridToMasterAi.TryGetValue((MyCubeGrid)block.CubeGrid, out ai) && ai.NoTargetLos.ContainsKey(target))
                return null;

            return GetPredictedTargetPosition(block, target, arg3);
        }

        private bool PbCanShootTarget(object arg1, long arg2, int arg3)
        {
            var block = arg1 as IMyTerminalBlock;
            var target = MyEntities.GetEntityById(arg2);
            GridAi ai;
            if (block != null && target != null && _session.GridToMasterAi.TryGetValue((MyCubeGrid)block.CubeGrid, out ai) && ai.NoTargetLos.ContainsKey(target))
                return false;

            return CanShootTarget(block, target, arg3);
        }

        private bool PbIsTargetAligned(object arg1, long arg2, int arg3)
        {
            var block = arg1 as IMyTerminalBlock;
            var target = MyEntities.GetEntityById(arg2);
            GridAi ai;
            if (block != null && target != null && _session.GridToMasterAi.TryGetValue((MyCubeGrid)block.CubeGrid, out ai) && ai.NoTargetLos.ContainsKey(target))
                return false;

            return IsTargetAligned(block, target, arg3);
        }

        private MyTuple<bool, Vector3D?> PbIsTargetAlignedExtended(object arg1, long arg2, int arg3)
        {
            var block = arg1 as IMyTerminalBlock;
            var target = MyEntities.GetEntityById(arg2);
            GridAi ai;
            if (block != null && target != null && _session.GridToMasterAi.TryGetValue((MyCubeGrid)block.CubeGrid, out ai) && ai.NoTargetLos.ContainsKey(target))
                return new MyTuple<bool, Vector3D?>();

            return IsTargetAlignedExtended(block, target, arg3);
        }
        
        private void PbSetBlockTrackingRange(object arg1, float arg2)
        {
            SetBlockTrackingRange(arg1 as IMyTerminalBlock, arg2);
        }

        private void PbSetTurretTargetTypes(object arg1, object arg2, int arg3)
        {
            SetTurretTargetTypes(arg1 as IMyTerminalBlock, (ICollection<string>) arg2, arg3);
        }

        private bool PbGetTurretTargetTypes(object arg1, object arg2, int arg3)
        {
            return GetTurretTargetTypes(arg1 as IMyTerminalBlock, (ICollection<string>) arg2, arg3);
        }

        private float PbGetMaxWeaponRange(object arg1, int arg2)
        {
            return GetMaxWeaponRange(arg1 as IMyTerminalBlock, arg2);
        }

        private bool PbIsWeaponReadyToFire(object arg1, int arg2, bool arg3, bool arg4)
        {
            return IsWeaponReadyToFire(arg1 as IMyTerminalBlock, arg2, arg3, arg4);
        }

        private void PbToggleWeaponFire(object arg1, bool arg2, bool arg3, int arg4)
        {
            ToggleWeaponFire(arg1 as IMyTerminalBlock, arg2, arg3, arg4);
        }

        private void PbFireWeaponOnce(object arg1, bool arg2, int arg3)
        {
            FireWeaponOnce(arg1 as IMyTerminalBlock, arg2, arg3);
        }

        private void PbSetWeaponTarget(object arg1, long arg2, int arg3)
        {
            SetWeaponTarget(arg1 as IMyTerminalBlock, MyEntities.GetEntityById(arg2), arg3);
        }

        private Sandbox.ModAPI.Ingame.MyDetectedEntityInfo PbGetWeaponTarget(object arg1, int arg2)
        {
            var block = arg1 as IMyTerminalBlock;
            var target = GetWeaponTarget(block, arg2);

            GridAi ai;
            if (block != null && target.Item4 != null && _session.GridToMasterAi.TryGetValue((MyCubeGrid)block.CubeGrid, out ai) && ai.NoTargetLos.ContainsKey((MyEntity) target.Item4))
                return new MyDetectedEntityInfo();

            var result = GetDetailedEntityInfo(target, (MyEntity)arg1);

            return result;
        }

        private bool PbSetAiFocus(object arg1, long arg2, int arg3)
        {
            return SetAiFocus(arg1 as IMyEntity, MyEntities.GetEntityById(arg2), arg3);
        }

        private Sandbox.ModAPI.Ingame.MyDetectedEntityInfo PbGetAiFocus(long arg1, int arg2)
        {
            var shooter = MyEntities.GetEntityById(arg1);
            return GetEntityInfo(GetAiFocus(shooter, arg2), shooter);
        }

        private MyDetectedEntityInfo GetDetailedEntityInfo(MyTuple<bool, bool, bool, IMyEntity> target, MyEntity shooter)
        {
            var e = target.Item4;
            var shooterGrid = shooter?.GetTopMostParent() as MyCubeGrid;
            var topTarget = e?.GetTopMostParent() as MyEntity;
            var block = e as IMyTerminalBlock;
            var voxel = e as MyVoxelBase;

            var player = e as IMyCharacter;
            long entityId = 0;
            var relation = MyRelationsBetweenPlayerAndBlock.NoOwnership;
            var type = MyDetectedEntityType.Unknown;
            var name = string.Empty;

            GridAi ai;
            GridAi.TargetInfo info = null;

            if (shooterGrid != null && topTarget != null && _session.GridToMasterAi.TryGetValue(shooterGrid, out ai) && ai.Targets.TryGetValue(topTarget, out info)) {
                relation = info.EntInfo.Relationship;
                type = info.EntInfo.Type;
                var maxDist = ai.MaxTargetingRange + shooterGrid.PositionComp.WorldAABB.Extents.Max();
                if (Vector3D.DistanceSquared(e.PositionComp.WorldMatrixRef.Translation, shooterGrid.PositionComp.WorldMatrixRef.Translation) > (maxDist * maxDist))
                {
                    return new MyDetectedEntityInfo();
                }
            }
            else if (voxel != null)
            {
                type = voxel.RootVoxel is MyPlanet ? MyDetectedEntityType.Planet : MyDetectedEntityType.Asteroid;
                name = voxel.GetFriendlyName();
            }


            if (!target.Item1 || e == null || topTarget?.Physics == null) {
                var projectile = target.Item2;
                var fake = target.Item3;
                if (fake) {
                    name = "ManualTargeting";
                    type = MyDetectedEntityType.None;
                    entityId = -2;
                }
                else if (projectile) {
                    name = "Projectile";
                    type = MyDetectedEntityType.Missile;
                    entityId = -1;
                }
                return new MyDetectedEntityInfo(entityId, name, type, info?.TargetPos, MatrixD.Zero, info != null ? (Vector3)info.Velocity : Vector3.Zero, relation, BoundingBoxD.CreateInvalid(), _session.Tick);
            }

            entityId = e.EntityId;
            var grid = topTarget as MyCubeGrid;
            if (grid != null) name = block != null ? block.CustomName : grid.DisplayName;
            else if (player != null) name = player.GetFriendlyName();
            else name = e.GetFriendlyName();

            return new MyDetectedEntityInfo(entityId, name, type, e.PositionComp.WorldAABB.Center, e.PositionComp.WorldMatrixRef, topTarget.Physics.LinearVelocity, relation, e.PositionComp.WorldAABB, _session.Tick);
        }

        private MyDetectedEntityInfo GetEntityInfo(IMyEntity target, MyEntity shooter)
        {
            var e = target;
            if (e?.Physics == null)
                return new MyDetectedEntityInfo();

            var shooterGrid = shooter?.GetTopMostParent() as MyCubeGrid;

            GridAi ai;
            if (shooterGrid != null && _session.GridToMasterAi.TryGetValue(shooterGrid, out ai))
            {
                var maxDist = ai.MaxTargetingRange + shooterGrid.PositionComp.WorldAABB.Extents.Max();
                if (Vector3D.DistanceSquared(target.PositionComp.WorldMatrixRef.Translation, shooterGrid.PositionComp.WorldMatrixRef.Translation) > (maxDist * maxDist))
                {
                    return new MyDetectedEntityInfo();
                }
            }

            var grid = e.GetTopMostParent() as MyCubeGrid;
            var block = e as IMyTerminalBlock;
            var player = e as IMyCharacter;
            var voxel = e as MyVoxelBase;

            string name;
            MyDetectedEntityType type;
            var relation = MyRelationsBetweenPlayerAndBlock.Enemies;

            if (grid != null) {
                name = block != null ? block.CustomName : grid.DisplayName;
                type = grid.GridSizeEnum == MyCubeSize.Large ? MyDetectedEntityType.LargeGrid : MyDetectedEntityType.SmallGrid;
            }
            else if (player != null) {
                type = MyDetectedEntityType.CharacterOther;
                name = player.GetFriendlyName();

            }
            else if (voxel != null)
            {
                type = voxel.RootVoxel is MyPlanet ? MyDetectedEntityType.Planet : MyDetectedEntityType.Asteroid;
                name = voxel.GetFriendlyName();
            }
            else {
                type = MyDetectedEntityType.Unknown;
                name = e.GetFriendlyName();
            }
            return new MyDetectedEntityInfo(e.EntityId, name, type, e.PositionComp.WorldAABB.Center, e.PositionComp.WorldMatrixRef, e.Physics.LinearVelocity, relation, e.PositionComp.WorldAABB, _session.Tick);
        }

        private readonly List<MyTuple<IMyEntity, float>> _tmpTargetList = new List<MyTuple<IMyEntity, float>>();
        private void PbGetSortedThreats(object arg1, object arg2)
        {
            var shooter = arg1 as IMyTerminalBlock;
            GetSortedThreats(shooter, _tmpTargetList);
            
            var dict = arg2 as IDictionary<Sandbox.ModAPI.Ingame.MyDetectedEntityInfo, float>;

            GridAi gridAi;
            if (shooter != null && dict != null && _session.GridTargetingAIs.TryGetValue((MyCubeGrid)shooter.CubeGrid, out gridAi)) {

                foreach (var i in _tmpTargetList) {

                    var ent = i.Item1 as MyEntity;
                    if (ent != null && !gridAi.NoTargetLos.ContainsKey(ent)) 
                        dict[GetDetailedEntityInfo(new MyTuple<bool, bool, bool, IMyEntity>(true, false, false, i.Item1), (MyEntity)shooter)] = i.Item2;
                }
            }
            _tmpTargetList.Clear();
        }

        private MyTuple<bool, int, int> PbGetProjectilesLockedOn(long arg)
        {
            return GetProjectilesLockedOn(MyEntities.GetEntityById(arg));
        }

        private bool PbGetBlockWeaponMap(object arg1, object arg2)
        {
            return GetBlockWeaponMap(arg1 as IMyTerminalBlock, (IDictionary<string, int>)arg2);
        }

        private long PbGetPlayerController(object arg1)
        {
            return GetPlayerController(arg1 as IMyTerminalBlock);
        }

        private Matrix PbGetWeaponAzimuthMatrix(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, int arg2)
        {
            return GetWeaponAzimuthMatrix(arg1 as IMyTerminalBlock, arg2);
        }

        private Matrix PbGetWeaponElevationMatrix(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, int arg2)
        {
            return GetWeaponElevationMatrix(arg1 as IMyTerminalBlock, arg2);
        }

        private bool PbIsTargetValid(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, long arg2, bool arg3, bool arg4)
        {

            var block = arg1 as IMyTerminalBlock;
            var target = MyEntities.GetEntityById(arg2);
            GridAi ai;
            if (block != null && target != null && _session.GridToMasterAi.TryGetValue((MyCubeGrid)block.CubeGrid, out ai) && ai.NoTargetLos.ContainsKey(target))
                return false;

            return IsTargetValid(block, target, arg3, arg4);
        }

        private MyTuple<Vector3D, Vector3D> PbGetWeaponScope(Sandbox.ModAPI.Ingame.IMyTerminalBlock arg1, int arg2)
        {
            return GetWeaponScope(arg1 as IMyTerminalBlock, arg2);
        }

        // Block EntityId, WeaponId, ProjectileId, LastHitId, LastPos, Start 
        internal static void PbMonitorProjectileCallback(Sandbox.ModAPI.Ingame.IMyTerminalBlock weaponBlock, int weaponId, Action<long, int, ulong, long, Vector3D, bool> callback)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.Weapons.Length > weaponId)
                comp.Monitors[weaponId].Add(callback);
        }

        // Block EntityId, WeaponId, ProjectileId, LastHitId, LastPos, Start 
        internal static void PbUnMonitorProjectileCallback(Sandbox.ModAPI.Ingame.IMyTerminalBlock weaponBlock, int weaponId, Action<long, int, ulong, long, Vector3D, bool> callback)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.Weapons.Length > weaponId)
                comp.Monitors[weaponId].Remove(callback);
        }

        // terminalBlock, Threat, Other, Something 
        private MyTuple<bool, bool> PbIsInRange(object arg1)
        {
            var tBlock = arg1 as IMyTerminalBlock;
            
            return tBlock != null ? IsInRange(MyEntities.GetEntityById(tBlock.EntityId)) : new MyTuple<bool, bool>();
        }
        
        // Non-PB Methods
        private void GetAllWeaponDefinitions(IList<byte[]> collection)
        {
            foreach (var wepDef in _session.WeaponDefinitions)
                collection?.Add(MyAPIGateway.Utilities.SerializeToBinary(wepDef));
        }

        private void GetCoreWeapons(ICollection<MyDefinitionId> collection)
        {
            foreach (var def in _session.WeaponCoreBlockDefs.Values)
                collection?.Add(def);
        }

        private void GetCoreStaticLaunchers(ICollection<MyDefinitionId> collection)
        {
            foreach (var def in _session.WeaponCoreFixedBlockDefs)
                collection?.Add(def);
        }

        private void GetCoreTurrets(ICollection<MyDefinitionId> collection)
        {
            foreach (var def in _session.WeaponCoreTurretBlockDefs)
                collection?.Add(def);
        }

        internal long GetPlayerController(IMyTerminalBlock weaponBlock)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
                return comp.Data.Repo.Base.State.PlayerId;

            return -1;
        }

        internal Matrix GetWeaponAzimuthMatrix(IMyTerminalBlock weaponBlock, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
            {
                var weapon = comp.Platform.Weapons[weaponId];
                var matrix = weapon.AzimuthPart?.Entity?.PositionComp.LocalMatrixRef ?? Matrix.Zero;
                return matrix;
            }

            return Matrix.Zero;
        }

        internal Matrix GetWeaponElevationMatrix(IMyTerminalBlock weaponBlock, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
            {
                var weapon = comp.Platform.Weapons[weaponId];
                var matrix = weapon.ElevationPart?.Entity?.PositionComp.LocalMatrixRef ?? Matrix.Zero;
                return matrix;
            }

            return Matrix.Zero;
        }

        private bool GetBlockWeaponMap(IMyTerminalBlock weaponBlock, IDictionary<string, int> collection)
        {
            WeaponStructure weaponStructure;
            if (weaponBlock != null && _session.WeaponPlatforms.TryGetValue(weaponBlock.SlimBlock.BlockDefinition.Id, out weaponStructure))
            {
                foreach (var weaponSystem in weaponStructure.WeaponSystems.Values)
                {
                    var system = weaponSystem;
                    if (!collection.ContainsKey(system.WeaponName))
                        collection.Add(system.WeaponName, system.WeaponId);
                }
                return true;
            }
            return false;
        }

        private MyTuple<bool, int, int> GetProjectilesLockedOn(IMyEntity victim)
        {
            var grid = victim?.GetTopMostParent() as MyCubeGrid;
            GridAi gridAi;
            MyTuple<bool, int, int> tuple;
            if (grid != null && _session.GridTargetingAIs.TryGetValue(grid, out gridAi))
            {
                var count = gridAi.LiveProjectile.Count;
                tuple = count > 0 ? new MyTuple<bool, int, int>(true, count, (int) (_session.Tick - gridAi.LiveProjectileTick)) : new MyTuple<bool, int, int>(false, 0, -1);
            }
            else tuple = new MyTuple<bool, int, int>(false, 0, -1);
            return tuple;
        }

        private void GetSortedThreats(IMyEntity shooter, ICollection<MyTuple<IMyEntity, float>> collection)
        {
            var grid = shooter?.GetTopMostParent() as MyCubeGrid;
            GridAi gridAi;
            if (grid != null && collection != null && _session.GridTargetingAIs.TryGetValue(grid, out gridAi))
            {
                for (int i = 0; i < gridAi.SortedTargets.Count; i++)
                {
                    var targetInfo = gridAi.SortedTargets[i];
                    collection.Add(new MyTuple<IMyEntity, float>(targetInfo.Target, targetInfo.OffenseRating));
                }
            }
        }

        private IMyEntity GetAiFocus(IMyEntity shooter, int priority = 0)
        {
            var shootingGrid = shooter?.GetTopMostParent() as MyCubeGrid;

            if (shootingGrid != null)
            {
                GridAi ai;
                if (_session.GridToMasterAi.TryGetValue(shootingGrid, out ai))
                    return MyEntities.GetEntityById(ai.Construct.Data.Repo.FocusData.Target[priority]);
            }
            return null;
        }

        private bool SetAiFocus(IMyEntity shooter, IMyEntity target, int priority = 0)
        {
            var shootingGrid = shooter?.GetTopMostParent() as MyCubeGrid;
            if (shootingGrid != null && target != null)
            {
                GridAi ai;
                if (_session.GridToMasterAi.TryGetValue(shootingGrid, out ai))
                {
                    if (!ai.Session.IsServer)
                        return false;

                    ai.Construct.Focus.ReassignTarget((MyEntity)target, priority, ai);
                    return true;
                }
            }
            return false;
        }

        private static MyTuple<bool, bool, bool, IMyEntity> GetWeaponTarget(IMyTerminalBlock weaponBlock, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
            {
                var weapon = comp.Platform.Weapons[weaponId];
                if (weapon.Target.IsFakeTarget)
                    return new MyTuple<bool, bool, bool, IMyEntity>(true, false, true, null);
                if (weapon.Target.IsProjectile)
                    return new MyTuple<bool, bool, bool, IMyEntity>(true, true, false, null);
                return new MyTuple<bool, bool, bool, IMyEntity>(weapon.Target.Entity != null, false, false, weapon.Target.Entity);
            }

            return new MyTuple<bool, bool, bool, IMyEntity>(false, false, false, null);
        }


        private static void SetWeaponTarget(IMyTerminalBlock weaponBlock, IMyEntity target, int weaponId = 0)
        {
            var validRequest = weaponBlock != null && target != null;
            WeaponComponent comp;
            if (validRequest && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
                GridAi.AcquireTarget(comp.Platform.Weapons[weaponId], false, (MyEntity)target);
        }

        private static void FireWeaponOnce(IMyTerminalBlock weaponBlock, bool allWeapons = true, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
            {
                var foundWeapon = false;
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    if (!allWeapons && i != weaponId) continue;
                    
                    foundWeapon = true;
                    comp.Platform.Weapons[i].State.WeaponMode(comp, ShootOnce);
                }

                if (foundWeapon)  {
                    var weaponKey = allWeapons ? -1 : weaponId;
                    comp.ShootOnceCheck(weaponKey);
                }
            }
        }

        private static void ToggleWeaponFire(IMyTerminalBlock weaponBlock, bool on, bool allWeapons = true, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
            {
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    if (!allWeapons && i != weaponId || !comp.Session.IsServer) continue;

                    var w = comp.Platform.Weapons[i];

                    if (!on && w.State.Action == ShootOn)
                    {
                        w.State.WeaponMode(comp, ShootOff);
                        w.StopShooting();
                    }
                    else if (on && w.State.Action != ShootOff)
                        w.State.WeaponMode(comp, ShootOn);
                    else if (on)
                        w.State.WeaponMode(comp, ShootOn);
                }
            }
        }

        private static bool IsWeaponReadyToFire(IMyTerminalBlock weaponBlock, int weaponId = 0, bool anyWeaponReady = true, bool shotReady = false)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId && comp.IsWorking)
            {
                for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                {
                    if (!anyWeaponReady && i != weaponId) continue;
                    var w = comp.Platform.Weapons[i];
                    if (w.ShotReady) return true;
                }
            }

            return false;
        }

        private static float GetMaxWeaponRange(IMyTerminalBlock weaponBlock, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
                return (float)comp.Platform.Weapons[weaponId].MaxTargetDistance;

            return 0f;
        }

        private static bool GetTurretTargetTypes(IMyTerminalBlock weaponBlock, ICollection<string> collection, int weaponId = 0)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
            {
                var weapon = comp.Platform.Weapons[weaponId];
                var threats = weapon.System.Values.Targeting.Threats;
                for (int i = 0; i < threats.Length; i++) collection.Add(threats[i].ToString());
                return true;
            }
            return false;
        }

        private static void SetTurretTargetTypes(IMyTerminalBlock weaponBlock, ICollection<string> collection, int weaponId = 0)
        {

        }

        private static void SetBlockTrackingRange(IMyTerminalBlock weaponBlock, float range)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
            {
                double maxTargetDistance = 0;
                foreach (var w in comp.Platform.Weapons)
                    if (w.MaxTargetDistance > maxTargetDistance) 
                        maxTargetDistance = w.MaxTargetDistance;
                
                comp.Data.Repo.Base.Set.Range = (float) (range > maxTargetDistance ? maxTargetDistance : range);
            }
        }

        private static bool IsTargetAligned(IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId)
        {
            var validRequest = weaponBlock != null && targetEnt != null;
            WeaponComponent comp;
            if (validRequest && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
            {
                var w = comp.Platform.Weapons[weaponId];

                w.NewTarget.Entity = (MyEntity) targetEnt;

                var dist = Vector3D.DistanceSquared(comp.MyCube.CubeGrid.PositionComp.WorldMatrixRef.Translation, targetEnt.PositionComp.WorldMatrixRef.Translation);
                if (dist > w.MaxTargetDistanceSqr)
                {
                    return false;
                }


                Vector3D targetPos;
                return Weapon.TargetAligned(w, w.NewTarget, out targetPos);
            }
            return false;
        }

        private static MyTuple<bool, Vector3D?> IsTargetAlignedExtended(IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId)
        {
            var validRequest = weaponBlock != null && targetEnt != null;
            WeaponComponent comp;
            if (validRequest && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
            {
                var w = comp.Platform.Weapons[weaponId];

                w.NewTarget.Entity = (MyEntity)targetEnt;

                Vector3D targetPos;
                var targetAligned = Weapon.TargetAligned(w, w.NewTarget, out targetPos);
                
                return new MyTuple<bool, Vector3D?>(targetAligned, targetAligned ? targetPos : (Vector3D?)null);
            }
            return new MyTuple<bool, Vector3D?>(false, null);
        }

        private static bool CanShootTarget(IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId)
        {
            var validRequest = weaponBlock != null && targetEnt != null;
            WeaponComponent comp;
            if (validRequest && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
            {
                var w = comp.Platform.Weapons[weaponId];
                var topMost = targetEnt.GetTopMostParent();
                var targetVel = topMost.Physics?.LinearVelocity ?? Vector3.Zero;
                var targetAccel = topMost.Physics?.AngularAcceleration ?? Vector3.Zero;
                Vector3D predictedPos;
                return Weapon.CanShootTargetObb(w, (MyEntity)targetEnt, targetVel, targetAccel, out predictedPos);
            }
            return false;
        }

        private static Vector3D? GetPredictedTargetPosition(IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId)
        {
            var validRequest = weaponBlock != null && targetEnt != null;
            WeaponComponent comp;
            if (validRequest && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
            {
                var w = comp.Platform.Weapons[weaponId];
                w.NewTarget.Entity = (MyEntity)targetEnt;
                var dist = Vector3D.DistanceSquared(comp.MyCube.CubeGrid.PositionComp.WorldMatrixRef.Translation, targetEnt.PositionComp.WorldMatrixRef.Translation);
                if (dist > w.MaxTargetDistanceSqr)
                {
                    return null;
                }

                Vector3D targetPos;
                Weapon.TargetAligned(w, w.NewTarget, out targetPos);
                return targetPos;
            }
            return null;
        }

        private static float GetHeatLevel(IMyTerminalBlock weaponBlock)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.MaxHeat > 0)
            {
                return comp.CurrentHeat;
            }
            return 0f;
        }

        private static float GetCurrentPower(IMyTerminalBlock weaponBlock)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
                return comp.SinkPower;

            return 0f;
        }

        private float GetMaxPower(MyDefinitionId weaponDef)
        {
            return 0f; //Need to implement
        }

        private static void DisableRequiredPower(IMyTerminalBlock weaponBlock)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
                comp.UnlimitedPower = true;
        }

        private bool HasGridAi(IMyEntity entity)
        {
            var grid = entity?.GetTopMostParent() as MyCubeGrid;

            return grid != null && _session.GridTargetingAIs.ContainsKey(grid);
        }

        private static bool HasCoreWeapon(IMyTerminalBlock weaponBlock)
        {
            return weaponBlock != null && weaponBlock.Components.Has<WeaponComponent>();
        }

        private float GetOptimalDps(IMyEntity entity)
        {
            var terminalBlock = entity as IMyTerminalBlock;
            if (terminalBlock != null)
            {
                WeaponComponent comp;
                if (terminalBlock.Components.TryGet(out comp) && comp.Platform.State == Ready)
                    return comp.PeakDps;
            }
            else
            {
                var grid = entity?.GetTopMostParent() as MyCubeGrid;
                GridAi gridAi;
                if (grid != null && _session.GridTargetingAIs.TryGetValue(grid, out gridAi))
                    return gridAi.OptimalDps;
            }
            return 0f;
        }

        private static string GetActiveAmmo(IMyTerminalBlock weaponBlock, int weaponId)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
                return comp.Platform.Weapons[weaponId].ActiveAmmoDef.AmmoDef.AmmoRound;

            return null;
        }

        private static void SetActiveAmmo(IMyTerminalBlock weaponBlock, int weaponId, string ammoTypeStr)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Session.IsServer && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
            {
                var w = comp.Platform.Weapons[weaponId];
                for (int i = 0; i < w.System.AmmoTypes.Length; i++)
                {
                    var ammoType = w.System.AmmoTypes[i];
                    if (ammoType.AmmoName == ammoTypeStr && ammoType.AmmoDef.Const.IsTurretSelectable)
                    {
                        if (comp.Session.IsServer) {
                            w.Ammo.AmmoTypeId = i;
                            if (comp.Session.MpActive)
                                comp.Session.SendWeaponReload(w);
                        }


                        break;
                    }
                }
            }
        }

        private void RegisterProjectileAddedCallback(Action<Vector3, float> callback)
        {
            _session.ProjectileAddedCallback += callback;
        }

        private void UnRegisterProjectileAddedCallback(Action<Vector3, float> callback)
        {
            try
            {
                _session.ProjectileAddedCallback -= callback;
            }
            catch (Exception e)
            {
                Log.Line($"Cannot remove Action, Action is not registered: {e}");
            }
        }

        // Block EntityId, WeaponId, ProjectileId, LastHitId, LastPos, Start 
        internal static void MonitorProjectileCallback(IMyTerminalBlock weaponBlock, int weaponId, Action<long, int, ulong, long, Vector3D, bool> callback)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.Weapons.Length > weaponId)
                comp.Monitors[weaponId].Add(callback);
        }

        // Block EntityId, WeaponId, ProjectileId, LastHitId, LastPos, Start 
        internal static void UnMonitorProjectileCallback(IMyTerminalBlock weaponBlock, int weaponId, Action<long, int, ulong, long, Vector3D, bool> callback)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.Weapons.Length > weaponId)
                comp.Monitors[weaponId].Remove(callback);
        }

        // POs, Dir, baseDamageLeft, HealthLeft, TargetEntityId, AmmoName 
        private MyTuple<Vector3D, Vector3D, float, float, long, string> GetProjectileState(ulong projectileId)
        {
            Projectile p;
            if (_session.MonitoredProjectiles.TryGetValue(projectileId, out p))
                return new MyTuple<Vector3D, Vector3D, float, float, long, string>(p.Position, p.Info.Direction, p.Info.BaseDamagePool, p.Info.BaseHealthPool, p.Info.Target.TargetId, p.Info.AmmoDef.AmmoRound);

            return new MyTuple<Vector3D, Vector3D, float, float, long, string>();
        }

        private float GetConstructEffectiveDps(IMyEntity entity)
        {
            var grid = entity.GetTopMostParent() as MyCubeGrid;
            GridAi gridAi;
            if (grid != null && _session.GridToMasterAi.TryGetValue(grid, out gridAi))
                return gridAi.EffectiveDps;

            return 0;
        }
        
        private bool IsTargetValid(IMyTerminalBlock weaponBlock, IMyEntity targetEntity, bool onlyThreats, bool checkRelations)
        {
            var validRequest = weaponBlock != null && targetEntity != null;
            WeaponComponent comp;
            if (validRequest && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready) {
                
                var ai = comp.Ai;
                
                GridAi.TargetInfo targetInfo;
                if (ai.Targets.TryGetValue((MyEntity)targetEntity, out targetInfo)) {
                    var marked = targetInfo.Target?.MarkedForClose;
                    if (!marked.HasValue || marked.Value)
                        return false;
                    
                    if (!onlyThreats && !checkRelations)
                        return true;
                    
                    var isThreat = targetInfo.OffenseRating > 0;
                    var relation = targetInfo.EntInfo.Relationship;

                    var o = comp.Data.Repo.Base.Set.Overrides;
                    var shootNoOwners = o.Unowned && relation == MyRelationsBetweenPlayerAndBlock.NoOwnership;
                    var shootNeutrals = o.Neutrals && relation == MyRelationsBetweenPlayerAndBlock.Neutral;
                    var shootFriends = o.Friendly && relation == MyRelationsBetweenPlayerAndBlock.Friends;
                    var shootEnemies = relation == MyRelationsBetweenPlayerAndBlock.Enemies;
                    
                    if (onlyThreats && checkRelations)
                        return isThreat && (shootEnemies || shootNoOwners || shootNeutrals || shootFriends);

                    if (onlyThreats)
                        return isThreat;

                    if (shootEnemies || shootNoOwners || shootNeutrals || shootFriends)
                        return true;
                }
            }
            return false;
        }

        internal MyTuple<Vector3D, Vector3D> GetWeaponScope(IMyTerminalBlock weaponBlock, int weaponId)
        {
            WeaponComponent comp;
            if (weaponBlock != null && weaponBlock.Components.TryGet(out comp) && comp.Platform.State == Ready && comp.Platform.Weapons.Length > weaponId)
            {
                var w = comp.Platform.Weapons[weaponId];
                var info = w.GetScope.Info;
                return new MyTuple<Vector3D, Vector3D>(info.Position, info.Direction);
            }
            return new MyTuple<Vector3D, Vector3D>();
        }
        
        // block/grid entityId, Threat, Other 
        private MyTuple<bool, bool> IsInRange(IMyEntity entity)
        {
            var grid = entity?.GetTopMostParent() as MyCubeGrid;
            GridAi ai;
            if (grid != null && _session.GridTargetingAIs.TryGetValue(grid, out ai))
            {
                return new MyTuple<bool, bool>(ai.TargetingInfo.ThreatInRange, ai.TargetingInfo.OtherInRange);
            }
            return new MyTuple<bool, bool>();
        }
    }
}
