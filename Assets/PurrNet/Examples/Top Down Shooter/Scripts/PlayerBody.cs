using System.Collections.Generic;
using UnityEngine;

namespace PurrNet.Examples.TopDownShooter
{
    public class PlayerBody : NetworkIdentity
    {
        [SerializeField] private List<GameObject> bodies = new();

        protected override void OnSpawned(bool asServer)
        {
            if (!owner.HasValue)
            {
                Debug.LogError($"No owner for player", this);
                return;
            }

            int index = 0;
            if (owner.Value.id != 0)
                index = (int)(owner.Value.id % bodies.Count);

            for (int i = 0; i < bodies.Count; i++)
            {
                bodies[i].SetActive(i == index);
            }
        }
    }
}