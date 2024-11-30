using UnityEngine;

namespace PurrNet
{
    public abstract class PurrMonoBehaviour : MonoBehaviour, IPurrEvents
    {
        public virtual void OnEnable()
        {
            NetworkManager.main.RegisterEvents(this);
        }
        
        public virtual void OnDisable()
        {
            NetworkManager.main.UnregisterEvents(this);
        }
        
        public abstract void Subscribe(NetworkManager manager, bool asServer);

        public abstract void Unsubscribe(NetworkManager manager, bool asServer);
    }
}