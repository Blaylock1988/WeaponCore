﻿using System.Collections.Generic;
using Sandbox.Game.Entities;
using WeaponCore.Platform;
using WeaponCore.Support;
using static WeaponCore.Support.GridAi;
namespace WeaponCore
{
    public partial class Session
    {
        public void ReproccessClientErrorPackets()
        {
            foreach (var errorPacket in ClientSideErrorPkt)
            {
                if (errorPacket == null) {
                    Log.Line($"ClientSideErrorPktListNew errorPacket is null");
                    continue;
                }

                if (errorPacket.MaxAttempts == 0)
                {
                    //set packet retry variables, based on type
                    //erroredPacket.MaxAttempts = 3;
                    //erroredPacket.RetryAttempt = 0;                    

                    switch (errorPacket.PType)
                    {
                        case PacketType.WeaponSyncUpdate:
                            errorPacket.MaxAttempts = 7;
                            errorPacket.RetryDelayTicks = 15;
                            break;

                        default:
                            errorPacket.MaxAttempts = 7;
                            errorPacket.RetryDelayTicks = 15;
                            break;
                    }

                    errorPacket.RetryTick = Tick + errorPacket.RetryDelayTicks;
                }

                //proccess packet logic

                if (errorPacket.RetryTick > Tick) continue;
                errorPacket.RetryAttempt++;

                var success = false;

                switch (errorPacket.PType)
                {
                    case PacketType.WeaponSyncUpdate:
                        var ent = MyEntities.GetEntityByIdOrDefault(errorPacket.Packet.EntityId);
                        if (ent == null) break;

                        var packet = errorPacket.Packet as GridWeaponPacket;
                        if (packet?.Data == null) {
                            Log.Line($"GridWeaponPacket errorPacket is null");
                            errorPacket.MaxAttempts = 0;
                            break;
                        }

                        var compsToCheck = new HashSet<long>();
                        for (int j = 0; j < packet.Data.Count; j++) {
                            var weaponData = packet.Data[j];
                            if (weaponData != null)
                                compsToCheck.Add(weaponData.CompEntityId);
                        }

                        if (compsToCheck.Count > 0)
                        {
                            PacketsToServer.Add(new RequestTargetsPacket
                            {
                                EntityId = errorPacket.Packet.EntityId,
                                SenderId = MultiplayerId,
                                PType = PacketType.WeaponUpdateRequest,
                                Comps = new List<long>(compsToCheck),
                            });
                            success = true;
                        }
                        break;

                    default:
                        var report = Reporter.ReportPool.Get();
                        report.Receiver = NetworkReporter.Report.Received.Client;
                        report.PacketSize = 0;
                        Reporter.ReportData[errorPacket.Packet.PType].Add(report);
                        var packetObj = PacketObjPool.Get();
                        packetObj.Packet = errorPacket.Packet; packetObj.PacketSize = 0; packetObj.Report = report; packetObj.ErrorPacket = errorPacket;

                        success = ProccessClientPacket(packetObj, false);
                        break;
                }

                if (success || errorPacket.RetryAttempt > errorPacket.MaxAttempts)
                {
                    if (!success)
                        Log.LineShortDate($"        [BadReprocess] Entity:{errorPacket.Packet?.EntityId} Cause:{errorPacket.Error ?? string.Empty}", "net");

                    ClientSideErrorPkt.Remove(errorPacket);
                }
                else
                    errorPacket.RetryTick = Tick + errorPacket.RetryDelayTicks;

                if (errorPacket.MaxAttempts == 0)
                    ClientSideErrorPkt.Remove(errorPacket);
            }
            ClientSideErrorPkt.ApplyChanges();
        }

