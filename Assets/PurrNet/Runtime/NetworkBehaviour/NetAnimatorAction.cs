using PurrNet.Packets;
using UnityEngine;

namespace PurrNet
{
    internal interface IApplyOnAnimator
    {
        void Apply(Animator anim);
    }
    
    public enum NetAnimatorAction : byte
    {
        SetBool,
        SetFloat,
        SetInt,
        SetTrigger,
        SetSpeed,
        SetAnimatePhysics,
        SetBodyPosition,
        SetBodyRotation,
        SetCullingMode,
        SetFireEvents,
        SetPlaybackTime,
        SetRootPosition,
        SetRootRotation,
        SetStabilizeFeet,
        SetUpdateMode,
        SetApplyRootMotion,
        SetFeetPivotActive,
        SetKeepAnimatorStateOnDisable,
        SetWriteDefaultValuesOnDisable,
        SetLogWarnings,
        SetLayersAffectMassCenter,
        ResetTrigger,
        Play_STATEHASH_LAYER_NORMALIZEDTIME,
        Play_STATEHASH_LAYER,
        PLAY_STATEHASH,
        Rebind,
        UpdateWithDelta,
        CrossFade_5,
        CrossFade_4,
        CrossFade_3,
        CrossFade_2,
        MatchTarget,
        InterruptMatchTarget,
        SetLayerWeight,
        WriteDefaultValues,
        ApplyBuiltinRootMotion,
        PlayInFixedTime,
        SetBoneLocalRotation,
        SetIKPosition,
        SetIKRotation,
        SetLookAtPosition,
        SetLookAtWeight,
        CrossFadeInFixedTime,
        SetIKHintPosition,
        SetIKPositionWeight,
        SetIKRotationWeight,
        SetIKHintPositionWeight
    }
    
    internal partial struct SetIKHintPosition : IAutoNetworkedData, IApplyOnAnimator
    {
        public AvatarIKHint hint;
        public Vector3 position;
        
        public void Apply(Animator anim)
        {
            anim.SetIKHintPosition(hint, position);
        }
    }
    
    internal partial struct SetIKPositionWeight : IAutoNetworkedData, IApplyOnAnimator
    {
        public AvatarIKGoal goal;
        public float value;
        
        public void Apply(Animator anim)
        {
            anim.SetIKPositionWeight(goal, value);
        }
    }
    
    internal partial struct SetIKRotationWeight : IAutoNetworkedData, IApplyOnAnimator
    {
        public AvatarIKGoal goal;
        public float value;
        
        public void Apply(Animator anim)
        {
            anim.SetIKRotationWeight(goal, value);
        }
    }
    
    internal partial struct SetIKHintPositionWeight : IAutoNetworkedData, IApplyOnAnimator
    {
        public AvatarIKHint hint;
        public float value;
        
        public void Apply(Animator anim)
        {
            anim.SetIKHintPositionWeight(hint, value);
        }
    }
    
    internal partial struct CrossFadeInFixedTime : IAutoNetworkedData, IApplyOnAnimator
    {
        public int stateHash;
        public float fixedTime;
        public int layer;
        public float fixedDuration;
        public float normalizedTime;
        
        public void Apply(Animator anim)
        {
            anim.CrossFadeInFixedTime(stateHash, fixedTime, layer, fixedDuration, normalizedTime);
        }
    }
    
    internal partial struct SetLookAtPosition : IAutoNetworkedData, IApplyOnAnimator
    {
        public Vector3 position;
        
        public void Apply(Animator anim)
        {
            anim.SetLookAtPosition(position);
        }
    }
    
    internal partial struct SetLookAtWeight : IAutoNetworkedData, IApplyOnAnimator
    {
        public float weight, bodyWeight, headWeight, eyesWeight, clampWeight;
        
        public void Apply(Animator anim)
        {
            anim.SetLookAtWeight(weight, bodyWeight, headWeight, eyesWeight, clampWeight);
        }
    }
    
    internal partial struct SetIKPosition : IAutoNetworkedData, IApplyOnAnimator
    {
        public AvatarIKGoal goal;
        public Vector3 position;
        
        public void Apply(Animator anim)
        {
            anim.SetIKPosition(goal, position);
        }
    }
    
    internal partial struct SetIKRotation : IAutoNetworkedData, IApplyOnAnimator
    {
        public AvatarIKGoal goal;
        public Quaternion rotation;
        
        public void Apply(Animator anim)
        {
            anim.SetIKRotation(goal, rotation);
        }
    }
    
