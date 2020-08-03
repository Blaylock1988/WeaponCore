﻿using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Support;
using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI.Interfaces;
using VRage.Utils;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
using static WeaponCore.Support.WeaponComponent;
namespace WeaponCore.Platform
{
    public partial class Weapon
    {
        internal void Shoot() // Inlined due to keens mod profiler
        {
            try
            {
                var session = Comp.Session;
                var tick = session.Tick;
                var bps = System.Values.HardPoint.Loading.BarrelsPerShot;
                #region Prefire
                if (_ticksUntilShoot++ < System.DelayToFire) {

                    if (AvCapable && System.PreFireSound && !PreFiringEmitter.IsPlaying)
                        StartPreFiringSound();

                    if (ActiveAmmoDef.AmmoDef.Const.MustCharge || System.AlwaysFireFullBurst)
                        FinishBurst = true;

                    if (!PreFired) {

                        var nxtMuzzle = NextMuzzle;
                        for (int i = 0; i < bps; i++) {
                            _muzzlesToFire.Clear();
                            _muzzlesToFire.Add(MuzzleIdToName[NextMuzzle]);
                            if (i == bps) NextMuzzle++;
                            nxtMuzzle = (nxtMuzzle + (System.Values.HardPoint.Loading.SkipBarrels + 1)) % _numOfBarrels;
                        }

                        EventTriggerStateChanged(EventTriggers.PreFire, true, _muzzlesToFire);

                        PreFired = true;
                    }
                    return;
                }

                if (PreFired) {
                    EventTriggerStateChanged(EventTriggers.PreFire, false);
                    _muzzlesToFire.Clear();
                    PreFired = false;

                    if (AvCapable && System.PreFireSound && PreFiringEmitter.IsPlaying)
                        StopPreFiringSound(false);
                }

                #endregion

                #region weapon timing
                if (System.HasBarrelRotation) {

                    SpinBarrel();
                    if (BarrelRate < 9) {

                        if (_spinUpTick <= tick) {
                            BarrelRate++;
                            _spinUpTick = tick + _ticksBeforeSpinUp;
                        }
                        return;
                    }
                }
                
                if (ShootTick > tick)
                    return;

                if (LockOnFireState && (Target.Entity?.EntityId != Comp.Ai.Construct.Data.Repo.FocusData.Target[0] || Target.Entity?.EntityId != Comp.Ai.Construct.Data.Repo.FocusData.Target[1])) {
                    
                    MyEntity focusTarget;
                    if (!Comp.Ai.Construct.Focus.GetPriorityTarget(Comp.Ai, out focusTarget))
                        return;
                    
                    Target.LockTarget(this, focusTarget);
                }

                ShootTick = tick + TicksPerShot;
                Target.CheckTick = 0;

                if (!IsShooting) StartShooting();

                var burstDelay = (uint)System.Values.HardPoint.Loading.DelayAfterBurst;

                if (ActiveAmmoDef.AmmoDef.Const.BurstMode && ++ShotsFired > System.ShotsPerBurst) {
                    ShotsFired = 1;
                    EventTriggerStateChanged(EventTriggers.BurstReload, false);
                }
                else if (ActiveAmmoDef.AmmoDef.Const.HasShotReloadDelay && System.ShotsPerBurst > 0 && ++ShotsFired == System.ShotsPerBurst) {
                    ShotsFired = 0;
                    ShootTick = burstDelay > TicksPerShot ? tick + burstDelay : tick + TicksPerShot;
                }

                if (Comp.Ai.VelocityUpdateTick != tick) {
                    Comp.Ai.GridVel = Comp.Ai.MyGrid.Physics?.LinearVelocity ?? Vector3D.Zero;
                    Comp.Ai.IsStatic = Comp.Ai.MyGrid.Physics?.IsStatic ?? false;
                    Comp.Ai.VelocityUpdateTick = tick;
                }

                #endregion

                #region Projectile Creation
                var rnd = Comp.Data.Repo.Targets[WeaponId].WeaponRandom;
                var pattern = ActiveAmmoDef.AmmoDef.Pattern;

                FireCounter++;
                List<NewVirtual> vProList = null;
                var selfDamage = 0f;
                for (int i = 0; i < bps; i++) {

                    if (ActiveAmmoDef.AmmoDef.Const.Reloadable) {
                        if (State.CurrentAmmo == 0 && !ShootOnce) {
                            if (System.Session.MpActive && System.Session.IsServer)
                                System.Session.SendCompState(Comp);

                            Log.Line($"Shoot break");
                            break;
                        }
                        if (State.CurrentAmmo > 0) --State.CurrentAmmo;

                        if (System.HasEjector && ActiveAmmoDef.AmmoDef.Const.HasEjectEffect)  {
                            if (ActiveAmmoDef.AmmoDef.Ejection.SpawnChance >= 1 || rnd.TurretRandom.Next(0, 1) >= ActiveAmmoDef.AmmoDef.Ejection.SpawnChance)
                                SpawnEjection();
                        }
                    }

                    var current = NextMuzzle;
                    var muzzle = Muzzles[current];
                    if (muzzle.LastUpdateTick != tick) {
                        var dummy = Dummies[current];
                        var newInfo = dummy.Info;
                        muzzle.Direction = newInfo.Direction;
                        muzzle.Position = newInfo.Position;
                        muzzle.LastUpdateTick = tick;
                    }

                    if (ActiveAmmoDef.AmmoDef.Const.HasBackKickForce && !Comp.Ai.IsStatic && Comp.Session.IsServer)
                        Comp.Ai.MyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, -muzzle.Direction * ActiveAmmoDef.AmmoDef.BackKickForce, muzzle.Position, Vector3D.Zero);

                    if (PlayTurretAv) {
                        if (System.BarrelEffect1 && tick - muzzle.LastAv1Tick > System.Barrel1AvTicks && !muzzle.Av1Looping) {
                            muzzle.LastAv1Tick = tick;
                            muzzle.Av1Looping = System.Values.HardPoint.Graphics.Barrel1.Extras.Loop;
                            session.Av.AvBarrels1.Add(new AvBarrel { Weapon = this, Muzzle = muzzle, StartTick = tick });
                        }

                        if (System.BarrelEffect2 && tick - muzzle.LastAv2Tick > System.Barrel2AvTicks && !muzzle.Av2Looping) {
                            muzzle.LastAv2Tick = tick;
                            muzzle.Av2Looping = System.Values.HardPoint.Graphics.Barrel2.Extras.Loop;
                            session.Av.AvBarrels2.Add(new AvBarrel { Weapon = this, Muzzle = muzzle, StartTick = tick });
                        }
                    }

                    for (int j = 0; j < System.Values.HardPoint.Loading.TrajectilesPerBarrel; j++) {

                        if (System.Values.HardPoint.DeviateShotAngle > 0) {
                            var dirMatrix = Matrix.CreateFromDir(muzzle.Direction);
                            var rnd1 = rnd.TurretRandom.NextDouble();
                            var rnd2 = rnd.TurretRandom.NextDouble();
                            var randomFloat1 = (float)(rnd1 * (System.Values.HardPoint.DeviateShotAngle + System.Values.HardPoint.DeviateShotAngle) - System.Values.HardPoint.DeviateShotAngle);
                            var randomFloat2 = (float)(rnd2 * MathHelper.TwoPi);
                            rnd.TurretCurrentCounter += 2;
                            muzzle.DeviatedDir = Vector3.TransformNormal(-new Vector3D(MyMath.FastSin(randomFloat1) * MyMath.FastCos(randomFloat2), MyMath.FastSin(randomFloat1) * MyMath.FastSin(randomFloat2), MyMath.FastCos(randomFloat1)), dirMatrix);
                        }
                        else muzzle.DeviatedDir = muzzle.Direction;
                        var patternIndex = 1;

                        if (!pattern.Enable || !pattern.Random)
                            patternIndex = ActiveAmmoDef.AmmoDef.Const.PatternIndexCnt;
                        else {
                            if (pattern.TriggerChance >= rnd.TurretRandom.NextDouble() || pattern.TriggerChance >= 1) {
                                patternIndex = rnd.TurretRandom.Next(pattern.RandomMin, pattern.RandomMax);
                                rnd.TurretCurrentCounter += 2;
                            }
                            else
                                rnd.TurretCurrentCounter++;
                        }

                        if (pattern.Random) {
                            for (int w = 0; w < ActiveAmmoDef.AmmoDef.Const.PatternIndexCnt; w++) {
                                var y = rnd.TurretRandom.Next(w + 1);
                                ActiveAmmoDef.AmmoDef.Const.AmmoShufflePattern[w] = ActiveAmmoDef.AmmoDef.Const.AmmoShufflePattern[y];
                                ActiveAmmoDef.AmmoDef.Const.AmmoShufflePattern[y] = w;
                            }
                        }
                        for (int k = 0; k < patternIndex; k++) {

                            var ammoPattern = ActiveAmmoDef.AmmoDef.Const.AmmoPattern[ActiveAmmoDef.AmmoDef.Const.AmmoShufflePattern[k]];

                            selfDamage += ammoPattern.DecayPerShot;

                            long patternCycle = FireCounter;
                            if (ammoPattern.AmmoGraphics.Lines.Tracer.VisualFadeStart > 0 && ammoPattern.AmmoGraphics.Lines.Tracer.VisualFadeEnd > 0)
                                patternCycle = ((FireCounter - 1) % ammoPattern.AmmoGraphics.Lines.Tracer.VisualFadeEnd) + 1;

                            if (ammoPattern.Const.VirtualBeams && j == 0) {
                                if (i == 0) {
                                    vProList = System.Session.Projectiles.VirtInfoPools.Get();
                                    System.Session.Projectiles.NewProjectiles.Add(new NewProjectile { NewVirts = vProList, AmmoDef = ammoPattern, Muzzle = muzzle, PatternCycle = patternCycle, Weapon = this, Type = NewProjectile.Kind.Virtual });
                                }

                                MyEntity primeE = null;
                                MyEntity triggerE = null;

                                if (ammoPattern.Const.PrimeModel)
                                    primeE = ammoPattern.Const.PrimeEntityPool.Get();

                                if (ammoPattern.Const.TriggerModel)
                                    triggerE = session.TriggerEntityPool.Get();

                                float shotFade;
                                if (ammoPattern.Const.HasShotFade) {
                                    if (patternCycle > ammoPattern.AmmoGraphics.Lines.Tracer.VisualFadeStart)
                                        shotFade = MathHelper.Clamp(((patternCycle - ammoPattern.AmmoGraphics.Lines.Tracer.VisualFadeStart)) * ammoPattern.Const.ShotFadeStep, 0, 1);
                                    else if (System.DelayCeaseFire && CeaseFireDelayTick != tick)
                                        shotFade = MathHelper.Clamp(((tick - CeaseFireDelayTick) - ammoPattern.AmmoGraphics.Lines.Tracer.VisualFadeStart) * ammoPattern.Const.ShotFadeStep, 0, 1);
                                    else shotFade = 0;
                                }
                                else shotFade = 0;

                                var maxTrajectory = ammoPattern.Const.MaxTrajectoryGrows && FireCounter < ammoPattern.Trajectory.MaxTrajectoryTime ? ammoPattern.Const.TrajectoryStep * FireCounter : ammoPattern.Const.MaxTrajectory;
                                var info = session.Projectiles.VirtInfoPool.Get();
                                
                                info.AvShot = session.Av.AvShotPool.Get();
                                info.InitVirtual(this, ammoPattern, primeE, triggerE, muzzle, maxTrajectory, shotFade);
                                vProList.Add(new NewVirtual { Info = info, Rotate = !ammoPattern.Const.RotateRealBeam && i == _nextVirtual, Muzzle = muzzle, VirtualId = _nextVirtual });
                            }
                            else
                                System.Session.Projectiles.NewProjectiles.Add(new NewProjectile {AmmoDef = ammoPattern,  Muzzle = muzzle, PatternCycle = patternCycle, Weapon = this, Type = NewProjectile.Kind.Normal});
                        }
                    }
                    _muzzlesToFire.Add(MuzzleIdToName[current]);

                    if (ShootOnce) {
                        Log.Line($"WeaponShoot ShootOnce - State:{State.Action}");
                        if (System.Session.IsServer && ShootOnce)
                            State.WeaponMode(Comp, ShootActions.ShootOff);

                        ShootOnce = false;
                    }

                    if (HeatPShot > 0) {

                        if (!HeatLoopRunning) {
                            Comp.Session.FutureEvents.Schedule(UpdateWeaponHeat, null, 20);
                            HeatLoopRunning = true;
                        }

                        State.Heat += HeatPShot;
                        Comp.CurrentHeat += HeatPShot;
                        if (State.Heat >= System.MaxHeat) {

                            if (!Comp.Session.IsClient && Comp.Data.Repo.Set.Overload > 1) {
                                var dmg = .02f * Comp.MaxIntegrity;
                                Comp.Slim.DoDamage(dmg, MyDamageType.Environment, true, null, Comp.Ai.MyGrid.EntityId);
                            }
                            EventTriggerStateChanged(EventTriggers.Overheated, true);
                            State.Overheated = true;
                            break;
                        }
                    }

                    if (i == bps) NextMuzzle++;

                    NextMuzzle = (NextMuzzle + (System.Values.HardPoint.Loading.SkipBarrels + 1)) % _numOfBarrels;
                }
                #endregion

                #region Reload and Animation
                if (IsShooting)
                    EventTriggerStateChanged(state: EventTriggers.Firing, active: true, muzzles: _muzzlesToFire);

                if ((System.Session.IsServer && State.CurrentAmmo == 0 && !Session.ComputeServerStorage(this) || System.Session.IsClient && State.CurrentAmmo == 0 && State.CurrentMags == 0) && ActiveAmmoDef.AmmoDef.Const.BurstMode) {

                    if (ShotsFired == System.ShotsPerBurst) {
                        uint delay = 0;
                        FinishBurst = false;
                        if (System.WeaponAnimationLengths.TryGetValue(EventTriggers.Firing, out delay)) {

                            session.FutureEvents.Schedule(o => 
                            {
                                EventTriggerStateChanged(EventTriggers.BurstReload, true);
                                ShootTick = burstDelay > TicksPerShot ? tick + burstDelay + delay : tick + TicksPerShot + delay;
                                StopShooting();
                                Log.Line($"StopShooting ShotsPerBurst delayed");

                            }, null, delay);
                        }
                        else
                            EventTriggerStateChanged(EventTriggers.BurstReload, true);

                        if (IsShooting) {
                            ShootTick = burstDelay > TicksPerShot ? tick + burstDelay + delay : tick + TicksPerShot + delay;
                            StopShooting();
                            Log.Line($"StopShooting ShotsPerBurst immediate");
                        }

                        if (System.Values.HardPoint.Loading.GiveUpAfterBurst)
                            Target.Reset(Comp.Session.Tick, Target.States.FiredBurst);
                    }
                    else if (System.AlwaysFireFullBurst && ShotsFired < System.ShotsPerBurst)
                        FinishBurst = true;
                }

                _muzzlesToFire.Clear();

                if (System.Session.IsServer && selfDamage > 0)
                    ((IMyDestroyableObject)Comp.MyCube.SlimBlock).DoDamage(selfDamage, MyDamageType.Grind, true, null, Comp.MyCube.EntityId);

                #endregion
                _nextVirtual = _nextVirtual + 1 < bps ? _nextVirtual + 1 : 0;
            }
            catch (Exception e) { Log.Line($"Error in shoot: {e}"); }
        }

