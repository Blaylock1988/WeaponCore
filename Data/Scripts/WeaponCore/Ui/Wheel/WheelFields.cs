﻿using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;

namespace WeaponCore
{
    internal partial class Wheel
    {
        private readonly Vector2D _wheelPosition = new Vector2D(0, 0);

        private string _currentMenu;
        internal readonly Dictionary<string, Menu> Menus = new Dictionary<string, Menu>();
        internal readonly List<string> GroupNames = new List<string>();
        internal readonly List<List<GroupMember>> BlockGroups = new List<List<GroupMember>>();
        internal readonly MyConcurrentPool<List<GroupMember>> MembersPool = new MyConcurrentPool<List<GroupMember>>();

        internal readonly Session Session;
        internal GridAi Ai;
        internal IMyHudNotification HudNotify;
        internal bool WheelActive;
        internal string ActiveGroupName;
        internal int ActiveGroupId;
        internal int ActiveWeaponId;
        internal int CurrentTextureId;
        internal int Version;

        internal enum Update
        {
            Parent,
            Sub,
            None
        }

        internal struct Names
        {
            internal string Name;
            internal string CurrentValue;
            internal string NextValue;
            internal string PreviousValue;
        }

        internal readonly Item[] CompGroups =
        {
            new Item {Title = "Select Group",ForeTexture = MyStringId.GetOrCompute("DS_Menu_SelectGroup"), BackTexture = MyStringId.GetOrCompute("DS_Menu_SelectGroupBackground"), SubName = "Group"},
        };

        internal readonly Item[] Group =
        {
            new Item {ItemMessage = "Group Settings", ForeTexture = MyStringId.GetOrCompute("DS_GroupSettings"), BackTexture = MyStringId.GetOrCompute("DS_Menu_ModifyGroup_Background"), ParentName = "CompGroups", SubName = "GroupSettings"},
            new Item {ItemMessage = "Modify Weapons",  ForeTexture = MyStringId.GetOrCompute("DS_ModifyWeapon"), BackTexture = MyStringId.GetOrCompute("DS_Menu_ModifyGroup_Background"), ParentName = "CompGroups", SubName = "Comps"},
        };

        internal readonly Item[] Settings =
        {
            new Item {Title = "Settings Menu", Dynamic = true, ParentName = "Group"},
        };

        internal readonly Item[] Comps =
        {
            new Item {Title = "Comps", ForeTexture = MyStringId.GetOrCompute("DS_Menu_SelectWeapon"), BackTexture = MyStringId.GetOrCompute("DS_Menu_SelectGroupBackground"), ParentName = "Group", SubName = "CompSettings"},
        };

        internal readonly Item[] CompSet =
        {
            new Item {Title = "Comp Settings", Dynamic = true, ParentName = "Comps"},
        };

