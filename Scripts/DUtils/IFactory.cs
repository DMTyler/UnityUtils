namespace DM.Utils
{
    public interface IFactory<out T>
    { 
        T Get(string name);
    }
    
}

