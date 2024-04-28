using System;
using UnityEngine;

namespace DM.BTree
{
    public class SequenceNode<T> : CompositeNode<T> where T : BehaviourTree<T>
    {
        protected override TaskState OnUpdate()
        {
            if (current >= children.Count)
            {
                current = 0;
                return TaskState.Success;
            }
            
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i] is not IInterraptor inter) continue;


                switch (interruptType)
                {
                    case InterruptType.Never:
                        break;
                    case InterruptType.Lower:
                        if (inter.Priority > children[current].Priority && inter.InterruptCondition())
                        {
                            children[current].Interrupt();
                            current = i;
                        }

                        break;
                    case InterruptType.Equal:
                        if (inter.Priority == children[current].Priority && inter.InterruptCondition())
                        {
                            children[current].Interrupt();
                            current = i;
                        }

                        break;
                    case InterruptType.NoHigher:
                        if (inter.Priority >= children[current].Priority && inter.InterruptCondition())
                        {
                            children[current].Interrupt();
                            current = i;
                        }

                        break;
                    case InterruptType.Always:
                        if (inter.InterruptCondition())
                        {
                            children[current].Interrupt();
                            current = i;
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            var state = children[current].Update();

            switch (state)
            {
                case TaskState.Success:
                    current++;
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

        public SequenceNode(T tree) : base(tree) { }

        protected override void OnStop()
        {
            if (current < children.Count)
                children[current].Interrupt();
            current = 0;
        }
    }
    
    public class SelectorNode<T> : CompositeNode<T> where T : BehaviourTree<T>
    {
        protected override TaskState OnUpdate()
        {
            if (current >= children.Count)
            {
                return TaskState.Failure;
            }
            
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i] is not IInterraptor inter) continue;

                switch (interruptType)
                {
                    case InterruptType.Never:
                        break;
                    case InterruptType.Lower:
                        if (inter.Priority > children[current].Priority && inter.InterruptCondition())
                        {
                            children[current].Interrupt();
                            current = i;
                        }

                        break;
                    case InterruptType.Equal:
                        if (inter.Priority == children[current].Priority && inter.InterruptCondition())
                        {
                            children[current].Interrupt();
                            current = i;
                        }

                        break;
                    case InterruptType.NoHigher:
                        if (inter.Priority >= children[current].Priority && inter.InterruptCondition())
                        {
                            children[current].Interrupt();
                            current = i;
                        }

                        break;
                    case InterruptType.Always:
                        if (inter.InterruptCondition())
                        {
                            children[current].Interrupt();
                            current = i;
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            var state = children[current].Update();

            switch (state)
            {
                case TaskState.Success:
                    current = 0;
                    return TaskState.Success;
                case TaskState.Failure:
                    current++;
                    break;
                case TaskState.Running:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return TaskState.Running;
        }
        
        protected override void OnStop()
        {
            if (current < children.Count)
                children[current].Interrupt();
            current = 0;
        }

        public SelectorNode(T tree) : base(tree) { }
    }
}