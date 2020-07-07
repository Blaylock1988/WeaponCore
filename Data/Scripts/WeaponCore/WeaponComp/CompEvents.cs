﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using static WeaponCore.Platform.MyWeaponPlatform;
using static WeaponCore.Session;
using static WeaponCore.Support.WeaponDefinition.AnimationDef.PartAnimationSetDef;

namespace WeaponCore.Support
{
    public partial class WeaponComponent
    {
        internal void RegisterEvents(bool register = true)
        {
            if (register)
            {
                if (Registered)
                    Log.Line($"Comp RegisterEvents error");
                //TODO change this
                Registered = true;
                TerminalBlock.AppendingCustomInfo += AppendingCustomInfo;

                MyCube.IsWorkingChanged += IsWorkingChanged;

                MyCube.OnMarkForClose += OnMarkForClose;
                IsWorkingChanged(MyCube);

                if (BlockInventory == null) Log.Line($"BlockInventory is null");
                else
                {
                    BlockInventory.InventoryContentChanged += OnContentsChanged;
                    Session.BlockInventoryItems[BlockInventory] = new ConcurrentDictionary<uint, BetterInventoryItem>();
                    Session.AmmoThreadItemList[BlockInventory] = new List<BetterInventoryItem>();

                    var items = BlockInventory.GetItems();
                    for (int i = 0; i < items.Count; i++)
                    {
                        var bItem = Session.BetterInventoryItems.Count > 0 ? Session.BetterInventoryItems.Pop() : new BetterInventoryItem();
                        bItem.Amount = (int)items[i].Amount;
                        bItem.ItemId = items[i].ItemId;
                        bItem.Content = items[i].Content;

                        Session.BlockInventoryItems[BlockInventory][items[i].ItemId] = bItem;
                    }
                }
            }
            else
            {
                if (!Registered)
                    Log.Line($"Comp UnRegisterEvents error");

                if (Registered)
                {
                    //TODO change this
                    Registered = false;
                    TerminalBlock.AppendingCustomInfo -= AppendingCustomInfo;

                    MyCube.IsWorkingChanged -= IsWorkingChanged;
                    MyCube.OnMarkForClose -= OnMarkForClose;

                    if (BlockInventory == null) Log.Line($"BlockInventory is null");
                    else
                    {
                        BlockInventory.InventoryContentChanged -= OnContentsChanged;
                        ConcurrentDictionary<uint, BetterInventoryItem> removedItems;
                        List<BetterInventoryItem> removedList;

                        if (Session.BlockInventoryItems.TryRemove(BlockInventory, out removedItems))
                        {
                            foreach (var inventoryItems in removedItems)
                                Session.BetterInventoryItems.Push(inventoryItems.Value);

                            removedItems.Clear();
                        }

                        if (Session.AmmoThreadItemList.TryRemove(BlockInventory, out removedList))
                            removedList.Clear();
                    }
                }
            }
        }

        private void OnContentsChanged(MyInventoryBase inv, MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            if (!Registered) return;

            BetterInventoryItem cachedItem;

            if (!Session.BlockInventoryItems[BlockInventory].TryGetValue(item.ItemId, out cachedItem))
            {
                cachedItem = Session.BetterInventoryItems.Count > 0 ? Session.BetterInventoryItems.Pop() : new BetterInventoryItem();
                cachedItem.Amount = (int)amount;
                cachedItem.Content = item.Content;
                cachedItem.ItemId = item.ItemId;
                Session.BlockInventoryItems[BlockInventory].TryAdd(item.ItemId, cachedItem);
            }
            else if (cachedItem.Amount + amount > 0)
                cachedItem.Amount += (int)amount;
            else if (cachedItem.Amount + amount <= 0)
            {
                BetterInventoryItem removedItem;
                if (Session.BlockInventoryItems[BlockInventory].TryRemove(item.ItemId, out removedItem))
                    Session.BetterInventoryItems.Push(removedItem);
            }

            for (int i = 0; i < Platform.Weapons.Length; i++)
            {
                var w = Platform.Weapons[i];

                if (!Session.IsCreative && !w.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo && w.ActiveAmmoDef.AmmoDefinitionId == item.Content.GetId())
                {
                    if (amount < 0)
                        ComputeStorage(w);
                    else
                        w.Reload();
                }
            }
        }

