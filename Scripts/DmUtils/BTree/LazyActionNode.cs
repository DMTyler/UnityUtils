using System;
using DM.Utils;
using UnityEngine;

namespace DM.BTree
{
    public class LazyActionNode<T> : ActionNode<T> where T : BehaviourTree<T>
    { 
        private Action _start = () => { };
        private Action _stop = () => { };
        private Action _interrupt;
        private Func<TaskState> _update = () => TaskState.Running;
        protected override void OnStart()
        {
            _start();
        }

        protected override TaskState OnUpdate()
        {
            return _update();
        }
        
        protected override void OnStop()
        {
            _stop();
        }

        protected override void OnInterrupt()
        {
            if (!_interrupt.IsNull())
            {
                _interrupt();
                return;
            }
            _stop();
        }

        public LazyActionNode<T> SetStart(Action start)
        {
            _start = start;
            return this;
        }
        
        public LazyActionNode<T> SetStop(Action stop)
        {
            _stop = stop;
            return this;
        }
        
        public LazyActionNode<T> SetUpdate(Func<TaskState> update)
        {
            _update = update;
            return this;
        }
        
        public LazyActionNode<T> SetInterrupt(Action interrupt)
        {
            _interrupt = interrupt;
            return this;
        }

        public LazyActionNode(T tree) : base(tree)
        {
        }
    }
    
    public class WaitNode<T> : ActionNode<T> where T : BehaviourTree<T>
    {
        private float _duration;
        private float _startTime;
        protected override void OnStart()
        {
            _startTime = Time.time;
        }

        protected override TaskState OnUpdate()
        {
            if (Time.time - _startTime >= _duration)
            {
                return TaskState.Success;
            }
            return TaskState.Running;
        }

        public WaitNode<T> SetDuration(float duration)
        {
            _duration = duration;
            return this;
        }

        public WaitNode(T tree) : base(tree)
        {
        }
    }
    
    public class WaitUntilNode<T> : ActionNode<T> where T : BehaviourTree<T>
    {
        private Func<bool> _condition = () => true;
        protected override TaskState OnUpdate()
        {
            if (_condition())
            {
                return TaskState.Success;
            }
            return TaskState.Running;
        }

        public WaitUntilNode<T> SetCondition(Func<bool> condition)
        {
            _condition = condition;
            return this;
        }

        public WaitUntilNode(T tree) : base(tree)
        {
        }
    }

    public class RestNode<T> : ActionNode<T> where T : BehaviourTree<T>
    {
        public RestNode(T tree) : base(tree)
        {
        }

        protected override TaskState OnUpdate()
        {
            return TaskState.Success;
        }
    }
}