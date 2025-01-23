using PurrNet;
using PurrNet.Logging;
using PurrNet.Modules;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadingExample : MonoBehaviour
{
    [SerializeField] private int sceneIndex;
    [SerializeField] private bool load;
    [SerializeField] private bool isPubic;
    [SerializeField] private LoadSceneMode loadSceneMode;

    [ContextMenu("Tests/Add All")]
    public void AddAllPlayers()
    {
        var scenes = NetworkManager.main.GetModule<ScenesModule>(true);
        var scenesPlayers = NetworkManager.main.GetModule<ScenePlayersModule>(true);
        var players = NetworkManager.main.GetModule<PlayersManager>(true);

        var scene = gameObject.scene;
        if (scenes.TryGetSceneID(scene, out var sceneID))
        {
            for (var i = 0; i < players.players.Count; i++)
            {
                var player = players.players[i];
                scenesPlayers.AddPlayerToScene(player, sceneID);
            }
        }
        else PurrLogger.LogError($"Scene with build index {sceneIndex} '{scene.name}' not found");
    }
    
    [ContextMenu("Tests/Move All Players")]
    public void MoveAllPlayers()
    {
        var scenes = NetworkManager.main.GetModule<ScenesModule>(true);
        var scenesPlayers = NetworkManager.main.GetModule<ScenePlayersModule>(true);
        var players = NetworkManager.main.GetModule<PlayersManager>(true);

        var scene = gameObject.scene;
        if (scenes.TryGetSceneID(scene, out var sceneID))
        {
            for (var i = 0; i < players.players.Count; i++)
            {
                var player = players.players[i];
                scenesPlayers.MovePlayerToSingleScene(player, sceneID);
            }
        }
    }
    
    [ContextMenu("Tests/Execute")]
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
