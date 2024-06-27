using System.Collections.Generic;
using Rabsi.Modules;
using Rabsi.Transports;

namespace Rabsi
{
    internal readonly struct ModulesCollection
    {
        private readonly List<INetworkModule> _modules;
        private readonly List<IConnectionListener> _connectionListeners;
        private readonly List<IConnectionStateListener> _connectionStateListeners;
        private readonly List<IDataListener> _dataListeners;

        private readonly NetworkManager _manager;
        private readonly bool _asServer;
        
        public ModulesCollection(NetworkManager manager, bool asServer)
        {
            _modules = new List<INetworkModule>();
            _connectionListeners = new List<IConnectionListener>();
            _connectionStateListeners = new List<IConnectionStateListener>();
            _dataListeners = new List<IDataListener>();
            _manager = manager;
            _asServer = asServer;
        }
        
        public bool TryGetModule<T>(out T module) where T : INetworkModule
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is T mod)
                {
                    module = mod;
                    return true;
                }
            }

            module = default;
            return false;
        }
        
        public void RegisterModules()
        {
            UnregisterModules();
            
            _manager.RegisterModules(this, _asServer);

            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i].Enable(_manager);
                
                // ReSharper disable once SuspiciousTypeConversion.Global
                if (_modules[i] is IConnectionListener connectionListener)
                    _connectionListeners.Add(connectionListener);
                
                // ReSharper disable once SuspiciousTypeConversion.Global
                if (_modules[i] is IConnectionStateListener connectionStateListener)
                    _connectionStateListeners.Add(connectionStateListener);
                
                // ReSharper disable once SuspiciousTypeConversion.Global
                if (_modules[i] is IDataListener dataListener)
                    _dataListeners.Add(dataListener);
            }
        }
        
        public void OnNewConnection(Connection conn, bool asserver)
        {
            for (int i = 0; i < _connectionListeners.Count; i++)
                _connectionListeners[i].OnConnected(conn, asserver);
        }

        public void OnLostConnection(Connection conn, bool asserver)
        {
            for (int i = 0; i < _connectionListeners.Count; i++)
                _connectionListeners[i].OnDisconnected(conn, asserver);
        }

        public void OnDataReceived(Connection conn, ByteData data, bool asserver)
        {
            for (int i = 0; i < _dataListeners.Count; i++)
                _dataListeners[i].OnDataReceived(conn, data, asserver);
        }

        public void OnConnectionState(ConnectionState state, bool asserver)
        {
            for (int i = 0; i < _connectionStateListeners.Count; i++)
                _connectionStateListeners[i].OnConnectionState(state, asserver);
        }

        private void UnregisterModules()
        {
            for (int i = 0; i < _modules.Count; i++)
                _modules[i].Disable(_manager);
            
            _modules.Clear();
            _connectionListeners.Clear();
            _connectionStateListeners.Clear();
            _dataListeners.Clear();
        }

        public void AddModule(INetworkModule module)
        {
            _modules.Add(module);
        }
    }
}
