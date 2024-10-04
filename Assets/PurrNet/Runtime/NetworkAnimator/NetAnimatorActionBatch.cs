using System.Collections.Generic;
using PurrNet.Packets;
using UnityEngine;

namespace PurrNet
{
    internal partial struct NetAnimatorActionBatch : IAutoNetworkedData
    {
        public List<NetAnimatorRPC> actions;
        
        public static NetAnimatorActionBatch CreateReconcile(Animator animator)
        {
            var actions = new List<NetAnimatorRPC>();

            for (var i = 0; i < animator.layerCount; i++)
            {
                var info = animator.GetCurrentAnimatorStateInfo(i);
                actions.Add(new NetAnimatorRPC(new Play_STATEHASH_LAYER_NORMALIZEDTIME
                {
                    stateHash = info.fullPathHash,
                    layer = i,
                    normalizedTime = info.normalizedTime
                }));
            }
            
            int paramCount = animator.parameterCount;
            
            for (var i = 0; i < paramCount; i++)
            {
                var param = animator.parameters[i];

                switch (param.type)
                {
                    case AnimatorControllerParameterType.Bool:
                    {
                        var setBool = new SetBool
                        {
                            value = animator.GetBool(param.name),
                            nameHash = param.nameHash
                        };

                        actions.Add(new NetAnimatorRPC(setBool));
                        break;
                    }
                    case AnimatorControllerParameterType.Float:
                    {
                        var setFloat = new SetFloat
                        {
                            value = animator.GetFloat(param.name),
                            nameHash = param.nameHash
                        };
                        
                        actions.Add(new NetAnimatorRPC(setFloat));
                        break;
                    }
                    case AnimatorControllerParameterType.Int:
                    {
                        var setInt = new SetInt
                        {
                            value = animator.GetInteger(param.name),
                            nameHash = param.nameHash
                        };
                        
                        actions.Add(new NetAnimatorRPC(setInt));
                        break;
                    }
                }
            }
            
            return new NetAnimatorActionBatch
            {
                actions = actions
            };
        }
    }
}