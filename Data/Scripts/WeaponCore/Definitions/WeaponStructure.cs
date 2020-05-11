﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using static WeaponCore.Support.PartAnimation;
using static WeaponCore.Support.WeaponDefinition;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
using static WeaponCore.Support.WeaponDefinition.HardPointDef;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.TrajectoryDef.GuidanceType;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.AreaDamageDef;
using static WeaponCore.Support.WeaponDefinition.AmmoDef.ShapeDef.Shapes;
namespace WeaponCore.Support
{
    public class WeaponSystem
    {
        private const string Arc = "Arc";

        public readonly MyStringHash MuzzlePartName;
        public readonly MyStringHash AzimuthPartName;
        public readonly MyStringHash ElevationPartName;
        public readonly WeaponDefinition Values;
        public readonly WeaponAmmoTypes[] WeaponAmmoTypes;

        public readonly Session Session;

        public readonly Dictionary<EventTriggers, PartAnimation[]> WeaponAnimationSet;
        public readonly Dictionary<EventTriggers, uint> WeaponAnimationLengths;
        public readonly HashSet<string> AnimationIdLookup;
        public readonly Dictionary<string, EmissiveState> WeaponEmissiveSet;
        public readonly Dictionary<string, Matrix[]> WeaponLinearMoveSet;
        public readonly Prediction Prediction;
        public readonly TurretType TurretMovement;
        public readonly FiringSoundState FiringSound;

        public readonly string WeaponName;
        public readonly string[] Barrels;
        public readonly string[] HeatingSubparts;

        public readonly int ReloadTime;
        public readonly int DelayToFire;
        public readonly int CeaseFireDelay;
        public readonly int MinAzimuth;
        public readonly int MaxAzimuth;
        public readonly int MinElevation;
        public readonly int MaxElevation;
        public readonly int MaxHeat;
        public readonly int WeaponIdHash;
        public readonly int WeaponId;
        public readonly int BarrelsPerShot;
        public readonly int HeatPerShot;
        public readonly int RateOfFire;
        public readonly int BarrelSpinRate;
        public readonly int ShotsPerBurst;

        public readonly bool HasBarrelRotation;
        public readonly bool BarrelEffect1;
        public readonly bool BarrelEffect2;
        public readonly bool HasBarrelShootAv;
        public readonly bool TargetSubSystems;
        public readonly bool OnlySubSystems;
        public readonly bool ClosestFirst;
        public readonly bool DegRof;
        public readonly bool TrackProjectile;
        public readonly bool TrackOther;
        public readonly bool TrackGrids;
        public readonly bool TrackCharacters;
        public readonly bool TrackMeteors;
        public readonly bool TrackNeutrals;
        public readonly bool DesignatorWeapon;
        public readonly bool DelayCeaseFire;
        public readonly bool AlwaysFireFullBurst;
        public readonly bool WeaponReloadSound;
        public readonly bool NoAmmoSound;
        public readonly bool HardPointRotationSound;
        public readonly bool BarrelRotationSound;
        public readonly bool PreFireSound;
        public readonly bool LockOnFocus;

        public readonly double MaxTargetSpeed;
        public readonly double AzStep;
        public readonly double ElStep;

        public readonly float Barrel1AvTicks;
        public readonly float Barrel2AvTicks;
        public readonly float WepCoolDown;
        public readonly float MinTargetRadius;
        public readonly float MaxTargetRadius;
        public readonly float MaxAmmoVolume;
        public readonly float FiringSoundDistSqr;
        public readonly float ReloadSoundDistSqr;
        public readonly float BarrelSoundDistSqr;
        public readonly float HardPointSoundDistSqr;
        public readonly float NoAmmoSoundDistSqr;
        public readonly float HardPointAvMaxDistSqr;

        public bool AnimationsInited;


        public enum FiringSoundState
        {
            None,
            PerShot,
            WhenDone
        }

        public enum TurretType
        {
            Full,
            AzimuthOnly,
            ElevationOnly,
            Fixed //not used yet
        }

        public WeaponSystem(Session session, MyStringHash muzzlePartName, MyStringHash azimuthPartName, MyStringHash elevationPartName, WeaponDefinition values, string weaponName, WeaponAmmoTypes[] weaponAmmoTypes, int weaponIdHash, int weaponId)
        {
            Session = session;
            MuzzlePartName = muzzlePartName;
            DesignatorWeapon = muzzlePartName.String == "Designator";
            AzimuthPartName = azimuthPartName;
            ElevationPartName = elevationPartName;

            Values = values;
            Barrels = values.Assignments.Barrels;
            WeaponIdHash = weaponIdHash;
            WeaponId = weaponId;
            WeaponName = weaponName;
            WeaponAmmoTypes = weaponAmmoTypes;
            MaxAmmoVolume = Values.HardPoint.HardWare.InventorySize;
            CeaseFireDelay = values.HardPoint.DelayCeaseFire;
            DelayCeaseFire = CeaseFireDelay > 0;
            DelayToFire = values.HardPoint.Loading.DelayUntilFire;
            ReloadTime = values.HardPoint.Loading.ReloadTime;
            MaxTargetSpeed = values.Targeting.StopTrackingSpeed > 0 ? values.Targeting.StopTrackingSpeed : double.MaxValue;
            ClosestFirst = values.Targeting.ClosestFirst;
            AlwaysFireFullBurst = Values.HardPoint.Loading.FireFullBurst;
            Prediction = Values.HardPoint.AimLeadingPrediction;
            LockOnFocus = Values.HardPoint.Ai.LockOnFocus && !Values.HardPoint.Ai.TrackTargets;

            TurretMovements(out AzStep, out ElStep, out MinAzimuth, out MaxAzimuth, out MinElevation, out MaxElevation, out TurretMovement);
            Heat(out DegRof, out MaxHeat, out WepCoolDown, out HeatPerShot);
            BarrelValues(out BarrelsPerShot, out RateOfFire, out ShotsPerBurst);
            BarrelsAv(out BarrelEffect1, out BarrelEffect2, out Barrel1AvTicks, out Barrel2AvTicks, out BarrelSpinRate, out HasBarrelRotation);
            Track(out TrackProjectile, out TrackGrids, out TrackCharacters, out TrackMeteors, out TrackNeutrals, out TrackOther);
            SubSystems(out TargetSubSystems, out OnlySubSystems);
            ValidTargetSize(out MinTargetRadius, out MaxTargetRadius);
            HardPointSoundSetup(out WeaponReloadSound, out HardPointRotationSound, out BarrelRotationSound, out NoAmmoSound, out PreFireSound, out HardPointAvMaxDistSqr, out FiringSound);
            HardPointSoundDistMaxSqr(WeaponAmmoTypes, out FiringSoundDistSqr, out ReloadSoundDistSqr, out BarrelSoundDistSqr, out HardPointSoundDistSqr, out NoAmmoSoundDistSqr, out HardPointAvMaxDistSqr);
            
            HasBarrelShootAv = BarrelEffect1 || BarrelEffect2 || HardPointRotationSound || FiringSound == FiringSoundState.WhenDone;

            Session.CreateAnimationSets(Values.Animations, this, out WeaponAnimationSet, out WeaponEmissiveSet, out WeaponLinearMoveSet, out AnimationIdLookup, out WeaponAnimationLengths, out HeatingSubparts);

            for (int i = 0; i < WeaponAmmoTypes.Length; i++)
            {
                var ammo = WeaponAmmoTypes[i];
                ammo.AmmoDef.Const = new AmmoConstants(ammo, Values, Session, this, i);
            }
        }

