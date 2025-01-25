using System.Text;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public static class GameObjectHasher
    {
        public static uint ComputeHashRecursive(GameObject obj)
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
            try
            {
                foreach (var component in components)
                {
                    sb.Append(component.GetType().FullName);

                    // Add serializable fields in a deterministic order
                    var fields = component.GetType()
                        .GetFields(System.Reflection.BindingFlags.Public |
                                   System.Reflection.BindingFlags.Instance);

                    foreach (var field in fields)
                    {
                        var value = field.GetValue(component);
                        if (value != null)
                        {
                            sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                                "{0}:{1}", field.Name, value.ToString());
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                sb.Append(e.Message);
            }

            var childCount = obj.transform.childCount;
            sb.Append(childCount);
            for (var i = 0; i < childCount; i++)
            {
                var child = obj.transform.GetChild(i);
                sb.Append(ComputeHashRecursive(child.gameObject));
            }

            return Hasher.ActualHash(sb.ToString());
        }
    }
}
