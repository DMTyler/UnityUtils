using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace DM.Utils
{
    public class ActionFactory
    {
        private static readonly Dictionary<string, UnityEvent> eventDict = new();
        public static EasyAction Create(Action action) => new(action);
        public static EasyAction<T> Create<T>(Action<T> action) => new(action);
        public static EasyAction<T1, T2> Create<T1, T2>(Action<T1, T2> action) => new(action);
        public static EasyAction<T1, T2, T3> Create<T1, T2, T3>(Action<T1, T2, T3> action) => new(action);
        public static EasyAction Lazy(Action action)
        {
            action?.Invoke();
            return new EasyAction(action);
        }

        public static EasyAction<T> Lazy<T>(T t, Action<T> action)
        {
            action?.Invoke(t);
            return new EasyAction<T>(action);
        }

        public static EasyAction<T1, T2> Lazy<T1, T2>(T1 t1, T2 t2, Action<T1, T2> action)
        {
            action?.Invoke(t1, t2);
            return new EasyAction<T1, T2>(action);
        }

        public static EasyAction<T1, T2, T3> Lazy<T1, T2, T3>(T1 t1, T2 t2, T3 t3, Action<T1, T2, T3> action)
        {
            action?.Invoke(t1, t2, t3);
            return new EasyAction<T1, T2, T3>(action);
        }

        public static UnityEvent Get(string name)
        {
            if (eventDict.TryGetValue(name, out var _event)) return _event;
            _event = new UnityEvent();
            eventDict.Add(name, _event);

            return _event;
        }

        public static bool TryGet(string name, out UnityEvent _event) => eventDict.TryGetValue(name, out _event);
    }

    public static class EasyActionExternals
    {
        public static BindingEasyAction Bind(this UnityEvent @event, Action action) => ActionFactory.Create(action).BindTo(@event);
        public static BindingEasyAction Bind<T>(this UnityEvent<T> @event, Action<T> action) => ActionFactory.Create(action).BindTo(@event);
        public static BindingEasyAction Bind<T1, T2>(this UnityEvent<T1, T2> @event, Action<T1, T2> action) => ActionFactory.Create(action).BindTo(@event);
        public static BindingEasyAction Bind<T1, T2, T3>(this UnityEvent<T1, T2, T3> @event, Action<T1, T2, T3> action) => ActionFactory.Create(action).BindTo(@event);
    }

    public abstract class EasyBase<T>
    {
        protected abstract T Core();
        
    }

    public class EasyAction
    {
        private readonly Action action = () => { };

        public BindingEasyAction BindTo(UnityEvent _event)
        {
            _event.AddListener(Action);
            return new BindingEasyAction(() => _event.RemoveListener(Action));
            
            void Action() => action();
        }
        
        

        public BindingEasyAction BindTo(Action _action)
        {
            _action += action;
            return new BindingEasyAction(() => _action -= action);
        }

        public BindingEasyAction BindTo(UnityAction _action)
        {
            _action += Action;
            return new BindingEasyAction(() => _action -= Action);

            void Action() => action();
        }
        
        internal EasyAction(Action _action)
        {
            action += _action;
        }
    }

    public class EasyAction<T>
    {
        private readonly Action<T> action = (_) => { };
        public BindingEasyAction BindTo(UnityEvent<T> _event)
        {
            _event.AddListener(Action);
            return new BindingEasyAction(() => _event.RemoveListener(Action));

            void Action(T _) => action(_);
        }

        public BindingEasyAction BindTo(Action<T> _action)
        {
            _action += action;
            return new BindingEasyAction(() => _action -= action);
        }

        public BindingEasyAction BindTo(UnityAction<T> _action)
        {
            _action += Action;
            return new BindingEasyAction(() => _action -= Action);

            void Action(T _) => action(_);
        }

        internal EasyAction(Action<T> _action)
        {
            action += _action;
        }
    }

    public class EasyAction<T1, T2>
    {
        private readonly Action<T1, T2> action = (_, __) => { };
        public BindingEasyAction BindTo(UnityEvent<T1, T2> _event)
        {
            _event.AddListener(Action);
            return new BindingEasyAction(() => _event.RemoveListener(Action));

            void Action(T1 _, T2 __) => action(_, __);
        }

        public BindingEasyAction BindTo(Action<T1, T2> _action)
        {
            _action += action;
            return new BindingEasyAction(() => _action -= action);
        }

        public BindingEasyAction BindTo(UnityAction<T1, T2> _action)
        {
            _action += Action;
            return new BindingEasyAction(() => _action -= Action);

            void Action(T1 _, T2 __) => action(_, __);
        }

        internal EasyAction(Action<T1, T2> _action)
        {
            action += _action;
        }
    }

    public class EasyAction<T1, T2, T3>
    {
        private readonly Action<T1, T2, T3> action = (_, __, ___) => { };
        public BindingEasyAction BindTo(UnityEvent<T1, T2, T3> _event)
        {
            _event.AddListener(Action);
            return new BindingEasyAction(() => _event.RemoveListener(Action));

            void Action(T1 _, T2 __, T3 ___) => action(_, __, ___);
        }

        public BindingEasyAction BindTo(Action<T1, T2, T3> _action)
        {
            _action += action;
            return new BindingEasyAction(() => _action -= action);
        }

        public BindingEasyAction BindTo(UnityAction<T1, T2, T3> _action)
        {
            _action += Action;
            return new BindingEasyAction(() => _action -= Action);

            void Action(T1 _, T2 __, T3 ___) => action(_, __, ___);
        }

        internal EasyAction(Action<T1, T2, T3> _action)
        {
            action += _action;
        }
    }
    public class BindingEasyAction
    {
        private Action unbind;
        private Action onUnbind;
        private Action onCancelled;

        private readonly CancellationTokenSource regret = new();

        internal BindingEasyAction(Action _unbind)
        {
            unbind = _unbind;
            onUnbind = () => { };
        }

        public void Unbind()
        {
            unbind();
            unbind = () => { };
            onUnbind();
        }
        public BoundedEasyEvent UnbindWhen(Func<bool> cond, PlayerLoopTiming timing = PlayerLoopTiming.Update, 
            CancellationToken token = default, bool unbindAfterCancel = true, bool cancelImmediately = false)
        {
            UniTask.Void(async() => 
                await UnbindWhen(UniTask.WaitUntil(cond, timing, cancelImmediately: cancelImmediately), token, unbindAfterCancel));
            return new BoundedEasyEvent(onUnbind, onCancelled, regret);
        }

        public BoundedEasyEvent UnbindWhenInvoke(UnityEvent eve)
        {
            eve.AddListener(Action);
            return new BoundedEasyEvent(onUnbind, onCancelled, regret);

            void Action()
            {
                Unbind();
                eve.RemoveListener(Action);
            }
        }
        
        public BoundedEasyEvent UnbindWhenInvoke<T>(UnityEvent<T> eve)
        {
            eve.AddListener(Action);
            return new BoundedEasyEvent(onUnbind, onCancelled, regret);
            
            void Action(T _)
            {
                Unbind();
                eve.RemoveListener(Action);
            }
        }
        
        public BoundedEasyEvent UnbindWhenInvoke<T1, T2>(UnityEvent<T1, T2> eve)
        {
            eve.AddListener(Action);
            return new BoundedEasyEvent(onUnbind, onCancelled, regret);
            
            void Action(T1 _, T2 __)
            {
                Unbind();
                eve.RemoveListener(Action);
            }
        }

        public BoundedEasyEvent UnbindWhenInvoke<T1, T2, T3>(UnityEvent<T1, T2, T3> eve)
        {
            eve.AddListener(Action);
            return new BoundedEasyEvent(onUnbind, onCancelled, regret);
            
            void Action(T1 _, T2 __, T3 ___)
            {
                Unbind();
                eve.RemoveListener(Action);
            }
        }
        
        public BoundedEasyEvent UnbindAfterInvoke(int invokeTime = 1)
        {
            
            return new BoundedEasyEvent(onUnbind, onCancelled, regret);
        }

        public BoundedEasyEvent UnbindWhenDestroyed(Component cp, PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken token = default, bool unbindAfterCancel = true, bool cancelImmediately = false)
        {
            var task = UniTask.WaitUntilCanceled(cp.GetCancellationTokenOnDestroy(), timing, cancelImmediately);
            UniTask.Void(async () => await UnbindWhen(task, token, unbindAfterCancel));
            return new BoundedEasyEvent(onUnbind, onCancelled, regret);
        }

        public BoundedEasyEvent UnbindWhenDestroyed(GameObject go, PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken token = default, bool unbindAfterCancel = true, bool cancelImmediately = false)
        {
            var task = UniTask.WaitUntilCanceled(go.GetCancellationTokenOnDestroy(), timing, cancelImmediately);
            UniTask.Void(async() => await UnbindWhen(task, token, unbindAfterCancel));
            return new BoundedEasyEvent(onUnbind, onCancelled, regret);
        }

        public BoundedEasyEvent UnbindAfterSeconds(float dur, PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken token = default,bool ignore = false, bool unbindAfterCancel = true, bool cancelImmediately = false)
        {
            UniTask.Void(async () => 
                await UnbindWhen(UniTask.WaitForSeconds(dur, ignore, timing, cancelImmediately: cancelImmediately), token, unbindAfterCancel));
            return new BoundedEasyEvent(onUnbind, onCancelled, regret);
        }

        public BoundedEasyEvent UnbindWhenUniTask(UniTask task, CancellationToken token = default, bool unbindAfterCancel = true)
        {
            UniTask.Void(async () => { 
                await UniTask.WhenAny(task, UniTask.WaitUntilCanceled(token));
                if (token.IsCancellationRequested && !unbindAfterCancel) return;
                Unbind();
            });
            return new BoundedEasyEvent(onUnbind, onCancelled, regret);
        }
        
        public BindingEasyAction OnUnbind(Action action)
        {
            onUnbind += action;
            return this;
        }

        public BindingEasyAction OnCancelled(Action action)
        {
            onCancelled += action;
            return this;
        }
        
        private async UniTask UnbindWhen(UniTask task, CancellationToken token, bool unbindAfterCancel = true)
        {
            await UniTask.WhenAny(task, UniTask.WaitUntilCanceled(token), UniTask.WaitUntilCanceled(regret.Token));
            if (regret.Token.IsCancellationRequested) return;
            if (token.IsCancellationRequested) onCancelled();
            if (token.IsCancellationRequested && !unbindAfterCancel) return;
            Unbind();
        }
        
        

    }
    public struct BoundedEasyEvent
    {
        private Action onUnbind;
        private Action onCancel;
        private Action onRegret;
        private CancellationTokenSource regret;
        internal BoundedEasyEvent(Action _onUnbind, Action _onCancel, CancellationTokenSource _regret)
        {
            onUnbind = _onUnbind;
            onCancel = _onCancel;
            onRegret = () => { };
            regret = _regret;
        }


        public BoundedEasyEvent OnRegret(Action action)
        {
            onRegret += action;
            return this;
        }

        public void RegretUnbind()
        {
            if (!regret.IsCancellationRequested)
                regret.Cancel();
            onRegret.Invoke();
        }
    }
}