        private void Heat(out bool degRof, out int maxHeat, out float wepCoolDown, out int heatPerShot)
        {
            degRof = Values.HardPoint.Loading.DegradeRof;
            maxHeat = Values.HardPoint.Loading.MaxHeat;
            wepCoolDown = Values.HardPoint.Loading.Cooldown;
            heatPerShot = Values.HardPoint.Loading.HeatPerShot;
            if (wepCoolDown < 0) wepCoolDown = 0;
            if (wepCoolDown > .95f) wepCoolDown = .95f;
        }

        private void BarrelsAv(out bool barrelEffect1, out bool barrelEffect2, out float barrel1AvTicks, out float barrel2AvTicks, out int barrelSpinRate, out bool hasBarrelRotation)
        {
            barrelEffect1 = Values.HardPoint.Graphics.Barrel1.Name != string.Empty;
            barrelEffect2 = Values.HardPoint.Graphics.Barrel2.Name != string.Empty;
            barrel1AvTicks = Values.HardPoint.Graphics.Barrel1.Extras.MaxDuration;
            barrel2AvTicks = Values.HardPoint.Graphics.Barrel2.Extras.MaxDuration;
            
            barrelSpinRate = 0;
            if (Values.HardPoint.Other.RotateBarrelAxis != 0) {
                if (Values.HardPoint.Loading.BarrelSpinRate > 0) barrelSpinRate = Values.HardPoint.Loading.BarrelSpinRate < 3600 ? Values.HardPoint.Loading.BarrelSpinRate : 3599;
                else barrelSpinRate = RateOfFire < 3699 ? RateOfFire : 3599;
            }

            hasBarrelRotation = barrelSpinRate > 0;
        }

        private void BarrelValues(out int barrelsPerShot, out int rateOfFire, out int shotsPerBurst)
        {
            barrelsPerShot = Values.HardPoint.Loading.BarrelsPerShot;
            rateOfFire = Values.HardPoint.Loading.RateOfFire;
            shotsPerBurst = Values.HardPoint.Loading.ShotsInBurst;
        }

        private void TurretMovements(out double azStep, out double elStep, out int minAzimuth, out int maxAzimuth, out int minElevation, out int maxElevation, out TurretType turretMove)
        {
            azStep = Values.HardPoint.HardWare.RotateRate;
            elStep = Values.HardPoint.HardWare.ElevateRate;
            minAzimuth = Values.HardPoint.HardWare.MinAzimuth;
            maxAzimuth = Values.HardPoint.HardWare.MaxAzimuth;
            minElevation = Values.HardPoint.HardWare.MinElevation;
            maxElevation = Values.HardPoint.HardWare.MaxElevation;
            
            turretMove = TurretType.Full;

            if (minAzimuth == maxAzimuth)
                turretMove = TurretType.ElevationOnly;
            if (minElevation == maxElevation && TurretMovement != TurretType.Full)
                turretMove = TurretType.Fixed;
            else if (minElevation == maxElevation)
                turretMove = TurretType.AzimuthOnly;
        }


        private void Track(out bool trackProjectile, out bool trackGrids, out bool trackCharacters, out bool trackMeteors, out bool trackNeutrals, out bool trackOther)
        {
            trackProjectile = false;
            trackGrids = false;
            trackCharacters = false;
            trackMeteors = false;
            trackNeutrals = false;
            trackOther = false;

            var threats = Values.Targeting.Threats;
            foreach (var threat in threats)
            {
                if (threat == TargetingDef.Threat.Projectiles)
                    trackProjectile = true;
                else if (threat == TargetingDef.Threat.Grids)
                {
                    trackGrids = true;
                    trackOther = true;
                }
                else if (threat == TargetingDef.Threat.Characters)
                {
                    trackCharacters = true;
                    trackOther = true;
                }
                else if (threat == TargetingDef.Threat.Meteors)
                {
                    trackMeteors = true;
                    trackOther = true;
                }
                else if (threat == TargetingDef.Threat.Neutrals)
                {
                    trackNeutrals = true;
                    trackOther = true;
                }
            }
        }

        private void SubSystems(out bool targetSubSystems, out bool onlySubSystems)
        {
            targetSubSystems = false;
            var anySystemDetected = false;
            if (Values.Targeting.SubSystems.Length > 0)
            {
                foreach (var system in Values.Targeting.SubSystems)
                {
                    if (system != TargetingDef.BlockTypes.Any) targetSubSystems = true;
                    else anySystemDetected = true;
                }
            }
            if (TargetSubSystems && anySystemDetected) onlySubSystems = false;
            else onlySubSystems = true;
        }

        private void ValidTargetSize(out float minTargetRadius, out float maxTargetRadius)
        {
            var minDiameter = Values.Targeting.MinimumDiameter;
            var maxDiameter = Values.Targeting.MaximumDiameter;

            minTargetRadius = (float)(minDiameter > 0 ? minDiameter * 0.5d : 0);
            maxTargetRadius = (float)(maxDiameter > 0 ? maxDiameter * 0.5d : float.MaxValue);
        }


        private void HardPointSoundSetup(out bool weaponReloadSound, out bool hardPointRotationSound, out bool barrelRotationSound, out bool noAmmoSound, out bool preFireSound, out float hardPointAvMaxDistSqr, out FiringSoundState firingSound)
        {
            weaponReloadSound = Values.HardPoint.Audio.ReloadSound != string.Empty;
            hardPointRotationSound = Values.HardPoint.Audio.HardPointRotationSound != string.Empty;
            barrelRotationSound = Values.HardPoint.Audio.BarrelRotationSound != string.Empty;
            noAmmoSound = Values.HardPoint.Audio.NoAmmoSound != string.Empty;
            preFireSound = Values.HardPoint.Audio.PreFiringSound != string.Empty;

            var fSoundStart = Values.HardPoint.Audio.FiringSound;
            if (fSoundStart != string.Empty && Values.HardPoint.Audio.FiringSoundPerShot)
                firingSound = FiringSoundState.PerShot;
            else if (fSoundStart != string.Empty && !Values.HardPoint.Audio.FiringSoundPerShot)
                firingSound = FiringSoundState.WhenDone;
            else firingSound = FiringSoundState.None;

            hardPointAvMaxDistSqr = 0;
            if (Values.HardPoint.Graphics.Barrel1.Extras.MaxDistance * Values.HardPoint.Graphics.Barrel1.Extras.MaxDistance > HardPointAvMaxDistSqr)
                hardPointAvMaxDistSqr = Values.HardPoint.Graphics.Barrel1.Extras.MaxDistance * Values.HardPoint.Graphics.Barrel1.Extras.MaxDistance;

            if (Values.HardPoint.Graphics.Barrel2.Extras.MaxDistance * Values.HardPoint.Graphics.Barrel2.Extras.MaxDistance > HardPointAvMaxDistSqr)
                hardPointAvMaxDistSqr = Values.HardPoint.Graphics.Barrel2.Extras.MaxDistance * Values.HardPoint.Graphics.Barrel2.Extras.MaxDistance;
        }

