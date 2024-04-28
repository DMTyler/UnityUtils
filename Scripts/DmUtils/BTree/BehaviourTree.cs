using System;
using System.Collections;
using System.Collections.Generic;
using DM.Utils;
using UnityEngine;

namespace DM.BTree
{
    public abstract class BehaviourTree<T> : MonoBehaviour where T : BehaviourTree<T>
    {
        protected BTNode<T> rootNode;
        protected virtual void Start()
        {
            SetTree();
        }

        protected virtual void Update()
        {
            if (rootNode.State == TaskState.Running)
            {
                rootNode.Update();
            }
        }
        protected abstract void SetTree();
    }
}