        internal readonly Dictionary<string, Dictionary<int, Names>> SettingCycleStrMap = new Dictionary<string, Dictionary<int, Names>>()
        {
            {
                "Active", new Dictionary<int, Names>
                {
                    [0] = new Names {Name = "Deactivated", CurrentValue = "Deactivate", NextValue = "Activate"},
                    [1] = new Names {Name = "Activated", CurrentValue = "Activate", NextValue = "Deactivate"},
                }
            },
            {
                "Neutrals", new Dictionary<int, Names>
                {
                    [0] = new Names {Name = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Name = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "Friendly", new Dictionary<int, Names>
                {
                    [0] = new Names {Name = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Name = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "Unowned", new Dictionary<int, Names>
                {
                    [0] = new Names {Name = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Name = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "TargetPainter", new Dictionary<int, Names>
                {
                    [0] = new Names {Name = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Name = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "ManualControl", new Dictionary<int, Names>
                {
                    [0] = new Names {Name = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Name = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "FocusTargets", new Dictionary<int, Names>
                {
                    [0] = new Names {Name = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Name = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "FocusSubSystem", new Dictionary<int, Names>
                {
                    [0] = new Names {Name = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Name = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "Meteors", new Dictionary<int, Names>
                {
                    [0] = new Names {Name = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Name = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "Biologicals", new Dictionary<int, Names>
                {
                    [0] = new Names {Name = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Name = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "Projectiles", new Dictionary<int, Names>
                {
                    [0] = new Names {Name = "Disabled", CurrentValue = "Disable", NextValue = "Enable"},
                    [1] = new Names {Name = "Enabled", CurrentValue = "Enable", NextValue = "Disable"},
                }
            },
            {
                "SubSystems", new Dictionary<int, Names>
                {
                    [0] = new Names {Name = "Any", CurrentValue = "Any", NextValue = "Offense", PreviousValue = "Steering"},
                    [1] = new Names {Name = "Offense", CurrentValue = "Offense", NextValue = "Utility", PreviousValue = "Any"},
                    [2] = new Names {Name = "Utility", CurrentValue = "Utility", NextValue = "Power", PreviousValue = "Offense"},
                    [3] = new Names {Name = "Power", CurrentValue = "Power", NextValue = "Production", PreviousValue = "Utility"},
                    [4] = new Names {Name = "Production", CurrentValue = "Production", NextValue = "Thrust", PreviousValue = "Power"},
                    [5] = new Names {Name = "Thrust", CurrentValue = "Thrust", NextValue = "Jumping", PreviousValue = "Production"},
                    [6] = new Names {Name = "Jumping", CurrentValue = "Jumping", NextValue = "Steering", PreviousValue = "Thrust"},
                    [7] = new Names {Name = "Steering", CurrentValue = "Steering", NextValue = "Any", PreviousValue = "Jumping"},
                }
            },
        };

        internal readonly Dictionary<string, Dictionary<string, int>> SettingStrToValues = new Dictionary<string, Dictionary<string, int>>()
        {
            {
                "Active", new Dictionary<string, int>
                {
                    ["Activate"] = 1,
                    ["Deactivate"] = 0,
                }
            },
            {
                "Neutrals", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "Unowned", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "Friendly", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "TargetPainter", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "ManualControl", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "FocusTargets", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "FocusSubSystem", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "Meteors", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "Biologicals", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "Projectiles", new Dictionary<string, int>
                {
                    ["Enable"] = 1,
                    ["Disable"] = 0,
                }
            },
            {
                "SubSystems", new Dictionary<string, int>
                {
                    ["Any"] = 0,
                    ["Offense"] = 1,
                    ["Utility"] = 2,
                    ["Power"] = 3,
                    ["Production"] = 4,
                    ["Thrust"] = 5,
                    ["Jumping"] = 6,
                    ["Steering"] = 7,
                }
            },
        };

        internal readonly Dictionary<string, Dictionary<string, MyStringId[]>> SettingStrToTextures = new Dictionary<string, Dictionary<string, MyStringId[]>>()
        {
            {
                "Active", new Dictionary<string, MyStringId[]>
                {
                    ["Activate"] = new []{MyStringId.GetOrCompute("DS_ActivatedEnabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                    ["Deactivate"] = new []{MyStringId.GetOrCompute("DS_ActivatedDisabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                }
            },
            {
                "Neutrals", new Dictionary<string, MyStringId[]>
                {
                    ["Enable"] = new [] {MyStringId.GetOrCompute("DS_NeutralEnabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                    ["Disable"] = new [] {MyStringId.GetOrCompute("DS_NeutralDisabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                }
            },
            {
                "Unowned", new Dictionary<string, MyStringId[]>
                {
                    ["Enable"] = new [] {MyStringId.GetOrCompute("DS_UnownedEnabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                    ["Disable"] = new [] {MyStringId.GetOrCompute("DS_UnownedDisabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                }
            },
            {
                "Friendly", new Dictionary<string, MyStringId[]>
                {
                    ["Enable"] = new [] {MyStringId.GetOrCompute("DS_FriendlyEnabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                    ["Disable"] = new [] {MyStringId.GetOrCompute("DS_FriendlyDisabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                }
            },
            {
                "TargetPainter", new Dictionary<string, MyStringId[]>
                {
                    ["Enable"] = new [] {MyStringId.GetOrCompute("DS_TargetPainterEnabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                    ["Disable"] = new [] {MyStringId.GetOrCompute("DS_TargetPainterDisabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                }
            },
            {
                "ManualControl", new Dictionary<string, MyStringId[]>
                {
                    ["Enable"] = new [] {MyStringId.GetOrCompute("DS_ManualControlEnabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                    ["Disable"] = new [] {MyStringId.GetOrCompute("DS_ManualControlDisabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                }
            },
            {
                "FocusTargets", new Dictionary<string, MyStringId[]>
                {
                    ["Enable"] = new [] {MyStringId.GetOrCompute("DS_FocusTargetEnabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                    ["Disable"] = new [] {MyStringId.GetOrCompute("DS_FocusTargetDisabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                }
            },
            {
                "FocusSubSystem", new Dictionary<string, MyStringId[]>
                {
                    ["Enable"] = new [] {MyStringId.GetOrCompute("DS_FocusSubsystemEnabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                    ["Disable"] = new [] {MyStringId.GetOrCompute("DS_FocusSubsystemDisabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                }
            },
            {
                "Meteors", new Dictionary<string, MyStringId[]>
                {
                    ["Enable"] = new [] {MyStringId.GetOrCompute("DS_MeteorsEnabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                    ["Disable"] = new [] {MyStringId.GetOrCompute("DS_MeteorsDisabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                }
            },
            {
                "Biologicals", new Dictionary<string, MyStringId[]>
                {
                    ["Enable"] = new [] {MyStringId.GetOrCompute("DS_BiologicalsEnabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                    ["Disable"] = new [] {MyStringId.GetOrCompute("DS_BiologicalsDisabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                }
            },
            {
                "Projectiles", new Dictionary<string, MyStringId[]>
                {
                    ["Enable"] = new [] {MyStringId.GetOrCompute("DS_ProjectilesEnabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                    ["Disable"] = new [] {MyStringId.GetOrCompute("DS_ProjectilesDisabled"), MyStringId.GetOrCompute("DS_MenuBackground")},
                }
            },
            {
                "SubSystems", new Dictionary<string, MyStringId[]>
                {
                    ["Any"] = new [] {MyStringId.GetOrCompute("DS_SubsystemAny"), MyStringId.GetOrCompute("DS_MenuBackground_Subsystems") },
                    ["Offense"] = new [] {MyStringId.GetOrCompute("DS_SubsystemOffense"), MyStringId.GetOrCompute("DS_MenuBackground_Subsystems") },
                    ["Utility"] = new [] {MyStringId.GetOrCompute("DS_SubsystemUtility"), MyStringId.GetOrCompute("DS_MenuBackground_Subsystems") },
                    ["Power"] = new [] {MyStringId.GetOrCompute("DS_SubsystemPower"), MyStringId.GetOrCompute("DS_MenuBackground_Subsystems") },
                    ["Production"] = new [] {MyStringId.GetOrCompute("DS_SubsystemProduction"), MyStringId.GetOrCompute("DS_MenuBackground_Subsystems") },
                    ["Thrust"] = new [] {MyStringId.GetOrCompute("DS_SubsystemThrust"), MyStringId.GetOrCompute("DS_MenuBackground_Subsystems") },
                    ["Jumping"] = new [] {MyStringId.GetOrCompute("DS_SubsystemJump"), MyStringId.GetOrCompute("DS_MenuBackground_Subsystems") },
                    ["Steering"] = new [] {MyStringId.GetOrCompute("DS_SubsystemSteering"), MyStringId.GetOrCompute("DS_MenuBackground_Subsystems") },
                }
            },
        };


        internal readonly List<string> SettingNames = new List<string>();

        internal enum State
        {
            Close,
            Open,
            NoChange,
        }

        internal State ChangeState
        {
            get
            {
                var controlBlockActive = Session.ActiveControlBlock != null && (Session.ActiveControlBlock is MyCockpit || Session.ActiveControlBlock is MyRemoteControl || Session.ActiveControlBlock is MyCameraBlock);

                var isGridAi = controlBlockActive && (Session.GridToMasterAi.TryGetValue(Session.ActiveControlBlock.CubeGrid, out Ai));
                if (MyAPIGateway.Input.WasMiddleMouseReleased() && !WheelActive && isGridAi) return State.Open;
                if (MyAPIGateway.Input.WasMiddleMouseReleased() && WheelActive) return State.Close;
                return State.NoChange;
            }
        }

        internal Wheel(Session session)
        {
            Session = session;
            var compGroups = new Menu(this, "CompGroups", CompGroups, CompGroups.Length, "Green");
            var group = new Menu(this, "Group", Group, Group.Length, "Green");
            var groupSettings = new Menu(this, "GroupSettings", Settings, Settings.Length, "Red");
            var comps = new Menu(this, "Comps", Comps, Comps.Length, "Yellow");
            var compSet = new Menu(this, "CompSettings", CompSet, CompSet.Length, "Yellow");

            var tmpGroupInfo = new GroupInfo();
            foreach (var groupInfo in tmpGroupInfo.Settings.Keys)
                SettingNames.Add(groupInfo);

            Menus.Add(compGroups.Name, compGroups);
            Menus.Add(group.Name, group);
            Menus.Add(groupSettings.Name, groupSettings);
            Menus.Add(comps.Name, comps);
            Menus.Add(compSet.Name, compSet);
        }
    }
}