    internal partial struct SetBoneLocalRotation : IAutoNetworkedData, IApplyOnAnimator
    {
        public HumanBodyBones bone;
        public Quaternion rotation;
        
        public void Apply(Animator anim)
        {
            anim.SetBoneLocalRotation(bone, rotation);
        }
    }
    
    internal partial struct SetBool : IAutoNetworkedData, IApplyOnAnimator
    {
        public int nameHash;
        public bool value;
        
        public void Apply(Animator anim)
        {
            anim.SetBool(nameHash, value);
        }
    }
    
    internal partial struct SetFloat : IAutoNetworkedData, IApplyOnAnimator
    {
        public int nameHash;
        public float value;
        
        public void Apply(Animator anim)
        {
            anim.SetFloat(nameHash, value);
        }
    }
    
    internal partial struct SetInt : IAutoNetworkedData, IApplyOnAnimator
    {
        public int nameHash;
        public int value;
        
        public void Apply(Animator anim)
        {
            anim.SetInteger(nameHash, value);
        }
    }
    
    internal partial struct SetTrigger : IAutoNetworkedData, IApplyOnAnimator
    {
        public int nameHash;
        
        public void Apply(Animator anim)
        {
            anim.SetTrigger(nameHash);
        }
    }
    
    internal partial struct ResetTrigger : IAutoNetworkedData, IApplyOnAnimator
    {
        public int nameHash;
        
        public void Apply(Animator anim)
        {
            anim.ResetTrigger(nameHash);
        }
    }
    
    internal partial struct SetSpeed : IAutoNetworkedData, IApplyOnAnimator
    {
        public float speed;
        
        public void Apply(Animator anim)
        {
            anim.speed = speed;
        }
    }
    
    internal partial struct SetAnimatePhysics : IAutoNetworkedData, IApplyOnAnimator
    {
        public bool value;
        
        public void Apply(Animator anim)
        {
            anim.animatePhysics = value;
        }
    }
    
    internal partial struct SetBodyPosition : IAutoNetworkedData, IApplyOnAnimator
    {
        public Vector3 value;
        
        public void Apply(Animator anim)
        {
            anim.bodyPosition = value;
        }
    }
        
    internal partial struct SetBodyRotation : IAutoNetworkedData, IApplyOnAnimator
    {
        public Quaternion value;
        
        public void Apply(Animator anim)
        {
            anim.bodyRotation = value;
        }
    }
    
    internal partial struct SetCullingMode : IAutoNetworkedData, IApplyOnAnimator
    {
        public AnimatorCullingMode value;
        
        public void Apply(Animator anim)
        {
            anim.cullingMode = value;
        }
    }
    
    internal partial struct SetFireEvents : IAutoNetworkedData, IApplyOnAnimator
    {
        public bool value;
        
        public void Apply(Animator anim)
        {
            anim.fireEvents = value;
        }
    }
    
    internal partial struct SetPlaybackTime : IAutoNetworkedData, IApplyOnAnimator
    {
        public float value;
        
        public void Apply(Animator anim)
        {
            anim.playbackTime = value;
        }
    }
    
    internal partial struct SetRootPosition : IAutoNetworkedData, IApplyOnAnimator
    {
        public Vector3 value;
        
        public void Apply(Animator anim)
        {
            anim.rootPosition = value;
        }
    }
    
    internal partial struct SetRootRotation : IAutoNetworkedData, IApplyOnAnimator
    {
        public Quaternion value;
        
        public void Apply(Animator anim)
        {
            anim.rootRotation = value;
        }
    }
    
    internal partial struct SetStabilizeFeet : IAutoNetworkedData, IApplyOnAnimator
    {
        public bool value;
        
        public void Apply(Animator anim)
        {
            anim.stabilizeFeet = value;
        }
    }
    
    internal partial struct SetUpdateMode : IAutoNetworkedData, IApplyOnAnimator
    {
        public AnimatorUpdateMode value;
        
        public void Apply(Animator anim)
        {
            anim.updateMode = value;
        }
    }
    
    internal partial struct SetApplyRootMotion : IAutoNetworkedData, IApplyOnAnimator
    {
        public bool value;
        
        public void Apply(Animator anim)
        {
            anim.applyRootMotion = value;
        }
    }
    
    internal partial struct SetFeetPivotActive : IAutoNetworkedData, IApplyOnAnimator
    {
        public float value;
        
        public void Apply(Animator anim)
        {
            anim.feetPivotActive = value;
        }
    }
    
    internal partial struct SetKeepAnimatorStateOnDisable : IAutoNetworkedData, IApplyOnAnimator
    {
        public bool value;
        
