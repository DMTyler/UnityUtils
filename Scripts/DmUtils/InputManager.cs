using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DM.Utils
{
    public static class InputManager
    {
        private static readonly Dictionary<KeyCode, UnityEvent> keyDownEvents = new();
        private static readonly Dictionary<KeyCode, UnityEvent> keyUpEvents = new();
        private static readonly Dictionary<KeyCode, UnityEvent> keyHoldEvents = new();
        
        private static UnityEvent anyKeyDownEvent;
        private static UnityEvent anyKeyHoldEvent;
        private static UnityEvent anyKeyUpEvent;
        
        public static BindingEasyAction BindToKey(this EasyAction action, KeyCode keyCode, KeyEventType eventType)
        {
            switch (eventType)
            {
                case KeyEventType.Down:
                {
                    if (keyDownEvents.TryGetValue(keyCode, out var keyDownEvent)) return action.BindTo(keyDownEvent);
                    keyDownEvent = RegisterKeyDownEvent(keyCode);
                    return action.BindTo(keyDownEvent);
                }
                case KeyEventType.Up:
                {
                    if (keyUpEvents.TryGetValue(keyCode, out var keyUpEvent)) return action.BindTo(keyUpEvent);
                    keyUpEvent = RegisterKeyUpEvent(keyCode);
                    return action.BindTo(keyUpEvent);
                }
                case KeyEventType.Hold:
                {
                    if (keyHoldEvents.TryGetValue(keyCode, out var keyHoldEvent)) return action.BindTo(keyHoldEvent);
                    keyHoldEvent = RegisterKeyHoldEvent(keyCode);
                    return action.BindTo(keyHoldEvent);
                }
                default:
                    throw new ArgumentException("Invalid eventType: " + eventType + "for function " + nameof(BindToKey));
            }
        }

        public static BindingEasyAction BindToKey(this EasyAction action, KeyEventType eventType)
        {
            switch (eventType)
            {
                case KeyEventType.Down:
                {
                    if (anyKeyDownEvent is not null) return action.BindTo(anyKeyDownEvent);
                    anyKeyDownEvent = RegisterAnyKeyDown();
                    return action.BindTo(anyKeyDownEvent);
                }
                
                case KeyEventType.Up:
                {
                    if (anyKeyUpEvent is not null) return action.BindTo(anyKeyUpEvent);
                    anyKeyUpEvent = RegisterAnyKeyUp();
                    return action.BindTo(anyKeyUpEvent);
                }
                
                case KeyEventType.Hold:
                {
                    if (anyKeyHoldEvent is not null) return action.BindTo(anyKeyHoldEvent);
                    anyKeyHoldEvent = BindAnyKeyHold();
                    return action.BindTo(anyKeyHoldEvent);
                }

                default:
                    throw new ArgumentException("Invalid eventType: " + eventType + "for function " + nameof(BindToKey));
            }
        }
        
        public static BindingEasyAction Bind(KeyCode keyCode, KeyEventType type, Action action)
        {
            var easyAction = ActionFactory.Create(action);
            return easyAction.BindToKey(keyCode, type);
        }
        
        public static BindingEasyAction Bind(Action action, KeyEventType type)
        {
            var easyAction = ActionFactory.Create(action);
            return easyAction.BindToKey(type);
        }
        
        private static UnityEvent RegisterAnyKeyUp()
        {
            var last = false;
            var _anyKeyUpEvent = new UnityEvent();
            MonoManager.Instance.BindToUpdate(Action);
            return _anyKeyUpEvent;

            void Action()
            {
                if (Input.anyKey)
                    last = true;
                else if (!Input.anyKey && last)
                {
                    _anyKeyUpEvent?.Invoke();
                    last = false;
                }
            }
        }
        
        private static UnityEvent RegisterAnyKeyDown()
        {
            var _anyKeyDownEvent = new UnityEvent();
            MonoManager.Instance.BindToUpdate(Action);
            return _anyKeyDownEvent;
            
            void Action()
            {
                if (Input.anyKeyDown)
                    _anyKeyDownEvent?.Invoke();
            }
        }
        
        private static UnityEvent BindAnyKeyHold()
        {
            var _anyKeyHoldEvent = new UnityEvent();
            MonoManager.Instance.BindToUpdate(Action);
            return _anyKeyHoldEvent;

            void Action()
            {
                if (Input.anyKey)
                    anyKeyHoldEvent?.Invoke();
            }
        }
        
        private static UnityEvent RegisterKeyHoldEvent(KeyCode key, EEventInvokeTiming timing = EEventInvokeTiming.Update)
        {
            var keyHoldEvent = new UnityEvent();
            keyHoldEvents.Add(key, keyHoldEvent);
            switch (timing)
            {
                case EEventInvokeTiming.Update:
                    MonoManager.Instance.BindToUpdate(() => Action(key));
                    break;
                case EEventInvokeTiming.FixedUpdate:
                    MonoManager.Instance.BindToFixedUpdate(() => Action(key));
                    break;
                default:
                    throw new ArgumentException("Invalid timing: " + timing + "for function " + nameof(RegisterKeyHoldEvent));
            }
            return keyHoldEvent;

            void Action(KeyCode _key)
            {
                if (Input.GetKey(_key))
                    keyHoldEvent?.Invoke();
            }
        }
        
        private static UnityEvent RegisterKeyUpEvent(KeyCode key, EEventInvokeTiming timing = EEventInvokeTiming.Update)
        {
            var keyUpEvent = new UnityEvent();
            keyUpEvents.Add(key, keyUpEvent);
            switch (timing)
            {
                case EEventInvokeTiming.Update:
                    MonoManager.Instance.BindToUpdate(() => Action(key));
                    break;
                case EEventInvokeTiming.FixedUpdate:
                    MonoManager.Instance.BindToFixedUpdate(() => Action(key));
                    break;
                default:
                    throw new ArgumentException("Invalid timing: " + timing + "for function " + nameof(RegisterKeyUpEvent));
            }
            return keyUpEvent;

            void Action(KeyCode _key)
            {
                if (Input.GetKeyUp(_key))
                    keyUpEvent?.Invoke();
            }
        }
        
        private static UnityEvent RegisterKeyDownEvent(KeyCode key, EEventInvokeTiming timing = EEventInvokeTiming.Update)
        {
            var keyDownEvent = new UnityEvent();
            keyDownEvents.Add(key, keyDownEvent);
            switch (timing)
            {
                case EEventInvokeTiming.Update:
                    MonoManager.Instance.BindToUpdate(() => Action(key));
                    break;
                case EEventInvokeTiming.FixedUpdate:
                    MonoManager.Instance.BindToFixedUpdate(() => Action(key));
                    break;
                default:
                    throw new ArgumentException("Invalid timing: " + timing + "for function " + nameof(RegisterKeyDownEvent));
            }
            return keyDownEvent;

            void Action(KeyCode _key)
            {
                if (Input.GetKeyDown(_key))
                    keyDownEvent?.Invoke();
            }
        }
    }
    public enum KeyEventType
    {
        Down,
        Up,
        Hold
    }
}

