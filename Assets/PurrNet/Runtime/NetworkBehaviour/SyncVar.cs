namespace PurrNet
{
    public class SyncVar<T>
    {
        public T Value
        {
            get => _value;
            set
            {
                if (!Equals(_value, value))
                {
                    var oldValue = _value;
                    _value = value;
                    SendChange(value);
                }
            }
        }

        public delegate void ReceiveChangeDelegate(T oldValue, T newValue, bool asServer);
        public event ReceiveChangeDelegate OnChange;

        private T _value;

        public SyncVar(T value = default)
        {
            _value = value;
        }
        
        private void SendChange(T value)
        {
            if(InstanceHandler.NetworkManager == null)
                return;
            
            var isServer = InstanceHandler.NetworkManager.isServer;

            if (isServer)
                ReceiveChange_Server(new(value));
            else
                InstanceHandler.NetworkManager.GetModule<PlayersBroadcaster>(false).SendToServer(new ServerData(value));
        }

        private void ReceiveChange_Server(ServerData data)
        {
            var oldValue = _value;
            _value = data.value;
            OnChangeInternal(oldValue, data.value, true);
            InstanceHandler.NetworkManager.GetModule<PlayersBroadcaster>(true).SendToAll(new ClientData(data.value));
        }
        
        private void ReceiveChange_Client(ClientData clientData)
        {
            var oldValue = _value;
            _value = clientData.value;
            OnChangeInternal(oldValue, clientData.value, false);
        }
        
        private void OnChangeInternal(T oldValue, T newValue, bool isServer)
        {
            OnChange?.Invoke(oldValue, newValue, isServer);
        }

        public static implicit operator T(SyncVar<T> syncVar) => syncVar.Value;
        
        private struct ClientData
        {
            public T value;
            public ClientData(T value)
            {
                this.value = value;
            }
        }
        private struct ServerData
        {
            public T value;
            public ServerData(T value)
            {
                this.value = value;
            }
        }
    }
}
