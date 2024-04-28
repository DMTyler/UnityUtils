using UnityEngine;

namespace DM.Decoration 
{
    public interface IBranch
    {
        
    }
    
    public interface IDecoration<out T>
    {
        public T OriginalTree { get; }
    }
    
    public interface IMonoDecoration<out T> where T : MonoBehaviour
    {
        public T OriginalTree { get;}
    }
   
}