using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace DM.Utils
{
    public class ReactiveProperty<T>
    {
        protected T value;
        protected Action<T> onValueChange = (_) => { };
        protected Action<T> onSetValue = (_) => { };
        protected static Func<T, T, bool> Comparer = (v1, v2) => v1.Equals(v2);
        public virtual T Value
        {
            get => value;
            set
            {
                onSetValue.Invoke(value);
                if (this.value is null && value is null) return;
                if (Comparer(this.value, value)) return;
                this.value = value;
                onValueChange.Invoke(this.value);
                
            }
        }

        public ReactiveProperty<T> SetWithoutInvoke(T _value)
        {
            value = _value;
            return this;
        }

        public ReactiveProperty<T> SetForceInvoke(T _value)
        {
            value = _value;
            ForceInvoke();
            return this;
        }

        public void ForceInvoke(bool _onSetValue = true, bool _onValueChange = true)
        {
            if (_onSetValue) onSetValue.Invoke(value);
            if (_onValueChange) onValueChange.Invoke(value);
        }
        
        /// <summary>
        /// will call the delegate when value is changed
        /// </summary>
        /// <param name="_action">the function to call</param>
        public BindingReactiveProperty<T> Bind(Action<T> _action)
        {
            onValueChange += _action;
            return new BindingReactiveProperty<T>(this,  () => onValueChange -= _action);
        }

        /// <summary>
        /// will call the delegate when value is set
        /// </summary>
        /// <param name="_action">the function to call</param>
        /// <returns></returns>
        public BindingReactiveProperty<T> BindOnSet(Action<T> _action)
        {
            onSetValue += _action;
            return new BindingReactiveProperty<T>(this,  () => onSetValue -= _action);
        }

        public BindingReactiveProperty<T> BindToTMP(IBaseUI ui, Func<T, string> func)
        {
            var text = ui.GameObject.GetComponent<TextMeshProUGUI>();
            if (text.IsNull())
                throw new NullReferenceException($"Cannot find {typeof(TextMeshProUGUI)} component on {ui.GameObject.name}");
            onValueChange += _Action;
            return new BindingReactiveProperty<T>(this, () => onValueChange -= _Action);
            
            void _Action(T v) => text.text = v.Apply(func);
        }

        public BindingReactiveProperty<T> BindToTMP(IBaseUI ui)
        {
            var text = ui.GameObject.GetComponent<TextMeshProUGUI>();
            if (text.IsNull())
                throw new NullReferenceException($"Cannot find {typeof(TextMeshProUGUI)} component on {ui.GameObject.name}");
            onValueChange += _Action;
            return new BindingReactiveProperty<T>(this, () => onValueChange -= _Action);
            
            void _Action(T v) => text.text = v.ToString();
        }
        
        public BindingReactiveProperty<T> BindToTMP(TextMeshProUGUI ui, Func<T, string> func)
        {
            var text = ui;
            if (text.IsNull())
                throw new NullReferenceException($"Cannot find {typeof(TextMeshProUGUI)} component");
            onValueChange += _Action;
            return new BindingReactiveProperty<T>(this, () => onValueChange -= _Action);
            
            void _Action(T v) => text.text = v.Apply(func);
        }

        public BindingReactiveProperty<T> BindToTMP(TextMeshProUGUI ui)
        {
            var text = ui;
            if (text.IsNull())
                throw new NullReferenceException($"Cannot find {typeof(TextMeshProUGUI)} component");
            onValueChange += _Action;
            return new BindingReactiveProperty<T>(this, () => onValueChange -= _Action);
            
            void _Action(T v) => text.text = v.ToString();
        }
        
        public ReactiveProperty(T _value) => value = _value;
        public static void SetComparer(Func<T, T, bool> func) => Comparer = func; 
        public static implicit operator T(ReactiveProperty<T> property) => property.Value;
    }

    public class BindingReactiveProperty<T> : BindingEasyAction
    {

        private ReactiveProperty<T> source;
        public BoundedEasyEvent UnbindWhen(Func<T, bool> cond, PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken token = default, bool unbindAfterCancel = true, bool cancelImmediately = false)
        {
            var v = source;
            return base.UnbindWhen(() => cond(v), timing, token, unbindAfterCancel, cancelImmediately);
        }

        internal BindingReactiveProperty(ReactiveProperty<T> _source,Action _unbind) : base(_unbind)
        {
            source = _source;
        }
    }

    public static class AutoRegister
    {
        [RuntimeInitializeOnLoadMethod]
        public static void AutoRegisterCompare()
        {
            ReactiveProperty<string>.SetComparer(string.Equals);
            IntReactiveProperty.SetComparer((a, b) => a == b);
            FloatReactiveProperty.SetComparer((a, b) => Mathf.Abs(a-b) <= 1e-7f);
        }
    }

    public class IntReactiveProperty : ReactiveProperty<int>
    {
        private Action<int> onValueIncrease = (_) => { };
        private Action<int> onValueDecrease = (_) => { };
        public override int Value
        {
            get => value;
            set
            {
                onSetValue.Invoke(value);
                if (Comparer(this.value, value)) return;
                if (value > this.value) onValueIncrease.Invoke(value - this.value);
                else if (value < this.value) onValueDecrease.Invoke(this.value - value);
                this.value = value;
                onValueChange.Invoke(this.value);
                
            }
        }

        public BindingReactiveProperty<int> BindOnIncrease(Action<int> _action)
        {
            onValueIncrease += _action;
            return new BindingReactiveProperty<int>(this, () => onValueIncrease -= _action);
        }
        
        public BindingReactiveProperty<int> BindOnDecrease(Action<int> _action)
        {
            onValueDecrease -= _action;
            return new BindingReactiveProperty<int>(this, () => onValueDecrease -= _action);
        }
        
        public IntReactiveProperty(int _value) : base(_value)
        {
        }
    }
    public class FloatReactiveProperty : ReactiveProperty<float>
    {
        private Action<float, float> onValueIncrease = (_, _) => { };
        private Action<float, float> onValueDecrease = (_, _) => { };
        public override float Value
        {
            get => value;
            set
            {
                onSetValue.Invoke(value);
                if (Comparer(this.value, value)) return;
                if (value > this.value) onValueIncrease.Invoke(this.value ,value - this.value);
                else if (value < this.value) onValueDecrease.Invoke(this.value, this.value - value);
                this.value = value;
                onValueChange.Invoke(this.value);
                
            }
        }
        
        public BindingReactiveProperty<float> BindOnIncrease(Action<float, float> _action)
        {
            onValueIncrease += _action;
            return new BindingReactiveProperty<float>(this, () => onValueIncrease -= _action);
        }
        
        public BindingReactiveProperty<float> BindOnDecrease(Action<float, float> _action)
        {
            onValueDecrease -= _action;
            return new BindingReactiveProperty<float>(this, () => onValueDecrease -= _action);
        }
        
        public FloatReactiveProperty(int _value) : base(_value)
        {
        }
    }
}