        private bool ClientCompData(PacketObj data)
        {
            var packet = data.Packet;
            var compDataPacket = (CompDataPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            if (comp.Data.Repo.Sync(comp, compDataPacket.Data))
                data.Report.PacketValid = true;

            return true;
        }

        private bool ClientFakeTargetUpdate(PacketObj data)
        {
            var packet = data.Packet;
            data.ErrorPacket.NoReprocess = true;
            var targetPacket = (FakeTargetPacket)packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;

            GridAi ai;
            //TODO client uses try get in case packets are out of order, no need to reprocess as fake targets are sent very often
            if (myGrid != null && GridTargetingAIs.TryGetValue(myGrid, out ai))
            {
                if (ai.MIds[(int)packet.PType] < packet.MId)
                {
                    ai.MIds[(int)packet.PType] = packet.MId;

                    long playerId;
                    if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
                    {
                        FakeTarget dummyTarget;
                        if (PlayerDummyTargets.TryGetValue(playerId, out dummyTarget))
                        {
                            dummyTarget.Update(targetPacket.Data, ai, null, true);
                            data.Report.PacketValid = true;
                        }
                        else
                            return Error(data, Msg("Player dummy target not found"));
                    }
                    else
                        return Error(data, Msg("SteamToPlayer missing Player"));
                }
                else Log.Line($"ClientFakeTargetUpdate comp MID failure");
            }
            else
                return Error(data, Msg($"GridId: {packet.EntityId}", myGrid != null), Msg("Ai"));

            return true;
        }

        private bool ClientRescanGroupRequest(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            if (myGrid == null) return Error(data, Msg($"GridId: {packet.EntityId}"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {

                if (ai.MIds[(int) packet.PType] < packet.MId) {
                    ai.MIds[(int)packet.PType] = packet.MId;

                    ai.ReScanBlockGroups(true);
                }
                else Log.Line($"ClientRescanGroupRequest MID failure");
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            data.Report.PacketValid = true;
            return true;

        }

        private bool ClientAiSyncUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            var aiSyncPacket = (AiSyncPacket)packet;
            if (myGrid == null) return Error(data, Msg($"Grid: {packet.EntityId}"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {

                if (ai.MIds[(int)packet.PType] < packet.MId) {
                    ai.MIds[(int)packet.PType] = packet.MId;

                    aiSyncPacket.Data.Sync(ai, aiSyncPacket.Data);

                    data.Report.PacketValid = true;
                }
                else Log.Line($"ClientAiSyncUpdate MID failure");
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }

        private bool ClientGridFocusListSync(PacketObj data)
        {
            var packet = data.Packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            var focusPacket = (GridFocusListPacket)packet;
            if (myGrid == null) return Error(data, Msg($"Grid: {packet.EntityId}"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {

                if (ai.MIds[(int)packet.PType] < packet.MId) {
                    ai.MIds[(int)packet.PType] = packet.MId;

                    for (int i = 0; i < focusPacket.EntityIds.Length; i++)
                    {
                        var eId = focusPacket.EntityIds[i];
                        var focusTarget = MyEntities.GetEntityByIdOrDefault(eId);
                        if (focusTarget == null) return Error(data, Msg($"FocusTargetId: {eId}"));

                        ai.Data.Repo.Focus.Target[i] = focusTarget.EntityId;
                    }
                    data.Report.PacketValid = true;
                }
                else Log.Line($"ClientGridFocusListSync MID failure");
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;
        }

        private bool ClientFocusStates(PacketObj data)
        {
            var packet = data.Packet;
            var focusPacket = (FocusPacket)packet;
            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            if (myGrid == null) return Error(data, Msg($"GridId: {packet.EntityId}"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {

                if (ai.MIds[(int)packet.PType] < packet.MId) {
                    ai.MIds[(int)packet.PType] = packet.MId;

                    var targetGrid = MyEntities.GetEntityByIdOrDefault(focusPacket.TargetId) as MyCubeGrid;
                    switch (packet.PType)
                    {

                        case PacketType.FocusUpdate:
                            if (targetGrid != null)
                                ai.Data.Repo.Focus.AddFocus(targetGrid, ai, true);
                            break;
                        case PacketType.ReassignTargetUpdate:
                            if (targetGrid != null)
                                ai.Data.Repo.Focus.ReassignTarget(targetGrid, focusPacket.FocusId, ai, true);
                            break;
                        case PacketType.NextActiveUpdate:
                            ai.Data.Repo.Focus.NextActive(focusPacket.AddSecondary, ai, true);
                            break;
                        case PacketType.ReleaseActiveUpdate:
                            ai.Data.Repo.Focus.ReleaseActive(ai, true);
                            break;
                    }
                    data.Report.PacketValid = true;
                }
                else Log.Line($"ClientFocusStates MID failure");
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;

        }
        // no storge sync

        private bool ClientWeaponSyncUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var targetPacket = (GridWeaponPacket)packet;
            if (targetPacket.Data == null) return Error(data, Msg("Data"));

            for (int j = 0; j < targetPacket.Data.Count; j++)
            {

                var weaponData = targetPacket.Data[j];
                var block = MyEntities.GetEntityByIdOrDefault(weaponData.CompEntityId) as MyCubeBlock;
                var comp = block?.Components.Get<WeaponComponent>();

                if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg("Comp", comp != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

                var validData = weaponData.SyncData != null && weaponData.WeaponRng != null;
                var validTargetData = weaponData.TargetData != null;

                if (!validTargetData && !validData)
                    Error(data, Msg($"No valid weapon Data or Timings for {comp.MyCube.BlockDefinition.Id.SubtypeName} - aiAge:{Tick - comp.Ai.AiSpawnTick}"));

                Weapon weapon;
                if (validData)
                {
                    weapon = comp.Platform.Weapons[weaponData.SyncData.WeaponId];
                    SyncWeapon(weapon, ref weaponData.SyncData);

                    weapon.Comp.Data.Repo.WepVal.WeaponRandom[weapon.WeaponId].Sync(weaponData.WeaponRng);
                }

                if (validTargetData)
                {

                    weapon = comp.Platform.Weapons[weaponData.TargetData.WeaponId];
                    weaponData.TargetData.SyncTarget(weapon.Target);

                    if (weapon.Target.HasTarget)
                    {

                        if (!weapon.Target.IsProjectile && !weapon.Target.IsFakeTarget && weapon.Target.Entity == null)
                        {
                            var oldChange = weapon.Target.TargetChanged;
                            weapon.Target.StateChange(true, Target.States.Invalid);
                            weapon.Target.TargetChanged = !weapon.FirstSync && oldChange;
                            weapon.FirstSync = false;
                        }
                        else if (weapon.Target.IsProjectile)
                        {

                            TargetType targetType;
                            AcquireProjectile(weapon, out targetType);

                            if (targetType == TargetType.None)
                            {
                                if (weapon.NewTarget.CurrentState != Target.States.NoTargetsSeen)
                                    weapon.NewTarget.Reset(weapon.Comp.Session.Tick, Target.States.NoTargetsSeen);
                                if (weapon.Target.CurrentState != Target.States.NoTargetsSeen) weapon.Target.Reset(weapon.Comp.Session.Tick, Target.States.NoTargetsSeen, !weapon.Comp.TrackReticle);
                            }
                        }
                    }
                }

                data.Report.PacketValid = true;
            }

            return true;
        }

        private bool ClientPlayerIdUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var updatePacket = (BoolUpdatePacket)packet;

            if (updatePacket.Data)
                PlayerConnected(updatePacket.EntityId);
            else //remove
                PlayerDisconnected(updatePacket.EntityId);

            data.Report.PacketValid = true;
            return true;
        }

        private bool ClientClientMouseEvent(PacketObj data)
        {
            var packet = data.Packet;
            var mousePacket = (InputPacket)packet;
            if (mousePacket.Data == null) return Error(data, Msg("Data"));

            long playerId;
            if (SteamToPlayer.TryGetValue(packet.SenderId, out playerId))
            {

                PlayerMouseStates[playerId] = mousePacket.Data;
                data.Report.PacketValid = true;
            }
            else
                return Error(data, Msg("No Player Mouse State Found"));

            return true;
        }

        private bool ClientActiveControlUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var dPacket = (BoolUpdatePacket)packet;
            var cube = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeBlock;
            if (cube == null) return Error(data, Msg($"CubeId: {packet.EntityId}"));

            long playerId;
            SteamToPlayer.TryGetValue(packet.SenderId, out playerId);

            UpdateActiveControlDictionary(cube, playerId, dPacket.Data);

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientActiveControlFullUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var csPacket = (CurrentGridPlayersPacket)packet;
            if (csPacket.Data.PlayersToControlledBlock == null) return Error(data, Msg($"playerControlBlock: {packet.EntityId}"));

            //null = 0 players in grid on stream/load
            if (csPacket.Data.PlayersToControlledBlock.Length > 0)
            {
                for (int i = 0; i < csPacket.Data.PlayersToControlledBlock.Length; i++)
                {

                    var playerBlock = csPacket.Data.PlayersToControlledBlock[i];
                    var cube = MyEntities.GetEntityByIdOrDefault(playerBlock.EntityId) as MyCubeBlock;
                    if (cube?.CubeGrid == null) return Error(data, Msg($"CubeId:{playerBlock.EntityId} - pId:{playerBlock.PlayerId}", cube != null), Msg("Grid"));

                    UpdateActiveControlDictionary(cube, playerBlock.PlayerId, true);
                }
            }
            else Log.Line($"ClientActiveControlFullUpdate had no players");

            data.Report.PacketValid = true;

            return true;
        }

        private bool ClientTargetExpireUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var idPacket = (WeaponIdPacket)packet;
            data.ErrorPacket.NoReprocess = true;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            //saving on extra field with new packet type
            if (comp.MIds[(int)packet.PType] < packet.MId) {
                comp.MIds[(int)packet.PType] = packet.MId;

                comp.Platform.Weapons[idPacket.WeaponId].Target.Reset(Tick, Target.States.ServerReset);
                data.Report.PacketValid = true;
            }


            return true;

        }

        private bool ClientFullMouseUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var mouseUpdatePacket = (MouseInputSyncPacket)packet;

            if (mouseUpdatePacket.Data == null) return Error(data, Msg("Data"));

            for (int i = 0; i < mouseUpdatePacket.Data.Length; i++)
            {
                var playerMousePackets = mouseUpdatePacket.Data[i];
                if (playerMousePackets.PlayerId != PlayerId)
                    PlayerMouseStates[playerMousePackets.PlayerId] = playerMousePackets.MouseStateData;
            }

            data.Report.PacketValid = true;
            return true;

        }

        // Unmanaged state changes below this point

        private bool ClientSentReport(PacketObj data)
        {
            var packet = data.Packet;
            var sentReportPacket = (ProblemReportPacket)packet;
            if (sentReportPacket.Data == null) return Error(data, Msg("SentReport"));
            Log.Line($"remote data received");
            ProblemRep.RemoteData = sentReportPacket.Data;
            data.Report.PacketValid = true;

            return true;

        }

        private bool ClientSendSingleShot(PacketObj data)
        {
            var packet = data.Packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            if (comp?.Ai == null || comp.Platform.State != MyWeaponPlatform.PlatformState.Ready) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai", comp?.Ai != null), Msg("Ai", comp?.Platform.State == MyWeaponPlatform.PlatformState.Ready));

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
                comp.Platform.Weapons[i].SingleShotCounter++;

            data.Report.PacketValid = true;

            return true;
        }
        // retire


        /*
        private bool ClientGridOverRidesSync(PacketObj data)
        {
            var packet = data.Packet;
            var gridOverRidePacket = (GridOverRidesSyncPacket)packet;

            var myGrid = MyEntities.GetEntityByIdOrDefault(packet.EntityId) as MyCubeGrid;
            if (myGrid == null) return Error(data, Msg($"GridId: {packet.EntityId}"));

            GridAi ai;
            if (GridTargetingAIs.TryGetValue(myGrid, out ai))
            {
                if (ai.MIds[(int) packet.PType] < packet.MId) {
                    ai.MIds[(int)packet.PType] = packet.MId;

                    ai.ReScanBlockGroups();

                    for (int i = 0; i < gridOverRidePacket.Data.Length; i++)
                    {

                        var groupName = gridOverRidePacket.Data[i].GroupName;
                        var overRides = gridOverRidePacket.Data[i].Overrides;

                        if (ai.BlockGroups.ContainsKey(groupName) && SyncGridOverrides(ai, packet, overRides, groupName))
                        {
                            data.Report.PacketValid = true;
                        }
                        else
                            return Error(data, Msg($"group did not exist: {groupName} - gridMarked:{myGrid.MarkedForClose} - aiMarked:{ai.MarkedForClose}({ai.Version})"));
                    }
                }
                else Log.Line($"ClientGridOverRidesSync MID failure");
            }
            else
                return Error(data, Msg($"GridAi not found, is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));

            return true;

        }
        */


        /*
        private bool ClientOverRidesUpdate(PacketObj data)
        {
            var packet = data.Packet;
            var overRidesPacket = (OverRidesPacket)packet;
            var ent = MyEntities.GetEntityByIdOrDefault(packet.EntityId);
            var comp = ent?.Components.Get<WeaponComponent>();
            var myGrid = ent as MyCubeGrid;

            if (comp?.Ai == null && myGrid == null) return Error(data, Msg($"CompId: {packet.EntityId}", comp != null), Msg("Ai+Grid"));

            if (overRidesPacket.Data == null) return Error(data, Msg("Data"));

            if (comp?.Ai != null) {
                if (comp.MIds[(int) packet.PType] < packet.MId){
                    comp.MIds[(int)packet.PType] = packet.MId;

                    comp.Ai.ReScanBlockGroups();
                    comp.Data.Repo.Set.Overrides.Sync(overRidesPacket.Data);
                    data.Report.PacketValid = true;
                }
                else Log.Line($"ClientOverRidesUpdate comp MID failure");
            }
            else if (myGrid != null)
            {
                GridAi ai;
                if (GridTargetingAIs.TryGetValue(myGrid, out ai)) {

                    if (SyncGridOverrides(ai, packet, overRidesPacket.Data, overRidesPacket.GroupName))
                    {
                        GroupInfo groups;
                        if (ai.BlockGroups.TryGetValue(overRidesPacket.GroupName, out groups)) {

                            foreach (var component in groups.Comps) {
                                component.Data.Repo.Set.Overrides.Sync(overRidesPacket.Data);
                                data.Report.PacketValid = true;
                            }

                        }
                        else
                            return Error(data, Msg("Block group not found"));
                    }
                    else Log.Line($"ClientOverRidesUpdate Ai MID failure");
                }
                else
                    return Error(data, Msg($"GridAi not found, grid is marked:{myGrid.MarkedForClose}, has root:{GridToMasterAi.ContainsKey(myGrid)}"));
            }

            return true;
        }
        */


    }
}
