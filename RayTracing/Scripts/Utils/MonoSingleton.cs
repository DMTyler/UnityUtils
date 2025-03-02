using UnityEngine;

namespace DM.Utils
{ 
    public abstract class MonoSingleton<T> : MonoBehaviour, ISingleton where T : MonoSingleton<T>
    {
        private static object _Lock = new object();
        protected static T instance;
        public static T Instance {
            get
            {
                lock (_Lock) // a lock to prevent multiple thread problems
                {
                    if (instance == null)
                    {
                        instance = SingletonCreator.CreateMonoSingleton<T>();
                    }

                    return instance;
                }
            }
        }
        public virtual void OnInstantiate() { } // from interface
        public void Dispose() => instance = null; // from interface
        protected void OnDestroy() => instance = null;
        protected virtual void OnApplicationQuit()
        {
            if (instance == null) return;
            Destroy(instance.gameObject);
            instance = null;
        }
    }
}

