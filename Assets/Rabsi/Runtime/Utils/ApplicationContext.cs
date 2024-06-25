using UnityEngine;

namespace Rabsi.Utils
{
    public static class ApplicationContext
    {
#if UNITY_SERVER && !UNITY_EDITOR
        public static bool isServerBuild => true;
        public static bool isClientBuild => false;
#elif !UNITY_EDITOR
        public static bool isServerBuild => Application.isBatchMode;
        public static bool isClientBuild => !Application.isBatchMode;
#else
        public static bool isServerBuild => !Application.isEditor && Application.isBatchMode;
        public static bool isClientBuild => false;
#endif
            
#if !UNITY_EDITOR
        public static bool isClone => false;
        public static bool isMainEditor => false;
#else
        public static bool isClone => ClonesContext.isClone;
        public static bool isMainEditor => Application.isEditor && !isClone;
#endif
    }
}