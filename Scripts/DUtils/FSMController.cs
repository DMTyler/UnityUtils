using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace DM.FSMachine
{
    public abstract class FSMController<T> : MonoBehaviour where T : FSMController<T>
    {
        public FSMState<T> CurrentState { get; private set; }
        public FSMState<T> DefaultState { get; private set; }
        private readonly Dictionary<Type, FSMState<T>> StateDictionary = new();
        public FSMState<T> GetState(Type type)
        {
            if (StateDictionary.TryGetValue(type, out var state)) return state;
            Debug.LogError("Unable to find state " + type + "!");
            return null;
        }

        public FSMState<T> GetState<TState>() where TState : FSMState<T>
        {
            var type = typeof(TState);
            return GetState(type);
        }
        
        public TState TransitionTo<TState>(bool isExit = true, bool isEnter = true) where TState : FSMState<T>
        {
            var type = typeof(TState);
            if (!StateDictionary.ContainsKey(type))
            {
                return null;
            }
            var toState = StateDictionary[type];
            if (toState is not TState state) return null;
            if (CurrentState == toState) return state;
            if (isExit) CurrentState.OnExit();
            CurrentState = state;
            if (isEnter) CurrentState.OnEnter();
            return state;
        }
        
        public FSMState<T> TransitionTo(Type type, bool isExit = true, bool isEnter = true) 
        {
            if (!StateDictionary.TryGetValue(type, out var toState)) return null;
            if (CurrentState == toState) return toState;
            if (isExit) CurrentState.OnExit();
            CurrentState = toState;
            if (isEnter) CurrentState.OnEnter();
            return toState;
        }
        
        public TState AddState<TState>() where TState : FSMState<T>
        {
            var type = typeof(TState);
            if (StateDictionary.TryGetValue(type, out var state))
            {
                Debug.LogError("State " + type + " already exists in StateDictionary");
                return state as TState;
            }
            var ctors = type.GetConstructors
                (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var ctor = Array.Find(ctors, 
                ctor => ctor.GetParameters().Length == 1 && ctor.GetParameters()[0].ParameterType == typeof(T));
            if (ctor is null)
            {
                Debug.LogError("Unable to find ctor to register instance of FSMState " + type + "!");
                return null;
            }
            if (ctor.Invoke(new object[] {this}) is not TState instance)
            {
                Debug.LogError("Unable to register instance of FSMState " + type + "!");
                return null;
            }
            StateDictionary.Add(type, instance);
            return instance;
        }

        public FSMState<T> AddState(Type type)
        {
            if (StateDictionary.TryGetValue(type, out var state))
            {
                Debug.LogError("State " + type + " already exists in StateDictionary");
                return state;
            }
            if (!typeof(IFSMState).IsAssignableFrom(type))
            {
                Debug.LogError("Type " + type + " is not assignable from IFSMState!");
                return null;
            }
            var ctors = type.GetConstructors
                (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var ctor = Array.Find(ctors, 
                ctor => ctor.GetParameters().Length == 1 && ctor.GetParameters()[0].ParameterType == typeof(T));
            if (ctor is null)
            {
                Debug.LogError("Unable to find ctor to register instance of FSMState " + type + "!");
                return null;
            }
            if (ctor.Invoke(new object[] {this}) is not FSMState<T> instance)
            {
                Debug.LogError("Unable to register instance of FSMState " + type + "!");
                return null;
            }
            StateDictionary.Add(type, instance);
            return instance;
        }

        public void AddTransition<TFrom, TTo>(Func<bool> condition, ETransitionJudgeType judgeType = ETransitionJudgeType.OnUpdate)
            where TFrom : FSMState<T>
            where TTo : FSMState<T>
        {
            var fromKey = typeof(TFrom);
            var toKey = typeof(TTo);
            AddTransition(fromKey, toKey, condition, judgeType);
        }

        public void AddTransition(Type tF, Type tT, Func<bool> condition, ETransitionJudgeType judgeType = ETransitionJudgeType.OnUpdate)
        {
            if (!StateDictionary.ContainsKey(tF))
                AddState(tF);
            if (!StateDictionary.ContainsKey(tT))
                AddState(tT);
            var fromState = StateDictionary[tF];
            fromState.SetJudgeType(judgeType).Transitions.Add((condition, tT));
        }
        
        public void AddTransition<TFrom, TTo>(UnityEvent _event, ETransitionJudgeType judgeType = ETransitionJudgeType.OnUpdate)
            where TFrom : FSMState<T>
            where TTo : FSMState<T>
        {
            if (_event is null) return;
            var check = false;
            _event.AddListener(() =>
            {
                if (CurrentState.GetType() == typeof(TFrom))
                    check = true;
            });
            var condition = new Func<bool>(() =>
            {
                if (!check) return check;
                check = false;
                return true;
            });
            AddTransition<TFrom, TTo>(condition, judgeType);
        }
        
        public void AddTransition(Type tF, Type tT, UnityEvent _event, ETransitionJudgeType judgeType = ETransitionJudgeType.OnUpdate)
        {
            if (_event is null) return;
            var check = false;
            _event.AddListener(() =>
            {
                if (CurrentState.GetType() == tF)
                    check = true;
            });
            var condition = new Func<bool>(() =>
            {
                if (!check) return check;
                check = false;
                return true;
            });
            AddTransition(tF, tT, condition, judgeType);
        }

        protected abstract void AddTransitions();
        protected abstract FSMState<T> SetDefaultState();
        protected abstract FSMState<T> SetStartState();
        
        protected virtual void Initialize()
        {
        }

        // private functions
        private void Start()
        {
            Initialize();
            AddTransitions();
            CurrentState = SetStartState();
            CurrentState?.OnEnter();
            
            DefaultState = SetDefaultState();
        }

        private void Update()
        {
            if (CurrentState is null) return;
            if (CurrentState.TransitionJudgeType == ETransitionJudgeType.OnUpdate)
                TransitionState();
            CurrentState.OnUpdate();
        }

        private void FixedUpdate()
        {
            if (CurrentState is null) return;
            if (CurrentState.TransitionJudgeType == ETransitionJudgeType.OnFixedUpdate)
                TransitionState();
            CurrentState.OnFixedUpdate();
        }

        private void LateUpdate()
        {
            if (CurrentState is null) return;
            if (CurrentState.TransitionJudgeType == ETransitionJudgeType.OnLateUpdate)
                TransitionState();
            CurrentState.OnLateUpdate();
        }

        private void TransitionState()
        {
            if (CurrentState is null) return;
            foreach (var trans in CurrentState.Transitions)
            {
                if (!trans.Item1()) continue;
                var key = trans.Item2;
                if (!StateDictionary.TryGetValue(key, out var toState)) continue;
                if (toState is null) continue;
                CurrentState?.OnExit();
                CurrentState = toState;
                CurrentState?.OnEnter();
                break;
            }
        }
        
    }
    
    public abstract class FSMState<T> : IFSMState 
        where T : FSMController<T>
    {
        protected readonly T _controller;
        public List<(Func<bool>, Type)> Transitions { get; } = new();
        public virtual void OnEnter() { }
        public virtual void OnUpdate() { }
        public virtual void OnFixedUpdate() { }
        public virtual void OnLateUpdate() { }
        public virtual void OnExit() { }
        public ETransitionJudgeType TransitionJudgeType { get; private set; }
        
        public FSMState<T> SetJudgeType(ETransitionJudgeType type)
        {
            TransitionJudgeType = type;
            return this;
        }
        protected FSMState(T controller)
        {
            _controller = controller;
            TransitionJudgeType = ETransitionJudgeType.OnUpdate;
        }
    }
    public interface IFSMState
    { 
        List<(Func<bool>, Type)> Transitions { get; }
    }
    
    public enum ETransitionJudgeType
    {
        OnUpdate,
        OnFixedUpdate,
        OnLateUpdate
    }
}

