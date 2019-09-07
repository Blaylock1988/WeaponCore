﻿using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using WeaponCore;
using static WeaponCore.Platform.Weapon.TerminalActionState;

namespace WepaonCore.Control
{
    public static class TerminalHelpers
    {
        internal static bool AlterActions<T>() where T : IMyLargeTurretBase
        {
            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<T>(out actions);
            for (int i = 0; i < actions.Count; i++)
            {
                var c = actions[i];
                //Log.Line($"Count: {i} ID:{c.Id}");
                if (c.Id != "Control" && !c.Id.Contains("OnOff") && !c.Id.Equals("Shoot") && !c.Id.Equals("ShootOnce"))
                    c.Enabled = b => !WepUi.CoreWeaponEnableCheck(b, 0);

                if (c.Id.Equals("Shoot") || c.Id.Equals("ShootOnce"))
                {
                    c.Action = blk =>
                    {
                        var comp = blk?.Components?.Get<WeaponComponent>();
                        if (comp == null) return;
                        for (int j = 0; j < comp.Platform.Weapons.Length; j++)
                            comp.Platform.Weapons[j].ManualShoot = ShootOnce;

                    };
                }
            }
            return false;
        }

        internal static bool AlterControls<T>() where T : IMyLargeTurretBase
        {
            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<T>(out controls);
            for (int i = 0; i < controls.Count; i++)
            {
                var c = controls[i];
                //Log.Line($"Count: {i} ID:{c.Id}");
                if ((i > 6 && i < 10) || i > 12 )
                    c.Visible = b => !WepUi.CoreWeaponEnableCheck(b, 0);
            }
            return false;
        }

        internal static IMyTerminalControlOnOffSwitch AddWeaponOnOff<T>(int id, string name, string title, string tooltip, string onText, string offText, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, Func<IMyTerminalBlock, int, bool> VisibleGetter) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>($"WC_{id}_Enable");

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.OnText = MyStringId.GetOrCompute(onText);
            c.OffText = MyStringId.GetOrCompute(offText);
            c.Enabled = b => true;
            c.Visible = b => VisibleGetter(b, id);
            c.Getter = getter;
            c.Setter = setter;
            MyAPIGateway.TerminalControls.AddControl<T>(c);

            CreateOnOffActionSet<T>(c, name, id, VisibleGetter);

            return c;
        }

        internal static IMyTerminalControlSeparator Separator<T>(int id, string name) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>(name);

            c.Enabled = x => true;
            c.Visible = x => true;
            MyAPIGateway.TerminalControls.AddControl<T>(c);

