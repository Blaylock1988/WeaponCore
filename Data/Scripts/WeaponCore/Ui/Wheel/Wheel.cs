﻿using System;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using WeaponCore.Support;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using static WeaponCore.Wheel.Menu;
namespace WeaponCore
{
    internal partial class Wheel
    {
        internal void UpdatePosition()
        {
            var s = Session;
            if (s.UiInput.MouseButtonPressed)
            {
                if (s.UiInput.MouseButtonMiddle && ChangeState == State.Open)
                {
                    OpenWheel();
                }
                else if (s.UiInput.MouseButtonMiddle && ChangeState == State.Close) CloseWheel();
            }
            else
            {
                if (WheelActive && !(Session.Session.ControlledObject is MyCockpit)) CloseWheel();
            }
            if (WheelActive)
            {
                var previousMenu = _currentMenu;
                if (s.UiInput.LeftMouseReleased)
                {
                    var menu = GetCurrentMenu();
                    var item = GetCurrentMenuItem();
                    if (item.SubName != null)
                    {
                        SaveMenuInfo(menu, item);
                        _currentMenu = item.SubName;
                        UpdateState(menu);
                    }
                }
                else if (s.UiInput.RightMouseReleased)
                {
                    var menu = GetCurrentMenu();
                    var item = GetCurrentMenuItem();
                    if (item.ParentName != null)
                    {
                        _currentMenu = item.ParentName;
                        UpdateState(menu);
                    }
                    else if (menu.Name == "WeaponGroups") CloseWheel();
                }

                if (!s.UiInput.UiKeyPressed && s.UiInput.WheelForward)
                {
                    GetNextWheelId(true);
                    GetCurrentMenu().Move(Movement.Forward);

                }
                else if (!s.UiInput.UiKeyPressed && s.UiInput.WheelBackward)
                {
                    GetNextWheelId(false);
                    GetCurrentMenu().Move(Movement.Backward);
                }

                if (previousMenu != _currentMenu) SetCurrentMessage();
            }
        }

        internal void DrawWheel()
        {
            var position = new Vector3D(_wheelPosition.X, _wheelPosition.Y, 0);
            var fov = Session.Camera.FovWithZoom;
            double aspectratio = Session.Camera.ViewportSize.X / MyAPIGateway.Session.Camera.ViewportSize.Y;
            var scale = 0.075 * Math.Tan(fov * 0.5);
            position.X *= scale * aspectratio;
            position.Y *= scale;
            var cameraWorldMatrix = Session.Camera.WorldMatrix;
            position = Vector3D.Transform(new Vector3D(position.X, position.Y, -.1), cameraWorldMatrix);

            var origin = position;
            var left = cameraWorldMatrix.Left;
            var up = cameraWorldMatrix.Up;
            scale = 1 * scale;
            SetCurrentMessage();
            MyTransparentGeometry.AddBillboardOriented(TextureIds[CurrentTextureId], Color.White, origin, left, up, (float)scale, BlendTypeEnum.PostPP);
        }

        internal void OpenWheel()
        {
            WheelActive = true;
            if (HudNotify == null) HudNotify = MyAPIGateway.Utilities.CreateNotification("[Grids]", 160, "UrlHighlight");
            if (string.IsNullOrEmpty(_currentMenu))
            {
                _currentMenu = "WeaponGroups";
                UpdateState(GetCurrentMenu());
            }
            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, MyAPIGateway.Session.Player.IdentityId, false);
            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, MyAPIGateway.Session.Player.IdentityId, false);
            var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, MyAPIGateway.Session.Player.IdentityId, false);
        }

        internal void CloseWheel()
        {
            _currentMenu = "WeaponGroups";
            WheelActive = false;
            var controlStringLeft = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Left).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringLeft, MyAPIGateway.Session.Player.IdentityId, true);
            var controlStringRight = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Right).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringRight, MyAPIGateway.Session.Player.IdentityId, true);
            var controlStringMiddle = MyAPIGateway.Input.GetControl(MyMouseButtonsEnum.Middle).GetGameControlEnum().String;
            MyVisualScriptLogicProvider.SetPlayerInputBlacklistState(controlStringMiddle, MyAPIGateway.Session.Player.IdentityId, true);
            Session.RemoveGps();
        }
        
        internal void SetCurrentMessage()
        {
            var currentMenu = GetCurrentMenu();
            var currentMessage = currentMenu.Message;

            if (currentMessage == string.Empty)
                currentMessage = currentMenu.CurrentItemMessage();

            if (currentMenu.GpsEntity != null)
            {
                var gpsName = currentMenu.GpsEntity.DisplayNameText;
                Session.AddGps();
                Session.SetGpsInfo(currentMenu.GpsEntity.PositionComp.GetPosition(), gpsName);
            }
            else Session.RemoveGps();

            HudNotify.Text = currentMessage;
            HudNotify.Show();
        }

        internal Menu GetCurrentMenu()
        {
            return Menus[_currentMenu];
        }

        internal Item GetCurrentMenuItem()
        {
            var menu = Menus[_currentMenu];
            return menu.Items[menu.CurrentSlot];
        }

        internal int GetNextWheelId(bool forward)
        {
            if (forward)
            {
                if (CurrentTextureId + 1 > 5) CurrentTextureId = 0;
                else CurrentTextureId++;
            }
            else
            {
                if (CurrentTextureId - 1 < 0) CurrentTextureId = 5;
                else CurrentTextureId--;
            }
            return CurrentTextureId;
        }

        internal void SaveMenuInfo(Menu menu, Item item)
        {
            switch (menu.Name)
            {
                case "WeaponGroups":
                    ActiveGroupId = item.SubSlot;
                    break;
                case "Weapons":
                    ActiveWeaponId = item.SubSlot;
                    break;
            }
        }

        internal void UpdateState(Menu oldMenu)
        {
            oldMenu.CleanUp();
            GroupNames.Clear();

            foreach (var group in BlockGroups)
            {
                group.Clear();
                MembersPool.Return(group);
            }

            BlockGroups.Clear();

            foreach (var group in Ai.BlockGroups)
            {
                var groupName = group.Key;
                GroupNames.Add(groupName);
                var membersList = MembersPool.Get();

                foreach (var comp in group.Value.Comps)
                {
                    var groupMember = new GroupMember { Comps = comp, Name = groupName };
                    membersList.Add(groupMember);
                }
                BlockGroups.Add(membersList);
            }

            var menu = Menus[_currentMenu];
            if (menu.ItemCount <= 1) menu.LoadInfo();
        }
    }
}
