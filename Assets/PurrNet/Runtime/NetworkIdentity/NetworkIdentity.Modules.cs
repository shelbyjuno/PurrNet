using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Modules;

namespace PurrNet
{
    public partial class NetworkIdentity
    {
        public IReadOnlyList<NetworkModule> modules => _externalModulesView;
        
        private readonly List<NetworkModule> _externalModulesView = new ();
        private readonly List<NetworkModule> _modules = new ();
        
        private byte _moduleId;

        [UsedByIL]
        public void RegisterModuleInternal(string moduleName, string type, NetworkModule module)
        {
            if (module == null)
            {
                ++_moduleId;
                
                if (_moduleId >= byte.MaxValue)
                {
                    PurrLogger.LogError($"Too many modules in {GetType().Name}! Max is {byte.MaxValue}.\n" +
                                        $"This could also happen with circular dependencies.", this);
                    return;
                }
                
                _modules.Add(null);
                PurrLogger.LogError($"Module in {GetType().Name} is null: <i>{type}</i> {moduleName};\n" +
                                    $"Ensure it isn't null once identity is spawned. A good place to initialize it could be in Awake().", this);
                return;
            }

            module.SetComponentParent(this, _moduleId++, moduleName);
            
            if (_moduleId >= byte.MaxValue)
            {
                PurrLogger.LogError($"Too many modules in {GetType().Name}! Max is {byte.MaxValue}.\n" +
                                    $"This could also happen with circular dependencies.", this);
                return;
            }
            
            _modules.Add(module);
            _externalModulesView.Add(module);
        }
        
        public bool TryGetModule(byte moduleId, out NetworkModule module)
        {
            if (moduleId >= _modules.Count)
            {
                module = null;
                return false;
            }
            
            module = _modules[moduleId];
            return true;
        }

        private void RegisterEvents()
        {
            for (var i = 0; i < _externalModulesView.Count; i++)
            {
                var module = _externalModulesView[i];
                if (module is ITick tickableModule)
                {
                    _tickables.Add(tickableModule);
                }
            }
        }
    }
}
