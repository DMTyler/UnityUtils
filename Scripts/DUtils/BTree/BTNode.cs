using System.Collections.Generic;

namespace DM.BTree
{
    public abstract class BTNode<T> where T : BehaviourTree<T>
    {
        protected T tree;
        public int Priority { get; protected set; }
        public TaskState State { get; protected set; } = TaskState.Running;
        public bool Started { get; protected set; }

        public TaskState Update()
        {
            if (!Started)
            {
                Started = true;
                OnStart();
            }

            State = OnUpdate();

            if (State is TaskState.Failure or TaskState.Success)
            {
                OnStop();
                Started = false;
            }

            return State;
        }
        public void Interrupt()
        {
            Started = false;
            State = TaskState.Interrupted;
            OnInterrupt();
        }

        public void SetPriority(int priority) => Priority = priority;
        protected virtual void OnStart() { }
        protected virtual void OnStop() { }
        protected virtual void OnInterrupt()
        {
            OnStop();
        }
        protected abstract TaskState OnUpdate();
        protected BTNode(T tree)
        {
            this.tree = tree;
        }
    }

    public abstract class ActionNode<T> : BTNode<T> where T : BehaviourTree<T>
    {
        protected ActionNode(T tree) : base(tree) { }
    }
    
    public abstract class CompositeNode<T> : BTNode<T> where T : BehaviourTree<T>
    {
        protected readonly List<BTNode<T>> children = new();
        protected int current;
        protected InterruptType interruptType = InterruptType.Never;
        public CompositeNode<T> AddChild(BTNode<T> node)
        {
            children.Add(node);
            return this;
        }
        
        public CompositeNode<T> AddChildren(params BTNode<T>[] nodes)
        {
            children.AddRange(nodes);
            return this;
        }
        
        public CompositeNode<T> SetInterrupt(InterruptType type)
        {
            interruptType = type;
            return this;
        }
        
        public new CompositeNode<T> SetPriority(int priority)
        {
            Priority = priority;
            return this;
        }
        
        protected CompositeNode(T tree) : base(tree) { }
    }
    
    public abstract class DecoratorNode<T> : BTNode<T> where T : BehaviourTree<T>
    {
        protected BTNode<T> child;
        public DecoratorNode<T> SetChild(BTNode<T> node)
        {
            child = node;
            return this;
        }

        protected DecoratorNode(T tree) : base(tree) { }

        public new DecoratorNode<T> SetPriority(int priority)
        {
            Priority = priority;
            return this;
        }
    }
    
    public enum TaskState
    {
        Success,
        Failure,
        Interrupted,
        Running
    }

    public enum InterruptType
    {
        Never,
        Lower,
        Equal,
        NoHigher,
        Always
    }

}