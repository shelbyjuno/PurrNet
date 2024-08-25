using System;
using System.Collections.Generic;
using PurrNet.Examples.Sumo;
using UnityEngine;

public class FloorCollider : MonoBehaviour
{
    [SerializeField] private List<Transform> spawnPoints = new();

    private void OnTriggerEnter(Collider other)
    {
        if (!other.transform.TryGetComponent(out Movement_RB_InputSync player))
            return;
        
        int index = UnityEngine.Random.Range(0, spawnPoints.Count);
        player.ResetPosition(spawnPoints[index].position);
    }
}
