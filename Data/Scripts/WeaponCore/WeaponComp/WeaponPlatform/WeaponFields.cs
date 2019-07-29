﻿using System;
using Sandbox.Game.Entities;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal const uint SuspendAmmoCount = 300;
        internal const uint UnSuspendAmmoCount = 1200;
        internal int NextMuzzle;
        internal static bool UiSet;
        internal volatile bool TargetExpired;

        private readonly Vector3 _localTranslation;

        private MyEntity _lastTarget;
        private Vector3D _lastPredictedPos;
        private double _lastTimeToIntercept;
        private int _rotationTime;
        private int _numOfBarrels;
        private int _shotsInCycle;
        private int _shots = 1;
        private int _maxHeat;
        private uint _ticksUntilShoot;
        private uint _lastPredictionTick;
        private uint _posChangedTick = 1;
        private uint _lastShotTick;
        private uint _ticksPerShot;
        private double _timePerShot;

        private bool _newCycle = false;
        //private bool _firstRun = true;

        internal IMyEntity EntityPart;
        internal WeaponSystem System;
        internal Dummy[] Dummies;
        internal Muzzle[] Muzzles;
        internal uint[] BeamSlot;
        internal WeaponComponent Comp;

        internal MyEntity NewTarget;
        internal MyEntity Target;
        internal Vector3D TargetPos;
        internal Vector3D TargetDir;
        internal MyParticleEffect[] BarrelEffects1;
        internal MyParticleEffect[] BarrelEffects2;
        internal MyParticleEffect[] HitEffects;
        internal MyCubeBlock[] ClosestBlocksByType = new MyCubeBlock[5];
        internal MySoundPair ReloadSound;
        internal MySoundPair FiringSound;
        internal MySoundPair RotateSound;
        internal readonly MyEntity3DSoundEmitter ReloadEmitter;
        internal readonly MyEntity3DSoundEmitter FiringEmitter;
        internal readonly MyEntity3DSoundEmitter RotateEmitter;
        internal readonly CachingDictionary<Muzzle, uint> BarrelAvUpdater = new CachingDictionary<Muzzle, uint>();

        internal uint SuspendAmmoTick;
        internal uint UnSuspendAmmoTick;
        internal uint ShotCounter;
        internal uint LastTargetCheck;
        internal int[] Deck = new int[0];
        internal int PrevDeckLength;
        internal int CurrentAmmo;
        internal int AmmoMagTimer = int.MaxValue;
        internal int DelayFireCount;
        internal int WeaponId;
        internal int CurrentHeat = 0;
        internal MyFixedPoint CurrentMags;
        internal double Azimuth;
        internal double Elevation;
        internal double DesiredAzimuth;
        internal double DesiredElevation;
        internal double AimingTolerance;
        internal double RotationSpeed;
        internal double ElevationSpeed;
        internal double MaxAzimuthRadians;
        internal double MinAzimuthRadians;
        internal double MaxElevationRadians;
        internal double MinElevationRadians;
        internal float RequiredPower => ((System.ShotEnergyCost * (System.Values.HardPoint.Loading.RateOfFire * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS)) * System.Values.HardPoint.Loading.BarrelsPerShot) * System.Values.HardPoint.Loading.TrajectilesPerBarrel;
        internal bool IsTurret;
        internal bool TurretMode;
        internal bool TrackTarget;
        internal bool AiReady;
        internal bool SeekTarget;
        internal bool TrackingAi;
        internal bool IsTracking;
        internal bool IsInView;
        internal bool IsAligned;
        internal bool AmmoSuspend;
        internal bool AmmoFull;
        internal bool IsShooting;
        internal bool BarrelMove;
        internal bool PlayTurretAv;
        internal bool AvCapable;
        internal bool DelayCeaseFire;
        internal bool Enabled;
        internal bool OrderedTargets;
        internal bool DegROF;
        internal bool Overheated = false;
        internal bool LoadAmmoMag
        {
            set
            {
                if (value)
                {
                    Comp.BlockInventory.RemoveItemsOfType(1, System.AmmoDefId);
                    AmmoMagTimer = System.ReloadTime;
                }
            }
        }

        internal bool AmmoMagLoaded
        {
            get
            {
                if (--AmmoMagTimer > 0) return false;
                CurrentAmmo = System.MagazineDef.Capacity;
                AmmoMagTimer = int.MaxValue;
                return true;
            }
        }

        public Weapon(IMyEntity entity, WeaponSystem system, int weaponId, WeaponComponent comp)
        {
            EntityPart = entity;
            _localTranslation = entity.LocalMatrix.Translation;
            System = system;
            Comp = comp;
            AvCapable = System.HasBarrelShootAv && !Comp.MyAi.MySession.DedicatedServer;

            if (AvCapable && system.FiringSound == WeaponSystem.FiringSoundState.WhenDone)
            {
                FiringEmitter = new MyEntity3DSoundEmitter(Comp.MyCube, true, 1f);
                FiringSound = new MySoundPair();
                FiringSound.Init(System.Values.Audio.HardPoint.FiringSound);
            }

            if (AvCapable && system.WeaponReloadSound)
            {
                ReloadEmitter = new MyEntity3DSoundEmitter(Comp.MyCube, true, 1f);
                ReloadSound = new MySoundPair();
                ReloadSound.Init(System.Values.Audio.HardPoint.ReloadSound);
            }

            if (AvCapable && system.BarrelRotationSound && system.Values.HardPoint.RotateBarrelAxis != 0)
            {
                RotateEmitter = new MyEntity3DSoundEmitter(Comp.MyCube, true, 1f);
                RotateSound = new MySoundPair();
                RotateSound.Init(System.Values.Audio.HardPoint.BarrelRotationSound);
            }

            if (AvCapable)
            {
                if (System.BarrelEffect1) BarrelEffects1 = new MyParticleEffect[System.Values.Assignments.Barrels.Length];
                if (System.BarrelEffect2) BarrelEffects2 = new MyParticleEffect[System.Values.Assignments.Barrels.Length];
                if (System.HitParticle && System.IsBeamWeapon) HitEffects = new MyParticleEffect[System.Values.Assignments.Barrels.Length];
            }

            WeaponId = weaponId;
            Enabled = true;
            OrderedTargets = System.Values.HardPoint.Targeting.Enable;
            IsTurret = System.Values.HardPoint.IsTurret;
            TurretMode = System.Values.HardPoint.TurretController;
            TrackTarget = System.Values.HardPoint.TrackTargets;
            DegROF = system.Values.HardPoint.Loading.DegradeROF;
            AimingTolerance = Math.Cos(MathHelper.ToRadians(System.Values.HardPoint.AimingTolerance));
            _ticksPerShot = (uint)(3600 / System.Values.HardPoint.Loading.RateOfFire);
            _timePerShot = (3600d / System.Values.HardPoint.Loading.RateOfFire);
            _maxHeat = System.Values.HardPoint.Loading.MaxHeat;
            _numOfBarrels = System.Barrels.Length;
            DelayCeaseFire = System.TimeToCeaseFire > 0;
            BeamSlot = new uint[_numOfBarrels];
        }
    }
}
