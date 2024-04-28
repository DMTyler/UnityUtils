namespace DM.Utils
{
    /// <summary>
    /// Interface of singleton
    /// </summary>
    public interface ISingleton
    {
        public void OnInstantiate(); // func to call when initialized
        public void Dispose();
    }
}