        public void Apply(Animator anim)
        {
            anim.keepAnimatorStateOnDisable = value;
        }
    }
    
    internal partial struct SetWriteDefaultValuesOnDisable : IAutoNetworkedData, IApplyOnAnimator
    {
        public bool value;
        
        public void Apply(Animator anim)
        {
            anim.writeDefaultValuesOnDisable = value;
        }
    }
    
    internal partial struct SetLogWarnings : IAutoNetworkedData, IApplyOnAnimator
    {
        public bool value;
        
        public void Apply(Animator anim)
        {
            anim.logWarnings = value;
        }
    }
    
    internal partial struct SetLayersAffectMassCenter : IAutoNetworkedData, IApplyOnAnimator
    {
        public bool value;
        
        public void Apply(Animator anim)
        {
            anim.layersAffectMassCenter = value;
        }
    }
    
    internal partial struct Play_STATEHASH_LAYER_NORMALIZEDTIME : IAutoNetworkedData, IApplyOnAnimator
    {
        public int stateHash;
        public int layer;
        public float normalizedTime;
        
        public void Apply(Animator anim)
        {
            anim.Play(stateHash, layer, normalizedTime);
        }
    }
    
    internal partial struct Play_STATEHASH_LAYER : IAutoNetworkedData, IApplyOnAnimator
    {
        public int stateHash;
        public int layer;
        
        public void Apply(Animator anim)
        {
            anim.Play(stateHash, layer);
        }
    }
    
    internal partial struct PLAY_STATEHASH : IAutoNetworkedData, IApplyOnAnimator
    {
        public int stateHash;
        
        public void Apply(Animator anim)
        {
            anim.Play(stateHash);
        }
    }
    
    internal partial struct Rebind : IAutoNetworkedData, IApplyOnAnimator
    {
        public void Apply(Animator anim)
        {
            anim.Rebind();
        }
    }
    
    internal partial struct UpdateWithDelta : IAutoNetworkedData, IApplyOnAnimator
    {
        public float delta;
        
        public void Apply(Animator anim)
        {
            anim.Update(delta);
        }
    }
    
    internal partial struct CrossFade_5 : IAutoNetworkedData, IApplyOnAnimator
    {
        public int stateHash;
        public float normalizedTime;
        public int layer;
        public float fixedTime;
        public float fixedDuration;
        
        public void Apply(Animator anim)
        {
            anim.CrossFade(stateHash, normalizedTime, layer, fixedTime, fixedDuration);
        }
    }
    
    internal partial struct CrossFade_4 : IAutoNetworkedData, IApplyOnAnimator
    {
        public int stateHash;
        public float normalizedTime;
        public int layer;
        public float fixedDuration;
        
        public void Apply(Animator anim)
        {
            anim.CrossFade(stateHash, normalizedTime, layer, fixedDuration);
        }
    }
    
    internal partial struct CrossFade_3 : IAutoNetworkedData, IApplyOnAnimator
    {
        public int stateHash;
        public float normalizedTime;
        public int layer;
        
        public void Apply(Animator anim)
        {
            anim.CrossFade(stateHash, normalizedTime, layer);
        }
    }
    
    internal partial struct CrossFade_2 : IAutoNetworkedData, IApplyOnAnimator
    {
        public int stateHash;
        public float normalizedTime;
        
        public void Apply(Animator anim)
        {
            anim.CrossFade(stateHash, normalizedTime);
        }
    }
    
    internal partial struct MatchTarget : IAutoNetworkedData, IApplyOnAnimator
    {
        public Vector3 matchPosition;
        public Quaternion matchRotation;
        public AvatarTarget targetBodyPart;
        public MatchTargetWeightMask weightMask;
        public float startNormalizedTime;
        public float targetNormalizedTime;
        public bool completeMatch;
        
        public void Apply(Animator anim)
        {
            anim.MatchTarget(matchPosition, matchRotation, targetBodyPart, weightMask, startNormalizedTime, targetNormalizedTime, completeMatch);
        }
    }
    
    internal partial struct InterruptMatchTarget : IAutoNetworkedData, IApplyOnAnimator
    {
        public bool completeMatch;
        
        public void Apply(Animator anim)
        {
            anim.InterruptMatchTarget(completeMatch);
        }
    }
    
    internal partial struct SetLayerWeight : IAutoNetworkedData, IApplyOnAnimator
    {
        public int layerIndex;
        public float weight;
        
        public void Apply(Animator anim)
        {
            anim.SetLayerWeight(layerIndex, weight);
        }
    }
    
