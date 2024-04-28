using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DM.Utils;
using UnityEngine;


namespace DM.Decoration
{
    public static class DecFactory
    {
        private static readonly MultiDictionary<Type, (IBranch, GameObject)> monoBuffer = new();
        
        public static List<T> GetMonoBranch<T>(bool reload = false) where T : IBranch
        {
            var result = new List<T>();
            if (!Application.isPlaying) return result;
            if (reload || !monoBuffer.TryGetValue(typeof(T), out var value)) 
                return LoadBuffer<T>();
            for (var i = 0; i < value.Count; i++)
            {
                var go = value[i].Item2;
                if (go.IsNull())
                    monoBuffer[typeof(T)].Remove(value[i]);
                else
                    result.Add((T)value[i].Item1);
            }
            return result;
        }

        public static List<T> GetBranch<T>() where T : IBranch
        {
            var assembly = Assembly.GetAssembly(typeof(T));
            var types = assembly.GetTypes();
            var result = new List<T>();
            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(T).IsAssignableFrom(type)) continue;
                if (typeof(MonoBehaviour).IsAssignableFrom(type)) continue;
                var dec = (T)Activator.CreateInstance(type);
                if (dec is null) continue;
                result.Add(dec);
            }
            return result;
        }

        public static List<T> LoadBuffer<T>() where T : IBranch
        {
            Debug.Log($"Loading branch {nameof(T)}");
            var assembly = Assembly.GetAssembly(typeof(T));
            var types = assembly.GetTypes();
            var result = new List<T>();
            foreach (var type in types)
            {
                if (!typeof(MonoBehaviour).IsAssignableFrom(type) || !typeof(T).IsAssignableFrom(type)) continue;
                var monoList = UnityEngine.Object.FindObjectsByType(typeof(GameObject), FindObjectsSortMode.None).ToList();
                foreach (var mono in monoList)
                {
                    if (mono is not GameObject gameObject) continue;
                    var _dec = gameObject.GetComponents<T>();
                    _dec?.ToList().ForEach(x =>
                    {
                        result.Add(x);
                        if (!monoBuffer.ContainsKey(typeof(T)) || !monoBuffer[typeof(T)].Contains((x, gameObject)))
                            monoBuffer.Add(typeof(T), (x, gameObject));
                    });
                }
            }
            return result;
        }

        public static GameObject LoadBranch(this GameObject go)
        {
            var components = go.GetComponents<IBranch>();
            foreach (var component in components)
            {
                var type = component.GetType();
                var interfaces = type.GetInterfaces();
                foreach (var @interface in interfaces)
                {
                    if (!@interface.IsInterface) continue;
                    if (!typeof(IBranch).IsAssignableFrom(@interface)) continue;
                    if (typeof(MonoBehaviour).IsAssignableFrom(type))
                        monoBuffer.Add(@interface, (component, go));
                }
            }
            
            return go;
        }

        public static T LoadBranch<T>(this T tree) where T : MonoBehaviour, IBranch
        {
            var type = tree.GetType();
            var interfaces = type.GetInterfaces();
            foreach (var @interface in interfaces)
            {
                if (!@interface.IsInterface) continue;
                if (typeof(IBranch).IsAssignableFrom(@interface))
                    monoBuffer.Add(@interface, ((tree, tree.gameObject)));
            }

            return tree;
        }
        
        public static TDecoration AddMonoDecoration<TTree, TDecoration> (this GameObject go) 
            where TDecoration : IMonoDecoration<TTree>, TTree
            where TTree : MonoBehaviour
        {
            var components = go.GetComponents<TTree>();
            if (components.Length == 0)
            {
                Debug.LogError($"Unable to find component with type: {typeof(TTree)} on gameObject {go.name}.");
                return null;
            }
            if (components.Length > 1)
            {
                Debug.LogError($"Multiple components with type: {typeof(TTree)} on gameObject {go.name} are found.\n" +
                               $"Only first one: {components[0].name} will be replaced");
            }
            var result = go.AddComponent<TDecoration>();
            UnityEngine.Object.Destroy(components[0]);
            return result;
        }

        public static TDecoration AddMonoDecoration<TTree, TDecoration>(this TTree tree) 
            where TDecoration : IMonoDecoration<TTree>, TTree
            where TTree : MonoBehaviour
        {
            var go = tree.gameObject;
            var components = go.GetComponents<TTree>();
            
            if (components.Length == 0)
            {
                Debug.LogError($"Unable to find component with type: {typeof(TTree)} on gameObject {go.name}.");
                return null;
            }
            if (components.Length > 1)
            {
                Debug.LogError($"Multiple components with type: {typeof(TTree)} on gameObject {go.name} are found.\n" +
                               $"Only first one: {components[0].name} will be replaced");
            }
            var result = go.AddComponent<TDecoration>();
            UnityEngine.Object.Destroy(components[0]);
            return result;
        }
        
        public static TDecoration AddDecoration<TTree, TDecoration>(this TTree tree) 
            where TDecoration : IDecoration<TTree>, TTree
        {
            if (tree is null || tree is MonoBehaviour)
            {
                Debug.LogError($"Tree: {typeof(TTree)} is null or MonoBehaviour, please use AddMonoDecoration for MonoDecorations");
                return default;
            }
            var result = (TDecoration) Activator.CreateInstance(typeof(TDecoration), tree);
            return result;
        }
        
        public static List<TDecoration> AddDecoration<TTree, TDecoration>(this List<TTree> trees) 
            where TDecoration : IDecoration<TTree>, TTree
        {
            if (typeof(MonoBehaviour).IsAssignableFrom(typeof(TDecoration)))
            {
                Debug.LogError($"IBranch: {typeof(TDecoration)} is MonoBehaviour, please use AddMonoDecoration for MonoDecorations");
                return default;
            }
            var result = new List<TDecoration>();
            foreach (var tree in trees)
            {
                var dec = (TDecoration) Activator.CreateInstance(typeof(TDecoration), tree);
                if (dec is null) continue;
                result.Add(dec);
            }
            return result;
        }

        
    }

    
}