        private void HardPointSoundDistMaxSqr(WeaponAmmoTypes[] weaponAmmo, out float firingSoundDistSqr, out float reloadSoundDistSqr, out float barrelSoundDistSqr, out float hardPointSoundDistSqr, out float noAmmoSoundDistSqr, out float hardPointAvMaxDistSqr)
        {
            var fireSound = string.Concat(Arc, Values.HardPoint.Audio.FiringSound);
            var reloadSound = string.Concat(Arc, Values.HardPoint.Audio.ReloadSound);
            var barrelSound = string.Concat(Arc, Values.HardPoint.Audio.BarrelRotationSound);
            var hardPointSound = string.Concat(Arc, Values.HardPoint.Audio.HardPointRotationSound);
            var noAmmoSound = string.Concat(Arc, Values.HardPoint.Audio.NoAmmoSound);

            firingSoundDistSqr = 0f;
            reloadSoundDistSqr = 0f;
            barrelSoundDistSqr = 0f;
            hardPointSoundDistSqr = 0f;
            noAmmoSoundDistSqr = 0f;
            hardPointAvMaxDistSqr = HardPointAvMaxDistSqr;

            foreach (var def in Session.SoundDefinitions)
            {
                var id = def.Id.SubtypeId.String;

                if (FiringSound != FiringSoundState.None && id == fireSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) firingSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (firingSoundDistSqr > hardPointAvMaxDistSqr) hardPointAvMaxDistSqr = FiringSoundDistSqr;
                }
                if (WeaponReloadSound && id == reloadSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) reloadSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (reloadSoundDistSqr > hardPointAvMaxDistSqr) hardPointAvMaxDistSqr = ReloadSoundDistSqr;

                }
                if (BarrelRotationSound && id == barrelSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) barrelSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (barrelSoundDistSqr > hardPointAvMaxDistSqr) hardPointAvMaxDistSqr = BarrelSoundDistSqr;
                }
                if (HardPointRotationSound && id == hardPointSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) hardPointSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (hardPointSoundDistSqr > hardPointAvMaxDistSqr) hardPointAvMaxDistSqr = HardPointSoundDistSqr;
                }
                if (NoAmmoSound && id == noAmmoSound)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) noAmmoSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (noAmmoSoundDistSqr > hardPointAvMaxDistSqr) hardPointAvMaxDistSqr = NoAmmoSoundDistSqr;
                }
            }

            if (firingSoundDistSqr <= 0)
                foreach (var ammoType in weaponAmmo)
                    if (ammoType.AmmoDef.Trajectory.MaxTrajectory * ammoType.AmmoDef.Trajectory.MaxTrajectory > firingSoundDistSqr)
                        firingSoundDistSqr = ammoType.AmmoDef.Trajectory.MaxTrajectory * ammoType.AmmoDef.Trajectory.MaxTrajectory;
        }
    }

    public class AmmoConstants
    {
        private const string Arc = "Arc";

        public readonly MyConcurrentPool<MyEntity> PrimeEntityPool;
        public readonly Dictionary<MyDefinitionBase, float> CustomBlockDefinitionBasesToScales;
        public readonly MyAmmoMagazineDefinition MagazineDef;
        public readonly AmmoDef[] AmmoPattern;
        public readonly int[] AmmoShufflePattern;
        public readonly MyStringId TracerMaterial;
        public readonly MyStringId TrailMaterial;
        public readonly MyStringId SegmentMaterial;
        public readonly MyPhysicalInventoryItem AmmoItem;
        public readonly AreaEffectType AreaEffect;
        public readonly string ModelPath;

        public readonly int MaxObjectsHit;
        public readonly int TargetLossTime;
        public readonly int MaxLifeTime;
        public readonly int MaxTargets;
        public readonly int PulseInterval;
        public readonly int PulseChance;
        public readonly int PulseGrowTime;
        public readonly int EnergyMagSize;
        public readonly int ChargSize;
        public readonly int ShrapnelId = -1;
        public readonly int MaxChaseTime;
        public readonly int MagazineSize;
        public readonly int PatternIndexCnt;
        public readonly int AmmoIdxPos;
        public readonly bool Pulse;
        public readonly bool PrimeModel;
        public readonly bool TriggerModel;
        public readonly bool CollisionIsLine;
        public readonly bool SelfDamage;
        public readonly bool VoxelDamage;
        public readonly bool OffsetEffect;
        public readonly bool Trail;
        public readonly bool IsMine;
        public readonly bool IsField;
        public readonly bool AmmoParticle;
        public readonly bool HitParticle;
        public readonly bool FieldParticle;
        public readonly bool AmmoAreaEffect;
        public readonly bool AmmoSkipAccel;
        public readonly bool LineWidthVariance;
        public readonly bool LineColorVariance;
        public readonly bool LineSegments;
        public readonly bool SegmentWidthVariance;
        public readonly bool SegmentColorVariance;
        public readonly bool OneHitParticle;
        public readonly bool DamageScaling;
        public readonly bool ArmorScaling;
        public readonly bool FallOffScaling;
        public readonly bool CustomDamageScales;
        public readonly bool SpeedVariance;
        public readonly bool RangeVariance;
        public readonly bool VirtualBeams;
        public readonly bool IsBeamWeapon;
        public readonly bool ConvergeBeams;
        public readonly bool RotateRealBeam;
        public readonly bool AmmoParticleShrinks;
        public readonly bool FieldParticleShrinks;
        public readonly bool HitParticleShrinks;
        public readonly bool DrawLine;
        public readonly bool Ewar;
        public readonly bool EwarEffect;
        public readonly bool TargetOffSet;
        public readonly bool HasBackKickForce;
        public readonly bool BurstMode;
        public readonly bool EnergyAmmo;
        public readonly bool Reloadable;
        public readonly bool MustCharge;
        public readonly bool HasShotReloadDelay;
        public readonly bool HitSound;
        public readonly bool AltHitSounds;
        public readonly bool AmmoTravelSound;
        public readonly bool IsHybrid;
        public readonly bool IsTurretSelectable;
        public readonly bool CanZombie;
        public readonly bool FeelsGravity;
        public readonly bool MaxTrajectoryGrows;
        public readonly bool HasShotFade;
        public readonly float TargetLossDegree;
        public readonly float TrailWidth;
        public readonly float ShieldBypassMod;
        public readonly float MagMass;
        public readonly float MagVolume;
        public readonly float BaseDamage;
        public readonly float AreaEffectDamage;
        public readonly float DetonationDamage;
        public readonly float DesiredProjectileSpeed;
        public readonly float HitSoundDistSqr;
        public readonly float AmmoTravelSoundDistSqr;
        public readonly float AmmoSoundMaxDistSqr;
        public readonly float BaseDps;
        public readonly float AreaDps;
        public readonly float EffectiveDps;
        public readonly float DetDps;
        public readonly float PeakDps;
        public readonly float ShotsPerSec;
        public readonly float MaxTrajectory;
        public readonly float ShotFadeStep;
        public readonly float TrajectoryStep;

        public readonly double AreaRadiusSmall;
        public readonly double AreaRadiusLarge;
        public readonly double AreaEffectSize;
        public readonly double DetonateRadiusSmall;
        public readonly double DetonateRadiusLarge;
        public readonly double ShieldModifier;
        public readonly double MaxLateralThrust;
        public readonly double EwarTriggerRange;
        public readonly double TracerLength;
        public readonly double CollisionSize;
        public readonly double SmartsDelayDistSqr;
        public readonly double SegmentStep;

        public AmmoConstants(WeaponAmmoTypes ammo, WeaponDefinition wDef, Session session, WeaponSystem system, int ammoIndex)
        {
            AmmoIdxPos = ammoIndex;
            MyInventory.GetItemVolumeAndMass(ammo.AmmoDefinitionId, out MagMass, out MagVolume);

            MagazineDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(ammo.AmmoDefinitionId);
            TracerMaterial = MyStringId.GetOrCompute(ammo.AmmoDef.AmmoGraphics.Lines.TracerMaterial);
            TrailMaterial = MyStringId.GetOrCompute(ammo.AmmoDef.AmmoGraphics.Lines.Trail.Material);
            SegmentMaterial = MyStringId.GetOrCompute(ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Material);

            if (ammo.AmmoDefinitionId.SubtypeId.String != "Energy" || ammo.AmmoDefinitionId.SubtypeId.String == string.Empty) AmmoItem = new MyPhysicalInventoryItem() { Amount = 1, Content = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_AmmoMagazine>(ammo.AmmoDefinitionId.SubtypeName) };

            for (int i = 0; i < wDef.Ammos.Length; i++)
            {
                var ammoType = wDef.Ammos[i];
                if (ammoType.AmmoRound.Equals(ammo.AmmoDef.Shrapnel.AmmoRound))
                    ShrapnelId = i;
            }

            IsMine = ammo.AmmoDef.Trajectory.Guidance == DetectFixed || ammo.AmmoDef.Trajectory.Guidance == DetectSmart || ammo.AmmoDef.Trajectory.Guidance == DetectTravelTo;
            IsField = ammo.AmmoDef.Trajectory.FieldTime > 0;
            IsHybrid = ammo.AmmoDef.HybridRound;
            IsTurretSelectable = !ammo.IsShrapnel || ammo.AmmoDef.HardPointUsable;

            AmmoParticleShrinks = ammo.AmmoDef.AmmoGraphics.Particles.Ammo.ShrinkByDistance;
            HitParticleShrinks = ammo.AmmoDef.AmmoGraphics.Particles.Hit.ShrinkByDistance;
            FieldParticleShrinks = ammo.AmmoDef.AreaEffect.Pulse.Particle.ShrinkByDistance;

            AmmoParticle = !string.IsNullOrEmpty(ammo.AmmoDef.AmmoGraphics.Particles.Ammo.Name);
            HitParticle = !string.IsNullOrEmpty(ammo.AmmoDef.AmmoGraphics.Particles.Hit.Name);
            FieldParticle = !string.IsNullOrEmpty(ammo.AmmoDef.AreaEffect.Pulse.Particle.Name);
            
            DrawLine = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Enable;
            LineColorVariance = ammo.AmmoDef.AmmoGraphics.Lines.ColorVariance.Start > 0 && ammo.AmmoDef.AmmoGraphics.Lines.ColorVariance.End > 0;
            LineWidthVariance = ammo.AmmoDef.AmmoGraphics.Lines.WidthVariance.Start > 0 || ammo.AmmoDef.AmmoGraphics.Lines.WidthVariance.End > 0;
            SegmentColorVariance = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.ColorVariance.Start > 0 && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.ColorVariance.End > 0;
            SegmentWidthVariance = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.WidthVariance.Start > 0 || ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.WidthVariance.End > 0;

            LineSegments = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.SegmentLength > 0 && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.SegmentGap > 0;
            SegmentStep = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Segmentation.Speed * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            SpeedVariance = ammo.AmmoDef.Trajectory.SpeedVariance.Start > 0 || ammo.AmmoDef.Trajectory.SpeedVariance.End > 0;
            RangeVariance = ammo.AmmoDef.Trajectory.RangeVariance.Start > 0 || ammo.AmmoDef.Trajectory.RangeVariance.End > 0;
            TrailWidth = ammo.AmmoDef.AmmoGraphics.Lines.Trail.CustomWidth > 0 ? ammo.AmmoDef.AmmoGraphics.Lines.Trail.CustomWidth : ammo.AmmoDef.AmmoGraphics.Lines.Tracer.Width;
            TargetOffSet = ammo.AmmoDef.Trajectory.Smarts.Inaccuracy > 0;
            TargetLossTime = ammo.AmmoDef.Trajectory.TargetLossTime > 0 ? ammo.AmmoDef.Trajectory.TargetLossTime : int.MaxValue;
            CanZombie = TargetLossTime > 0 && TargetLossTime != int.MaxValue && !IsMine;
            MaxLifeTime = ammo.AmmoDef.Trajectory.MaxLifeTime > 0 ? ammo.AmmoDef.Trajectory.MaxLifeTime : int.MaxValue;
            MaxChaseTime = ammo.AmmoDef.Trajectory.Smarts.MaxChaseTime > 0 ? ammo.AmmoDef.Trajectory.Smarts.MaxChaseTime : int.MaxValue;
            MaxObjectsHit = ammo.AmmoDef.ObjectsHit.MaxObjectsHit > 0 ? ammo.AmmoDef.ObjectsHit.MaxObjectsHit : int.MaxValue;
            BaseDamage = ammo.AmmoDef.BaseDamage;
            MaxTargets = ammo.AmmoDef.Trajectory.Smarts.MaxTargets;
            TargetLossDegree = ammo.AmmoDef.Trajectory.TargetLossDegree > 0 ? (float)Math.Cos(MathHelper.ToRadians(ammo.AmmoDef.Trajectory.TargetLossDegree)) : 0;

            ShieldModifier = ammo.AmmoDef.DamageScales.Shields.Modifier > 0 ? ammo.AmmoDef.DamageScales.Shields.Modifier : 1;
            ShieldBypassMod = ammo.AmmoDef.DamageScales.Shields.BypassModifier > 0 && ammo.AmmoDef.DamageScales.Shields.BypassModifier < 1 ? ammo.AmmoDef.DamageScales.Shields.BypassModifier : 1;
            AmmoSkipAccel = ammo.AmmoDef.Trajectory.AccelPerSec <= 0;
            FeelsGravity = ammo.AmmoDef.Trajectory.GravityMultiplier > 0;

            MaxTrajectory = ammo.AmmoDef.Trajectory.MaxTrajectory;
            HasBackKickForce = ammo.AmmoDef.BackKickForce > 0;

            MaxLateralThrust = MathHelperD.Clamp(ammo.AmmoDef.Trajectory.Smarts.MaxLateralThrust, 0.000001, 1);

            ComputeAmmoPattern(ammo, wDef, out AmmoPattern, out PatternIndexCnt, out AmmoShufflePattern);

            Fields(ammo.AmmoDef, out PulseInterval, out PulseChance, out Pulse, out PulseGrowTime);
            AreaEffects(ammo.AmmoDef, out AreaEffect, out AreaEffectDamage, out AreaEffectSize, out DetonationDamage, out AmmoAreaEffect, out AreaRadiusSmall, out AreaRadiusLarge, out DetonateRadiusSmall, out DetonateRadiusLarge, out Ewar, out EwarEffect, out EwarTriggerRange);

            DamageScales(ammo.AmmoDef, out DamageScaling, out FallOffScaling,out ArmorScaling, out CustomDamageScales, out CustomBlockDefinitionBasesToScales, out SelfDamage, out VoxelDamage);
            Beams(ammo.AmmoDef, out IsBeamWeapon, out VirtualBeams, out RotateRealBeam, out ConvergeBeams, out OneHitParticle, out OffsetEffect);
            CollisionShape(ammo.AmmoDef, out CollisionIsLine, out CollisionSize, out TracerLength);
            SmartsDelayDistSqr = (CollisionSize * ammo.AmmoDef.Trajectory.Smarts.TrackingDelay) * (CollisionSize * ammo.AmmoDef.Trajectory.Smarts.TrackingDelay);
            PrimeEntityPool = Models(ammo.AmmoDef, wDef, out PrimeModel, out TriggerModel, out ModelPath);
            Energy(ammo, system, wDef, out EnergyAmmo, out MustCharge, out Reloadable, out EnergyMagSize, out ChargSize, out BurstMode, out HasShotReloadDelay);
            Sound(ammo.AmmoDef, session, out HitSound, out AltHitSounds, out AmmoTravelSound, out HitSoundDistSqr, out AmmoTravelSoundDistSqr, out AmmoSoundMaxDistSqr);
            MagazineSize = EnergyAmmo ? EnergyMagSize : MagazineDef.Capacity;
            GetPeakDps(ammo, system, wDef, out PeakDps, out EffectiveDps, out ShotsPerSec, out BaseDps, out AreaDps, out DetDps);

            DesiredProjectileSpeed = (!IsBeamWeapon ? ammo.AmmoDef.Trajectory.DesiredSpeed : MaxTrajectory * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
            Trail = ammo.AmmoDef.AmmoGraphics.Lines.Trail.Enable;
            HasShotFade =  ammo.AmmoDef.AmmoGraphics.Lines.Tracer.VisualFadeStart > 0 && ammo.AmmoDef.AmmoGraphics.Lines.Tracer.VisualFadeEnd > 1;
            MaxTrajectoryGrows = ammo.AmmoDef.Trajectory.MaxTrajectoryTime > 1;
            ComputeSteps(ammo, out ShotFadeStep, out TrajectoryStep);
        }

        internal void GetParticleInfo(WeaponAmmoTypes ammo, WeaponDefinition wDef, Session session)
        {
            var list = MyDefinitionManager.Static.GetAllSessionPreloadObjectBuilders();
            var comparer = new Session.HackEqualityComparer();
            for (int i = 0; i < list.Count; i++)
            {
                var tuple = (IStructuralEquatable)list[i];
                if (tuple != null)
                {
                    tuple.GetHashCode(comparer);
                    var hacked = comparer.Def;
                    if (hacked != null)
                    {
                        if (hacked.ParticleEffects != null)
                        {
                            foreach (var particle in hacked.ParticleEffects)
                            {
                                if (particle.Id.SubtypeId.Contains("Spark"))
                                    Log.Line($"test: {particle.Id.SubtypeId} - {ammo.AmmoDef.AmmoGraphics.Particles.Hit.Name}");
                            }
                        }
                    }
                }
            }
        }

        private void ComputeSteps(WeaponAmmoTypes ammo, out float shotFadeStep, out float trajectoryStep)
        {
            var changeFadeSteps = ammo.AmmoDef.AmmoGraphics.Lines.Tracer.VisualFadeEnd - ammo.AmmoDef.AmmoGraphics.Lines.Tracer.VisualFadeStart;
            shotFadeStep = 1f / changeFadeSteps;

            trajectoryStep = MaxTrajectoryGrows ? MaxTrajectory / ammo.AmmoDef.Trajectory.MaxTrajectoryTime : MaxTrajectory;
        }

        private void ComputeAmmoPattern(WeaponAmmoTypes ammo, WeaponDefinition wDef, out AmmoDef[] ammoPattern, out int patternIndex, out int[] ammoShufflePattern)
        {
            var pattern = ammo.AmmoDef.Pattern;
            var indexPos = 0;
            
            int indexCount;
            if (!pattern.Enable)
                indexCount = 1;
            else
            {
                indexCount = pattern.Ammos.Length;
                if (!pattern.SkipParent) indexCount += 1;
            }

            patternIndex = indexCount;

            ammoPattern = new AmmoDef[indexCount];

            ammoShufflePattern = new int[indexCount];
            for (int i = 0; i < indexCount; i++)
                ammoShufflePattern[i] = i;

            if (!pattern.Enable || !pattern.SkipParent)
                ammoPattern[indexPos++] = ammo.AmmoDef;

            if (pattern.Enable)
            {
                for (int i = 0; i < wDef.Ammos.Length; i++)
                {
                    var ammoDef = wDef.Ammos[i];
                    for (int j = 0; j < ammo.AmmoDef.Pattern.Ammos.Length; j++)
                    {
                        var aPattern = ammo.AmmoDef.Pattern.Ammos[j];
                        if (aPattern.Equals(ammoDef.AmmoRound))
                            ammoPattern[indexPos++] = ammoDef;
                    }
                }
            }
        }
        private void GetPeakDps(WeaponAmmoTypes ammoDef, WeaponSystem system, WeaponDefinition wDef, out float peakDps, out float effectiveDps, out float shotsPerSec, out float baseDps, out float areaDps, out float detDps)
        {
            var s = system;
            var a = ammoDef.AmmoDef;
            var hasShrapnel = ShrapnelId > -1;
            var l = wDef.HardPoint.Loading;

            var mexLogLevel = 0; //dirty log levels :P


            if (mexLogLevel >= 1) Log.Line($"-----");
            if (mexLogLevel >= 1) Log.Line($"Name = {s.WeaponName}"); //a.EnergyMagazineSize
            if (mexLogLevel >= 2) Log.Line($"EnergyMag = {a.EnergyMagazineSize}");

            var baselineRange = 1000;

            //Inaccuracy
            var inaccuracyRadius = Math.Tan(wDef.HardPoint.DeviateShotAngle / 2) * baselineRange;

            var inaccuracyScore = ((Math.PI * 10 * 10) / (Math.PI * inaccuracyRadius * inaccuracyRadius));
            inaccuracyScore = inaccuracyScore > 1 ? 1 : inaccuracyScore;
            inaccuracyScore = wDef.HardPoint.DeviateShotAngle <= 0 ? 1 : inaccuracyScore;


            //EffectiveRange
            var effectiveRangeScore = 1 / (baselineRange / a.Trajectory.DesiredSpeed);
            effectiveRangeScore = effectiveRangeScore > 1 ? 1 : effectiveRangeScore;
            effectiveRangeScore = a.Beams.Enable ? 1 : effectiveRangeScore;
            effectiveRangeScore = 1;


            //TrackingScore
            var coverageScore = ((Math.Abs(s.MinElevation) + (float)Math.Abs(s.MaxElevation)) * ((Math.Abs(s.MinAzimuth) + Math.Abs(s.MaxAzimuth)))) / (360 * 90);
            coverageScore = coverageScore > 1 ? 1 : coverageScore;

            var speedEl = (wDef.HardPoint.HardWare.ElevateRate * (180 / Math.PI)) * 60;
            var coverageElevateScore = speedEl / (180d / 5d);
            var speedAz = (wDef.HardPoint.HardWare.RotateRate * (180 / Math.PI)) * 60;
            var coverageRotateScore = speedAz / (180d / 5d);




            var trackingScore = (coverageScore + ((coverageRotateScore + coverageElevateScore) * 0.5d)) * 0.5d;
            //if a sorter weapon use several barrels with only elevation or rotation the score should be uneffected since its designer to work
            if (MyUtils.IsZero(Math.Abs(s.MinElevation) + (float)Math.Abs(s.MaxElevation)))
                trackingScore = (coverageScore + ((coverageRotateScore + 1) * 0.5d)) * 0.5d;

            if ((Math.Abs(s.MinAzimuth) + Math.Abs(s.MaxAzimuth)) == 0)
                trackingScore = (coverageScore + ((coverageElevateScore + 1) * 0.5d)) * 0.5d;

            if (MyUtils.IsZero(Math.Abs(s.MinElevation) + (float)Math.Abs(s.MaxElevation) + (Math.Abs(s.MinAzimuth) + Math.Abs(s.MaxAzimuth))))
                trackingScore = 1.0d;

            trackingScore = trackingScore > 1 ? 1 : trackingScore;

            //FinalScore
            var effectiveModifier = ((effectiveRangeScore * inaccuracyScore) * trackingScore);

            //Logs for effective dps
            if (mexLogLevel >= 2) Log.Line($"newInaccuracyRadius = {inaccuracyRadius}");
            if (mexLogLevel >= 2) Log.Line($"DeviationAngle = { wDef.HardPoint.DeviateShotAngle}");
            if (mexLogLevel >= 1) Log.Line($"InaccuracyScore = {inaccuracyScore}");
            if (mexLogLevel >= 1) Log.Line($"effectiveRangeScore = {effectiveRangeScore}");
            if (mexLogLevel >= 2) Log.Line($"coverageScore = {coverageScore}");
            if (mexLogLevel >= 2) Log.Line($"ElevateRate = {(wDef.HardPoint.HardWare.ElevateRate * (180 / Math.PI))}");
            if (mexLogLevel >= 2) Log.Line($"coverageElevate = {speedEl}");
            if (mexLogLevel >= 2) Log.Line($"coverageElevateScore = {coverageElevateScore}");
            if (mexLogLevel >= 2) Log.Line($"RotateRate = {(wDef.HardPoint.HardWare.RotateRate * (180 / Math.PI))}");
            if (mexLogLevel >= 2) Log.Line($"coverageRotate = {speedAz}");
            if (mexLogLevel >= 2) Log.Line($"coverageRotateScore = {coverageRotateScore}");

            if (mexLogLevel >= 2) Log.Line($"CoverageScore = {(coverageScore + ((coverageRotateScore + coverageElevateScore) * 0.5d)) * 0.5d}");
            if (mexLogLevel >= 1) Log.Line($"trackingScore = {trackingScore}");
            if (mexLogLevel >= 1) Log.Line($"effectiveModifier = {effectiveModifier}");


            //DPS Calc


            if (!EnergyAmmo && MagazineSize > 0 || IsHybrid)
            {
                if (IsHybrid) Log.Line($"IsHybrid");
                var burstPerMag = l.ShotsInBurst > 0 ? (int)Math.Floor((double)(MagazineSize / l.ShotsInBurst)) : 0;
                burstPerMag = burstPerMag >= 1 ? burstPerMag - 1 : burstPerMag;

                var drainPerMin = ((MagazineSize / (double)s.RateOfFire) / s.BarrelsPerShot) * 3600d;
                drainPerMin = MagazineSize >= 1 ? drainPerMin : 1;

                var timeSpentOnBurst = l.DelayAfterBurst > 0 ? burstPerMag * l.DelayAfterBurst : 0;
                var timePerMag = drainPerMin + s.ReloadTime + timeSpentOnBurst;

                shotsPerSec = (float) (((3600d / timePerMag) * MagazineSize) / 60 * l.TrajectilesPerBarrel);
            }



            else if (EnergyAmmo && a.EnergyMagazineSize > 0)
            {
                var burstPerMag = l.ShotsInBurst > 0 ? (int)Math.Floor((double)(a.EnergyMagazineSize / l.ShotsInBurst)) : 0;
                burstPerMag = burstPerMag >= 1 ? burstPerMag - 1 : burstPerMag;

                var drainPerMin = ((a.EnergyMagazineSize / (double)s.RateOfFire) / s.BarrelsPerShot) * 3600d;
                drainPerMin = a.EnergyMagazineSize >= 1 ? drainPerMin : 1;

                var timeSpentOnBurst = l.DelayAfterBurst > 0 ? burstPerMag * l.DelayAfterBurst : 0;
                var timePerMag = drainPerMin + s.ReloadTime + timeSpentOnBurst;

                shotsPerSec = (float) (((3600d / timePerMag) * a.EnergyMagazineSize) / 60 * l.TrajectilesPerBarrel);

            }
            else
            {
                //Log.Line($"Burst Fire");
                var shotyPerBurst = s.ShotsPerBurst > 0 ? s.ShotsPerBurst : 1;
                var burstTime = ((((3600f / s.RateOfFire) * shotyPerBurst) + l.DelayAfterBurst) + s.ReloadTime) / 60;
                //Log.Line($"BURST - burstTime = {burstTime}");
                var projectilesInBurst = ((s.BarrelsPerShot * l.TrajectilesPerBarrel) * (s.ShotsPerBurst > 0 ? s.ShotsPerBurst : 1));
                //Log.Line($"BURST - projectilesInBurst = {projectilesInBurst}");
                var burstPerMin = (60 / burstTime);
                var burstProjectilesPerMin = burstPerMin * projectilesInBurst;
                var burstPerSec = burstProjectilesPerMin / 60;
                //Log.Line($"BURST - shotsPerSec = {burstPerSec}");
                shotsPerSec = burstPerSec;
            }
            var shotsPerSecPower = shotsPerSec; //save for power calc

            if (s.HeatPerShot > 0)
            {


                var heatGenPerSec = (l.HeatPerShot * shotsPerSec) - l.HeatSinkRate; //heat - cooldown



                if (heatGenPerSec > 0)
                {

                    var safeToOverheat = (l.MaxHeat - (l.MaxHeat * l.Cooldown)) / heatGenPerSec;
                    var cooldownTime = (l.MaxHeat - (l.MaxHeat * l.Cooldown)) / l.HeatSinkRate;

                    var timeHeatCycle = (safeToOverheat + cooldownTime);


                    shotsPerSec = ((safeToOverheat / timeHeatCycle) * shotsPerSec);

                    if ((mexLogLevel >= 1))
                    {
                        Log.Line($"Name = {s.WeaponName}");
                        Log.Line($"HeatPerShot = {l.HeatPerShot}");
                        Log.Line($"HeatGenPerSec = {heatGenPerSec}");

                        Log.Line($"WepCoolDown = {l.Cooldown}");

                        Log.Line($"safeToOverheat = {safeToOverheat}");
                        Log.Line($"cooldownTime = {cooldownTime}");


                        Log.Line($"timeHeatCycle = {timeHeatCycle}s");

                        Log.Line($"shotsPerSec wHeat = {shotsPerSec}");
                    }

                }

            }

            baseDps = BaseDamage * shotsPerSec;
            areaDps = (float)(!AmmoAreaEffect ? 0 : ((a.AreaEffect.AreaEffectDamage * (a.AreaEffect.AreaEffectRadius * 0.5d)) * shotsPerSec));
            detDps = (float)(a.AreaEffect.Detonation.DetonateOnEnd ? (a.AreaEffect.Detonation.DetonationDamage * (a.AreaEffect.Detonation.DetonationRadius * 0.5d)) * shotsPerSec : 0);

            if (hasShrapnel)
            {
                var sAmmo = wDef.Ammos[ShrapnelId];
                var fragments = a.Shrapnel.Fragments;
                baseDps += (sAmmo.BaseDamage * fragments) * shotsPerSec;
                areaDps += sAmmo.AreaEffect.AreaEffect == AreaEffectType.Disabled ? 0 : (float)((sAmmo.AreaEffect.AreaEffectDamage * (sAmmo.AreaEffect.AreaEffectRadius * 0.5d)) * fragments) * shotsPerSec;
                detDps += sAmmo.AreaEffect.Detonation.DetonateOnEnd ? ((sAmmo.AreaEffect.Detonation.DetonationDamage * (sAmmo.AreaEffect.Detonation.DetonationRadius * 0.5f)) * fragments) * shotsPerSec : 0;
            }
            peakDps = (baseDps + areaDps + detDps);
            effectiveDps = (float) (peakDps * effectiveModifier);
            if (mexLogLevel >= 1) Log.Line($"peakDps= {peakDps}");

            if (mexLogLevel >= 1) Log.Line($"Effecetive DPS(mult) = {effectiveDps}");
        }

        private void Fields(AmmoDef ammoDef, out int pulseInterval, out int pulseChance, out bool pulse, out int growTime)
        {
            pulseInterval = ammoDef.AreaEffect.Pulse.Interval;
            growTime = ammoDef.AreaEffect.Pulse.GrowTime;
            pulseChance = ammoDef.AreaEffect.Pulse.PulseChance;
            pulse = pulseInterval > 0 && pulseChance > 0;
        }

        private void AreaEffects(AmmoDef ammoDef, out AreaEffectType areaEffect, out float areaEffectDamage, out double areaEffectSize, out float detonationDamage, out bool ammoAreaEffect, out double areaRadiusSmall, out double areaRadiusLarge, out double detonateRadiusSmall, out double detonateRadiusLarge, out bool eWar, out bool eWarEffect, out double eWarTriggerRange)
        {
            areaEffect = ammoDef.AreaEffect.AreaEffect;
            areaEffectDamage = ammoDef.AreaEffect.AreaEffectDamage;
            areaEffectSize = ammoDef.AreaEffect.AreaEffectRadius;
            detonationDamage = ammoDef.AreaEffect.Detonation.DetonationDamage;
            ammoAreaEffect = ammoDef.AreaEffect.AreaEffect != AreaEffectType.Disabled;
            areaRadiusSmall = Session.ModRadius(ammoDef.AreaEffect.AreaEffectRadius, false);
            areaRadiusLarge = Session.ModRadius(ammoDef.AreaEffect.AreaEffectRadius, true);
            detonateRadiusSmall = Session.ModRadius(ammoDef.AreaEffect.Detonation.DetonationRadius, false);
            detonateRadiusLarge = Session.ModRadius(ammoDef.AreaEffect.Detonation.DetonationRadius, true);
            eWar = areaEffect > (AreaEffectType)2;
            eWarEffect = areaEffect > (AreaEffectType)3;
            eWarTriggerRange = eWar && Pulse && ammoDef.AreaEffect.EwarFields.TriggerRange > 0 ? ammoDef.AreaEffect.EwarFields.TriggerRange : 0;
        }


        private MyConcurrentPool<MyEntity> Models(AmmoDef ammoDef, WeaponDefinition wDef, out bool primeModel, out bool triggerModel, out string primeModelPath)
        {
            if (ammoDef.AreaEffect.AreaEffect > (AreaEffectType)3 && IsField) triggerModel = true;
            else triggerModel = false;
            primeModel = ammoDef.AmmoGraphics.ModelName != string.Empty;
            primeModelPath = primeModel ? wDef.ModPath + ammoDef.AmmoGraphics.ModelName : string.Empty;
            return primeModel ? new MyConcurrentPool<MyEntity>(256, PrimeEntityClear, 10000, PrimeEntityActivator) : null;
        }


        private void Beams(AmmoDef ammoDef, out bool isBeamWeapon, out bool virtualBeams, out bool rotateRealBeam, out bool convergeBeams, out bool oneHitParticle, out bool offsetEffect)
        {
            isBeamWeapon = ammoDef.Beams.Enable;
            virtualBeams = ammoDef.Beams.VirtualBeams && IsBeamWeapon;
            rotateRealBeam = ammoDef.Beams.RotateRealBeam && VirtualBeams;
            convergeBeams = !RotateRealBeam && ammoDef.Beams.ConvergeBeams && VirtualBeams;
            oneHitParticle = ammoDef.Beams.OneParticle && IsBeamWeapon && VirtualBeams;
            offsetEffect = ammoDef.AmmoGraphics.Lines.OffsetEffect.MaxOffset > 0;
        }

        private void CollisionShape(AmmoDef ammoDef, out bool collisionIsLine, out double collisionSize, out double tracerLength)
        {
            var isLine = ammoDef.Shape.Shape == LineShape;
            var size = ammoDef.Shape.Diameter;

            if (IsBeamWeapon)
                tracerLength = MaxTrajectory;
            else tracerLength = ammoDef.AmmoGraphics.Lines.Tracer.Length > 0 ? ammoDef.AmmoGraphics.Lines.Tracer.Length : 0.1;

            if (size <= 0)
            {
                if (!isLine) isLine = true;
                size = 1;
            }
            else if (!isLine) size *= 0.5;
            if (size > 2.5) Log.Line($"largeCollisionSize: {size}");
            collisionIsLine = isLine;
            collisionSize = size;
        }

        private void DamageScales(AmmoDef ammoDef, out bool damageScaling, out bool fallOffScaling, out bool armorScaling, out bool customDamageScales, out Dictionary<MyDefinitionBase, float> customBlockDef, out bool selfDamage, out bool voxelDamage)
        {
            armorScaling = false;
            customDamageScales = false;
            fallOffScaling = false;
            var d = ammoDef.DamageScales;
            customBlockDef = null;
            if (d.Custom.Types != null && d.Custom.Types.Length > 0)
            {
                foreach (var def in MyDefinitionManager.Static.GetAllDefinitions())
                foreach (var customDef in d.Custom.Types)
                    if (customDef.Modifier >= 0 && def.Id.SubtypeId.String == customDef.SubTypeId)
                    {
                        if (customBlockDef == null) customBlockDef = new Dictionary<MyDefinitionBase, float>();
                        customBlockDef.Add(def, customDef.Modifier);
                        customDamageScales = customBlockDef.Count > 0;
                    }
            }
            damageScaling = d.FallOff.MinMultipler > 0 || d.MaxIntegrity > 0 || d.Armor.Armor >= 0 || d.Armor.NonArmor >= 0 || d.Armor.Heavy >= 0 || d.Armor.Light >= 0 || d.Grids.Large >= 0 || d.Grids.Small >= 0 || customDamageScales;
            if (damageScaling)
            {
                armorScaling = d.Armor.Armor >= 0 || d.Armor.NonArmor >= 0 || d.Armor.Heavy >= 0 || d.Armor.Light >= 0;
                fallOffScaling = d.FallOff.MinMultipler > 0;
            }
            selfDamage = ammoDef.DamageScales.SelfDamage && !IsBeamWeapon;
            voxelDamage = ammoDef.DamageScales.DamageVoxels;
        }

        private void Energy(WeaponAmmoTypes ammoPair, WeaponSystem system, WeaponDefinition wDef, out bool energyAmmo, out bool mustCharge, out bool reloadable, out int energyMagSize, out int chargeSize, out bool burstMode, out bool shotReload)
        {
            energyAmmo = ammoPair.AmmoDefinitionId.SubtypeId.String == "Energy" || ammoPair.AmmoDefinitionId.SubtypeId.String == string.Empty;
            mustCharge = (energyAmmo || IsHybrid) && system.ReloadTime > 0;

            reloadable = !energyAmmo || mustCharge;

            burstMode = wDef.HardPoint.Loading.ShotsInBurst > 0 && (energyAmmo || MagazineDef.Capacity >= wDef.HardPoint.Loading.ShotsInBurst);

            shotReload = !burstMode && wDef.HardPoint.Loading.ShotsInBurst > 0 && wDef.HardPoint.Loading.DelayAfterBurst > 0;

            if (mustCharge)
            {
                var ewar = (int)ammoPair.AmmoDef.AreaEffect.AreaEffect > 3;
                var shotEnergyCost = ewar ? ammoPair.AmmoDef.EnergyCost * AreaEffectDamage : ammoPair.AmmoDef.EnergyCost * BaseDamage;
                var requiredPower = (((shotEnergyCost * ((system.RateOfFire / MyEngineConstants.UPDATE_STEPS_PER_SECOND) * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS)) * wDef.HardPoint.Loading.BarrelsPerShot) * wDef.HardPoint.Loading.TrajectilesPerBarrel);

                chargeSize = (int)Math.Ceiling(requiredPower * (system.ReloadTime / MyEngineConstants.UPDATE_STEPS_PER_SECOND));

                energyMagSize = ammoPair.AmmoDef.EnergyMagazineSize > 0 ? ammoPair.AmmoDef.EnergyMagazineSize : chargeSize;
                return;
            }
            chargeSize = 0;
            energyMagSize = int.MaxValue;

        }

        private void Sound(AmmoDef ammoDef, Session session, out bool hitSound, out bool altHitSounds, out bool ammoTravelSound, out float hitSoundDistSqr, out float ammoTravelSoundDistSqr, out float ammoSoundMaxDistSqr)
        {
            hitSound = ammoDef.AmmoAudio.HitSound != string.Empty;
            altHitSounds = true; //ammoDef.AmmoAudio.VoxelHitSound != string.Empty || ammoDef.AmmoAudio.PlayerHitSound != string.Empty || ammoDef.AmmoAudio.FloatingHitSound != string.Empty;
            ammoTravelSound = ammoDef.AmmoAudio.TravelSound != string.Empty;
            var hitSoundStr = string.Concat(Arc, ammoDef.AmmoAudio.HitSound);
            var travelSoundStr = string.Concat(Arc, ammoDef.AmmoAudio.TravelSound);
            hitSoundDistSqr = 0;
            ammoTravelSoundDistSqr = 0;
            ammoSoundMaxDistSqr = 0;

            foreach (var def in session.SoundDefinitions)
            {
                var id = def.Id.SubtypeId.String;
                if (HitSound && id == hitSoundStr)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) hitSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (hitSoundDistSqr > ammoSoundMaxDistSqr) ammoSoundMaxDistSqr = hitSoundDistSqr;
                }
                else if (AmmoTravelSound && id == travelSoundStr)
                {
                    var ob = def.GetObjectBuilder() as MyObjectBuilder_AudioDefinition;
                    if (ob != null) ammoTravelSoundDistSqr = ob.MaxDistance * ob.MaxDistance;
                    if (ammoTravelSoundDistSqr > ammoSoundMaxDistSqr) ammoSoundMaxDistSqr = ammoTravelSoundDistSqr;
                }
            }
        }

        private MyEntity PrimeEntityActivator()
        {
            var ent = new MyEntity();
            ent.Init(null, ModelPath, null, null, null);
            ent.Render.CastShadows = false;
            ent.IsPreview = true;
            ent.Save = false;
            ent.SyncFlag = false;
            ent.NeedsWorldMatrix = false;
            ent.Flags |= EntityFlags.IsNotGamePrunningStructureObject;
            MyEntities.Add(ent, false);
            return ent;
        }

        private static void PrimeEntityClear(MyEntity myEntity)
        {
            myEntity.PositionComp.SetWorldMatrix(ref MatrixD.Identity, null, false, false, false);
            myEntity.InScene = false;
            myEntity.Render.RemoveRenderObjects();
        }
    }


    public class WeaponStructure
    {
        public readonly Dictionary<MyStringHash, WeaponSystem> WeaponSystems;
        public readonly Dictionary<int, int> HashToId;

        public readonly MyStringHash[] MuzzlePartNames;
        public readonly bool MultiParts;
        public readonly int GridWeaponCap;
        public readonly Session Session;

        public WeaponStructure(Session session, KeyValuePair<string, Dictionary<string, MyTuple<string, string, string>>> tDef, List<WeaponDefinition> wDefList)
        {
            Session = session;
            var map = tDef.Value;
            var numOfParts = wDefList.Count;
            MultiParts = numOfParts > 1;
            var muzzlePartNames = new MyStringHash[numOfParts];
            var weaponId = 0;
            WeaponSystems = new Dictionary<MyStringHash, WeaponSystem>(MyStringHash.Comparer);
            HashToId = new Dictionary<int, int>();

            var gridWeaponCap = 0;
            foreach (var w in map)
            {
                var myMuzzleNameHash = MyStringHash.GetOrCompute(w.Key);
                var myAzimuthNameHash = MyStringHash.GetOrCompute(w.Value.Item2);
                var myElevationNameHash = MyStringHash.GetOrCompute(w.Value.Item3);

                muzzlePartNames[weaponId] = myMuzzleNameHash;

                var typeName = w.Value.Item1;
                var weaponDef = new WeaponDefinition();

                foreach (var weapon in wDefList)
                    if (weapon.HardPoint.WeaponName == typeName) weaponDef = weapon;

                var cap = weaponDef.HardPoint.Other.GridWeaponCap;
                if (gridWeaponCap == 0 && cap > 0) gridWeaponCap = cap;
                else if (cap > 0 && gridWeaponCap > 0 && cap < gridWeaponCap) gridWeaponCap = cap;

                weaponDef.HardPoint.DeviateShotAngle = MathHelper.ToRadians(weaponDef.HardPoint.DeviateShotAngle);

                var shrapnelNames = new HashSet<string>();
                for (int i = 0; i < weaponDef.Ammos.Length; i++)
                {
                    var ammo = weaponDef.Ammos[i];
                    if (!shrapnelNames.Contains(ammo.Shrapnel.AmmoRound) && !string.IsNullOrEmpty(ammo.Shrapnel.AmmoRound))
                        shrapnelNames.Add(ammo.Shrapnel.AmmoRound);
                }
                    

                var weaponAmmo = new WeaponAmmoTypes[weaponDef.Ammos.Length];
                for (int i = 0; i < weaponDef.Ammos.Length; i++)
                {
                    var ammo = weaponDef.Ammos[i];
                    var ammoDefId = new MyDefinitionId();
                    var ammoEnergy = ammo.AmmoMagazine == string.Empty || ammo.AmmoMagazine == "Energy";
                    foreach (var def in Session.AllDefinitions)
                        if (ammoEnergy && def.Id.SubtypeId.String == "Energy" || def.Id.SubtypeId.String == ammo.AmmoMagazine)
                            ammoDefId = def.Id;

                    Session.AmmoDefIds.Add(ammoDefId);
                    weaponAmmo[i] = new WeaponAmmoTypes { AmmoDef = ammo, AmmoDefinitionId = ammoDefId, AmmoName = ammo.AmmoRound, IsShrapnel = shrapnelNames.Contains(ammo.AmmoRound) };
                }

                var weaponIdHash = (tDef.Key + myElevationNameHash + myMuzzleNameHash + myAzimuthNameHash).GetHashCode();
                HashToId.Add(weaponIdHash, weaponId);
                WeaponSystems.Add(myMuzzleNameHash, new WeaponSystem(Session, myMuzzleNameHash, myAzimuthNameHash, myElevationNameHash, weaponDef, typeName, weaponAmmo, weaponIdHash, weaponId));
                weaponId++;
            }

            GridWeaponCap = gridWeaponCap;
            MuzzlePartNames = muzzlePartNames;
        }
    }

}
