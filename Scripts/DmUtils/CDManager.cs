using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace DM.Utils
{
    public class CDManager : MonoSingleton<CDManager>
    {
        private readonly List<IEnumerator> requests = new();
        private readonly List<Cooldown> cds = new();

        public void RequestStart(Cooldown cd, IEnumerator coroutine)
        {
            if (!requests.Contains(coroutine))
                requests.Add(coroutine);
            if (!cds.Contains(cd))
                cds.Add(cd);
        }

        private void Update()
        {
            if (requests.Count > 0)
                requests.ForEach(coroutine => StartCoroutine(coroutine));
            requests.Clear();
        }
    }
    
    public abstract class CDFactory
    {
        public static Cooldown Create(float duration) => new(duration);

        public static Cooldown Create(float duration, Action action)
        {
            var cd = new Cooldown(duration);
            cd.Bind(action);
            return cd;
        }
    }

    public class Cooldown
    {
        private float duration;
        private float remainingTime;
        private readonly UnityEvent actions = new();

        public bool IsRunning { get; private set; }
        public bool Started { get; private set; }
        public bool Paused { get; private set; }
        public bool Finished => !IsRunning && Started;

        private Action<float> onPause = (remaining) => { };
        private Action<float> onResume = (remaining) => { };
        private Action<float> onStop = (remaining) => { };
        private Action<float> onRunning = (remaining) => { };

        public BindingEasyAction Bind(Action action) => actions.Bind(action);
        public Cooldown(float _duration)
        {
            duration = _duration;
            remainingTime = duration;
        }

        public bool Start()
        {
            if (Started || IsRunning) return false;
            Started = true;
            IsRunning = true;
            remainingTime = duration;
            CDManager.Instance.RequestStart(this, CdEnumerator());
            return true;
        }

        public bool Stop()
        {
            if (!IsRunning) return false;
            remainingTime = 0;
            IsRunning = false;
            onStop?.Invoke(remainingTime);
            return true;
        }
        
        public void Skip()
        {
            if (!IsRunning) return;
            remainingTime = 0;
        }
        
        public bool Pause()
        {
            if (!IsRunning) return false;
            Paused = true;
            onPause?.Invoke(remainingTime);
            return true;
        }

        public bool Resume()
        {
            if (!IsRunning) return false;
            Paused = false;
            onResume?.Invoke(remainingTime);
            return true;
        }
        
        public void Restart()
        {
            if (IsRunning)
            {
                Paused = true;
                remainingTime = duration;
                Paused = false;
                return;
            }

            Started = false;
            IsRunning = false;
            Start();
        }
        
        public void Set(float _duration)
        {
            duration = _duration;
            remainingTime = duration;
        }

        public Cooldown OnRunning(Action<float> action)
        {
            onRunning += action;
            return this;
        }
        
        public Cooldown OnPause(Action<float> action)
        {
            onPause += action;
            return this;
        }
        
        public Cooldown OnResume(Action<float> action)
        {
            onResume += action;
            return this;
        }
        
        public Cooldown OnStop(Action<float> action)
        {
            onStop += action;
            return this;
        }

        public async UniTask Task()
        {
            await CdEnumerator();
        }
        
        public static explicit operator float(Cooldown cd) => cd.remainingTime;
        
        private IEnumerator CdEnumerator()
        {
            yield return null; // wait for next frame
            while (remainingTime > 0 && IsRunning)
            {
                if (Paused) yield return null;
                remainingTime -= Time.deltaTime;
                onRunning?.Invoke(remainingTime);
                yield return null;
            }
            if (!IsRunning) yield break;
            IsRunning = false;
            actions.Invoke();
        }
    }
}
