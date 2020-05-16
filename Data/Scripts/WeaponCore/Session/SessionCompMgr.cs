﻿using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Utils;
using WeaponCore.Platform;
using WeaponCore.Support;

namespace WeaponCore
{
    public partial class Session
    {
        public struct CompReAdd
        {
            public WeaponComponent Comp;
            public GridAi Ai;
        }

        private void StartComps()
        {
            //var reassign = false;
            for (int i = 0; i < CompsToStart.Count; i++)
            {
                var weaponComp = CompsToStart[i];
                if (weaponComp.MyCube.CubeGrid.IsPreview)
                {
                    //Log.Line($"[IsPreview] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid}");
                    weaponComp.RemoveComp();
                    PlatFormPool.Return(weaponComp.Platform);
                    weaponComp.Platform = null;
                    CompsToStart.Remove(weaponComp);
                    continue;
                }
                if (weaponComp.MyCube.CubeGrid.Physics == null && !weaponComp.MyCube.CubeGrid.MarkedForClose && weaponComp.MyCube.BlockDefinition.HasPhysics)
                    continue;
                /*
                if (weaponComp.Ai.MyGrid != weaponComp.MyCube.CubeGrid)
                {
                    if (!GridToFatMap.ContainsKey(weaponComp.MyCube.CubeGrid))
                        continue;

                    Log.Line($"[StartComps - gridMisMatch] MyCubeId:{weaponComp.MyCube.EntityId} - Grid:{weaponComp.MyCube.CubeGrid.DebugName} - WeaponName:{weaponComp.MyCube.BlockDefinition.Id.SubtypeId.String} - !Marked:{!weaponComp.MyCube.MarkedForClose} - inScene:{weaponComp.MyCube.InScene} - gridMatch:{weaponComp.MyCube.CubeGrid == weaponComp.Ai.MyGrid} - {weaponComp.Ai.MyGrid.MarkedForClose}");
                    InitComp(weaponComp.MyCube, false);
                    reassign = true;
                    CompsToStart.Remove(weaponComp);
                }
                */
                if (weaponComp.Platform.State == MyWeaponPlatform.PlatformState.Fresh)
                {
                    if (weaponComp.MyCube.MarkedForClose)
                    {
                        CompsToStart.Remove(weaponComp);
                        continue;
                    }
                    if (!GridToFatMap.ContainsKey(weaponComp.MyCube.CubeGrid))
                        continue;

                    weaponComp.MyCube.Components.Add(weaponComp);
                    CompsToStart.Remove(weaponComp);
                }
                else
                {
                    Log.Line($"comp didn't match CompsToStart condition, removing");
                    CompsToStart.Remove(weaponComp);
                }
            }
            CompsToStart.ApplyRemovals();
            /*
            if (reassign)
            {
                CompsToStart.ApplyAdditions();
                StartComps();
            }
            */
        }

        private void InitComp(MyCubeBlock cube, bool thread = true)
        {
            using (cube.Pin())
            {
                if (cube.MarkedForClose)
                    return;

                var blockDef = ReplaceVanilla && VanillaIds.ContainsKey(cube.BlockDefinition.Id) ? VanillaIds[cube.BlockDefinition.Id] : cube.BlockDefinition.Id.SubtypeId;
                
                var weaponComp = new WeaponComponent(this, cube, blockDef);
                CompsToStart.Add(weaponComp);
                if (thread) CompsToStart.ApplyAdditions();
            }
        }

        private void ChangeReAdds()
        {
            for (int i = CompReAdds.Count - 1; i >= 0; i--)
            {
                var reAdd = CompReAdds[i];
                if (!GridToFatMap.ContainsKey(reAdd.Comp.MyCube.CubeGrid))
                    continue;

                if (reAdd.Comp.Ai != null && reAdd.Comp.Entity != null) 
                    reAdd.Comp.OnAddedToSceneTasks();
                CompReAdds.RemoveAtFast(i);
            }
        }

        private void DelayedComps(bool forceRemove = false)
        {
            for (int i = CompsDelayed.Count - 1; i >= 0; i--)
            {
                var delayed = CompsDelayed[i];
                if (delayed.MyCube.MarkedForClose || delayed.Entity == null || forceRemove)
                    CompsDelayed.RemoveAtFast(i);
                else if (delayed.MyCube.IsFunctional)
                {
                    delayed.PlatformInit();
                    CompsDelayed.RemoveAtFast(i);
                }
            }
        }

        private void DelayedGridAiCleanup()
        {
            Log.Line($"delayed cleanup");
            for (int i = 0; i < DelayedGridAiClean.Count; i++)
            {
                var gridAi = DelayedGridAiClean[i];
                gridAi.GridClose(null);
                if (!gridAi.GridInit)
                {
                    DelayedGridAiClean.Remove(gridAi);
                    Log.Line($"removed gridAi");
                }
            }
            DelayedGridAiClean.ApplyRemovals();
        }

        internal void CloseComps(MyEntity ent)
        {
            try
            {
                var cube = (MyCubeBlock)ent;
                cube.OnClose -= CloseComps;
                if (cube.CubeGrid.IsPreview)
                    return;

                WeaponComponent comp;
                if (!cube.Components.TryGet(out comp)) return;

                if (comp.Platform.State == MyWeaponPlatform.PlatformState.Ready)
                {
                    comp.StopAllSounds();
                    comp.CleanCompParticles();
                    comp.CleanCompSounds();
                    comp.Platform.RemoveParts(comp);
                }

                if (comp.Ai != null)
                {
                    Log.Line("Comp still had AI on close");
                    comp.Ai = null;
                }
                
                if (comp.Registered)
                {
                    Log.Line($"comp still registered");
                    comp.RegisterEvents(false);
                }

                PlatFormPool.Return(comp.Platform);
                comp.Platform = null;
                var sinkInfo = new MyResourceSinkInfo()
                {
                    ResourceTypeId = comp.GId,
                    MaxRequiredInput = 0f,
                    RequiredInputFunc = null,
                };

                comp.MyCube.ResourceSink.Init(MyStringHash.GetOrCompute("Charging"), sinkInfo);
            }
            catch (Exception ex) { Log.Line($"Exception in DelayedCompClose: {ex}"); }
        }
    }
}