    internal partial struct WriteDefaultValues : IAutoNetworkedData, IApplyOnAnimator
    {
        public void Apply(Animator anim)
        {
            anim.WriteDefaultValues();
        }
    }
    
    internal partial struct ApplyBuiltinRootMotion : IAutoNetworkedData, IApplyOnAnimator
    {
        public void Apply(Animator anim)
        {
            anim.ApplyBuiltinRootMotion();
        }
    }
    
    internal partial struct PlayInFixedTime : IAutoNetworkedData, IApplyOnAnimator
    {
        public int stateHash;
        public int layer;
        public float fixedTime;
        
        public void Apply(Animator anim)
        {
            anim.PlayInFixedTime(stateHash, layer, fixedTime);
        }
    }
    
    internal partial struct NetAnimatorRPC : INetworkedData
    {
        internal NetAnimatorAction type;
        
        internal SetBool _bool;
        internal SetFloat _float;
        internal SetInt _int;
        internal SetTrigger _trigger;
        internal SetSpeed _speed;
        internal SetAnimatePhysics _animatePhysics;
        internal SetBodyPosition _bodyPosition;
        internal SetBodyRotation _bodyRotation;
        internal SetCullingMode _cullingMode;
        internal SetFireEvents _fireEvents;
        internal SetPlaybackTime _playbackTime;
        internal SetRootPosition _rootPosition;
        internal SetRootRotation _rootRotation;
        internal SetStabilizeFeet _stabilizeFeet;
        internal SetUpdateMode _updateMode;
        internal SetApplyRootMotion _applyRootMotion;
        internal SetFeetPivotActive _feetPivotActive;
        internal SetKeepAnimatorStateOnDisable _keepAnimatorStateOnDisable;
        internal SetWriteDefaultValuesOnDisable _writeDefaultValuesOnDisable;
        internal SetLogWarnings _logWarnings;
        internal SetLayersAffectMassCenter _layersAffectMassCenter;
        internal ResetTrigger _resetTrigger;
        internal Play_STATEHASH_LAYER_NORMALIZEDTIME _play_STATEHASH_LAYER_NORMALIZEDTIME;
        internal Play_STATEHASH_LAYER _play_STATEHASH_LAYER;
        internal PLAY_STATEHASH _PLAY_STATEHASH;
        internal Rebind _rebind;
        internal UpdateWithDelta _updateWithDelta;
        internal CrossFade_5 _crossFade_5;
        internal CrossFade_4 _crossFade_4;
        internal CrossFade_3 _crossFade_3;
        internal CrossFade_2 _crossFade_2;
        internal MatchTarget _matchTarget;
        internal InterruptMatchTarget _interruptMatchTarget;
        internal SetLayerWeight _setLayerWeight;
        internal WriteDefaultValues _writeDefaultValues;
        internal ApplyBuiltinRootMotion _applyBuiltinRootMotion;
        internal PlayInFixedTime _playInFixedTime;
        internal SetBoneLocalRotation _setBoneLocalRotation;
        internal SetIKPosition _setIKPosition;
        internal SetIKRotation _setIKRotation;
        internal SetLookAtPosition _setLookAtPosition;
        internal SetLookAtWeight _setLookAtWeight;
        internal CrossFadeInFixedTime _crossFadeInFixedTime;
        internal SetIKHintPosition _setIKHintPosition;
        internal SetIKPositionWeight _setIKPositionWeight;
        internal SetIKRotationWeight _setIKRotationWeight;
        internal SetIKHintPositionWeight _setIKHintPositionWeight;
        
        public NetAnimatorRPC(SetBoneLocalRotation action) : this()
        {
            type = NetAnimatorAction.SetBoneLocalRotation;
            _setBoneLocalRotation = action;
        }
        
        public NetAnimatorRPC(SetIKHintPosition action) : this()
        {
            type = NetAnimatorAction.SetIKHintPosition;
            _setIKHintPosition = action;
        }
        
        public NetAnimatorRPC(SetIKPositionWeight action) : this()
        {
            type = NetAnimatorAction.SetIKPositionWeight;
            _setIKPositionWeight = action;
        }
        
        public NetAnimatorRPC(SetIKRotationWeight action) : this()
        {
            type = NetAnimatorAction.SetIKRotationWeight;
            _setIKRotationWeight = action;
        }
        
        public NetAnimatorRPC(SetIKHintPositionWeight action) : this()
        {
            type = NetAnimatorAction.SetIKHintPositionWeight;
            _setIKHintPositionWeight = action;
        }
        
