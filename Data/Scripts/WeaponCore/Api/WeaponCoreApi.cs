﻿using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ModAPI;
using VRageMath;

namespace WeaponCore.Support
{
    internal class WeaponCoreApi
    {
        private bool _apiInit;

        private Func<IList<MyDefinitionId>> _getAllCoreWeapons;
        private Func<IList<MyDefinitionId>> _getAllCoreStaticLaunchers;
        private Func<IList<MyDefinitionId>> _getAllCoreTurrets;
        private Action<IMyEntity, IMyEntity, int> _setTargetEntity;
        private Action<IMyTerminalBlock, bool, int> _fireWeaponOnce;
        private Action<IMyTerminalBlock, bool, bool, int> _toggleWeaponFire;
        private Func<IMyTerminalBlock, int, bool, bool, bool> _isWeaponReadyToFire;
        private Func<IMyTerminalBlock, float> _getMaxWeaponRange;
        private Func<IMyTerminalBlock, IList<IList<string>>> _getTurretTargetTypes;
        private Action<IMyTerminalBlock, IList<IList<string>>> _setTurretTargetTypes;
        private Action<IMyTerminalBlock, float> _setTurretTargetingRange;
        private Func<IMyTerminalBlock, IList<IMyEntity>> _getTargetedEntity;
        private Func<IMyTerminalBlock, IMyEntity, int, bool> _isTargetAligned;
        private Func<IMyTerminalBlock, IMyEntity, int, Vector3D?> _getPredictedTargetPos;
        private Func<IMyTerminalBlock, float> _getHeatLevel;
        private Func<IMyTerminalBlock, float> _currentPowerConsumption;
        private Func<MyDefinitionId, float> _maxPowerConsumption;
        private Action<IMyTerminalBlock> _disablePowerRequirements;
        private Action<IList<byte[]>> _getAllWeaponDefinitions;
        private Func<IMyTerminalBlock, IDictionary<string, int>, bool> _getBlockWeaponMap;

        private const long Channel = 67549756549;
        private readonly List<byte[]> _byteArrays = new List<byte[]>();

        public bool IsReady { get; private set; }
        public readonly List<WeaponDefinition> WeaponDefinitions = new List<WeaponDefinition>();

        private void HandleMessage(object o)
        {
            if (_apiInit) return;
            var dict = o as IReadOnlyDictionary<string, Delegate>;
            if (dict == null)
                return;
            ApiLoad(dict);
            IsReady = true;
        }

        private bool _isRegistered;

        public bool Load()
        {
            if (!_isRegistered)
            {
                _isRegistered = true;
                MyAPIGateway.Utilities.RegisterMessageHandler(Channel, HandleMessage);
            }
            if (!IsReady)
                MyAPIGateway.Utilities.SendModMessage(Channel, "ApiEndpointRequest");
            return IsReady;
        }

        public void Unload()
        {
            if (_isRegistered)
            {
                _isRegistered = false;
                MyAPIGateway.Utilities.UnregisterMessageHandler(Channel, HandleMessage);
            }
            IsReady = false;
        }

