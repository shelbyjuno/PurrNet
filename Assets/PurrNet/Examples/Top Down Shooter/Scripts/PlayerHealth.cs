using UnityEngine;

namespace PurrNet.Examples.TopDownShooter
{
    public class PlayerHealth : NetworkIdentity
    {
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private TextMesh healthText;
        private readonly SyncVar<int> _health = new();

        protected override void OnSpawned(bool asServer)
        {
            if (asServer)
                _health.value = maxHealth;
        }

        private void Update()
        {
            Vector3 direction = healthText.transform.position - Camera.main.transform.position;
            direction.x = 0;
            healthText.transform.rotation = Quaternion.LookRotation(direction);
        }

        private void FixedUpdate()
        {
            healthText.text = _health.value.ToString();
        }
        
        public void ChangeHealth(int change)
        {
            if (_health + change <= 0)
            {
                Destroy(gameObject);
                return;
            }

            ChangeHealth_Server(change);
        }

        [ServerRPC(requireOwnership: false)]
        private void ChangeHealth_Server(int change)
        {
            _health.value += change;
        }
    }
}