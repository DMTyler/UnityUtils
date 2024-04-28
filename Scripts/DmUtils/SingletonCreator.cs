using System;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DM.Utils
{
    public static class SingletonCreator

    {
    public static T CreateSingleton<T>() where T : Singleton<T>
    {
        var type = typeof(T);
        // get all ctors
        var ctors = type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);
        // get ctor with no parameter
        var ctor = Array.Find(ctors, ctor => ctor.GetParameters().Length == 0);
        if (ctor == null)
        {
            throw new Exception("Non-Public Constructor not found! in " + type);
        }

        var instance = ctor.Invoke(null) as T;
        instance?.OnInstantiate();
        return instance;
    }

    public static T CreateMonoSingleton<T>(bool dontDestroyOnLoad = false) where T : MonoSingleton<T>
    {
        var type = typeof(T);
        // return null if application is not running
        if (!Application.isPlaying) return null;
        // try find game object with component T
        var instance = Object.FindObjectOfType(type) as T;
        if (instance != null)
        {
            instance.OnInstantiate();
            return instance;
        }

        // if not find, initialize a new game object and attach component to it
        var go = new GameObject(typeof(T).Name);
        if (dontDestroyOnLoad) Object.DontDestroyOnLoad(go);
        instance = go.AddComponent(type) as T;
        if (instance != null) instance.OnInstantiate();
        return instance;
    }
    }
}

