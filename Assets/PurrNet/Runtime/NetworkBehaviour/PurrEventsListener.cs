using System;
using UnityEngine;

namespace PurrNet
{
    [DefaultExecutionOrder(-36000)]
    internal sealed class PurrEventsListener : MonoBehaviour
    {
        internal event Action onDestroy;
        
        private void OnDestroy()
        {
            onDestroy?.Invoke();
        }
    }
    
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(PurrEventsListener))]
    class OnGameObjectDestroyedListenerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() { }
    }
#endif
}
