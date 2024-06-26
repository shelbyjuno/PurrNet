using System.Collections.Generic;
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
        
        public void RegisterModules()
        {
            UnregisterModules();
            
            NetworkManager.RegisterModules(this, _asServer);
            _modules.Sort((moduleA, moduleB) => moduleA.priority.CompareTo(moduleB.priority));

            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i].Setup(_manager);
                
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
        
        public void UnregisterModules()
        {
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
