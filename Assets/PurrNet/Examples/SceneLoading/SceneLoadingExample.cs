using PurrNet;
using PurrNet.Modules;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadingExample : MonoBehaviour
{
    [SerializeField] private int sceneIndex;
    [SerializeField] private bool load;
    [SerializeField] private bool isPubic;
    [SerializeField] private LoadSceneMode loadSceneMode;

    [ContextMenu("Add All")]
    public void AddAllPlayers()
    {
        var scenes = NetworkManager.main.GetModule<ScenesModule>(true);
        var scenesPlayers = NetworkManager.main.GetModule<ScenePlayersModule>(true);
        var players = NetworkManager.main.GetModule<PlayersManager>(true);

        var scene = SceneManager.GetSceneByBuildIndex(sceneIndex);
        if (scenes.TryGetSceneID(scene, out var sceneID))
        {
            for (var i = 0; i < players.connectedPlayers.Count; i++)
            {
                var player = players.connectedPlayers[i];
                scenesPlayers.AddPlayerToScene(player, sceneID);
            }
        }
    }
    
    [ContextMenu("Execute")]
    public void Execute()
    {
        var scenes = NetworkManager.main.GetModule<ScenesModule>(true);
        
        if (load)
        {
            scenes.LoadSceneAsync(sceneIndex, new PurrSceneSettings
            {
                mode = loadSceneMode,
                isPublic = isPubic
            });
        }
        else
        {
            var scene = SceneManager.GetSceneByBuildIndex(sceneIndex);
            scenes.UnloadSceneAsync(scene);
        }
    }
}
