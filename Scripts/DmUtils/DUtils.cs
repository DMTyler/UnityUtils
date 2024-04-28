using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace DM.Utils
{
    /// <summary>
    /// 一些链式调用的扩展方法
    /// </summary>
    public static class DUtils
    {
        /// <summary>
        /// 从一个列表中移除所用指定元素，并返回列表自身，O(n)
        /// </summary>
        /// <param name="any">指定元素</param>
        /// <returns>列表自身</returns>
        public static List<T> Erase<T>(this List<T> list, T any)
        {
            var count = list.Count;
            for (var i = 0; i < count; i++)
            {
                if (!Equals(list[i], any)) continue;
                var newCount = i++;
                for (; i < count; i++)
                    if (!Equals(list[i], any))
                        list[newCount++] = list[i];
                list.RemoveRange(newCount, count - newCount);
                Debug.Log(list.Count);
                break;
            }

            return list;
        }

        /// <summary>
        /// 从一个列表中移除所用空元素，O(n)
        /// </summary>
        /// <returns>列表自身</returns>
        public static List<T> RemoveNulls<T>(this List<T> list)
        {
            var count = list.Count;
            for (var i = 0; i < count; i++)
            {
                if (list[i] is not null) continue;
                var newCount = i++;
                for (; i < count; i++)
                    if (list[i] is not null)
                        list[newCount++] = list[i];
                list.RemoveRange(newCount, count - newCount);
                break;
            }

            return list;
        }

        /// <summary>
        /// 精准设置列表中的元素，如果索引超出列表长度则填充默认值
        /// </summary>
        /// <param name="list">列表</param>
        /// <param name="index">索引</param>
        /// <param name="item">元素</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>列表自身</returns>
        public static List<T> Precise<T>(this List<T> list, int index, T item)
        {
            while (list.Count <= index)
                list.Add(default);
            list[index] = item;
            return list;
        }

        public static List<T> Random<T>(this List<T> list, int count)
        {
            var result = new int[count];
            var index = new int[list.Count];
            for (var i = 0; i < index.Length; i++)
                index[i] = i;
            var site = list.Count;
            for (var i = 0; i < count; i++)
            {
                var r = UnityEngine.Random.Range(i, site);
                result[i] = index[r];
                index[r] = index[i];
                site--;
            }

            var newList = new List<T>();
            for (var i = 0; i < count; i++)
                newList.Add(list[result[i]]);
            return newList;
        }

        public static bool IsNull<T>(this T self) => self is null || self.Equals(null);

        public static bool IsNull(this GameObject go) => go is null || go.Equals(null) || !go;

        public static GameObject Instantiate(this GameObject prefab) => Object.Instantiate(prefab);

        public static GameObject Instantiate(this GameObject prefab, Transform parent)
        {
            var go = Object.Instantiate(prefab, parent);
            go.name = prefab.name;
            return go;
        }

        public static GameObject Instantiate(this GameObject prefab, Vector3 position, Quaternion rotation,
            Transform parent = null)
        {
            var go = Object.Instantiate(prefab, position, rotation, parent);
            go.name = prefab.name;
            return go;
        }

        public static GameObject SetPosition(this GameObject go, Vector3 position)
        {
            go.transform.position = position;
            return go;
        }

        /// <summary>
        /// 将自己作为参数传入委托
        /// </summary>
        /// <param name="action">目标委托</param>
        public static void Self<T>(this T self, Action<T> action) => action?.Invoke(self);

        /// <summary>
        /// 将自己作为参数传入委托
        /// </summary>
        /// <param name="action">目标委托</param>
        public static void Self<T>(this T self, UnityAction<T> action) => action?.Invoke(self);

        /// <summary>
        /// 将自己作为参数传入委托
        /// </summary>
        /// <param name="action">目标委托</param>
        public static void Self<T>(this T self, UnityEvent<T> action) => action?.Invoke(self);

        /// <summary>
        /// 以自己作为参数将委托转化为无参委托并返回
        /// </summary>
        /// <param name="action">目标委托</param>
        /// <returns></returns>
        public static Action Embed<T>(this T self, Action<T> action) => () => action?.Invoke(self);

        /// <summary>
        /// 以自己作为参数将委托转化为无参委托并返回
        /// </summary>
        /// <param name="action">目标委托</param>
        /// <returns></returns>
        public static UnityAction Embed<T>(this T self, UnityAction<T> action) => () => action?.Invoke(self);

        /// <summary>
        /// 将自己作用于委托并返回
        /// </summary>
        /// <param name="func">目标委托</param>
        /// <returns></returns>
        public static TRe Apply<TArg, TRe>(this TArg self, Func<TArg, TRe> func) => func.Invoke(self);
    }

    public static class DMath
    {
        public static Vector2 SquareToCircle(Vector2 input)
        {
            var output = Vector2.zero;
            output.x = input.x * Mathf.Sqrt(1 - (input.y * input.y) / 2);
            output.y = input.y * Mathf.Sqrt(1 - (input.x * input.x) / 2);
            return output;
        }

        public static Vector2 CircleToSquare(Vector2 input)
        {
            var output = Vector2.zero;
            output.x = input.x * Mathf.Sqrt(2 - (input.y * input.y));
            output.y = input.y * Mathf.Sqrt(2 - (input.x * input.x));
            return output;
        }

        /// <summary>
        /// 计算一条射线与一个平面的交点
        /// </summary>
        /// <param name="planePoint"></param>
        /// <param name="startPoint">射线起始点</param>
        /// <param name="planeNormal">射线的起点</param>
        /// <param name="lineDirection">射线的方向</param>
        /// <param name="intersection">焦点</param>
        /// <returns></returns>
        public static bool PlaneRayIntersection(Vector3 planePoint, Vector3 startPoint, Vector3 planeNormal,
            Vector3 lineDirection, out Vector3 intersection)
        {
            intersection = Vector3.zero;
            var angle = Vector3.Dot(planeNormal, lineDirection);
            if (Mathf.Abs(angle) < 0.0001f) return false;
            var t = Vector3.Dot(planeNormal, planePoint - startPoint) / angle;
            if (t < 0) return false;
            intersection = startPoint + t * lineDirection;
            return true;
        }
        public static Vector2 XY(this Vector3 vector3) => new Vector2(vector3.x, vector3.y);
        public static Vector2 XZ(this Vector3 vector3) => new Vector2(vector3.x, vector3.z);
        public static Vector2 YZ(this Vector3 vector3) => new Vector2(vector3.y, vector3.z);
    }
}


