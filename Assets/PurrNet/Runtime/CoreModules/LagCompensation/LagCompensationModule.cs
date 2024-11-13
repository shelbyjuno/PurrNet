using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using PurrNet.Logging;

namespace PurrNet.Modules
{
    public class LagCompensationModule : INetworkModule, IFixedUpdate
    {
        private readonly NetworkManager _networkManager;
        private readonly uint _maxHistoryTicks;
        private TickManager _tickManager;
        
        private readonly Dictionary<NetworkIdentity, List<NetworkSnapshot>> _entityHistory = new();
        private static readonly List<(Collider collider, NetworkSnapshot snapshot)> _potentialHits = new();

        public LagCompensationModule(NetworkManager networkManager, uint maxHistoryTicks = 60)
        {
            _networkManager = networkManager;
            _maxHistoryTicks = maxHistoryTicks;
        }

        public void Enable(bool asServer)
        {
            if (asServer)
            {
                _tickManager = _networkManager.GetModule<TickManager>(true);
                _tickManager.onTick += RecordSnapshots;
            }
        }

        public void Disable(bool asServer)
        {
            if (asServer && _tickManager != null)
            {
                _tickManager.onTick -= RecordSnapshots;
            }
            
            _entityHistory.Clear();
        }

        public void FixedUpdate()
        {
            // Module needs IFixedUpdate for interface compatibility, but actual updates are handled via TickManager
        }
        
        /// <summary>
        /// Registers a network entity to be tracked for lag compensation.
        /// Only registered entities will have their positions recorded and rewound.
        /// </summary>
        /// <param name="identity">The NetworkIdentity to track</param>
        public void RegisterEntity(NetworkIdentity identity)
        {
            if (!_entityHistory.ContainsKey(identity))
            {
                _entityHistory[identity] = new List<NetworkSnapshot>();
            }
        }
        
        /// <summary>
        /// Unregisters a network entity from lag compensation tracking.
        /// </summary>
        /// <param name="identity">The NetworkIdentity to stop tracking</param>
        public void UnregisterEntity(NetworkIdentity identity)
        {
            _entityHistory.Remove(identity);
        }

        private void RecordSnapshots()
        {
            uint currentTick = _tickManager.tick;
            
            foreach (var kvp in _entityHistory)
            {
                var identity = kvp.Key;
                
                if (identity != null && identity.gameObject.activeInHierarchy)
                {
                    var snapshot = NetworkSnapshot.Create(
                        identity.transform,
                        identity.GetComponent<Collider>(),
                        currentTick
                    );
                    
                    kvp.Value.Add(snapshot);
                }
            }

            CleanupOldSnapshots(currentTick);
        }

        private void CleanupOldSnapshots(uint currentTick)
        {
            uint cutoffTick = currentTick - _maxHistoryTicks;
            
            foreach (var history in _entityHistory.Values)
            {
                history.RemoveAll(snapshot => snapshot.Tick < cutoffTick);
            }
        }
        
        /// <summary>
        /// Performs a raycast with lag compensation, rewinding tracked entities to their positions at the specified tick.
        /// </summary>
        /// <param name="ray">The ray to cast</param>
        /// <param name="tick">The historical tick to check against</param>
        /// <param name="hit">Information about the raycast hit</param>
        /// <param name="maxDistance">Maximum distance of the raycast</param>
        /// <returns>True if the raycast hit something, false otherwise</returns>
        public bool RaycastAtTick(Ray ray, uint tick, out RaycastHit hit, float maxDistance = Mathf.Infinity, bool debug = false)
        {
            hit = default;
            _potentialHits.Clear();
            float closestHit = maxDistance;
            LagCompensatedHit? bestHit = null;

            foreach (var kvp in _entityHistory)
            {
                var snapshot = FindClosestSnapshot(kvp.Value, tick);
                if (!snapshot.HasValue) continue;

                if (snapshot.Value.ColliderBounds.IntersectRay(ray, out float distance) && distance < closestHit)
                {
                    var collider = kvp.Key.GetComponent<Collider>();
                    if (collider != null)
                    {
                        _potentialHits.Add((collider, snapshot.Value));
                    }
                }
            }

            if (_potentialHits.Count == 0)
                return false;

            foreach (var (collider, snapshot) in _potentialHits)
            {
                if (ComputeRayColliderIntersection(ray, collider, snapshot, out var lagHit))
                {
                    if (lagHit.distance < closestHit)
                    {
                        closestHit = lagHit.distance;
                        bestHit = lagHit;
                    }
                }
            }

        #if UNITY_EDITOR
            if (debug)
            {
                DrawDebugInfo(ray, tick, bestHit.HasValue);
            }
        #endif

            if (bestHit.HasValue)
            {
                return true;
            }

            return false;
        }

