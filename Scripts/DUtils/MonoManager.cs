using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace DM.Utils
{
    public class MonoManager : MonoSingleton<MonoManager>
    {
        private readonly UnityEvent onAwake = new();
        private readonly UnityEvent onDelayAwake = new();
        private readonly UnityEvent onStart = new();
        private readonly UnityEvent onDelayStart = new();
        
        private readonly UnityEvent onUpdate = new();
        private readonly UnityEvent onFixedUpdate = new();
        
        
        public void BindToAwake(Action action, bool delay = false) => (delay? onDelayAwake : onAwake).AddListener(() => action());
        public void BindToStart(Action action, bool delay = false) => (delay? onDelayStart : onStart).AddListener(() => action());

        public BindingEasyAction BindToUpdate(Action action) =>
            ActionFactory.Create(action).BindTo(onUpdate);

        public BindingEasyAction BindToFixedUpdate(Action action) =>
            ActionFactory.Create(action).BindTo(onFixedUpdate);

        public BindingEasyAction BindSecondDurationEvent(float duration, Action action, 
            EEventInvokeTiming timing = EEventInvokeTiming.Update, bool invokeOnRegister = false)
        {
            var _duration = duration;
            return timing switch
            {
                EEventInvokeTiming.Update => BindToUpdate(Action),
                EEventInvokeTiming.FixedUpdate => BindToFixedUpdate(Action),
                _ => BindToUpdate(Action)
            };

            void Action()
            {
                if (invokeOnRegister) action?.Invoke();
                if (_duration <= 0)
                {
                    _duration = duration;
                    action?.Invoke();
                }

                var delta = timing switch
                {
                    EEventInvokeTiming.Update => Time.deltaTime,
                    EEventInvokeTiming.FixedUpdate => Time.fixedDeltaTime,
                    _ => Time.deltaTime
                };
                _duration -= delta;
            }
        }
        private async void Awake()
        {
            onAwake?.Invoke();
            await UniTask.NextFrame();
            onDelayAwake?.Invoke();
        }

        private async void Start()
        {
            onStart?.Invoke();
            await UniTask.NextFrame();
            onDelayStart?.Invoke();
        }

        private void Update()
        {
            onUpdate?.Invoke();
        }

        private void FixedUpdate()
        {
            onFixedUpdate?.Invoke();
        }
        
    }

    public static class MonoBehaviourExternals
    {
        public static EasyCoroutine StartEasyCoroutine(this MonoBehaviour mono, IEnumerator coroutine, bool startImmediately = true)
        {
            var eco = new EasyCoroutine(mono, coroutine);
            return startImmediately ? eco.Start() : eco;
        }
    }

    public enum EEventInvokeTiming
    {
        Update,
        FixedUpdate
    }
}

