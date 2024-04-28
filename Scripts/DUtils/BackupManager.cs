using System.Collections.Generic;
using System.Linq;
using DM.Decoration;
using DM.Utils;
using UnityEngine;
using MonoManager = DM.Utils.MonoManager;

namespace DM.Backup
{
    public static class BackupManager
    {
        public static int DefaultBackupNum = 50;
        private static readonly Dictionary<ICanBackup, Memo> memoDict = new();
        private static readonly object _lock = new();
        
        public static bool GetNext(ICanBackup source, out Memo value)
        {
            lock (_lock)
            {
                value = default;
                if (!memoDict.TryGetValue(source, out var _value) || _value.Next.IsNull()) return false;
                _value.OnGetNext();
                value = _value.Next;
                memoDict[source] = _value.Next;
                return true;
            }
        }
        
        public static bool GetPrev(ICanBackup source, out Memo value)
        {
            lock (_lock)
            {
                value = default;
                if (!memoDict.TryGetValue(source, out var _value) || _value.Prev.IsNull()) return false;
                value = _value.Prev;
                memoDict[source] = _value.Prev;
                memoDict[source].OnGetPrev();
                return true;
            }
        }
        
        public static bool Peek(ICanBackup source, out Memo value)
        {
            if (!memoDict.TryGetValue(source, out value)) return false;
            return true;
        }
        
        public static void Push(ICanBackup source, object[] data)
        {
            lock (_lock)
            {
                if (!memoDict.TryGetValue(source, out var value))
                {
                    memoDict.Add(source, new Memo(data, source.MaxBackupNum));
                    return;
                }
                value.Next = new Memo(data, source.MaxBackupNum) { Prev = value };
                memoDict[source] = value.Next;
                value.Next.OnPush();
            }
        }

        public static void BackupAll()
        {
            lock (_lock)
            {
                DecFactory.GetMonoBranch<ICanBackup>().ForEach(x => x.Backup());
            }
        }
        
        public static void UndoAll()
        {
            lock (_lock)
            {
                memoDict.ToList().ForEach(x => x.Key.Undo());
            }
        }
        
        [RuntimeInitializeOnLoadMethod]
        public static void RegisterAutoBackup()
        {
            DecFactory.GetMonoBranch<IAutoBackup>().ForEach(x =>
            {
                object[] last = null;
                Utils.MonoManager.Instance.BindToUpdate(() =>
                    {
                        if (x.RequestUnbindAutoBackup) return;

                        var current = x.WriteMemo();

                        // return if theres no difference
                        if (last.SequenceEqual(current)) return;

                        // return if the difference is caused by undo / redo


                        // backup
                        x.Backup();
                        // update state flag
                        last = current;
                    })
                    .UnbindWhen(() => x.IsNull() || x.RequestUnbindAutoBackup);
            });
        }

    }

    public class Memo
    {
        public Memo Prev;
        public Memo Next;
        
        private int count;
        private readonly int maxCount;
        
        public readonly object[] Data;

        public Memo(object[] data, int maxCount)
        {
            Data = data;
            this.maxCount = maxCount;
            count = 0;
        }

        public void OnPush()
        {
            Prev?.OnPush();
            count++;
            if (count >= maxCount)
                Prev = null; 
        }

        public void OnGetPrev()
        {
            Prev?.OnGetPrev();
            count--;
        }
        
        public void OnGetNext()
        {
            Prev?.OnGetNext();
            count++;
        }
    }

    public static class BackupExtensions
    {
        public static ICanBackup Backup(this ICanBackup source)
        {
            BackupManager.Push(source, source.WriteMemo());
            return source;
        }

        public static T Backup<T>(this T source) where T : ICanBackup
        {
            BackupManager.Push(source, source.WriteMemo());
            return source;
        }

        public static bool Undo(this ICanBackup source)
        {
            if (!BackupManager.GetPrev(source, out var value)) return false;
            source.ReadMemo(value.Data);
            return true;
        }

        public static bool Undo<T>(this T source) where T : ICanBackup
        {
            if (!BackupManager.GetPrev(source, out var value)) return false;
            source.ReadMemo(value.Data);
            return true;
        }
        
        public static bool Redo(this ICanBackup source)
        {
            if (!BackupManager.GetNext(source, out var value)) return false;
            source.ReadMemo(value.Data);
            return true;
        }
        
        public static bool Redo<T>(this T source) where T : ICanBackup
        {
            if (!BackupManager.GetNext(source, out var value)) return false;
            source.ReadMemo(value.Data);
            return true;
        }
    }

    public interface ICanBackup : IBranch
    {
        public int MaxBackupNum { get;  }
        object[] WriteMemo();
        void ReadMemo(object[] obj);
    }
    
    public interface IAutoBackup : ICanBackup
    {
        bool RequestUnbindAutoBackup { get;}
    }
}