        private bool ComputeRayColliderIntersection(Ray ray, Collider collider, NetworkSnapshot snapshot, out LagCompensatedHit hitInfo)
        {
            hitInfo = new LagCompensatedHit();

            Matrix4x4 worldToSnapshot = Matrix4x4.TRS(snapshot.Position, snapshot.Rotation, snapshot.Scale);
            Matrix4x4 snapshotToWorld = worldToSnapshot.inverse;

            Vector3 rayOriginLocal = snapshotToWorld.MultiplyPoint3x4(ray.origin);
            Vector3 rayDirectionLocal = snapshotToWorld.MultiplyVector(ray.direction).normalized;
            Ray localRay = new Ray(rayOriginLocal, rayDirectionLocal);

            bool hit = false;
            Vector3 hitPoint = Vector3.zero;
            Vector3 hitNormal = Vector3.zero;

            switch (collider)
            {
                case BoxCollider box:
                    hit = IntersectRayBox(localRay, box.center, box.size / 2f, out hitPoint, out hitNormal);
                    break;

                case SphereCollider sphere:
                    hit = IntersectRaySphere(localRay, sphere.center, sphere.radius, out hitPoint, out hitNormal);
                    break;

                case CapsuleCollider capsule:
                    hit = IntersectRayCapsule(localRay, capsule, out hitPoint, out hitNormal);
                    break;
                
                case CharacterController character:
                    var capsuleParams = new CapsuleCollider
                    {
                        center = character.center,
                        radius = character.radius,
                        height = character.height,
                        direction = 1  // Y-axis (0 = X, 1 = Y, 2 = Z)
                    };
                    hit = IntersectRayCapsule(localRay, capsuleParams, out hitPoint, out hitNormal);
                    break;

                default:
                    PurrLogger.LogWarning($"Unsupported collider type: {collider.GetType().Name}");
                    return false;
            }

            if (hit)
            {
                Vector3 worldHitPoint = worldToSnapshot.MultiplyPoint3x4(hitPoint);
                Vector3 worldHitNormal = worldToSnapshot.MultiplyVector(hitNormal).normalized;

                hitInfo = new LagCompensatedHit
                {
                    point = worldHitPoint,
                    normal = worldHitNormal,
                    distance = Vector3.Distance(ray.origin, worldHitPoint),
                    collider = collider
                };
                return true;
            }

            return false;
        }
        
        private bool IntersectRayBox(Ray ray, Vector3 center, Vector3 halfExtents, out Vector3 hitPoint, out Vector3 hitNormal)
        {
            hitPoint = Vector3.zero;
            hitNormal = Vector3.zero;

            Vector3 min = center - halfExtents;
            Vector3 max = center + halfExtents;

            float tMin = float.MinValue;
            float tMax = float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                if (Mathf.Approximately(ray.direction[i], 0f))
                {
                    if (ray.origin[i] < min[i] || ray.origin[i] > max[i])
                        return false;
                }
                else
                {
                    float invD = 1f / ray.direction[i];
                    float t1 = (min[i] - ray.origin[i]) * invD;
                    float t2 = (max[i] - ray.origin[i]) * invD;

                    if (t1 > t2)
                    {
                        float temp = t1;
                        t1 = t2;
                        t2 = temp;
                    }

                    tMin = Mathf.Max(tMin, t1);
                    tMax = Mathf.Min(tMax, t2);

                    if (tMin > tMax)
                        return false;
                }
            }