        private void SpawnEjection()
        {
            var eInfo = Ejector.Info;

            if (ActiveAmmoDef.AmmoDef.Ejection.Type == WeaponDefinition.AmmoDef.AmmoEjectionDef.SpawnType.Item)
                MyFloatingObjects.Spawn(ActiveAmmoDef.AmmoDef.Const.EjectItem, eInfo.Position, eInfo.Direction, MyPivotUp, null, EjectionSpawnCallback);
            else if (System.Session.HandlesInput)
            {
                var particle = ActiveAmmoDef.AmmoDef.AmmoGraphics.Particles.Eject;
                MyParticleEffect ejectEffect;
                var matrix = MatrixD.CreateTranslation(eInfo.Position);
                if (MyParticlesManager.TryCreateParticleEffect(particle.Name, ref matrix, ref eInfo.Position, uint.MaxValue, out ejectEffect))
                {
                    ejectEffect.UserColorMultiplier = particle.Color;
                    var scaler = 1;
                    ejectEffect.UserRadiusMultiplier = particle.Extras.Scale * scaler;
                    var scale = particle.ShrinkByDistance ? MathHelper.Clamp(MathHelper.Lerp(1, 0, Vector3D.Distance(System.Session.CameraPos, eInfo.Position) / particle.Extras.MaxDistance), 0.05f, 1) : 1;
                    ejectEffect.UserScale = (float)scale * scaler;
                    ejectEffect.Velocity = eInfo.Direction * ActiveAmmoDef.AmmoDef.Ejection.Speed;
                }
            }
        }

        private void EjectionSpawnCallback(MyEntity entity)
        {
            var ejectDef = ActiveAmmoDef.AmmoDef.Ejection;
            
            if (ejectDef.Speed > 0) {
                var delay = ejectDef.CompDef.Delay;
                if (delay <=0)
                    SetSpeed(entity);
                else
                    System.Session.FutureEvents.Schedule(SetSpeed, entity, (uint)(System.Session.Tick + delay));
            }

            if (ejectDef.CompDef.ItemLifeTime > 0)
                System.Session.FutureEvents.Schedule(RemoveEjection, entity, (uint)(System.Session.Tick + ejectDef.CompDef.ItemLifeTime));
        }

        private void SetSpeed(object o)
        {
            var entity = (MyEntity)o;
            var ejectDef = ActiveAmmoDef.AmmoDef.Ejection;
            entity.Physics.SetSpeeds(Ejector.CachedDir * (ejectDef.Speed * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS), Vector3.Zero);
        }

        private static void RemoveEjection(object o)
        {
            var entity = (MyEntity) o;
            
            if (entity != null) {
                using (entity.Pin())  {
                    if (!entity.MarkedForClose && !entity.Closed)
                        entity.Close();
                }
            }
        }
    }
}