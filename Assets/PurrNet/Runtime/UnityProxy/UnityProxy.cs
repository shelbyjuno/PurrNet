using PurrNet.Modules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PurrNet
{
    [UsedByIL]
    public class UnityProxy
    {
        [UsedByIL]
        public static Object Instantiate(Object original)
        {
            return Object.Instantiate(original);
        }

        [UsedByIL]
        public static Object Instantiate(Object original, Transform parent, bool instantiateInWorldSpace)
        {
            return Object.Instantiate(original, parent, instantiateInWorldSpace);
        }
        
        [UsedByIL]
        public static Object Instantiate(Object original, Vector3 position, Quaternion rotation)
        {
            return Object.Instantiate(original, position, rotation);
        }

        [UsedByIL]
        public static Object Instantiate(
            Object original,
            Vector3 position,
            Quaternion rotation,
            Transform parent)
        {
            return Object.Instantiate(original, position, rotation, parent);
        }

        [UsedByIL]
        public static Object Instantiate(Object original, Scene scene)
        {
            return Object.Instantiate(original, scene);
        }
        
        [UsedByIL]
        public static Object Instantiate(Object original, Transform parent)
        {
            return Object.Instantiate(original, parent);
        }

        [UsedByIL]
        public static T Instantiate<T>(T original) where T : Object
        {
            return Object.Instantiate(original);
        }

        [UsedByIL]
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object
        {
            return Object.Instantiate(original, position, rotation);
        }
        
        [UsedByIL]
        public static T Instantiate<T>(
            T original,
            Vector3 position,
            Quaternion rotation,
            Transform parent)
            where T : Object
        {
            return Object.Instantiate(original, position, rotation, parent);
        }

        [UsedByIL]
        public static T Instantiate<T>(T original, Transform parent) where T : Object
        {
            return Object.Instantiate(original, parent);
        }
        
        [UsedByIL]
        public static T Instantiate<T>(T original, Transform parent, bool worldPositionStays) where T : Object
        {
            return Object.Instantiate(original, parent, worldPositionStays);
        }
    }
}
