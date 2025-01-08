using System;
using JetBrains.Annotations;
using UnityEngine.Scripting;

namespace PurrNet
{
    [AttributeUsage(AttributeTargets.Class), Preserve]
    public class RegisterNetworkTypeAttribute : Attribute
    {
        public RegisterNetworkTypeAttribute([UsedImplicitly] Type type) { }
    }
}
