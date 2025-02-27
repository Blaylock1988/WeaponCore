﻿using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        internal volatile bool InventoryInited;
        internal volatile BlockType BaseType;

        internal readonly MyCubeBlock MyCube;
        internal readonly IMySlimBlock Slim;
        internal readonly MyStringHash SubtypeHash;
        internal readonly List<PartAnimation> AllAnimations = new List<PartAnimation>();
        internal readonly List<int> AmmoSelectionWeaponIds = new List<int>();
        internal readonly List<Action<long, int, ulong, long, Vector3D, bool>>[] Monitors;
        internal readonly Session Session;
        internal readonly MyInventory BlockInventory;
        internal readonly IMyTerminalBlock TerminalBlock;
        internal readonly IMyFunctionalBlock FunctionalBlock;
        internal readonly IMyLargeTurretBase TurretBase;
        internal readonly CompData Data;

        internal bool InControlPanel => MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel;

        internal InputStateData InputState;
        internal GridAi Ai;
        internal Weapon TrackingWeapon;
        internal MyWeaponPlatform Platform;
        internal uint LastRayCastTick;
        internal uint IsWorkingChangedTick;

        internal int WeaponsTracking;
        internal double MaxTargetDistance = double.MinValue;
        internal double MaxTargetDistanceSqr = double.MinValue;
        internal double MinTargetDistance = double.MaxValue;
        internal double MinTargetDistanceSqr = double.MaxValue;
        internal long PreviousOwner = long.MaxValue;

        internal float EffectiveDps;
        internal float PeakDps;
        internal float ShotsPerSec;
        internal float BaseDps;
        internal float AreaDps;
        internal float DetDps;
        internal float CurrentDps;
        internal float CurrentHeat;
        internal float MaxHeat;
        internal float HeatPerSecond;
        internal float HeatSinkRate;
        internal float SinkPower;
        internal float MaxRequiredPower;
        internal float IdlePower = 0.001f;
        internal float MaxIntegrity;
        internal float CurrentCharge;
        internal float CurrentInventoryVolume;
        internal bool TargetNonThreats;
        internal bool IsAsleep;
        internal bool IsFunctional;
        internal bool IsWorking;
        internal bool IsDisabled;
        internal bool HasEnergyWeapon;
        internal bool HasGuidanceToggle;
        internal bool HasDamageSlider;
        internal bool HasRofSlider;
        internal bool CanOverload;
        internal bool HasTurret;
        internal bool OverrideLeads;
        internal bool HasChargeWeapon;
        internal bool WasControlled;
        internal bool UpdatedState;
        internal bool UserControlled;
        internal bool Debug;
        internal bool UnlimitedPower;
        internal bool Registered;
        internal bool ResettingSubparts;
        internal bool HasArmor;
        internal bool UiEnabled;
        internal bool ShootSubmerged;
        internal bool HasTracking;
        internal bool HasGuidedAmmo;
        internal bool HasDelayToFire;
        internal bool UnexpectedMag;
        internal bool ManualMode;
        internal bool PainterMode;
        internal bool FakeMode;
        internal string CustomIcon;

        internal MyDefinitionId GId = MyResourceDistributorComponent.ElectricityId;

        internal Start Status;

        internal enum Start
        {
            Started,
            Starting,
            Stopped,
            ReInit,
        }

        internal enum BlockType
        {
            Turret,
            Fixed,
            Sorter
        }

        public enum ShootActions
        {
            ShootOff,
            ShootOn,
            ShootOnce,
            ShootClick,
        }


        public WeaponComponent(Session session,  MyCubeBlock myCube, MyStringHash subtype)
        {
            Session = session;
            MyCube = myCube;
            Slim = myCube.SlimBlock;
            SubtypeHash = subtype;

            MaxIntegrity = Slim.MaxIntegrity;

            var turret = MyCube as IMyLargeTurretBase;
            if (turret != null)
            {
                TurretBase = turret;
                TurretBase.EnableIdleRotation = false;
                BaseType = BlockType.Turret;
            }
            else if (MyCube is IMyConveyorSorter)
                BaseType = BlockType.Sorter;
            else
                BaseType = BlockType.Fixed;

            TerminalBlock = myCube as IMyTerminalBlock;
            FunctionalBlock = myCube as IMyFunctionalBlock;
            
            BlockInventory = (MyInventory)MyCube.GetInventoryBase();
            SinkPower = IdlePower;
            Platform = session.PlatFormPool.Get();
            Platform.Setup(this);

            Monitors = new List<Action<long, int, ulong, long, Vector3D, bool>>[Platform.Weapons.Length];
            for (int i = 0; i < Monitors.Length; i++)
                Monitors[i] = new List<Action<long, int, ulong, long, Vector3D, bool>>();

            Data = new CompData(this);

            MyCube.OnClose += Session.CloseComps;
        }        
    }
}
