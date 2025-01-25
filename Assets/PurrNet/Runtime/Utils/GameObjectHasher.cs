using System.Text;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public static class GameObjectHasher
    {
        public static StringBuilder ComputeStringRecursive(GameObject obj)
        {
            var sb = new StringBuilder();
            
            // Use invariant culture for consistent string formatting
            sb.Append(obj.name);
            sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, 
                "P:{0:F6},{1:F6},{2:F6}", 
                obj.transform.position.x, 
                obj.transform.position.y, 
                obj.transform.position.z);
            
            sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                "R:{0:F6},{1:F6},{2:F6},{3:F6}", 
                obj.transform.rotation.x, 
                obj.transform.rotation.y, 
                obj.transform.rotation.z,
                obj.transform.rotation.w);
                
            sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                "S:{0:F6},{1:F6},{2:F6}", 
                obj.transform.localScale.x, 
                obj.transform.localScale.y, 
                obj.transform.localScale.z);
            
            var components = obj.GetComponents<Component>();
            sb.Append(components.Length);

            foreach (var component in components)
            {
                if (component)
                    sb.Append(component.GetType().FullName);
            }

            var childCount = obj.transform.childCount;
            sb.Append(childCount);
            for (var i = 0; i < childCount; i++)
            {
                var child = obj.transform.GetChild(i);
                sb.Append(ComputeStringRecursive(child.gameObject));
            }

            return sb;
        }
        
        public static uint ComputeHashRecursive(GameObject obj)
        {
            return Hasher.ActualHash(ComputeStringRecursive(obj).ToString());
        }
    }
}
