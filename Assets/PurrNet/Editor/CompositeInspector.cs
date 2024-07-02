using PurrNet.Transports;
using UnityEditor;
using UnityEngine;

namespace PurrNet.Editor
{
    [CustomEditor(typeof(CompositeTransport), true)]
    public class CompositeInspector : UnityEditor.Editor
    {
        private SerializedProperty _transportArray;

        private void OnEnable()
        {
            _transportArray = serializedObject.FindProperty("_transports");
        }

        public override void OnInspectorGUI()
        {
            var composite = (CompositeTransport)target;
            if (composite.clientState != ConnectionState.Disconnected || composite.listenerState != ConnectionState.Disconnected)
                GUI.enabled = false;
            EditorGUILayout.PropertyField(_transportArray);
            GUI.enabled = true;
            
            TransportInspector.DrawTransportStatus(composite);
        }
    }
}