        public NetAnimatorRPC(CrossFadeInFixedTime action) : this()
        {
            type = NetAnimatorAction.CrossFadeInFixedTime;
            _crossFadeInFixedTime = action;
        }
        
        public NetAnimatorRPC(SetIKPosition action) : this()
        {
            type = NetAnimatorAction.SetIKPosition;
            _setIKPosition = action;
        }
        
        public NetAnimatorRPC(SetIKRotation action) : this()
        {
            type = NetAnimatorAction.SetIKRotation;
            _setIKRotation = action;
        }
        
        public NetAnimatorRPC(SetBool action) : this()
        {
            type = NetAnimatorAction.SetBool;
            _bool = action;
        }
        
        public NetAnimatorRPC(SetFloat action) : this()
        {
            type = NetAnimatorAction.SetFloat;
            _float = action;
        }
        
        public NetAnimatorRPC(SetInt action) : this()
        {
            type = NetAnimatorAction.SetInt;
            _int = action;
        }
        
        public NetAnimatorRPC(SetTrigger action) : this()
        {
            type = NetAnimatorAction.SetTrigger;
            _trigger = action;
        }
        
        public NetAnimatorRPC(ResetTrigger action) : this()
        {
            type = NetAnimatorAction.ResetTrigger;
            _resetTrigger = action;
        }
        
        public NetAnimatorRPC(SetSpeed action) : this()
        {
            type = NetAnimatorAction.SetSpeed;
            _speed = action;
        }
        
        public NetAnimatorRPC(SetAnimatePhysics action) : this()
        {
            type = NetAnimatorAction.SetAnimatePhysics;
            _animatePhysics = action;
        }
        
        public NetAnimatorRPC(SetBodyPosition action) : this()
        {
            type = NetAnimatorAction.SetBodyPosition;
            _bodyPosition = action;
        }
        
        public NetAnimatorRPC(SetBodyRotation action) : this()
        {
            type = NetAnimatorAction.SetBodyRotation;
            _bodyRotation = action;
        }
        
        public NetAnimatorRPC(SetCullingMode action) : this()
        {
            type = NetAnimatorAction.SetCullingMode;
            _cullingMode = action;
        }
        
        public NetAnimatorRPC(SetFireEvents action) : this()
        {
            type = NetAnimatorAction.SetFireEvents;
            _fireEvents = action;
        }
        
        public NetAnimatorRPC(SetPlaybackTime action) : this()
        {
            type = NetAnimatorAction.SetPlaybackTime;
            _playbackTime = action;
        }
        
        public NetAnimatorRPC(SetRootPosition action) : this()
        {
            type = NetAnimatorAction.SetRootPosition;
            _rootPosition = action;
        }
        
        public NetAnimatorRPC(SetRootRotation action) : this()
        {
            type = NetAnimatorAction.SetRootRotation;
            _rootRotation = action;
        }
        
        public NetAnimatorRPC(SetStabilizeFeet action) : this()
        {
            type = NetAnimatorAction.SetStabilizeFeet;
            _stabilizeFeet = action;
        }
        
        public NetAnimatorRPC(SetUpdateMode action) : this()
        {
            type = NetAnimatorAction.SetUpdateMode;
            _updateMode = action;
        }
        
        public NetAnimatorRPC(SetApplyRootMotion action) : this()
        {
            type = NetAnimatorAction.SetApplyRootMotion;
            _applyRootMotion = action;
        }
        
        public NetAnimatorRPC(SetFeetPivotActive action) : this()
        {
            type = NetAnimatorAction.SetFeetPivotActive;
            _feetPivotActive = action;
        }
        
        public NetAnimatorRPC(SetKeepAnimatorStateOnDisable action) : this()
        {
            type = NetAnimatorAction.SetKeepAnimatorStateOnDisable;
            _keepAnimatorStateOnDisable = action;
        }
        
        public NetAnimatorRPC(SetWriteDefaultValuesOnDisable action) : this()
        {
            type = NetAnimatorAction.SetWriteDefaultValuesOnDisable;
            _writeDefaultValuesOnDisable = action;
        }
        
        public NetAnimatorRPC(SetLogWarnings action) : this()
        {
            type = NetAnimatorAction.SetLogWarnings;
            _logWarnings = action;
        }
        
        public NetAnimatorRPC(SetLayersAffectMassCenter action) : this()
        {
            type = NetAnimatorAction.SetLayersAffectMassCenter;
            _layersAffectMassCenter = action;
        }
        
