using System;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkIdentity playerPrefab;

        [SerializeField] private List<Transform> spawnPoints = new();
        private int _currentSpawnPoint;
        
        private void Awake()
        {
            if (NetworkManager.main.isServer)
                OnServerConnectionState(ConnectionState.Connected);
            NetworkManager.main.onServerConnectionState += OnServerConnectionState;
            
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (!spawnPoints[i])
                {
                    PurrLogger.LogError($"Spawn point at index {i} is null. Removing it from the list.", this);
                    spawnPoints.RemoveAt(i);
                    i--;
                }
            }
        }

        private void OnValidate()
        {
            if (playerPrefab != null && playerPrefab is not PrefabLink)
                playerPrefab = playerPrefab.GetComponent<PrefabLink>();
        }

        private void OnServerConnectionState(ConnectionState obj)
        {
            if (obj != ConnectionState.Connected)
                return;
            
            if(NetworkManager.main && NetworkManager.main.TryGetModule(out ScenePlayersModule scenePlayersModule, true))
                scenePlayersModule.onPlayerLoadedScene += OnPlayerLoadedScene;
        }
 
        private void OnDestroy()
        {
            if(NetworkManager.main && NetworkManager.main.TryGetModule(out ScenePlayersModule scenePlayersModule, true))
                scenePlayersModule.onPlayerLoadedScene += OnPlayerLoadedScene;
        }

        private void OnPlayerLoadedScene(PlayerID player, SceneID scene, bool asServer)
        {
            if (!asServer)
                return;

            bool isDestroyOnDisconnectEnabled = NetworkManager.main.networkRules.ShouldDespawnOnOwnerDisconnect();

            if (!isDestroyOnDisconnectEnabled && NetworkManager.main.TryGetModule(out GlobalOwnershipModule ownership, true) && 
                ownership.PlayerOwnsSomething(player))
                return;
            
            NetworkIdentity newPlayer;
            
            if (spawnPoints.Count > 0)
            {
                var spawnPoint = spawnPoints[_currentSpawnPoint];
                _currentSpawnPoint = (_currentSpawnPoint + 1) % spawnPoints.Count;
                newPlayer = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
            }
            else
            {
                newPlayer = Instantiate(playerPrefab);
            }
            
            newPlayer.GiveOwnership(player);
        }
    }
}
