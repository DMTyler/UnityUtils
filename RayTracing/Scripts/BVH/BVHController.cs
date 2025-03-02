using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DM.Utils;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class BVHController : MonoSingleton<BVHController>
{
    public static BVHNode Root { get; private set; }
    public bool ShowBVH = true;
    public bool InactiveIncluded = false;
    public bool StaticOnly = false;
    
    [OnValueChanged("SetAssetPath"), FolderPath]
    public string WritePath = "Assets";
    
    [Sirenix.OdinInspector.FilePath(Extensions = ".bvh;.BVH")]
    public string ReadPath = "Assets/SerializedBVH.bvh";
    public BVHNode Build()
    { 
        var scene = SceneManager.GetActiveScene();
        var gameObjects = scene.GetRootGameObjects();
        if (!InactiveIncluded) gameObjects = gameObjects.Where(t => t.activeInHierarchy).ToArray();
        if (StaticOnly) gameObjects = gameObjects.Where(t => t.isStatic).ToArray();
        Root = BVH.Build(gameObjects.Select(g => g.transform));
        BVHSerializer.Write(Root);
        return Root;
    }
    
    public void Read(string path)
    {
        if (!BVHSerializer.Read(path, out var root)) return;
        Root = root;
    }

    [Button]
    public void ReadBVH()
    {
        Read(ReadPath);
    }

    [Button]
    private void BuildBVH()
    {
        Build();
    }

    private void OnDrawGizmos()
    {
        if (Root == null)
        {
            return;
        }
        
        DrawNode(Root);
    }
    
    private void DrawNode(BVHNode node)
    {
        if (!ShowBVH || node == null)
        {
            return;
        }
        
        Gizmos.color = new Color(1f, 1f, 1f, 0.1f);
        Gizmos.DrawCube(node.AABB.ToBounds().center, node.AABB.ToBounds().size);
        
        if (node.Left != null)
        {
            DrawNode(node.Left);
        }
        
        if (node.Right != null)
        {
            DrawNode(node.Right);
        }
    }
    
    private void SetAssetPath()
    {
        BVHSerializer.AssetPath = WritePath + "/SerializedBVH.BVH";
    }
}
