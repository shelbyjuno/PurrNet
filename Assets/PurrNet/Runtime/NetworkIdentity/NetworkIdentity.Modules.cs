using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Modules;

namespace PurrNet
{
    public partial class NetworkIdentity
    {
        private readonly List<NetworkModule> _modules = new ();

        [UsedByIL]
        protected void RegisterModuleInternal(string moduleName, string type, NetworkModule module)
        {
            if (module == null)
            {
                PurrLogger.LogError($"Module in {GetType().Name} is null: <i>{type}</i> {moduleName};\nEnsure it isn't null once identity is spawned. A good place to initialize it could be in Awake().", this);
                return;
            }

            module.SetComponentParent(this, (byte)_modules.Count, moduleName);
            _modules.Add(module);
        }
    }
}
