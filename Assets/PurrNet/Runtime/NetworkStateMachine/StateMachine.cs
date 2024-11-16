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
        NetworkedStateMachineData _networkedData;
        
        public StateMachineState currentState => _currentState;

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
        private void RpcStateChange(StateMachineState state)
        {
            if (isServer) return;
            
            Debug.Log($"Hello :-)");
            var activeState = _currentState.stateId < 0 || _currentState.stateId >= _states.Count ? 
                null : _states[_currentState.stateId];
            
            bool isResume = state.resuming && _currentState.stateId == state.stateId;

            if (activeState != null && !isResume)
            {
                activeState.Exit(false);
            }
            
            state.resuming = false;
            _currentState = state;
            
            if (_currentState.stateId < 0 || _currentState.stateId >= _states.Count)
                return;
            
            var newState = _states[_currentState.stateId];

            if (_currentState.data is INetworkedData networkedData)
            {
                var dataType = networkedData.GetType();
                var method = newState.GetType().GetMethod(isResume ? "Resume" : "Enter", 
                    new[] { dataType, typeof(bool) });

                if (method == null)
                {
                    PurrLogger.LogException($"StateNode at index {_currentState.stateId} does not have a method with signature 'void Enter({networkedData.GetType().Name})'");
                }
                
                method.Invoke(newState, new [] {_currentState.data, false});
            }
            
            if (isResume)
                 newState.Resume(false);
            else newState.Enter(false);
            
            onReceivedNewData?.Invoke();
        }

        [ObserversRpc(bufferLast:true)]
        private void RpcNetworkedNodeData(NetworkedStateMachineData data)
        {
            if (!isServer)
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
                
                _currentState.stateId = newStateId;
            }
        }

        public void SetState<T>(StateNode<T> state, T data) where T : INetworkedData
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
    
            BroadcastState();

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

            BroadcastNodeData();
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
    
            BroadcastState();

            if (state)
            {
                state.Enter(true);

                if (!isServer)
                    state.Enter(false);
            }

            BroadcastNodeData();
        }

        private void BroadcastState()
        {
            _currentState.resuming = false;
            RpcStateChange(_currentState);
        }
        
        private void BroadcastNodeData()
        {
            var currentNode = _currentState.stateId < 0 || _currentState.stateId >= _states.Count ? 
                null : _states[_currentState.stateId];
    
            if (currentNode && currentNode is INetworkedData)
                RpcNetworkedNodeData(new NetworkedStateMachineData(_currentState.stateId));
        }

        public void Next<T>(T data) where T : INetworkedData
        {
            var nextNodeId = _currentState.stateId + 1;

            if (nextNodeId >= _states.Count)
            {
                SetState(null);
                return;
            }
    
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
            {
                SetState(null);
                return;
            }
    
            SetState(_states[nextNodeId]);
        }

        public void Previous()
        {
            var prevNodeId = _currentState.stateId - 1;
    
            if (prevNodeId < 0)
                return;
    
            SetState(_states[prevNodeId]);
        }

        public void Previous<T>(T data) where T : INetworkedData
        {
            var prevNodeId = _currentState.stateId - 1;
    
            if (prevNodeId < 0)
                return;
    
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
            else PurrLogger.LogException($"Data of type '{data!.GetType().Name}' does not implement INetworkedData");
        }
    }

    internal partial struct NetworkedStateMachineData : INetworkedData
    {
        private int stateId;
        
        public NetworkedStateMachineData(int stateId)
        {
            this.stateId = stateId;
        }

        static StateMachine machine => StateMachine.instance;

        public void Serialize(NetworkStream stream)
        {
            stream.Serialize(ref stateId);
            
            var node = stateId < 0 || stateId >= machine.states.Count ? 
                null : machine.states[stateId];

            if (node && node is INetworkedData networkedNode)
                networkedNode.Serialize(stream);
        }
    }
}