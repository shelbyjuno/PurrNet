using System.Collections.Generic;
using JetBrains.Annotations;
using PurrNet.Packets;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet
{
    public enum NetworkAnimatorParameterType : byte
    {
        Bool,
        Float,
        Int
    }
    
    public partial struct NetworkAnimatorParameter : INetworkedData
    {
        public int nameHash;
        public NetworkAnimatorParameterType type;
        
        public float floatValue;
        public int intValue;
        public bool boolValue;
        
        public NetworkAnimatorParameter(Animator anim, AnimatorControllerParameter param)
        {
            nameHash = param.nameHash;
            
            type = param.type switch
            {
                AnimatorControllerParameterType.Bool => NetworkAnimatorParameterType.Bool,
                AnimatorControllerParameterType.Float => NetworkAnimatorParameterType.Float,
                AnimatorControllerParameterType.Int => NetworkAnimatorParameterType.Int,
                _ => NetworkAnimatorParameterType.Bool
            };

            floatValue = param.defaultFloat;
            intValue = param.defaultInt;
            boolValue = param.defaultBool;

            switch (type)
            {
                case NetworkAnimatorParameterType.Bool:
                    boolValue = anim.GetBool(param.nameHash);
                    break;
                
                case NetworkAnimatorParameterType.Float:
                    floatValue = anim.GetFloat(param.nameHash);
                    break;
                
                case NetworkAnimatorParameterType.Int:
                    intValue = anim.GetInteger(param.nameHash);
                    break;
            }
        }
        
        public void Apply(Animator anim)
        {
            switch (type)
            {
                case NetworkAnimatorParameterType.Bool:
                    anim.SetBool(nameHash, boolValue);
                    break;
                
                case NetworkAnimatorParameterType.Float:
                    anim.SetFloat(nameHash, floatValue);
                    break;
                
                case NetworkAnimatorParameterType.Int:
                    anim.SetInteger(nameHash, intValue);
                    break;
            }
        }

        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref nameHash);
            packer.Serialize(ref type);
            
            switch (type)
            {
                case NetworkAnimatorParameterType.Bool:
                    packer.Serialize(ref boolValue);
                    break;
                
                case NetworkAnimatorParameterType.Float:
                    packer.Serialize(ref floatValue);
                    break;
                
                case NetworkAnimatorParameterType.Int:
                    packer.Serialize(ref intValue);
                    break;
            }
        }

        public bool AreEqual(NetworkAnimatorParameter cached)
        {
            return type switch
            {
                NetworkAnimatorParameterType.Bool => boolValue == cached.boolValue,
                NetworkAnimatorParameterType.Float => Mathf.Approximately(floatValue, cached.floatValue),
                NetworkAnimatorParameterType.Int => intValue == cached.intValue,
                _ => true
            };
        }
    }
    
    public class NetworkAnimator : NetworkIdentity, IServerSceneEvents
    {
        [Tooltip("The animator to sync")]
        [SerializeField, PurrLock] private Animator _animator;
        [SerializeField, PurrLock] private bool _ownerAuth = true;
            
        readonly Dictionary<int, NetworkAnimatorParameter> _cachedParameters = new ();
        readonly List<NetworkAnimatorParameter> _dirtyParameters = new ();
        readonly List<NetworkAnimatorParameter> _fullHistory = new ();
        
        private bool isController => hasConnectedOwner ? (isOwner && _ownerAuth) || (!_ownerAuth && isServer) : isServer;

        private void Reset()
        {
            _animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (!_animator.runtimeAnimatorController)
                return;
            
            var count = _animator.parameterCount;
            
            for (int i = 0; i < count; ++i)
            {
                var parameter = _animator.GetParameter(i);
                
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Bool:
                        _cachedParameters.Add(parameter.nameHash, new NetworkAnimatorParameter(_animator, parameter));
                        break;
                    case AnimatorControllerParameterType.Float:
                        _cachedParameters.Add(parameter.nameHash,new NetworkAnimatorParameter(_animator, parameter));
                        break;
                    case AnimatorControllerParameterType.Int:
                        _cachedParameters.Add(parameter.nameHash,new NetworkAnimatorParameter(_animator, parameter));
                        break;
                }
            }
        }
        
        [UsedImplicitly]
        public void SetTrigger(string triggerName) => SetTrigger(Animator.StringToHash(triggerName));

        [UsedImplicitly]
        public void SetTrigger(int nameHash)
        {
            if (!isController)
                return;
            
            _animator.SetTrigger(nameHash);
            
            if (isServer)
                 SetTriggerOthers(nameHash);
            else SetTriggerForward(nameHash);
        }
        
        [ServerRPC]
        private void SetTriggerForward(int nameHash)
        {
            if (!_ownerAuth)
                return;
            
            _animator.SetTrigger(nameHash);
            SetTriggerOthers(nameHash);
        }
        
        [ObserversRPC]
        private void SetTriggerOthers(int nameHash)
        {
            if (isController) return;
            _animator.SetTrigger(nameHash);
        }
        
        protected override void OnTick(float delta)
        {
            if (!isController || !_animator.runtimeAnimatorController)
                return;
            
            var count = _animator.parameterCount;
            
            for (int i = 0; i < count; ++i)
            {
                var parameter = _animator.GetParameter(i);
                if (parameter.type == AnimatorControllerParameterType.Trigger) continue;
                
                var current = new NetworkAnimatorParameter(_animator, parameter);
                
                if (_cachedParameters.TryGetValue(parameter.nameHash, out var cached))
                {
                    if (!current.AreEqual(cached))
                    {
                        _dirtyParameters.Add(current);
                        _cachedParameters[parameter.nameHash] = current;
                    }
                }
                else
                {
                    _dirtyParameters.Add(current);
                    _cachedParameters.Add(parameter.nameHash, current);
                }
            }
            
            Flush();
        }
        
        private void Flush()
        {
            if (_dirtyParameters.Count == 0)
                return;
            
            Optimize(_dirtyParameters);
            
            _fullHistory.AddRange(_dirtyParameters);
            
            if (isServer)
                 SyncParameters(_dirtyParameters);
            else ForwardParameters(_dirtyParameters);
            
            _dirtyParameters.Clear();
            
            Optimize(_fullHistory);
        }
        
        static void Optimize(List<NetworkAnimatorParameter> parameters)
        {
            for (int i = 0; i < parameters.Count; ++i)
            {
                var current = parameters[i];
                
                for (int j = i + 1; j < parameters.Count; ++j)
                {
                    var next = parameters[j];
                    
                    if (current.nameHash == next.nameHash)
                    {
                        parameters.RemoveAt(j);
                        j--;
                    }
                }
            }
        }
        
        [ServerRPC]
        private void ForwardParameters(List<NetworkAnimatorParameter> parameters)
        {
            if (!_ownerAuth)
                return;
            
            for (int i = 0; i < parameters.Count; ++i)
            {
                parameters[i].Apply(_animator);
                _cachedParameters[parameters[i].nameHash] = parameters[i];
            }
            
            SyncParameters(parameters);
        }
        
        [ObserversRPC]
        private void SyncParameters(List<NetworkAnimatorParameter> parameters)
        {
            if (isController) return;
            
            for (int i = 0; i < parameters.Count; ++i)
            {
                parameters[i].Apply(_animator);
                _cachedParameters[parameters[i].nameHash] = parameters[i];
            }
        }
        
        [TargetRPC]
        private void SyncParameters([UsedImplicitly] PlayerID player, List<NetworkAnimatorParameter> parameters)
        {
            for (int i = 0; i < parameters.Count; ++i)
            {
                parameters[i].Apply(_animator);
                _cachedParameters[parameters[i].nameHash] = parameters[i];
            }
        }

        public void OnPlayerJoinedScene(PlayerID playerId)
        {
            SyncParameters(playerId, _fullHistory);
        }

        public void OnPlayerLeftScene(PlayerID playerId) { }
    }
}
