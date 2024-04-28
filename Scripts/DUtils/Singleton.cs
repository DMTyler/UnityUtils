namespace DM.Utils
{
    public abstract class Singleton<T> : ISingleton where T : Singleton<T>
    {
        private static T instance;
        private static object _Lock = new object();
        public static T Instance
        {
            get
            {
                lock(_Lock)
                {
                    if (instance == null)
                    {
                        instance = SingletonCreator.CreateSingleton<T>();
                    }
                    return instance;
                }
            }
        }
        public virtual void OnInstantiate() { }
        public virtual void Dispose() => instance = null;
    }
}
