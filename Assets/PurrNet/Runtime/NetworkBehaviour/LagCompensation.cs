using PurrNet.Logging;
using PurrNet.Modules;
using UnityEngine;

namespace PurrNet
{
    /// <summary>
    /// Stores a snapshot of an entity's transform and collider state at a specific time
    /// </summary>
    public struct NetworkSnapshot
    {
        public uint Tick;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public Bounds ColliderBounds;

        public static NetworkSnapshot Create(Transform transform, Collider collider, uint tick)
        {
            return new NetworkSnapshot
            {
                Tick = tick,
                Position = transform.position,
                Rotation = transform.rotation,
                Scale = transform.localScale,
                ColliderBounds = collider != null ? collider.bounds : new Bounds(transform.position, Vector3.zero)
            };
        }
    }
    
    public struct LagCompensatedHit
    {
        public Vector3 point;
        public Vector3 normal;
        public float distance;
        public Collider collider;
    }
    
    /// <summary>
    /// Add this component to any NetworkIdentity that needs lag compensation
    /// </summary>
    public class LagCompensation : NetworkBehaviour
    {
        private LagCompensationModule lagCompensation;

        protected override void OnSpawned()
        {
            base.OnSpawned();
        
            if (isServer)
            {
                if (!networkManager.TryGetModule(out LagCompensationModule module, isServer))
                {
                    PurrLogger.LogError($"LagCompensated entity '{name}' could not find LagCompensationModule on server.", this);
                    return;
                }
                lagCompensation = module;
                if (lagCompensation != null)
                {
                    lagCompensation.RegisterEntity(this);
                }
            }
        }

        protected override void OnDespawned()
        {
            if (isServer && lagCompensation != null)
            {
                lagCompensation.UnregisterEntity(this);
            }
        
            base.OnDespawned();
        }
        
        public static bool Raycast(Vector3 origin, Vector3 direction, uint tick, out RaycastHit hit, bool debug = false)
        {
            if (!InstanceHandler.NetworkManager.isServer)
            {
                PurrLogger.LogError($"Can't perform LagCompensated Raycast as client.");
                hit = default;
                return false;
            }
            
            if (!InstanceHandler.NetworkManager.TryGetModule(out LagCompensationModule module, true))
            {
                hit = default;
                return false;
            }

            return module.RaycastAtTick(new Ray(origin, direction), tick, out hit, Mathf.Infinity, debug);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !isServer) return;
        
            if (TryGetComponent<Collider>(out var collider))
            {
                Gizmos.color = new Color(1, 1, 0, 0.3f);
                Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
            }
        }
#endif
    }
}