        public NetAnimatorRPC(Play_STATEHASH_LAYER_NORMALIZEDTIME action) : this()
        {
            type = NetAnimatorAction.Play_STATEHASH_LAYER_NORMALIZEDTIME;
            _play_STATEHASH_LAYER_NORMALIZEDTIME = action;
        }
        
        public NetAnimatorRPC(Play_STATEHASH_LAYER action) : this()
        {
            type = NetAnimatorAction.Play_STATEHASH_LAYER;
            _play_STATEHASH_LAYER = action;
        }
        
        public NetAnimatorRPC(PLAY_STATEHASH action) : this()
        {
            type = NetAnimatorAction.PLAY_STATEHASH;
            _PLAY_STATEHASH = action;
        }
        
        public NetAnimatorRPC(Rebind action) : this()
        {
            type = NetAnimatorAction.Rebind;
            _rebind = action;
        }
        
        public NetAnimatorRPC(UpdateWithDelta action) : this()
        {
            type = NetAnimatorAction.UpdateWithDelta;
            _updateWithDelta = action;
        }
        
        public NetAnimatorRPC(CrossFade_5 action) : this()
        {
            type = NetAnimatorAction.CrossFade_5;
            _crossFade_5 = action;
        }
        
        public NetAnimatorRPC(CrossFade_4 action) : this()
        {
            type = NetAnimatorAction.CrossFade_4;
            _crossFade_4 = action;
        }
        
        public NetAnimatorRPC(CrossFade_3 action) : this()
        {
            type = NetAnimatorAction.CrossFade_3;
            _crossFade_3 = action;
        }
        
        public NetAnimatorRPC(CrossFade_2 action) : this()
        {
            type = NetAnimatorAction.CrossFade_2;
            _crossFade_2 = action;
        }
        
        public NetAnimatorRPC(MatchTarget action) : this()
        {
            type = NetAnimatorAction.MatchTarget;
            _matchTarget = action;
        }
        
        public NetAnimatorRPC(InterruptMatchTarget action) : this()
        {
            type = NetAnimatorAction.InterruptMatchTarget;
            _interruptMatchTarget = action;
        }
        
        public NetAnimatorRPC(SetLayerWeight action) : this()
        {
            type = NetAnimatorAction.SetLayerWeight;
            _setLayerWeight = action;
        }
        
        public NetAnimatorRPC(WriteDefaultValues action) : this()
        {
            type = NetAnimatorAction.WriteDefaultValues;
            _writeDefaultValues = action;
        }
        
        public NetAnimatorRPC(ApplyBuiltinRootMotion action) : this()
        {
            type = NetAnimatorAction.ApplyBuiltinRootMotion;
            _applyBuiltinRootMotion = action;
        }
        
        public NetAnimatorRPC(PlayInFixedTime action) : this()
        {
            type = NetAnimatorAction.PlayInFixedTime;
            _playInFixedTime = action;
        }
        
        public NetAnimatorRPC(SetLookAtPosition action) : this()
        {
            type = NetAnimatorAction.SetLookAtPosition;
            _setLookAtPosition = action;
        }
        
        public NetAnimatorRPC(SetLookAtWeight action) : this()
        {
            type = NetAnimatorAction.SetLookAtWeight;
            _setLookAtWeight = action;
        }
        
