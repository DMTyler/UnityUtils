using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace DM.Utils
{
    public class EasyCoroutine
    {
        private bool running;
        private bool runningFlag;
        private bool paused; // start paused
        private bool stopOnMonoDestroyed;
        private readonly MonoBehaviour mono;
        private readonly IEnumerator coroutine;
        private Action callback = () => { };
        
        public bool IsRunning => runningFlag;
        public bool IsPaused => paused;

        public EasyCoroutine Start()
        {
            if (running) return this;
            running = true;
            runningFlag = true;
            if (mono.enabled == false) return this;
            mono.StartCoroutine(enumerator());
            return this;

            IEnumerator enumerator()
            {
                yield return null;
                if (mono.enabled == false) yield break;
                mono.StartCoroutine(RealCoroutine());
            }
        }

        public EasyCoroutine StartWhen(Func<bool> condition)
        {
            UniTask.Void(async () =>
            {
                await UniTask.WaitUntil(condition);
                Start();
            });
            return this;
        }

        public void Stop()
        {
            running = false;
        }

        public EasyCoroutine StopWhen(Func<bool> condition)
        {
            UniTask.Void(async () => 
            {
                await UniTask.WaitUntil(condition);
                Stop();
            });
            return this;
        }
        
        /// <summary>
        /// Force the coroutine to stop(Not recommended)
        /// </summary>
        /// <returns></returns>
        public void ForceStop()
        {
            running = false;
            MonoManager.Instance.StopCoroutine((nameof(RealCoroutine)));
        }

        public EasyCoroutine Pause()
        {
            paused = true;
            return this;
        }

        public EasyCoroutine Resume()
        {
            paused = false;
            return this;
        }

        /// <summary>
        /// Converse EasyCoroutine to Coroutine
        /// </summary>
        /// <param name="_running">Run after conversion</param>
        /// <returns></returns>
        public Coroutine ToCoroutine(bool _running = false)
        {
            running = _running;
            return mono.StartCoroutine(nameof(RealCoroutine));
        }

        public EasyCoroutine(MonoBehaviour _mono, IEnumerator _coroutine)
        {
            mono = _mono;
            coroutine = _coroutine;
        }
        
        public void OnComplete(Action action) => callback += action;
        
        private IEnumerator RealCoroutine()
        {
            // Delay 1 frame to wait for Awake / Start
            yield return 0;
            var e = coroutine;
            while (running)
            {
                if (paused)
                    yield return 0; 
                else
                {
                    if (e != null && e.MoveNext())
                    {
                        yield return e.Current; 
                    }
                    else
                    {
                        running = false;
                    }
                }
            }
            runningFlag = false;
            callback?.Invoke();
        }
    }
}

