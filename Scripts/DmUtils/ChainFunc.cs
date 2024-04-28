using System;
using System.Collections.Generic;
using System.Linq;

namespace DM.Utils
{
    public class ChainFunc<T>
    {
        private SortedDictionary<int, List<Func<T,T>>> funcs = new();
        
        /// <summary>
        /// Bind an action to the chain, lower layer will be executed first
        /// </summary>
        /// <param name="func">the action</param>
        /// <param name="layer">the layer</param>
        /// <returns>Binding Action</returns>
        public BindingEasyAction Bind(Func<T, T> func, int layer = 0)
        {
            if (!funcs.ContainsKey(layer))
            {
                funcs.Add(layer, new List<Func<T, T>>());
            }
            funcs[layer].Add(func);
            return new BindingEasyAction(() => funcs[layer].Remove(func));
        }
        
        public T Invoke(T value)
        {
            return funcs
                .SelectMany(list => list.Value)
                .Aggregate(value, (current, func) => current.Apply(func));
        }

        public ChainFunc()
        {
            Bind(value => value, 0);
        }
    }

    
    public class ChainFunc<T, TR>
    {
        private SortedDictionary<int, List<Func<T,TR,TR>>> funcs = new();
        
        /// <summary>
        /// Bind an action to the chain, lower layer will be executed first
        /// </summary>
        /// <param name="func">the action</param>
        /// <param name="layer">the layer</param>
        /// <returns>Binding Action</returns>
        public BindingEasyAction Bind(Func<T, TR, TR> func, int layer = 0)
        {
            if (!funcs.ContainsKey(layer))
            {
                funcs.Add(layer, new List<Func<T, TR, TR>>());
            }
            funcs[layer].Add(func);
            return new BindingEasyAction(() => funcs[layer].Remove(func));
        }
        
        public TR Invoke(T v1, TR v2)
        {
            return funcs
                .SelectMany(list => list.Value)
                .Aggregate(v2, (current, func) => current.Apply((v) => func(v1, v)));
        }

        public ChainFunc()
        {
            Bind((_,value) => value, 0);
        }
    }
}

