using UnityEngine;

namespace PurrNet.Examples.BoxCarry
{
    public class PlayerPickUp : NetworkBehaviour
    {
        [SerializeField] private float pickupRadius = 1;
        [SerializeField] private Vector3 pickupOffset;
        
        protected override void OnSpawned()
        {
            enabled = isOwner;
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
                
                box.PickUpBox(this);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(pickupOffset), pickupRadius);
        }
    }
}