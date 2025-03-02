using System;
using System.Collections;
using System.Collections.Generic;
using DM.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

public class PathTracingController : MonoSingleton<PathTracingController>
{
    public ComputeShader PathTracingShader;
    public ComputeShader RayComputeShader;
    private static readonly int Triangles = Shader.PropertyToID("Triangles");
    private static readonly int BVH = Shader.PropertyToID("BVH");

    [Button]
    public void Initialize()
    {
        BVHController.Instance.Build();
        
        if (BVHController.Root is null)
        {
            Debug.LogError("BVH is null");
            return;
        }
        
        if (PathTracingShader is null)
        {
            Debug.LogError("IntersectionShader is null");
            return;
        }
        
        var root = BVHController.Root;
        
        BVHSerializer.Serialize(root, out var triangleData, out var aabbData);
        var kernel = PathTracingShader.FindKernel("CSMain");
        
        if (kernel == -1)
        {
            Debug.LogError("Kernel not found");
            return;
        }

        /*
        for (var i = 0; i < aabbData.Count; i += 9)
        {
            Debug.Log(
                $"Current Index: {i}, \n" +
                $"Parent: {aabbData[i + 6]}, \n" +
                $"Left: {aabbData[i + 7]}, \n" +
                $"Right: {aabbData[i + 8]}, \n");
        }
        */
        
        var triangleBuffer = new ComputeBuffer(triangleData.Count, sizeof(float));
        triangleBuffer.SetData(triangleData);
        PathTracingShader.SetBuffer(kernel, Triangles, triangleBuffer);
        
        var aabbBuffer = new ComputeBuffer(aabbData.Count, sizeof(float));
        aabbBuffer.SetData(aabbData);
        PathTracingShader.SetBuffer(kernel, BVH, aabbBuffer);
    }
}
