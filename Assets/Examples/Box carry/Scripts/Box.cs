using UnityEngine;
using UnityEngine.Serialization;

namespace PurrNet.Examples.BoxCarry
{
    [RequireComponent(typeof(Rigidbody))]
    public class Box : NetworkBehaviour
    {
        [SerializeField] private Material ownerMaterial, nonOwnerMaterial;
        [FormerlySerializedAs("renderer")] [SerializeField] private Renderer _renderer;

        private Rigidbody _rigidbody;
        private readonly SyncVar<bool> _gettingCarried = new SyncVar<bool>(ownerAuth: true);
        
        private void Awake()
        {
            if(!TryGetComponent(out _rigidbody))
                Debug.LogError($"Box could not get rigidbody!", this);
        }

        protected override void OnSpawned(bool asServer)
        {
            if (asServer && localPlayer.HasValue)
            {
                GiveOwnership(localPlayer.Value);
                _renderer.material = ownerMaterial;
                _rigidbody.isKinematic = false;
            }
        }

        protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer)
        {
            if (localPlayer == newOwner)
            {
                _renderer.material = ownerMaterial;
            }
            else
            {
                _renderer.material = nonOwnerMaterial;
                _rigidbody.isKinematic = true;
            }
        }

        public void PickUpBox(PlayerPickUp playerPickUp)
        {
            if (!playerPickUp.owner.HasValue)
                return;
            
            GiveOwnership(playerPickUp.owner.Value);
            transform.SetParent(playerPickUp.transform);
            transform.localPosition = Vector3.up + Vector3.forward;
            _rigidbody.isKinematic = true;
            _gettingCarried.value = true;
            
            //TODO: This should be able to go directly to ObserversRPC from client, once logic is in place
            BoxPickedUp_Server();
        }

        [ServerRpc]
        private void BoxPickedUp_Server()
        {
            BoxPickedUp();
        }
        
        [ObserversRpc(excludeOwner:true)]
        private void BoxPickedUp()
        {
            if(!InstanceHandler.TryGetInstance<PlayerPickUp>(out var playerPickUp))
                return;
            
            playerPickUp.BoxTaken(this);
        }
        
        public void DropBox()
        {
            transform.SetParent(null);
            _rigidbody.isKinematic = false;
            _gettingCarried.value = false;
        }

        private void OnCollisionEnter(Collision other)
        {
            if (_gettingCarried.value)
                return;
            
            if (other.gameObject.TryGetComponent(out PlayerPickUp playerPickUp))
            {
                if (!playerPickUp.owner.HasValue)
                    return;

                if (playerPickUp.owner.Value == owner || playerPickUp.owner.Value != localPlayer)
                    return;
            
                GiveOwnership(localPlayer.Value);
            }

            if (other.gameObject.TryGetComponent(out Box box))
            {
                if (!box.owner.HasValue)
                    return;
                
                if (box.owner.Value == owner || box.owner.Value != localPlayer)
                    return;
                
                GiveOwnership(localPlayer.Value);
            }
        }
    }
}