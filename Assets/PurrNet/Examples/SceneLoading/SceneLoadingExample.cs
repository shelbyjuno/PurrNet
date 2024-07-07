using PurrNet;
using PurrNet.Modules;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadingExample : MonoBehaviour
{
    [SerializeField] private int sceneIndex;
    [SerializeField] private bool async;
    [SerializeField] private bool load;
    [SerializeField] private LoadSceneMode loadSceneMode;
    
    [ContextMenu("Execute")]
    public void Execute()
    {
        var scenes = NetworkManager.main.GetModule<ScenesModule>(true);
        
        if (load)
        {
            if (async)
            {
                scenes.LoadSceneAsync(sceneIndex, loadSceneMode);
            }
            else
            {
                scenes.LoadSceneAsync(sceneIndex, loadSceneMode);
            }
        }
        else
        {
            scenes.UnloadSceneAsync(sceneIndex);
        }
    }
}
