using System.Collections.Generic;
using System.Threading.Tasks;
using PurrNet;
using PurrNet.Logging;
using PurrNet.Modules;
using UnityEngine;

/// <summary>
/// Test component to visualize and debug lag compensation.
/// Add this to any object that you want to shoot from.
/// </summary>
public class LagCompensationTester : NetworkBehaviour
{
    [SerializeField] private KeyCode _fireKey = KeyCode.Space;
    [SerializeField] private float _rayLength = 100f;
    [SerializeField] private float _debugDrawDuration = 2f;
    
    // Cache the camera for performance
    private Camera _camera;

    private class DebugShot
    {
        public Vector3 Origin;
        public Vector3 Direction;
        public float Distance;
        public bool Hit;
        public float EndTime;
    }
    
    private readonly List<DebugShot> _activeShots = new();

    private void Awake()
    {
        _camera = Camera.main;
    }

    private void Update()
    {
        if (!isOwner) return;
        
        if (Input.GetKeyDown(_fireKey))
        {
            FireServerRpc(transform.position + transform.forward, transform.forward);
        }
        
#if UNITY_EDITOR
        float currentTime = Time.time;
        _activeShots.RemoveAll(shot => currentTime > shot.EndTime);
        
        foreach (var shot in _activeShots)
        {
            float alpha = Mathf.Clamp01((shot.EndTime - currentTime) / _debugDrawDuration);
            Color shotColor = shot.Hit ? Color.red : Color.yellow;
            shotColor.a = alpha;
            
            Debug.DrawLine(shot.Origin, shot.Origin + shot.Direction * shot.Distance, shotColor);
            
            Vector3 endPoint = shot.Origin + shot.Direction * shot.Distance;
            Debug.DrawLine(endPoint + Vector3.up * 0.1f, endPoint + Vector3.down * 0.1f, shotColor);
            Debug.DrawLine(endPoint + Vector3.left * 0.1f, endPoint + Vector3.right * 0.1f, shotColor);
            Debug.DrawLine(endPoint + Vector3.forward * 0.1f, endPoint + Vector3.back * 0.1f, shotColor);
        }
#endif
    }

    [ServerRpc]
    private void FireServerRpc(Vector3 origin, Vector3 direction)
    {
        var tick = networkManager.GetModule<TickManager>(true).tick;
        
        RaycastHit hit;
        bool didHit = LagCompensation.Raycast(origin, direction, tick, out hit, debug: true);

        ShowShotClientRpc(origin, direction, didHit ? hit.distance : _rayLength, didHit);
        
        if (didHit)
        {
            Debug.Log($"Hit {hit.collider.name} at {hit.point}");
        }
        else
        {
            Debug.Log($"Missed shot");
        }
        
    }

    [ObserversRpc]
    private void ShowShotClientRpc(Vector3 origin, Vector3 direction, float distance, bool hit)
    {
#if UNITY_EDITOR
        _activeShots.Add(new DebugShot
        {
            Origin = origin,
            Direction = direction,
            Distance = distance,
            Hit = hit,
            EndTime = Time.time + _debugDrawDuration
        });
#endif
    }
}