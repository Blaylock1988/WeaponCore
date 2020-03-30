﻿using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRageMath;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Platform.Weapon;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;
using static WeaponCore.Support.WeaponDefinition.TargetingDef;

namespace WeaponCore
{
    public partial class Session
    {

        #region Packet Creation Methods

        internal struct PacketInfo
        {
            internal MyEntity Entity;
            internal Packet Packet;
            internal bool SingleClient;
        }

        internal class ErrorPacket
        {
            internal uint RecievedTick;
            internal uint RetryTick;
            internal uint RetryDelayTicks;
            internal int RetryAttempt;
            internal int MaxAttempts;
            internal string Error;
            internal PacketType PType;
            internal Packet Packet;

            public virtual bool Equals(ErrorPacket other)
            {
                if (Packet == null) return false;

                return Packet.Equals(other.Packet);
            }

            public override bool Equals(object obj)
            {
                if (Packet == null) return false;
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ErrorPacket)obj);
            }

            public override int GetHashCode()
            {
                if (Packet == null) return 0;

                return Packet.GetHashCode();
            }
        }

        internal void SendMouseUpdate(MyEntity entity)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new MouseInputPacket
                {
                    EntityId = entity.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ClientMouseEvent,
                    Data = UiInput.ClientMouseState
                });
            }
            else if (MpActive && IsServer)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = entity,
                    Packet = new MouseInputPacket
                    {
                        EntityId = entity.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.ClientMouseEvent,
                        Data = UiInput.ClientMouseState
                    }
                });
            }
        }

        internal void SendActiveControlUpdate(MyCubeBlock controlBlock, bool active)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new BoolUpdatePacket
                {
                    EntityId = controlBlock.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ActiveControlUpdate,
                    Data = active
                });
            }
            else
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = controlBlock,
                    Packet = new BoolUpdatePacket
                    {
                        EntityId = controlBlock.EntityId,
                        SenderId = 0,
                        PType = PacketType.ActiveControlUpdate,
                        Data = active
                    }
                });
            }
        }

        internal void SendCycleAmmoNetworkUpdate(Weapon weapon, int ammoId)
        {
            weapon.Comp.SyncIds.MIds[(int)PacketType.CycleAmmo]++;
            if (IsClient)
            {
                PacketsToServer.Add(new CycleAmmoPacket
                {
                    EntityId = weapon.Comp.MyCube.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.CycleAmmo,
                    AmmoId = ammoId,
                    MId = weapon.Comp.SyncIds.MIds[(int)PacketType.CycleAmmo],
                    WeaponId = weapon.WeaponId
                });
            }
            else
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = weapon.Comp.MyCube,
                    Packet = new CycleAmmoPacket
                    {
                        EntityId = weapon.Comp.MyCube.EntityId,
                        SenderId = 0,
                        PType = PacketType.CycleAmmo,
                        AmmoId = ammoId,
                        MId = weapon.Comp.SyncIds.MIds[(int)PacketType.CycleAmmo],
                        WeaponId = weapon.WeaponId
                    }
                });
            }
        }

        internal void SendOverRidesUpdate(GridAi ai, string groupName, GroupOverrides overRides)
        {
            if (MpActive)
            {
                ai.UiMId++;
                if (IsClient)
                {
                    PacketsToServer.Add(new OverRidesPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        MId = ai.UiMId,
                        GroupName = groupName,
                        PType = PacketType.OverRidesUpdate,
                        Data = overRides,
                    });
                }
                else if (IsServer)
                {
                    PacketsToClient.Add(new PacketInfo
                    {
                        Entity = ai.MyGrid,
                        Packet = new OverRidesPacket
                        {
                            EntityId = ai.MyGrid.EntityId,
                            SenderId = 0,
                            MId = ai.UiMId,
                            GroupName = groupName,
                            PType = PacketType.OverRidesUpdate,
                            Data = overRides,
                        }
                    });
                }
            }
        }

        internal void SendOverRidesUpdate(WeaponComponent comp, GroupOverrides overRides)
        {
            if (MpActive)
            {
                comp.SyncIds.MIds[(int)PacketType.OverRidesUpdate]++;
                if (IsClient)
                {
                    PacketsToServer.Add(new OverRidesPacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        SenderId = MultiplayerId,
                        MId = comp.SyncIds.MIds[(int)PacketType.OverRidesUpdate],
                        PType = PacketType.OverRidesUpdate,
                        Data = comp.Set.Value.Overrides,
                    });
                }
                else
                {
                    PacketsToClient.Add(new PacketInfo
                    {
                        Entity = comp.MyCube,
                        Packet = new OverRidesPacket
                        {
                            EntityId = comp.MyCube.EntityId,
                            SenderId = 0,
                            MId = comp.SyncIds.MIds[(int)PacketType.OverRidesUpdate],
                            PType = PacketType.OverRidesUpdate,
                            Data = comp.Set.Value.Overrides,
                        }
                    });
                }
            }
        }

        internal void SendActionShootUpdate(WeaponComponent comp, TerminalActionState state, int weaponId = -1)
        {

            var mId = 0u;

            if (weaponId == -1)
            {
                comp.SyncIds.MIds[(int)PacketType.CompToolbarShootState]++;
                mId = comp.SyncIds.MIds[(int)PacketType.CompToolbarShootState];
            }
            else
            {
                comp.SyncIds.MIds[(int)PacketType.WeaponToolbarShootState]++;
                mId = comp.SyncIds.MIds[(int)PacketType.WeaponToolbarShootState];
            }

            if (IsClient)
            {
                comp.Session.PacketsToServer.Add(new ShootStatePacket
                {
                    EntityId = comp.MyCube.EntityId,
                    SenderId = comp.Session.MultiplayerId,
                    PType = weaponId == -1 ? PacketType.CompToolbarShootState : PacketType.WeaponToolbarShootState,
                    MId = mId,
                    Data = state,
                    WeaponId = weaponId,
                });
            }
            else if (HandlesInput)
            {
                comp.Session.PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new ShootStatePacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        SenderId = comp.Session.MultiplayerId,
                        PType = weaponId == -1 ? PacketType.CompToolbarShootState : PacketType.WeaponToolbarShootState,
                        MId = mId,
                        Data = state,
                        WeaponId = weaponId,
                    }
                });
            }
        }

        internal void SendRangeUpdate(WeaponComponent comp, float range)
        {
            comp.SyncIds.MIds[(int)PacketType.RangeUpdate]++;

            if (IsClient)
            {
                comp.Session.PacketsToServer.Add(new RangePacket
                {
                    EntityId = comp.MyCube.EntityId,
                    SenderId = comp.Session.MultiplayerId,
                    PType = PacketType.RangeUpdate,
                    MId = comp.SyncIds.MIds[(int)PacketType.RangeUpdate],
                    Data = range,
                });
            }
            else if (HandlesInput)
            {
                comp.Session.PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new RangePacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        SenderId = comp.Session.MultiplayerId,
                        PType = PacketType.RangeUpdate,
                        MId = comp.SyncIds.MIds[(int)PacketType.RangeUpdate],
                        Data = range,
                    }
                });
            }
        }

        internal void SendControlingPlayer(WeaponComponent comp)
        {
            comp.SyncIds.MIds[(int)PacketType.PlayerControlUpdate]++;
            if (IsClient)
            {
                PacketsToServer.Add(new ControllingPlayerPacket
                {
                    EntityId = comp.MyCube.EntityId,
                    SenderId = MultiplayerId,
                    MId = comp.SyncIds.MIds[(int)PacketType.PlayerControlUpdate],
                    PType = PacketType.PlayerControlUpdate,
                    Data = comp.State.Value.CurrentPlayerControl,
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new ControllingPlayerPacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        SenderId = 0,
                        MId = comp.SyncIds.MIds[(int)PacketType.PlayerControlUpdate],
                        PType = PacketType.PlayerControlUpdate,
                        Data = comp.State.Value.CurrentPlayerControl,
                    }
                });
            }
        }
        
        internal void SendFakeTargetUpdate(GridAi ai, Vector3 hitPos)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FakeTargetPacket
                {
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = ai.Session.MultiplayerId,
                    PType = PacketType.FakeTargetUpdate,
                    Data = hitPos,
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new FakeTargetPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = ai.Session.MultiplayerId,
                        PType = PacketType.FakeTargetUpdate,
                        Data = hitPos,
                    }
                });
            }
        }
        
        internal void SendCompStateUpdate(WeaponComponent comp)
        {
            comp.SyncIds.MIds[(int)PacketType.CompStateUpdate]++;

            if (IsClient)// client, send settings to server
            {
                PacketsToServer.Add(new StatePacket
                {
                    EntityId = comp.MyCube.EntityId,
                    PType = PacketType.CompStateUpdate,
                    SenderId = MultiplayerId,
                    Data = comp.State.Value,
                    MId = comp.SyncIds.MIds[(int)PacketType.CompStateUpdate]
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new StatePacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        SenderId = 0,
                        PType = PacketType.CompStateUpdate,
                        Data = comp.State.Value
                    }
                });
            }
        }

        internal void SendCompSettingUpdate(WeaponComponent comp)
        {
            comp.SyncIds.MIds[(int)PacketType.CompSettingsUpdate]++;

            if (IsClient)// client, send settings to server
            {
                PacketsToServer.Add(new SettingPacket
                {
                    EntityId = comp.MyCube.EntityId,
                    PType = PacketType.CompSettingsUpdate,
                    SenderId = MultiplayerId,
                    Data = comp.Set.Value,
                    MId = comp.SyncIds.MIds[(int)PacketType.CompSettingsUpdate]
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new SettingPacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        SenderId = 0,
                        PType = PacketType.CompSettingsUpdate,
                        Data = comp.Set.Value,
                        MId = comp.SyncIds.MIds[(int)PacketType.CompSettingsUpdate]
                    }
                });
            }
        }

        internal void SendUpdateRequest(long entityId, PacketType ptype)
        {
            PacketsToServer.Add(new Packet
            {
                EntityId = entityId,
                SenderId = MultiplayerId,
                PType = ptype
            });
        }
        
        internal void SendTrackReticleUpdate(WeaponComponent comp)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new BoolUpdatePacket
                {
                    EntityId = comp.MyCube.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ReticleUpdate,
                    Data = comp.TrackReticle
                });
            }
            else if (IsServer)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = comp.MyCube,
                    Packet = new BoolUpdatePacket
                    {
                        EntityId = comp.MyCube.EntityId,
                        PType = PacketType.ReticleUpdate,
                        Data = comp.TrackReticle
                    }
                });
            }
        }

        internal void SendPlayerConnectionUpdate(long id, bool connected)
        {
            PacketsToClient.Add(new PacketInfo
            {
                Entity = null,
                Packet = new BoolUpdatePacket
                {
                    EntityId = id,
                    SenderId = 0,
                    PType = PacketType.PlayerIdUpdate,
                    Data = connected
                }
            });
        }
        
        internal void SendTargetExpiredUpdate(WeaponComponent comp, int weaponId)
        {
            PacketsToClient.Add(new PacketInfo
            {
                Entity = comp.MyCube,
                Packet = new WeaponIdPacket
                {
                    EntityId = comp.MyCube.EntityId,
                    SenderId = 0,
                    PType = PacketType.TargetExpireUpdate,
                    WeaponId = weaponId,
                }
            });
        }

        #region AIFocus packets
        internal void SendFocusTargetUpdate(GridAi ai, long targetId)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FocusPacket
                {
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.FocusUpdate,
                    TargetId = targetId
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new FocusPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.FocusUpdate,
                        TargetId = targetId
                    }
                });
            }
        }

        internal void SendReassignTargetUpdate(GridAi ai, long targetId, int focusId)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FocusPacket
                {
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ReassignTargetUpdate,
                    TargetId = targetId,
                    FocusId = focusId
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new FocusPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.ReassignTargetUpdate,
                        TargetId = targetId,
                        FocusId = focusId
                    }
                });
            }
        }

        internal void SendNextActiveUpdate(GridAi ai, bool addSecondary)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FocusPacket
                {
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.NextActiveUpdate,
                    AddSecondary = addSecondary
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new FocusPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.NextActiveUpdate,
                        AddSecondary = addSecondary
                    }
                });
            }
        }

        internal void SendReleaseActiveUpdate(GridAi ai)
        {
            if (IsClient)
            {
                PacketsToServer.Add(new FocusPacket
                {
                    EntityId = ai.MyGrid.EntityId,
                    SenderId = MultiplayerId,
                    PType = PacketType.ReleaseActiveUpdate
                });
            }
            else if (HandlesInput)
            {
                PacketsToClient.Add(new PacketInfo
                {
                    Entity = ai.MyGrid,
                    Packet = new FocusPacket
                    {
                        EntityId = ai.MyGrid.EntityId,
                        SenderId = MultiplayerId,
                        PType = PacketType.ReleaseActiveUpdate
                    }
                });
            }
        }
        #endregion
        #endregion


        #region Misc Network Methods
        internal void SyncWeapon(Weapon weapon, WeaponTimings timings, ref WeaponSyncValues weaponData, bool setState = true)
        {
            var comp = weapon.Comp;
            var cState = comp.State.Value;
            var wState = weapon.State;

            var wasReloading = wState.Sync.Reloading;

            if (setState)
            {
                comp.CurrentHeat -= weapon.State.Sync.Heat;
                cState.CurrentCharge -= weapon.State.Sync.CurrentCharge;


                weaponData.SetState(wState.Sync);

                comp.CurrentHeat += weapon.State.Sync.Heat;
                cState.CurrentCharge += weapon.State.Sync.CurrentCharge;
            }

            comp.WeaponValues.Timings[weapon.WeaponId] = timings;
            weapon.Timings = timings;

            var hasMags = weapon.State.Sync.CurrentMags > 0 || IsCreative;
            var hasAmmo = weapon.State.Sync.CurrentAmmo > 0;

            var canReload = weapon.CanReload;

            if (!canReload)
                weapon.ActiveAmmoDef = weapon.System.WeaponAmmoTypes[weapon.Set.AmmoTypeId];

            if (canReload)
                weapon.StartReload();

            else if (wasReloading && !weapon.State.Sync.Reloading && hasAmmo)
            {
                if (!weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge && weapon.ReloadSubscribed)
                {
                    weapon.ReloadSubscribed = false;
                    weapon.CancelableReloadAction -= weapon.Reloaded;
                }

                weapon.EventTriggerStateChanged(EventTriggers.Reloading, false);
            }
            else if (wasReloading && weapon.State.Sync.Reloading && !weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge)
            {
                if (weapon.ReloadSubscribed)
                {
                    weapon.ReloadSubscribed = false;
                    weapon.CancelableReloadAction -= weapon.Reloaded;
                }

                comp.Session.FutureEvents.Schedule(weapon.Reloaded, null, weapon.Timings.ReloadedTick);
            }

            else if (weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge && weapon.State.Sync.Reloading && !comp.Session.ChargingWeaponsCheck.ContainsKey(weapon))
                weapon.ChargeReload(true);

            if (weapon.State.Sync.Heat > 0 && !weapon.HeatLoopRunning)
            {
                weapon.HeatLoopRunning = true;
                var delay = weapon.Timings.LastHeatUpdateTick > 0 ? weapon.Timings.LastHeatUpdateTick : 20;
                comp.Session.FutureEvents.Schedule(weapon.UpdateWeaponHeat, null, delay);
            }
        }

        public void UpdateActiveControlDictionary(MyCubeBlock block, long playerId, bool updateAdd)
        {
            var grid = block?.CubeGrid;

            if (block == null || grid == null) return;
            GridAi trackingAi;
            if (updateAdd) //update/add
            {
                if (GridTargetingAIs.TryGetValue(grid, out trackingAi))
                {
                    trackingAi.ControllingPlayers[playerId] = block;
                    trackingAi.ControllingPlayers.ApplyAdditionsAndModifications();
                }
            }
            else //remove
            {
                if (GridTargetingAIs.TryGetValue(grid, out trackingAi))
                    trackingAi.ControllingPlayers.TryGetValue(playerId, out block);
            }
        }

        internal static void SyncGridOverrides(GridAi ai, string groupName, GroupOverrides o)
        {
            ai.BlockGroups[groupName].Settings["Active"] = o.Activate ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Neutrals"] = o.Neutrals ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Projectiles"] = o.Projectiles ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Biologicals"] = o.Biologicals ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Meteors"] = o.Meteors ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Friendly"] = o.Friendly ? 1 : 0;
            ai.BlockGroups[groupName].Settings["Unowned"] = o.Unowned ? 1 : 0;
            ai.BlockGroups[groupName].Settings["TargetPainter"] = o.TargetPainter ? 1 : 0;
            ai.BlockGroups[groupName].Settings["ManualControl"] = o.ManualControl ? 1 : 0;
            ai.BlockGroups[groupName].Settings["FocusTargets"] = o.FocusTargets ? 1 : 0;
            ai.BlockGroups[groupName].Settings["FocusSubSystem"] = o.FocusSubSystem ? 1 : 0;
            ai.BlockGroups[groupName].Settings["SubSystems"] = (int)o.SubSystem;
        }

        internal static GroupOverrides GetOverrides(GridAi ai, string groupName)
        {
            var o = new GroupOverrides();
            o.Activate = ai.BlockGroups[groupName].Settings["Active"] == 1 ? true : false;
            o.Neutrals = ai.BlockGroups[groupName].Settings["Neutrals"] == 1 ? true : false;
            o.Projectiles = ai.BlockGroups[groupName].Settings["Projectiles"] == 1 ? true : false;
            o.Biologicals = ai.BlockGroups[groupName].Settings["Biologicals"] == 1 ? true : false;
            o.Meteors = ai.BlockGroups[groupName].Settings["Meteors"] == 1 ? true : false;
            o.Friendly = ai.BlockGroups[groupName].Settings["Friendly"] == 1 ? true : false;
            o.Unowned = ai.BlockGroups[groupName].Settings["Unowned"] == 1 ? true : false;
            o.TargetPainter = ai.BlockGroups[groupName].Settings["TargetPainter"] == 1 ? true : false;
            o.ManualControl = ai.BlockGroups[groupName].Settings["ManualControl"] == 1 ? true : false;
            o.FocusTargets = ai.BlockGroups[groupName].Settings["FocusTargets"] == 1 ? true : false;
            o.FocusSubSystem = ai.BlockGroups[groupName].Settings["FocusSubSystem"] == 1 ? true : false;
            o.SubSystem = (BlockTypes)ai.BlockGroups[groupName].Settings["SubSystems"];

            return o;
        }
        #endregion
    }
}