            return c;
        }

        internal static IMyTerminalControlSlider AddSlider<T>(int id, string name, string title, string tooltip,int min, int max, float incAmt,Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock,int,bool> visibleGetter) where T : IMyTerminalBlock
        {
            var s = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>("WC_" + name);

            s.Title = MyStringId.GetOrCompute(title);
            s.Tooltip = MyStringId.GetOrCompute(tooltip);
            s.Enabled = b => true;
            s.Visible = b => visibleGetter(b, id);
            s.Getter = getter;
            s.Setter = setter;
            s.Writer = (b, v) => v.Append(getter(b).ToString("N2"));
            MyAPIGateway.TerminalControls.AddControl<T>(s);

            CreateSliderActionSet<T>(s, name, id, min/100, max/100, incAmt, visibleGetter);
            return s;
        }

        internal static IMyTerminalControlCheckbox AddCheckbox<T>(int id, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, Func<IMyTerminalBlock, int, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, T>("WC_" + name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Getter = getter;
            c.Setter = setter;
            c.Visible = b => visibleGetter(b, id);
            c.Enabled = b => true;

            MyAPIGateway.TerminalControls.AddControl<T>(c);

            CreateOnOffActionSet<T>(c, name, id, visibleGetter);

            return c;
        }

        internal static void CreateOnOffActionSet<T>(IMyTerminalControlOnOffSwitch tc, string name, int id, Func<IMyTerminalBlock, int,bool> enabler) where T : IMyTerminalBlock
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"{name} Toggle On/Off");
            action.Action = (b) => tc.Setter(b, !tc.Getter(b));
            action.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action.Enabled = (b) => enabler(b, id);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);

            action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle_On");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action.Name = new StringBuilder($"{name} On");
            action.Action = (b) => tc.Setter(b, true);
            action.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action.Enabled = (b) => enabler(b, id);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);

            action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle_Off");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            action.Name = new StringBuilder($"{name} Off");
            action.Action = (b) => tc.Setter(b, true);
            action.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action.Enabled = (b) => enabler(b, id);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        internal static void CreateOnOffActionSet<T>(IMyTerminalControlCheckbox tc, string name, int id, Func<IMyTerminalBlock, int, bool> enabler) where T : IMyTerminalBlock
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder($"{name} Toggle On/Off");
            action.Action = (b) => tc.Setter(b, !tc.Getter(b));
            action.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action.Enabled = (b) => enabler(b, id);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);

            action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle_On");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";
            action.Name = new StringBuilder($"{name} On");
            action.Action = (b) => tc.Setter(b, true);
            action.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action.Enabled = (b) => enabler(b, id);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);

            action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Toggle_Off");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOff.dds";
            action.Name = new StringBuilder($"{name} Off");
            action.Action = (b) => tc.Setter(b, true);
            action.Writer = (b, t) => t.Append(tc.Getter(b) ? tc.OnText : tc.OffText);
            action.Enabled = (b) => enabler(b, id);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        internal static void CreateSliderActionSet<T>(IMyTerminalControlSlider tc, string name, int id, int min, int max, float incAmt, Func<IMyTerminalBlock, int, bool> enabler) where T : IMyTerminalBlock
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Increase");
            action.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
            action.Name = new StringBuilder($"Increase {name}");
            action.Action = (b) => tc.Setter(b, tc.Getter(b) + incAmt <= max ? tc.Getter(b) + incAmt : max);
            action.Writer = (b, t) => t.Append("");
            action.Enabled = (b) => enabler(b, id);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);

            action = MyAPIGateway.TerminalControls.CreateAction<T>($"WC_{id}_Decrease");
            action.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
            action.Name = new StringBuilder($"Decrease {name}");
            action.Action = (b) => tc.Setter(b, tc.Getter(b) - incAmt >= min ? tc.Getter(b) - incAmt : min);
            action.Writer = (b, t) => t.Append("");
            action.Enabled = (b) => enabler(b, id);
            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

            internal static bool WeaponFunctionEnabled(IMyTerminalBlock block, int id)
        {
            var comp = block?.Components?.Get<WeaponComponent>();
            if (comp == null || !comp.Platform.Inited) return false;

            for (int i = 0; i < comp.Platform.Weapons.Length; i++)
            {
                if (comp.Platform.Weapons[i].System.WeaponId == id)
                    return true;
            }
            return false;
        }

        #region Saved Code

        internal static IMyTerminalControlCombobox AddCombobox<T>(T block, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, long> getter, Action<IMyTerminalBlock, long> setter, Action<List<MyTerminalControlComboBoxItem>> fillAction, Func<IMyTerminalBlock, int, int, bool> visibleGetter = null, Func<IMyTerminalBlock, int, int, bool> enabledGetter = null) where T : IMyTerminalBlock
        {
            var cmb = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, T>(name);

            cmb.Title = MyStringId.GetOrCompute(title);
            cmb.Tooltip = MyStringId.GetOrCompute(tooltip);
            cmb.Enabled = x => true;
            cmb.Visible = x => true;
            cmb.ComboBoxContent = fillAction;
            cmb.Getter = getter;
            cmb.Setter = setter;
            MyAPIGateway.TerminalControls.AddControl<T>(cmb);
            return cmb;
        }

        internal static IMyTerminalControlButton AddButton<T>(T block, string name, string title, string tooltip, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Visible = visibleGetter;

            MyAPIGateway.TerminalControls.AddControl<T>(c);
            return c;
        }

        internal static IMyTerminalControlColor AddColorEditor<T>(T block, int id, string name, string title, string tooltip, Func<IMyTerminalBlock, Color> getter, Action<IMyTerminalBlock, Color> setter, Func<IMyTerminalBlock, int, int, bool> enabledGetter = null, Func<IMyTerminalBlock, int, int, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, T>(name);

            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.Enabled = x => true;
            c.Visible = x => true;
            c.Getter = getter;
            c.Setter = setter;
            MyAPIGateway.TerminalControls.AddControl<T>(c);

            return c;
        }
        #endregion
    }
}