            hitPoint = ray.origin + ray.direction * tMin;
            
            Vector3 pointLocal = hitPoint - center;
            float epsilon = 0.0001f;
            
            if (Mathf.Abs(pointLocal.x - halfExtents.x) < epsilon) hitNormal = Vector3.right;
            else if (Mathf.Abs(pointLocal.x + halfExtents.x) < epsilon) hitNormal = Vector3.left;
            else if (Mathf.Abs(pointLocal.y - halfExtents.y) < epsilon) hitNormal = Vector3.up;
            else if (Mathf.Abs(pointLocal.y + halfExtents.y) < epsilon) hitNormal = Vector3.down;
            else if (Mathf.Abs(pointLocal.z - halfExtents.z) < epsilon) hitNormal = Vector3.forward;
            else if (Mathf.Abs(pointLocal.z + halfExtents.z) < epsilon) hitNormal = Vector3.back;

            return true;
        }

        private bool IntersectRaySphere(Ray ray, Vector3 center, float radius, out Vector3 hitPoint, out Vector3 hitNormal)
        {
            hitPoint = Vector3.zero;
            hitNormal = Vector3.zero;

            Vector3 m = ray.origin - center;
            float b = Vector3.Dot(m, ray.direction);
            float c = Vector3.Dot(m, m) - radius * radius;

            if (c > 0f && b > 0f)
                return false;

            float discriminant = b * b - c;

            if (discriminant < 0f)
                return false;

            float t = -b - Mathf.Sqrt(discriminant);

            if (t < 0f)
                t = -b + Mathf.Sqrt(discriminant);

            hitPoint = ray.origin + t * ray.direction;
            hitNormal = (hitPoint - center).normalized;

            return true;
        }

        private bool IntersectRayCapsule(Ray ray, CapsuleCollider capsule, out Vector3 hitPoint, out Vector3 hitNormal)
        {
            hitPoint = Vector3.zero;
            hitNormal = Vector3.zero;

            Vector3 point1 = capsule.center;
            Vector3 point2 = capsule.center;
            float height = capsule.height;
            
            switch (capsule.direction)
            {
                case 0:
                    point1.x -= height * 0.5f;
                    point2.x += height * 0.5f;
                    break;
                case 1:
                    point1.y -= height * 0.5f;
                    point2.y += height * 0.5f;
                    break;
                case 2:
                    point1.z -= height * 0.5f;
                    point2.z += height * 0.5f;
                    break;
            }

            Vector3 d = point2 - point1;
            Vector3 m = ray.origin - point1;
            float md = Vector3.Dot(m, d);
            float dd = Vector3.Dot(d, d);
            float nd = Vector3.Dot(ray.direction, d);
            
            float nn = Vector3.Dot(ray.direction, ray.direction);
            float mn = Vector3.Dot(m, ray.direction);
            float a = dd * nn - nd * nd;
            float k = Vector3.Dot(m, m) - capsule.radius * capsule.radius;
            float c = dd * k - md * md;
            
            if (Mathf.Abs(a) < 0.0001f)
            {
                if (c > 0.0f)
                    return false;
                
                if (md < 0.0f)
                {
                    if (nd >= 0.0f)
                        return false;
                }
                else if (md > dd)
                {
                    if (nd <= 0.0f)
                        return false;
                }
                
                hitPoint = ray.origin - ray.direction * (k / Vector3.Dot(ray.direction, m));
                hitNormal = (hitPoint - (md < 0.0f ? point1 : md > dd ? point2 : 
                    point1 + d * (md / dd))).normalized;
                return true;
            }
            
            float b = dd * mn - nd * md;
            float discr = b * b - a * c;
            
            if (discr < 0.0f)
                return false;
            
            float t = (-b - Mathf.Sqrt(discr)) / a;
            float t0 = md + t * nd;
            
            if (t0 < 0.0f)
            {
                if (IntersectRaySphere(ray, point1, capsule.radius, out hitPoint, out hitNormal))
                    return true;
            }
            else if (t0 > dd)
            {
                if (IntersectRaySphere(ray, point2, capsule.radius, out hitPoint, out hitNormal))
                    return true;
            }
            else
            {
                hitPoint = ray.origin + t * ray.direction;
                Vector3 closest = point1 + d * (t0 / dd);
                hitNormal = (hitPoint - closest).normalized;
                return true;
            }
            
            return false;
        }

