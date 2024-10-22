using UnityEngine;

namespace PurrNet.Examples.BoxCarry
{
    public class PlayerPickUp : NetworkBehaviour
    {
        [SerializeField] private float pickupRadius = 1;
        [SerializeField] private Vector3 pickupOffset;

        private Box _carriedBox;
        
        protected override void OnSpawned()
        {
            enabled = isOwner;
            if(isOwner)
                InstanceHandler.RegisterInstance(this);
        }

        protected override void OnDespawned(bool asServer)
        {
            if(isOwner)
                InstanceHandler.UnregisterInstance<PlayerPickUp>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.E))
                AttemptPickUp();
        }

        private static Collider[] _check = new Collider[30];
        private void AttemptPickUp()
        {
            int hits = Physics.OverlapSphereNonAlloc(transform.TransformPoint(pickupOffset), pickupRadius, _check);
            for (int i = 0; i < hits; i++)
            {
                if (!_check[i].TryGetComponent(out Box box))
                    continue;

                if (_carriedBox && _carriedBox == box)
                    continue;
                
                if(_carriedBox) _carriedBox.DropBox();
                _carriedBox = box;
                box.PickUpBox(this);
                return;
            }
            
            if(_carriedBox) _carriedBox.DropBox();
            _carriedBox = null;
        }

        public void BoxTaken(Box box)
        {
            if (box == _carriedBox)
                _carriedBox = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(pickupOffset), pickupRadius);
        }
    }
}