        public void Apply(Animator anim)
        {
            switch (type)
            {
                case NetAnimatorAction.SetBool: _bool.Apply(anim); break;
                case NetAnimatorAction.SetFloat: _float.Apply(anim); break;
                case NetAnimatorAction.SetInt: _int.Apply(anim); break;
                case NetAnimatorAction.SetTrigger: _trigger.Apply(anim); break;
                case NetAnimatorAction.SetSpeed: _speed.Apply(anim); break;
                case NetAnimatorAction.SetAnimatePhysics: _animatePhysics.Apply(anim); break;
                case NetAnimatorAction.SetBodyPosition: _bodyPosition.Apply(anim); break;
                case NetAnimatorAction.SetBodyRotation: _bodyRotation.Apply(anim); break;
                case NetAnimatorAction.SetCullingMode: _cullingMode.Apply(anim); break;
                case NetAnimatorAction.SetFireEvents: _fireEvents.Apply(anim); break;
                case NetAnimatorAction.SetPlaybackTime: _playbackTime.Apply(anim); break;
                case NetAnimatorAction.SetRootPosition: _rootPosition.Apply(anim); break;
                case NetAnimatorAction.SetRootRotation: _rootRotation.Apply(anim); break;
                case NetAnimatorAction.SetStabilizeFeet: _stabilizeFeet.Apply(anim); break;
                case NetAnimatorAction.SetUpdateMode: _updateMode.Apply(anim); break;
                case NetAnimatorAction.SetApplyRootMotion: _applyRootMotion.Apply(anim); break;
                case NetAnimatorAction.SetFeetPivotActive: _feetPivotActive.Apply(anim); break;
                case NetAnimatorAction.SetKeepAnimatorStateOnDisable: _keepAnimatorStateOnDisable.Apply(anim); break;
                case NetAnimatorAction.SetWriteDefaultValuesOnDisable: _writeDefaultValuesOnDisable.Apply(anim); break;
                case NetAnimatorAction.SetLogWarnings: _logWarnings.Apply(anim); break;
                case NetAnimatorAction.SetLayersAffectMassCenter: _layersAffectMassCenter.Apply(anim); break;
                case NetAnimatorAction.ResetTrigger: _resetTrigger.Apply(anim); break;
                case NetAnimatorAction.Play_STATEHASH_LAYER_NORMALIZEDTIME: _play_STATEHASH_LAYER_NORMALIZEDTIME.Apply(anim); break;
                case NetAnimatorAction.Play_STATEHASH_LAYER: _play_STATEHASH_LAYER.Apply(anim); break;
                case NetAnimatorAction.PLAY_STATEHASH: _PLAY_STATEHASH.Apply(anim); break;
                case NetAnimatorAction.Rebind: _rebind.Apply(anim); break;
                case NetAnimatorAction.UpdateWithDelta: _updateWithDelta.Apply(anim); break;
                case NetAnimatorAction.CrossFade_5: _crossFade_5.Apply(anim); break;
                case NetAnimatorAction.CrossFade_4: _crossFade_4.Apply(anim); break;
                case NetAnimatorAction.CrossFade_3: _crossFade_3.Apply(anim); break;
                case NetAnimatorAction.CrossFade_2: _crossFade_2.Apply(anim); break;
                case NetAnimatorAction.MatchTarget: _matchTarget.Apply(anim); break;
                case NetAnimatorAction.InterruptMatchTarget: _interruptMatchTarget.Apply(anim); break;
                case NetAnimatorAction.SetLayerWeight: _setLayerWeight.Apply(anim); break;
                case NetAnimatorAction.WriteDefaultValues: _writeDefaultValues.Apply(anim); break;
                case NetAnimatorAction.ApplyBuiltinRootMotion: _applyBuiltinRootMotion.Apply(anim); break;
                case NetAnimatorAction.PlayInFixedTime: _playInFixedTime.Apply(anim); break;
                case NetAnimatorAction.SetBoneLocalRotation: _setBoneLocalRotation.Apply(anim); break;
                case NetAnimatorAction.SetIKPosition: _setIKPosition.Apply(anim); break;
                case NetAnimatorAction.SetIKRotation: _setIKRotation.Apply(anim); break;
                case NetAnimatorAction.SetLookAtPosition: _setLookAtPosition.Apply(anim); break;
                case NetAnimatorAction.SetLookAtWeight: _setLookAtWeight.Apply(anim); break;
                case NetAnimatorAction.CrossFadeInFixedTime: _crossFadeInFixedTime.Apply(anim); break;
                case NetAnimatorAction.SetIKHintPosition: _setIKHintPosition.Apply(anim); break;
                case NetAnimatorAction.SetIKPositionWeight: _setIKPositionWeight.Apply(anim); break;
                case NetAnimatorAction.SetIKRotationWeight: _setIKRotationWeight.Apply(anim); break;
                case NetAnimatorAction.SetIKHintPositionWeight: _setIKHintPositionWeight.Apply(anim); break;
                default:
                    throw new System.NotImplementedException(type.ToString());
            }
        }

