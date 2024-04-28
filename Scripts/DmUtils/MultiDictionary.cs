using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DM.Utils
{
    public class MultiDictionary<TKey, TValue> : IEnumerable<TValue>
    {
        private Dictionary<TKey,List<TValue>> dict = new();

        public List<TValue> this[TKey key] => dict[key];
        
        public void Add(TKey key, TValue value)
        {
            if (!dict.ContainsKey(key))
            {
                dict.Add(key, new List<TValue>());
            }
            dict[key].Add(value);
        }

        public void Add(TKey key, List<TValue> values)
        {
            dict.Add(key, values);
        }
        
        public void Clear()
        {
            dict.Clear();
        }
        
        public void Clear(TKey key)
        {
            if (dict.ContainsKey(key)) dict[key].Clear();
        }
        
        public bool Remove(TKey key, TValue value)
        {
            bool b;
            if ((b = dict.ContainsKey(key)) && dict[key].Contains(value)) return dict[key].Remove(value);
            Debug.LogError((!b ?"Key " + key : "Value " + value) + " not found in MultiDictionary!");
            return false;
        }
        
        public bool Remove(TKey key)
        {
            if (dict.ContainsKey(key)) return dict.Remove(key);
            
            Debug.LogError("Key " + key + " not found in MultiDictionary!");
            return false;
        }
        
        public bool ContainsKey(TKey key)
        {
            return dict.ContainsKey(key);
        }
        
        public bool TryGetValue(TKey key, out List<TValue> value)
        {
            return dict.TryGetValue(key, out value);
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            return dict.Values.SelectMany(list => list).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
