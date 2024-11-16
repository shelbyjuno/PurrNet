using System;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Packets;
using UnityEngine;

namespace PurrNet.StateMachine
{
    [DefaultExecutionOrder(-1000)]
    public sealed class StateMachine : NetworkBehaviour
    {
        public static StateMachine instance { get; private set; }
        
        [SerializeField] List<StateNode> _states;
        
        public IReadOnlyList<StateNode> states => _states;
        
        public event Action onReceivedNewData;
        
        StateMachineState _currentState;
        private int _previousStateId = -1;
        
        public StateMachineState currentState => _currentState;
        public int previousStateId => _previousStateId;

        private void Awake()
        {
            if (instance)
            {
                PurrLogger.LogError("There should only be one StateMachine in the scene");
                return;
            }
            
            instance = this;
            _currentState.stateId = -1;
            
            for (var i = 0; i < _states.Count; i++)
            {
                var state = _states[i];
                state.Setup(this);
            }
        }

        private void OnDestroy()
        {
            instance = null;
        }

        private void Update()
        {
            if (_currentState.stateId < 0 || _currentState.stateId >= _states.Count)
                return;
            
            var node = _states[_currentState.stateId];
            node.StateUpdate(isServer);
        }

        protected override void OnSpawned(bool asServer)
        {
            base.OnSpawned(asServer);
            
            if(asServer && _states.Count > 0)
                SetState(_states[0]);
        }

        public Type GetDataType(int stateId)
        {
            if (stateId < 0 || stateId >= _states.Count)
                return null;
            
            var node = _states[stateId];
            var type = node.GetType();
            var generics = type.BaseType!.GenericTypeArguments;

            return generics.Length == 0 ? null : generics[0];
        }

        [ObserversRpc(bufferLast:true)]
        private void RpcStateChange<T>(StateMachineState state, bool hasData, T data)
        {
            if (isServer) return;
    
            var activeState = _currentState.stateId < 0 || _currentState.stateId >= _states.Count ? 
                null : _states[_currentState.stateId];
    
            bool isResume = state.resuming && _currentState.stateId == state.stateId;

            if (activeState != null && !isResume)
            {
                activeState.Exit(false);
            }
    
            state.resuming = false;
            _currentState = state;
            _currentState.data = data;
    
            if (_currentState.stateId < 0 || _currentState.stateId >= _states.Count)
                return;
    
            var newState = _states[_currentState.stateId];
    
            if (hasData && newState is StateNode<T> node)
            {
                if (isResume)
                    node.Resume(data, false);
                else 
                    node.Enter(data, false);
            }
            else
            {
                if (isResume)
                    newState.Resume(false);
                else 
                    newState.Enter(false);
            }
    
            onReceivedNewData?.Invoke();
        }

        private void UpdateStateId(StateNode node)
        {
            var idx = node == null ? -2 : _states.IndexOf(node);
            
            if (idx == -1) 
                PurrLogger.LogException($"State '{node.name}' of type {node.GetType().Name} not in states list");

            var newStateId = idx < 0 ? -1 : idx;
            if (_currentState.stateId != newStateId)
            {
                var oldState = _currentState.stateId < 0 || _currentState.stateId >= _states.Count ? 
                    null : _states[_currentState.stateId];

                if (oldState)
                {
                    oldState.Exit(true);
                    if (!isServer)
                        oldState.Exit(false);
                }
                
                _previousStateId = _currentState.stateId;
                _currentState.stateId = newStateId;
            }
        }

        public void SetState<T>(StateNode<T> state, T data)
        {
            if (!isServer)
            {
                PurrLogger.LogError(
                    $"Only the server can set state. Client tried to set state to {state.name}:{state.GetType().Name}"
                );
                return;
            }

            UpdateStateId(state);
            _currentState.data = data;

            if (state)
            {
                state.Enter(data, true);
                state.Enter(true);

                if (!isServer)
                {
                    state.Enter(data, false);
                    state.Enter(false);
                }
            }
    
            _currentState.resuming = false;
            RpcStateChange(_currentState, true, data);
        }

        public void SetState(StateNode state)
        {
            if (!isServer)
            {
                PurrLogger.LogError(
                    $"Only the server can set state. Client tried to set state to {state.name}:{state.GetType().Name}"
                );
                return;
            }

            UpdateStateId(state);
            _currentState.data = null;

            if (state)
            {
                state.Enter(true);

                if (!isServer)
                    state.Enter(false);
            }
    
            _currentState.resuming = false;
            RpcStateChange<ushort>(_currentState, false, 0);
        }

        public void Next<T>(T data)
        {
            var nextNodeId = _currentState.stateId + 1;
            if (nextNodeId >= _states.Count)
                nextNodeId = 0;
        
            var nextNode = _states[nextNodeId];

            if (nextNode is StateNode<T> stateNode)
            {
                SetState(stateNode, data);
            }
            else
            {
                PurrLogger.LogException($"Node {nextNode.name}:{nextNode.GetType().Name} does not have a generic type argument of type {typeof(T).Name}");
            }
        }

        public void Next()
        {
            var nextNodeId = _currentState.stateId + 1;
            if (nextNodeId >= _states.Count)
                nextNodeId = 0;
    
            SetState(_states[nextNodeId]);
        }

        public void Previous()
        {
            var prevNodeId = _currentState.stateId - 1;
            if (prevNodeId < 0)
                prevNodeId = _states.Count - 1;
    
            SetState(_states[prevNodeId]);
        }

        public void Previous<T>(T data)
        {
            var prevNodeId = _currentState.stateId - 1;
            if (prevNodeId < 0)
                prevNodeId = _states.Count - 1;
    
            var prevNode = _states[prevNodeId];

            if (prevNode is StateNode<T> stateNode)
            {
                SetState(stateNode, data);
            }
            else
            {
                PurrLogger.LogException($"Node {prevNode.name}:{prevNode.GetType().Name} does not have a generic type argument of type {typeof(T).Name}");
            }
        }
    }
    
    public partial struct StateMachineState : INetworkedData
    {
        public int stateId;
        public object data;
        public bool resuming;
        
        static StateMachine machine => StateMachine.instance;
        
        private Type RequireDataType()
        {
            var dataType = machine.GetDataType(stateId);
            
            if (dataType == null)
                PurrLogger.LogException($"StateNode at index {stateId} was expected to have a generic type argument, but did not.");
            
            return dataType;
        }
        
        public void Serialize(NetworkStream stream)
        {
            stream.Serialize(ref resuming);
            stream.Serialize(ref stateId);
            
            bool isNull = data == null || stateId < 0 || stateId >= machine.states.Count;
            stream.Serialize(ref isNull);

            if (isNull)
            {
                data = null;
                return;
            }
            
            var dataType = RequireDataType();
            
            if (data == null || data.GetType() != dataType)
                data = Activator.CreateInstance(dataType);

            if (data is INetworkedData networkedData)
                networkedData.Serialize(stream);
        }
    }
}