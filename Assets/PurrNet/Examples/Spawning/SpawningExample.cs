using PurrNet;
using PurrNet.Modules;
using PurrNet.Transports;
using UnityEngine;

public class SpawningExample : MonoBehaviour
{
    [SerializeField] NetworkManager _networkManager;
    [SerializeField] GameObject _prefab;
    [SerializeField] private Vector3 _minSpawnPosition;
    [SerializeField] private Vector3 _maxSpawnPosition;

    private void OnEnable()
    {
        _networkManager.onServerState += OnServerState;
    }

    private void OnDisable()
    {
        _networkManager.onServerState -= OnServerState;
    }

    private void OnServerState(ConnectionState state)
    {
        var players = _networkManager.GetModule<PlayersManager>(true);

        switch (state)
        {
            case ConnectionState.Connected:
                players.OnPlayerJoined += OnPlayerJoined;
                break;
            case ConnectionState.Disconnected:
                players.OnPlayerJoined -= OnPlayerJoined;
                break;
        }
    }

    private void OnPlayerJoined(PlayerID player, bool asserver)
    {
        var randomPosition = new Vector3(
            Random.Range(_minSpawnPosition.x, _maxSpawnPosition.x),
            Random.Range(_minSpawnPosition.y, _maxSpawnPosition.y),
            Random.Range(_minSpawnPosition.z, _maxSpawnPosition.z)
        );

        var entry = Instantiate(_prefab, randomPosition, Quaternion.identity);

        _networkManager.GetModule<SpawnManager>(true).Spawn(entry);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube((_minSpawnPosition + _maxSpawnPosition) / 2, _maxSpawnPosition - _minSpawnPosition);
    }
}