        private void OnMarkForClose(MyEntity myEntity)
        {
            var cube = (MyCubeBlock)myEntity;
            
            var comp = cube.Components.Get<WeaponComponent>();
            if (comp.Slim == comp.Ai.FakeShipController.SlimBlock)
            {
                comp.Ai.PowerDirty = true;
            }
        }

        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            try {

                var wasFunctional = IsFunctional;
                IsFunctional = myCubeBlock.IsFunctional;

                if (Platform.State == PlatformState.Incomplete) {
                    Log.Line($"Init on Complete");
                    Init();
                }
                else {

                    if (!wasFunctional && IsFunctional && IsWorkingChangedTick > 0)
                        Status = Start.ReInit;
                    IsWorking = myCubeBlock.IsWorking;
                    if (MyCube.ResourceSink.CurrentInputByType(GId) < 0) Log.Line($"IsWorking:{IsWorking}(was:{wasFunctional}) - Func:{IsFunctional} - GridAvailPow:{Ai.GridAvailablePower} - SinkPow:{SinkPower} - SinkReq:{MyCube.ResourceSink.RequiredInputByType(GId)} - SinkCur:{MyCube.ResourceSink.CurrentInputByType(GId)}");

                    if (!IsWorking && Registered) {
                        foreach (var w in Platform.Weapons)
                            w.StopShooting();
                    }
                    IsWorkingChangedTick = Session.Tick;
                }

                if (wasFunctional && !IsFunctional && Platform.State == PlatformState.Ready) {

                    for (int i = 0; i < Platform.Weapons.Length; i++) {

                        var w = Platform.Weapons[i];
                        PartAnimation[] partArray;
                        if (w.AnimationsSet.TryGetValue(EventTriggers.TurnOff, out partArray)) {
                            for (int j = 0; j < partArray.Length; j++) 
                                w.PlayEmissives(partArray[j]);
                        }
                        if (!Session.IsClient && !IsWorking) 
                            w.Target.Reset(Session.Tick, Target.States.Offline);
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in IsWorkingChanged: {ex}"); }
        }

        internal string GetSystemStatus()
        {
            if (!MyCube.IsFunctional) return "[Fault]";
            if (!MyCube.IsWorking) return "[Offline]";
            return "[Online]";
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var status = GetSystemStatus();

                stringBuilder.Append(status + 
                    "\nConstruct DPS: " + Ai.EffectiveDps.ToString("0.0") +
                    "\nShotsPerSec: " + ShotsPerSec.ToString("0.000") +
                    "\n" +
                    "\nRealDps: " + EffectiveDps.ToString("0.0") +
                    "\nPeakDps: " + PeakDps.ToString("0.0") +
                    "\nBaseDps: " + BaseDps.ToString("0.0") +
                    "\nAreaDps: " + AreaDps.ToString("0.0") +
                    "\nExplode: " + DetDps.ToString("0.0") +
                    "\nCurrent: " + CurrentDps.ToString("0.0") +" ("+ (CurrentDps/ PeakDps).ToString("P") + ")");

                if (HeatPerSecond > 0)
                    stringBuilder.Append("\n__________________________________" +
                    "\nHeat Generated / s: " + HeatPerSecond.ToString("0.0") + " W" +
                    "\nHeat Dissipated / s: " + HeatSinkRate.ToString("0.0") + " W" +
                    "\nCurrent Heat: " +CurrentHeat.ToString("0.0") + " j (" + (CurrentHeat / MaxHeat).ToString("P")+")");

                if (HeatPerSecond > 0 && HasEnergyWeapon)
                    stringBuilder.Append("\n__________________________________");

                if (HasEnergyWeapon)
                {
                    stringBuilder.Append("\nCurrent Draw: " + SinkPower.ToString("0.00") + " MWs");
                    if(HasChargeWeapon) stringBuilder.Append("\nCurrent Charge: " + CurrentCharge.ToString("0.00") + " MWs");
                    stringBuilder.Append("\nRequired Power: " + MaxRequiredPower.ToString("0.00") + " MWs");
                }
                
                stringBuilder.Append("\n\n==== Weapons ====");

                var weaponCnt = Platform.Weapons.Length;
                for (int i = 0; i < weaponCnt; i++)
                {
                    var w = Platform.Weapons[i];
                    string shots;
                    if (w.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo)
                    {
                        shots = "\nCharging:" + w.Charging;
                    }
                    else shots = "\n" + w.ActiveAmmoDef.AmmoDef.AmmoMagazine + ": " + w.State.CurrentAmmo;

                    var burst = w.ActiveAmmoDef.AmmoDef.Const.BurstMode ? "\nBurst: " + w.ShotsFired + "(" + w.System.ShotsPerBurst + ") - Delay: " + w .System.Values.HardPoint.Loading.DelayAfterBurst : string.Empty;

                    var endReturn = i + 1 != weaponCnt ? "\n" : string.Empty;

                    stringBuilder.Append("\nName: " + w.System.WeaponName + shots + burst + "\nReloading: " + w.Reloading + endReturn);
                }


                stringBuilder.Append("\n\n** Use Weapon Wheel Menu\n** to control weapons using\n** MMB outside of this terminal");
                if (Debug)
                {
                    foreach (var weapon in Platform.Weapons)
                    {
                        stringBuilder.Append($"\n\nWeapon: {weapon.System.WeaponName} - Enabled: {IsWorking && weapon.Comp.Data.Repo.Set.Overrides.Activate}");
                        stringBuilder.Append($"\nTargetState: {weapon.Target.CurrentState} - Manual: {weapon.Comp.UserControlled || weapon.Target.IsFakeTarget}");
                        stringBuilder.Append($"\nEvent: {weapon.LastEvent} - Ammo :{!weapon.NoMagsToLoad}");
                        stringBuilder.Append($"\nOverHeat: {weapon.State.Overheated} - Shooting: {weapon.IsShooting}");
                        stringBuilder.Append($"\nisAligned: {weapon.Target.IsAligned}");
                        stringBuilder.Append($"\nCanShoot: {weapon.ShotReady} - Charging: {weapon.Charging}");
                        stringBuilder.Append($"\nAiShooting: {weapon.AiShooting} - lastCheck: {weapon.Comp.Session.Tick - weapon.Target.CheckTick}");
                        stringBuilder.Append($"\n{(weapon.ActiveAmmoDef.AmmoDef.Const.EnergyAmmo ? "ChargeSize: " + weapon.ActiveAmmoDef.AmmoDef.Const.ChargSize.ToString() : "MagSize: " +  weapon.ActiveAmmoDef.AmmoDef.Const.MagazineSize.ToString())} - CurrentCharge: {CurrentCharge}({weapon.State.CurrentCharge})");
                        stringBuilder.Append($"\nChargeTime: {weapon.ChargeUntilTick}({weapon.Comp.Ai.Session.Tick}) - Delay: {weapon.ChargeDelayTicks}");
                        stringBuilder.Append($"\nCharging: {weapon.Charging}({weapon.ActiveAmmoDef.AmmoDef.Const.MustCharge}) - Delay: {weapon.ChargeDelayTicks}");
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Weapon AppendingCustomInfo: {ex}"); }
        }
    }
}
