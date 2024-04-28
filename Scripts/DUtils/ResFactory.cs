using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DM.Decoration;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace DM.Utils
{
    public class ResFactory<T> : IFactory<T> where T : Object
    {
        protected static readonly Dictionary<string, T> resDic = new();
        public struct Load
        { 
            public static T Sync(string name)
            {
                if (resDic.TryGetValue(name, out var prefab)) return prefab;
                prefab = Addressables.LoadAssetAsync<T>(name).WaitForCompletion();
                return prefab;
            }

            public static async UniTask<T> Async(string name)
            {
                if (resDic.TryGetValue(name, out var prefab)) return prefab;
                prefab = await Addressables.LoadAssetAsync<T>(name);
                Debug.Log($"Prefab: {name} successfully loaded.");
                resDic.Add(name, prefab);
                return prefab;
            }
            
            public static void CallBack(string name, UnityAction<T> callback, UnityAction fail = null)
            {
                if (resDic.TryGetValue(name, out var prefab))
                {
                    callback?.Invoke(prefab);
                    return;
                }
                Addressables.LoadAssetAsync<T>(name).Completed += handle =>
                {
                    if (handle.Status != AsyncOperationStatus.Succeeded)
                    {
#if UNITY_EDITOR
                        Debug.LogError($"{nameof(ResFactory<T>)}: {name} failed to load.");
#endif
                        fail?.Invoke();
                        return;
                    }
                    callback?.Invoke(handle.Result);
                };
            }
        }
        
        public static bool TryGet(string name, out T value) => resDic.TryGetValue(name, out value);

        public static T Get(string name)
        {
            if (!resDic.TryGetValue(name, out var prefab))
                throw new ArgumentException("Cannot find prefab with name: " + name + "for function " + nameof(Get) + 
                                            ", please check if you have loaded the prefab.");
            return prefab;
        }

        public static bool Release(string name)
        {
            if (!resDic.TryGetValue(name, out var prefab)) return false;
            Addressables.Release(prefab);
            resDic.Remove(name);
            return true;
        }

        T IFactory<T>.Get(string name)
        {
            return Get(name);
        }
       
        protected ResFactory()
        {
            
        }
    }

    public class GOFactory : ResFactory<GameObject>
    {
        public struct Instantiate
        {
            public static GameObject Sync(string name)
            {
                if (resDic.TryGetValue(name, out var prefab)) return Object.Instantiate(prefab).LoadBranch();
                prefab = Load.Sync(name);
                return Object.Instantiate(prefab).LoadBranch();
            }

            public static async UniTask<GameObject> Async(string name)
            {
                if (resDic.TryGetValue(name, out var prefab)) return Object.Instantiate(prefab).LoadBranch();
                prefab = await Load.Async(name);
                return Object.Instantiate(prefab).LoadBranch();
            }
            
            public static void CallBack(string name, UnityAction<GameObject> callback)
            {
                if (resDic.TryGetValue(name, out var prefab))
                {
                    callback?.Invoke(Object.Instantiate(prefab).LoadBranch());
                    return;
                }
                Addressables.LoadAssetAsync<GameObject>(name).Completed += handle =>
                {
                    callback?.Invoke(Object.Instantiate(handle.Result).LoadBranch());
                };
            }

        }
        
        private GOFactory()
        {
            
        }
    }
}

