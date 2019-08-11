﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Projectiles;
using static WeaponCore.Support.SubSystemDefinition;

namespace WeaponCore.Support
{
    public partial class GridAi
    {
        internal volatile bool Ready;
        internal static volatile bool SubGridUpdate;
        internal readonly MyCubeGrid MyGrid;
        internal readonly MyConcurrentPool<Dictionary<BlockTypes, List<MyCubeBlock>>> BlockTypePool = new MyConcurrentPool<Dictionary<BlockTypes, List<MyCubeBlock>>>(50);
        internal readonly MyConcurrentPool<List<MyCubeBlock>> CubePool = new MyConcurrentPool<List<MyCubeBlock>>(50);
        internal readonly ConcurrentDictionary<MyCubeBlock, WeaponComponent> WeaponBase = new ConcurrentDictionary<MyCubeBlock, WeaponComponent>();
        internal readonly ConcurrentQueue<Projectile> DeadProjectiles = new ConcurrentQueue<Projectile>();
        internal readonly Dictionary<MyEntity, Dictionary<BlockTypes, List<MyCubeBlock>>> ValidGrids = new Dictionary<MyEntity, Dictionary<BlockTypes, List<MyCubeBlock>>>();

        internal readonly HashSet<MyResourceSourceComponent> Sources = new HashSet<MyResourceSourceComponent>();
        internal readonly HashSet<BlockTypes> BlockTypeIsSorted = new HashSet<BlockTypes>();
        internal readonly HashSet<MyCubeGrid> SubGrids = new HashSet<MyCubeGrid>();
        internal readonly HashSet<Projectile> LiveProjectile = new HashSet<Projectile>();

        internal readonly List<GridAi> ThreatsTmp = new List<GridAi>();
        internal readonly List<GridAi> Threats = new List<GridAi>();
        internal readonly List<MyEntity> EntitiesInRange = new List<MyEntity>();
        internal readonly List<MyEntity> ObstructionsTmp = new List<MyEntity>();
        internal readonly List<MyEntity> Obstructions = new List<MyEntity>();
        internal readonly List<DetectInfo> NewEntities = new List<DetectInfo>();

        internal readonly MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;
        internal List<TargetInfo> SortedTargets = new List<TargetInfo>();

        internal MyResourceDistributorComponent MyResourceDist;
        internal MyGridTargeting Targeting { get; set; }
        internal bool TargetNeutrals;
        internal bool TargetNoOwners;
        internal bool SubUpdate;
        internal Session MySession;
        internal DSUtils DsWatch = new DSUtils();
        internal uint SubTick;
        internal uint TargetsUpdatedTick;
        internal long MyOwner;
        internal int DbUpdating;
        internal bool DbReady;
        internal bool GridInit;
        internal bool UpdatePowerSources;
        internal bool updateSinks;
        internal float LastAvailablePower;
        internal float GridMaxPower;
        internal float GridCurrentPower;
        internal float GridAvailablePower;
        internal float BatteryMaxPower;
        internal float BatteryCurrentOutput;
        internal float BatteryCurrentInput;
        internal double MaxTargetingRange;
        internal double MaxTargetingRangeSqr;


        internal BoundingBoxD GroupAABB;
        internal readonly TargetCompare TargetCompare1 = new TargetCompare();

        internal GridAi(MyCubeGrid grid, Session mySession)
        {
            MyGrid = grid;
            MySession = mySession;
            Targeting = MyGrid.Components.Get<MyGridTargeting>();
        }
    }
}