        private NetworkSnapshot? FindClosestSnapshot(List<NetworkSnapshot> snapshots, uint tick)
        {
            if (snapshots.Count == 0) return null;

            return snapshots
                .Where(s => s.Tick <= tick)
                .OrderByDescending(s => s.Tick)
                .FirstOrDefault();
        }
        
#if UNITY_EDITOR
private void DrawDebugInfo(Ray ray, uint tick, bool hit)
{
    Debug.DrawLine(ray.origin, ray.origin + ray.direction * 100f, hit ? Color.red : Color.yellow, 1f);

    foreach (var kvp in _entityHistory)
    {
        var snapshot = FindClosestSnapshot(kvp.Value, tick);
        if (!snapshot.HasValue) continue;

        Color boundsColor = new Color(0, 1, 0, 0.3f);
        Vector3 center = snapshot.Value.Position;
        Vector3 size = snapshot.Value.ColliderBounds.size;
        Quaternion rotation = snapshot.Value.Rotation;

        Vector3[] points = new Vector3[8];
        points[0] = center + rotation * new Vector3(-size.x, -size.y, -size.z) * 0.5f;
        points[1] = center + rotation * new Vector3(size.x, -size.y, -size.z) * 0.5f;
        points[2] = center + rotation * new Vector3(size.x, -size.y, size.z) * 0.5f;
        points[3] = center + rotation * new Vector3(-size.x, -size.y, size.z) * 0.5f;
        points[4] = center + rotation * new Vector3(-size.x, size.y, -size.z) * 0.5f;
        points[5] = center + rotation * new Vector3(size.x, size.y, -size.z) * 0.5f;
        points[6] = center + rotation * new Vector3(size.x, size.y, size.z) * 0.5f;
        points[7] = center + rotation * new Vector3(-size.x, size.y, size.z) * 0.5f;

        Debug.DrawLine(points[0], points[1], boundsColor, 1f);
        Debug.DrawLine(points[1], points[2], boundsColor, 1f);
        Debug.DrawLine(points[2], points[3], boundsColor, 1f);
        Debug.DrawLine(points[3], points[0], boundsColor, 1f);

        Debug.DrawLine(points[4], points[5], boundsColor, 1f);
        Debug.DrawLine(points[5], points[6], boundsColor, 1f);
        Debug.DrawLine(points[6], points[7], boundsColor, 1f);
        Debug.DrawLine(points[7], points[4], boundsColor, 1f);

        Debug.DrawLine(points[0], points[4], boundsColor, 1f);
        Debug.DrawLine(points[1], points[5], boundsColor, 1f);
        Debug.DrawLine(points[2], points[6], boundsColor, 1f);
        Debug.DrawLine(points[3], points[7], boundsColor, 1f);
    }
}
#endif
    }

    public static class LagCompensationExtensions
    {
        /// <summary>
        /// Extension method for performing a raycast with lag compensation.
        /// Provides a more convenient syntax for simple raycasts.
        /// </summary>
        /// <param name="module">The LagCompensationModule to use</param>
        /// <param name="origin">Starting point of the ray</param>
        /// <param name="direction">Direction of the ray</param>
        /// <param name="tick">The historical tick to check against</param>
        /// <param name="hit">Information about the raycast hit</param>
        /// <param name="maxDistance">Maximum distance of the raycast</param>
        /// <returns>True if the raycast hit something, false otherwise</returns>
        public static bool RaycastWithCompensation(
            this LagCompensationModule module,
            Vector3 origin,
            Vector3 direction,
            out RaycastHit hit,
            uint tick,
            float maxDistance = Mathf.Infinity)
        {
            return module.RaycastAtTick(new Ray(origin, direction), tick, out hit, maxDistance);
        }
    }
}