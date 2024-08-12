using UnityEngine;

namespace PurrNet.Examples.BoxCarry
{
    [RequireComponent(typeof(Rigidbody))]
    public class Box : NetworkBehaviour
    {
        [SerializeField] private Material ownerMaterial, nonOwnerMaterial;
        [SerializeField] private Renderer renderer;

        private Rigidbody _rigidbody;
        
        private void Awake()
        {
            if(!TryGetComponent(out _rigidbody))
                Debug.LogError($"Box could not get rigidbody!", this);
        }

        protected override void OnSpawned(bool asServer)
        {
            if (asServer)
            {
                GiveOwnership(localPlayer);
                renderer.material = ownerMaterial;
                _rigidbody.isKinematic = false;
            }
        }

        protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer)
        {
            if (localPlayer == newOwner)
            {
                renderer.material = ownerMaterial;
                _rigidbody.isKinematic = false;
            }
            else
            {
                renderer.material = nonOwnerMaterial;
                _rigidbody.isKinematic = true;
            }
        }

        public void PickUpBox(PlayerPickUp playerPickUp)
        {
            if (!playerPickUp.owner.HasValue)
                return;
            
            GiveOwnership(playerPickUp.owner.Value);
        }

        private void OnCollisionEnter(Collision other)
        {
            if (!other.gameObject.TryGetComponent(out PlayerPickUp playerPickUp))
                return;
            
            if (!playerPickUp.owner.HasValue)
                return;
            
            GiveOwnership(playerPickUp.owner.Value);
        }
    }
}