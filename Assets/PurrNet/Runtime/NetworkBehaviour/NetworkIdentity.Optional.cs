using System;
using UnityEngine;

namespace PurrNet
{
    public partial class NetworkIdentity
    {
        [Header("Network Rules Override")]
        [SerializeField, HideInInspector] private OptionalSpawnRules optionalSpawnRules = new OptionalSpawnRules();
        [SerializeField, HideInInspector] private OptionalOwnershipRules optionalOwnershipRules = new OptionalOwnershipRules();
        [SerializeField, HideInInspector] private OptionalNetworkIdentityRules optionalIdentityRules = new OptionalNetworkIdentityRules();

        [Serializable]
        public struct OptionalSpawnRules
        {
            public Optional<ConnectionAuth> spawnAuth;
            public Optional<ActionAuth> despawnAuth;
            public Optional<DefaultOwner> defaultOwner;
            public Optional<bool> propagateOwnership;
            public Optional<bool> despawnIfOwnerDisconnects;
        }

        [Serializable]
        public struct OptionalOwnershipRules
        {
            public Optional<ConnectionAuth> assignAuth;
            public Optional<ActionAuth> transferAuth;
            public Optional<ActionAuth> removeAuth;
        }

        [Serializable]
        public struct OptionalNetworkIdentityRules
        {
            public Optional<bool> syncComponentActive;
            public Optional<ActionAuth> syncComponentAuth;
            public Optional<bool> syncGameObjectActive;
            public Optional<ActionAuth> syncGameObjectActiveAuth;
        }

        [Serializable]
        public struct Optional<T>
        {
            public bool overridden;
            public T value;

            public bool IsOverridden => overridden;
            public T Value => value;
        }

        public ConnectionAuth GetEffectiveSpawnAuth()
        {
            return optionalSpawnRules.spawnAuth.IsOverridden
                ? optionalSpawnRules.spawnAuth.Value
                : networkManager.networkRules.GetDefaultSpawnRules().spawnAuth;
        }

        public ActionAuth GetEffectiveDespawnAuth()
        {
            return optionalSpawnRules.despawnAuth.IsOverridden
                ? optionalSpawnRules.despawnAuth.Value
                : networkManager.networkRules.GetDefaultSpawnRules().despawnAuth;
        }

        public DefaultOwner GetEffectiveDefaultOwner()
        {
            Debug.Log(networkManager.networkRules);
            
            return optionalSpawnRules.defaultOwner.IsOverridden
                ? optionalSpawnRules.defaultOwner.Value
                : networkManager.networkRules.GetDefaultSpawnRules().defaultOwner;
        }

        public bool GetEffectivePropagateOwnership()
        {
            return optionalSpawnRules.propagateOwnership.IsOverridden
                ? optionalSpawnRules.propagateOwnership.Value
                : networkManager.networkRules.GetDefaultSpawnRules().propagateOwnership;
        }

        public bool GetEffectiveDespawnIfOwnerDisconnects()
        {
            return optionalSpawnRules.despawnIfOwnerDisconnects.IsOverridden
                ? optionalSpawnRules.despawnIfOwnerDisconnects.Value
                : networkManager.networkRules.GetDefaultSpawnRules().despawnIfOwnerDisconnects;
        }
    }
}