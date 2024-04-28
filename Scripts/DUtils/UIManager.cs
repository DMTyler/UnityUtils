using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;

namespace DM.Utils
{
    public class UIManager
    {
        private static readonly Dictionary<string, IBaseUI> uiDict = new();
        private static GameObject danvas;
        
        public struct Load
        {
            public static IBaseUI Sync(string name)
            {
                if (uiDict.TryGetValue(name, out var ui) && !ui.IsNull() && !ui.GameObject.IsNull()) return ui;
                uiDict.Remove(name);
                var prefab = GOFactory.Instantiate.Sync(name);
                ui = prefab.GetComponent<IBaseUI>();
                if (ui.IsNull())
                    throw new NullReferenceException($"Cannot find {typeof(IBaseUI)} component on {prefab.name}");
                if (danvas.IsNull())
                    danvas = GOFactory.Instantiate.Sync("UI/Danvas");
                prefab.transform.SetParent(danvas.transform);
                uiDict.Add(name, ui);
                ui.OnInit();
                return ui;
            }
            
            public static async UniTask<IBaseUI> Async(string name)
            {
                if (uiDict.TryGetValue(name, out var ui) && !ui.IsNull() && !ui.GameObject.IsNull()) return ui;
                uiDict.Remove(name);
                var prefab = await GOFactory.Instantiate.Async(name);
                ui = prefab.GetComponent<IBaseUI>();
                if (ui == null)
                    throw new NullReferenceException($"Cannot find {typeof(IBaseUI)} component on {prefab.name}");
                if (danvas.IsNull())
                    danvas = await GOFactory.Instantiate.Async("UI/Danvas");
                prefab.transform.SetParent(danvas.transform);
                uiDict.Add(name, ui);
                ui.OnInit();
                return ui;
            }
            
            public static void CallBack(string name, UnityAction<IBaseUI> callback)
            {
                if (uiDict.TryGetValue(name, out var ui) && !ui.IsNull() && !ui.GameObject.IsNull())
                {
                    callback?.Invoke(ui);
                    return;
                }
                uiDict.Remove(name);
                GOFactory.Instantiate.CallBack(name, go =>
                {
                    ui = go.GetComponent<IBaseUI>();
                    if (ui == null)
                        throw new NullReferenceException($"Cannot find {typeof(IBaseUI)} component on {go.name}");
                    if (danvas.IsNull())
                        danvas = GameObject.Find("Danvas");
                    if (danvas.IsNull())
                        danvas = GOFactory.Instantiate.Sync("UI/Danvas");
                    go.transform.SetParent(danvas.transform);
                    uiDict.Add(name, ui);
                    ui.OnInit();
                    ui.Self(callback);
                });
            }
        }
        
        public static bool Release(string name)
        {
            if (!uiDict.TryGetValue(name, out var ui)) return false;
            Addressables.Release(ui);
            uiDict.Remove(name);
            return true;
        }
        
        public static bool TryGet(string name, out IBaseUI ui) => uiDict.TryGetValue(name, out ui);

        public static IBaseUI Get(string name) => uiDict[name];
        
        public static T Get<T>(string name) where T : IBaseUI => (T) uiDict[name];

        public static IBaseUI Show(string name)
        {
            var ui = uiDict[name];
            ui?.GameObject.SetActive(true);
            ui?.OnShow();
            return ui;
        }

        public static IBaseUI Hide(string name)
        {
            var ui = uiDict[name];
            ui?.OnHide();
            ui?.GameObject.SetActive(false);
            return ui;
        }
    }

    public static class BaseUIExternals
    {
        public static void Show(this IBaseUI ui)
        {
            ui?.GameObject.SetActive(true);
            ui?.OnShow();
        }
        
        public static void Hide(this IBaseUI ui)
        {
            ui?.OnHide();
            ui?.GameObject.SetActive(false);
        }
    }
    
    public interface IBaseUI
    {
        public RectTransform RectTransform { get; }
        public GameObject GameObject { get; }
        void OnInit();
        void OnShow();
        void OnHide();
    }
}