        public void ApiLoad(IReadOnlyDictionary<string, Delegate> delegates, bool getWeaponDefinitions = false)
        {
            _apiInit = true;
            _getAllCoreWeapons = (Func<IList<MyDefinitionId>>)delegates["GetAllCoreWeapons"];
            _getAllCoreStaticLaunchers = (Func<IList<MyDefinitionId>>)delegates["GetCoreStaticLaunchers"];
            _getAllCoreTurrets = (Func<IList<MyDefinitionId>>)delegates["GetCoreTurrets"];
            _setTargetEntity = (Action<IMyEntity, IMyEntity, int>)delegates["SetTargetEntity"];
            _fireWeaponOnce = (Action<IMyTerminalBlock, bool, int>)delegates["FireOnce"];
            _toggleWeaponFire = (Action<IMyTerminalBlock, bool, bool, int>)delegates["ToggleFire"];
            _isWeaponReadyToFire = (Func<IMyTerminalBlock, int, bool, bool, bool>)delegates["WeaponReady"];
            _getMaxWeaponRange = (Func<IMyTerminalBlock, float>)delegates["GetMaxRange"];
            _getTurretTargetTypes = (Func<IMyTerminalBlock, IList<IList<string>>>)delegates["GetTurretTargetTypes"];
            _setTurretTargetingRange = (Action <IMyTerminalBlock, float>)delegates["SetTurretRange"];
            _setTurretTargetTypes = (Action<IMyTerminalBlock, IList<IList<string>>>)delegates["SetTurretTargetTypes"];
            _getTargetedEntity = (Func<IMyTerminalBlock, IList<IMyEntity>>)delegates["GetTargetedEntity"];
            _isTargetAligned = (Func<IMyTerminalBlock, IMyEntity, int, bool>)delegates["IsTargetAligned"];
            _getPredictedTargetPos = (Func<IMyTerminalBlock, IMyEntity, int, Vector3D?>)delegates["GetPredictedTargetPosition"];
            _getHeatLevel = (Func<IMyTerminalBlock, float>)delegates["GetHeatLevel"];
            _currentPowerConsumption = (Func<IMyTerminalBlock, float>)delegates["CurrentPower"];
            _maxPowerConsumption = (Func<MyDefinitionId, float>)delegates["MaxPower"];
            _disablePowerRequirements = (Action<IMyTerminalBlock>)delegates["DisableRequiredPower"];
            _getAllWeaponDefinitions = (Action<IList<byte[]>>)delegates["GetAllWeaponDefinitions"];
            _getBlockWeaponMap = (Func<IMyTerminalBlock, IDictionary<string,int>, bool>)delegates["GetBlockWeaponMap"];
            if (getWeaponDefinitions)
            {
                GetAllWeaponDefinitions(_byteArrays);
                foreach (var byteArray in _byteArrays)
                {
                    WeaponDefinitions.Add(MyAPIGateway.Utilities.SerializeFromBinary<WeaponDefinition>(byteArray));
                }
            }
        }
        public void GetAllWeaponDefinitions(IList<byte[]> collection) => _getAllWeaponDefinitions?.Invoke(collection);
        public IList<MyDefinitionId> GetAllCoreWeapons() => _getAllCoreWeapons?.Invoke();
        public IList<MyDefinitionId> GetAllCoreStaticLaunchers() => _getAllCoreStaticLaunchers?.Invoke();
        public IList<MyDefinitionId> GetAllCoreTurrets() => _getAllCoreTurrets?.Invoke();
        public IList<IList<string>> GetTurretTargetTypes(IMyTerminalBlock weapon) => _getTurretTargetTypes?.Invoke(weapon);
        public IList<IMyEntity> GetTargetedEntity(IMyTerminalBlock weapon) => _getTargetedEntity?.Invoke(weapon);
        public bool IsWeaponReadyToFire(IMyTerminalBlock weapon, int weaponId = 0, bool anyWeaponReady = true, bool shootReady = false) => _isWeaponReadyToFire?.Invoke(weapon, weaponId, anyWeaponReady, shootReady) ?? false;
        public float GetMaxWeaponRange(IMyTerminalBlock weapon) => _getMaxWeaponRange?.Invoke(weapon) ?? 0f;
        public float GetHeatLevel(IMyTerminalBlock weapon) => _getHeatLevel?.Invoke(weapon) ?? 0f;
        public float CurrentPowerConsumption(IMyTerminalBlock weapon) => _currentPowerConsumption?.Invoke(weapon) ?? 0f;
        public float MaxPowerConsumption(MyDefinitionId weaponDef) => _maxPowerConsumption?.Invoke(weaponDef) ?? 0f;
        public void DisablePowerRequirements(IMyTerminalBlock weapon) => _disablePowerRequirements?.Invoke(weapon);
        public void SetTurretTargetingRange(IMyTerminalBlock weapon, float range) => _setTurretTargetingRange?.Invoke(weapon, range);
        public void SetTargetEntity(IMyEntity shooter, IMyEntity target, int priority) => _setTargetEntity?.Invoke(shooter, target, priority);
        public void FireWeaponOnce(IMyTerminalBlock weapon, bool allWeapons = true, int weaponId = 0) => _fireWeaponOnce?.Invoke(weapon, allWeapons, weaponId);
        public void ToggleWeaponFire(IMyTerminalBlock weapon, bool on, bool allWeapons, int weaponId = 0) => _toggleWeaponFire?.Invoke(weapon, on, allWeapons, weaponId);
        public void SetTurretTargetTypes(IMyTerminalBlock weapon, IList<IList<string>> threats) => _setTurretTargetTypes?.Invoke(weapon, threats);
        public bool IsTargetAligned(IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId) => _isTargetAligned?.Invoke(weaponBlock, targetEnt, weaponId) ?? false;
        public Vector3D? GetPredictedTargetPosition(IMyTerminalBlock weaponBlock, IMyEntity targetEnt, int weaponId) => _getPredictedTargetPos?.Invoke(weaponBlock, targetEnt, weaponId) ?? null;
        public bool GetBlockWeaponMap(IMyTerminalBlock weaponBlock, IDictionary<string, int> collection) => _getBlockWeaponMap?.Invoke(weaponBlock, collection) ?? false;
    }
}
