using System.Collections.Generic;
using UnityEngine;

namespace PurrNet.Examples.Template
{
    public class PlayerBody : NetworkIdentity
    {
        [SerializeField] private List<GameObject> bodies = new List<GameObject>();

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