        public void Serialize(NetworkStream packer)
        {
            packer.Serialize(ref type);
            
            switch (type)
            {
                case NetAnimatorAction.SetBool: packer.Serialize(ref _bool); break;
                case NetAnimatorAction.SetFloat: packer.Serialize(ref _float); break;
                case NetAnimatorAction.SetInt: packer.Serialize(ref _int); break;
                case NetAnimatorAction.SetTrigger: packer.Serialize(ref _trigger); break;
                case NetAnimatorAction.SetSpeed: packer.Serialize(ref _speed); break;
                case NetAnimatorAction.SetAnimatePhysics: packer.Serialize(ref _animatePhysics); break;
                case NetAnimatorAction.SetBodyPosition: packer.Serialize(ref _bodyPosition); break;
                case NetAnimatorAction.SetBodyRotation: packer.Serialize(ref _bodyRotation); break;
                case NetAnimatorAction.SetCullingMode: packer.Serialize(ref _cullingMode); break;
                case NetAnimatorAction.SetFireEvents: packer.Serialize(ref _fireEvents); break;
                case NetAnimatorAction.SetPlaybackTime: packer.Serialize(ref _playbackTime); break;
                case NetAnimatorAction.SetRootPosition: packer.Serialize(ref _rootPosition); break;
                case NetAnimatorAction.SetRootRotation: packer.Serialize(ref _rootRotation); break;
                case NetAnimatorAction.SetStabilizeFeet: packer.Serialize(ref _stabilizeFeet); break;
                case NetAnimatorAction.SetUpdateMode: packer.Serialize(ref _updateMode); break;
                case NetAnimatorAction.SetApplyRootMotion: packer.Serialize(ref _applyRootMotion); break;
                case NetAnimatorAction.SetFeetPivotActive: packer.Serialize(ref _feetPivotActive); break;
                case NetAnimatorAction.SetKeepAnimatorStateOnDisable: packer.Serialize(ref _keepAnimatorStateOnDisable); break;
                case NetAnimatorAction.SetWriteDefaultValuesOnDisable: packer.Serialize(ref _writeDefaultValuesOnDisable); break;
                case NetAnimatorAction.SetLogWarnings: packer.Serialize(ref _logWarnings); break;
                case NetAnimatorAction.SetLayersAffectMassCenter: packer.Serialize(ref _layersAffectMassCenter); break;
                case NetAnimatorAction.ResetTrigger: packer.Serialize(ref _resetTrigger); break;
                case NetAnimatorAction.Play_STATEHASH_LAYER_NORMALIZEDTIME: packer.Serialize(ref _play_STATEHASH_LAYER_NORMALIZEDTIME); break;
                case NetAnimatorAction.Play_STATEHASH_LAYER: packer.Serialize(ref _play_STATEHASH_LAYER); break;
                case NetAnimatorAction.PLAY_STATEHASH: packer.Serialize(ref _PLAY_STATEHASH); break;
                case NetAnimatorAction.Rebind: packer.Serialize(ref _rebind); break;
                case NetAnimatorAction.UpdateWithDelta: packer.Serialize(ref _updateWithDelta); break;
                case NetAnimatorAction.CrossFade_5: packer.Serialize(ref _crossFade_5); break;
                case NetAnimatorAction.CrossFade_4: packer.Serialize(ref _crossFade_4); break;
                case NetAnimatorAction.CrossFade_3: packer.Serialize(ref _crossFade_3); break;
                case NetAnimatorAction.CrossFade_2: packer.Serialize(ref _crossFade_2); break;
                case NetAnimatorAction.MatchTarget: packer.Serialize(ref _matchTarget); break;
                case NetAnimatorAction.InterruptMatchTarget: packer.Serialize(ref _interruptMatchTarget); break;
                case NetAnimatorAction.SetLayerWeight: packer.Serialize(ref _setLayerWeight); break;
                case NetAnimatorAction.WriteDefaultValues: packer.Serialize(ref _writeDefaultValues); break;
                case NetAnimatorAction.ApplyBuiltinRootMotion: packer.Serialize(ref _applyBuiltinRootMotion); break;
                case NetAnimatorAction.PlayInFixedTime: packer.Serialize(ref _playInFixedTime); break;
                case NetAnimatorAction.SetBoneLocalRotation: packer.Serialize(ref _setBoneLocalRotation); break;
                case NetAnimatorAction.SetIKPosition: packer.Serialize(ref _setIKPosition); break;
                case NetAnimatorAction.SetIKRotation: packer.Serialize(ref _setIKRotation); break;
                case NetAnimatorAction.SetLookAtPosition: packer.Serialize(ref _setLookAtPosition); break;
                case NetAnimatorAction.SetLookAtWeight: packer.Serialize(ref _setLookAtWeight); break;
                case NetAnimatorAction.CrossFadeInFixedTime: packer.Serialize(ref _crossFadeInFixedTime); break;
                case NetAnimatorAction.SetIKHintPosition: packer.Serialize(ref _setIKHintPosition); break;
                case NetAnimatorAction.SetIKPositionWeight: packer.Serialize(ref _setIKPositionWeight); break;
                case NetAnimatorAction.SetIKRotationWeight: packer.Serialize(ref _setIKRotationWeight); break;
                case NetAnimatorAction.SetIKHintPositionWeight: packer.Serialize(ref _setIKHintPositionWeight); break;
                default:
                    throw new System.NotImplementedException(type.ToString());
            }
        }
    }
    
}