using System;
using UnityEngine;

namespace DM.BTree
{
   public class LoopNode<T> : DecoratorNode<T> where T : BehaviourTree<T>
   {
      private int _loopCount;

      public int LoopCount { get; private set; }

      protected override TaskState OnUpdate()
      {
         if (LoopCount >= _loopCount && _loopCount >= 0)
         {
            return TaskState.Success;
         }
         
         var state = child.Update();
         
         switch (state)
         {
            case TaskState.Success:
               LoopCount++;
               break;
            case TaskState.Failure:
               return TaskState.Failure;
            case TaskState.Running:
               break;
            default:
               throw new ArgumentOutOfRangeException();
         }
         return TaskState.Running;
      }

      protected override void OnInterrupt()
      {
         child?.Interrupt();
      }

      public LoopNode<T> SetLoopCount(int loopCount)
      {
         _loopCount = loopCount;
         return this;
      }

      public LoopNode(T tree) : base(tree)
      {
      }
   }
   
   /// <summary>
   /// 效果：当条件满足时，执行子节点
   /// </summary>
   /// <typeparam name="T"></typeparam>
   public class ConditionalNode<T> : DecoratorNode<T>, IInterraptor where T : BehaviourTree<T>
   {
      private Func<bool> _condition = () => true;
      protected override TaskState OnUpdate()
      {
         if (_condition())
         {
            return child.Update();
         }
         return TaskState.Failure;
      }
      protected override void OnStop()
      {
         child?.Interrupt();
      }
      public ConditionalNode<T> SetCondition(Func<bool> condition)
      {
         _condition = condition;
         return this;
      }
      int IInterraptor.Priority => Priority;
      public bool InterruptCondition() => _condition();
      public ConditionalNode(T tree) : base(tree)
      {
      }
   }

   public class TriggerNode<T> : DecoratorNode<T>, IInterraptor where T : BehaviourTree<T>
   {
      private Func<bool> _condition = () => true;
      private bool _triggered = false;
      protected override TaskState OnUpdate()
      {
         if (!_triggered && _condition())
         {
            _triggered = true;
         }

         if (_triggered)
         {
            var result = child.Update();
            if (result is TaskState.Failure or TaskState.Interrupted or TaskState.Success)
               _triggered = false;
            return result;
         }
         return TaskState.Failure;
      }
      protected override void OnStop()
      {
         _triggered = false;
         child?.Interrupt();
      }
      public TriggerNode<T> SetCondition(Func<bool> condition)
      {
         _condition = condition;
         return this;
      }
      int IInterraptor.Priority => Priority;
      public bool InterruptCondition() => _condition();
      
      public TriggerNode(T tree) : base(tree)
      {
      }
   }

   public interface IInterraptor
   {
      int Priority { get; }
      bool InterruptCondition();
   }
}