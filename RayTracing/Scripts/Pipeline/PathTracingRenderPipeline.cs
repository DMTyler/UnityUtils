using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PathTracingRenderPipeline : RenderPipeline
{
    private ComputeShader _pathTracingShader;
    private List<RenderTexture> _rts = new();
    
    private static readonly int Width = Shader.PropertyToID("Width");
    private static readonly int Height = Shader.PropertyToID("Height");
    private static readonly int CameraPosition = Shader.PropertyToID("CameraPosition");
    private static readonly int View = Shader.PropertyToID("I_View");
    private static readonly int Projection = Shader.PropertyToID("I_Projection");
    private static readonly int Result = Shader.PropertyToID("Result");
    private static readonly int NearClip = Shader.PropertyToID("NearClip");
    private static readonly int Aspect = Shader.PropertyToID("Aspect");
    private static readonly int Fov = Shader.PropertyToID("Fov");
    private static readonly int Object2World = Shader.PropertyToID("Object2World");
    private static readonly int RayDir = Shader.PropertyToID("RayDir");
    private static readonly int TriangleIndex = Shader.PropertyToID("_TriangleIndex");
    private static readonly int Materials = Shader.PropertyToID("Materials");
    private static readonly int Meshes = Shader.PropertyToID("Meshes");
    private static readonly int MaterialCount = Shader.PropertyToID("MaterialCount");
    private static readonly int MeshCount = Shader.PropertyToID("MeshCount");

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {

        foreach (var camera in cameras)
        {
            ClearSingleCamera(context, camera);
            RenderSingleCamera(context, camera);
        }
        
        context.Submit();
        
        _rts.ForEach(rt => rt.Release());
        _rts.Clear();
    }

    private void ClearSingleCamera(ScriptableRenderContext context, Camera camera)
    {
        context.SetupCameraProperties(camera);
        
        var cmd = CommandBufferPool.Get("Clear Camera");
        cmd.ClearRenderTarget(true, true, Color.red);
        
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
    
    private void RenderSingleCamera(ScriptableRenderContext context, Camera camera)
    {
        context.SetupCameraProperties(camera);
        
        var root = BVHController.Root;
        if (root is null) return;
        
        if (_pathTracingShader is null)
        {
            _pathTracingShader = PathTracingController.Instance.PathTracingShader;
        }
        
        if (_pathTracingShader is null) return;
        
        var cmd = CommandBufferPool.Get("Path Tracing");
        var rt = CreateRdmRenderTexture(camera.pixelWidth, camera.pixelHeight);
        if (!RenderFrame(camera, ref rt, out var mats, out var meshes)) return;
        
        cmd.Blit(rt, BuiltinRenderTextureType.CameraTarget);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    private bool RenderFrame(Camera camera, ref RenderTexture rt, out int[] mats, out int[] meshes)
    {
        mats = new int[BVH.MaterialCount];
        meshes = new int[BVH.MeshCount];
        var computeShader = _pathTracingShader;
        
        if (_pathTracingShader is null)
        {
            _pathTracingShader = PathTracingController.Instance.PathTracingShader;
        }
        
        if (_pathTracingShader is null) return false;
        
        var kernel = computeShader.FindKernel("CSMain");
        if (kernel == -1)
        {
            Debug.LogError("Kernel not found");
            return false;
        }

        var width = camera.pixelWidth;
        var height = camera.pixelHeight;

        var rayDir = CreateRdmRenderTexture(width, height);
        
        if (!GetRayDirections(camera, ref rayDir)) return false;
        
        var cameraPos = camera.transform.position;
        computeShader.SetVector(CameraPosition, new Vector4(cameraPos.x, cameraPos.y, cameraPos.z, 0));
        computeShader.SetTexture(kernel, RayDir, rayDir);

        var materialsBuffer = new ComputeBuffer(BVH.MaterialCount, sizeof(int));
        var meshesBuffer = new ComputeBuffer(BVH.MeshCount, sizeof(int));
        computeShader.SetTexture(kernel, Result, rt);
        computeShader.SetBuffer(kernel, Materials, materialsBuffer);
        computeShader.SetBuffer(kernel, Meshes, meshesBuffer);
        
        var threadGroupsX = width / 8;
        var threadGroupsY = height / 8;
        computeShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
        
        materialsBuffer.GetData(mats);
        meshesBuffer.GetData(meshes);
        
        materialsBuffer.Release();
        meshesBuffer.Release();
        return true;
    }

    private bool GetRayDirections(Camera camera, ref RenderTexture rt)
    {
        if (PathTracingController.Instance.RayComputeShader is null) return false;
        var rotation = camera.transform.rotation;
        var matrix = Matrix4x4.Rotate(rotation);
        
        var computeShader = PathTracingController.Instance.RayComputeShader;
        computeShader.SetFloat(NearClip, camera.nearClipPlane);
        computeShader.SetFloat(Width, camera.pixelWidth);
        computeShader.SetFloat(Height, camera.pixelHeight);
        computeShader.SetFloat(Aspect, camera.aspect);
        computeShader.SetFloat(Fov, camera.fieldOfView * Mathf.Deg2Rad);
        computeShader.SetMatrix(Object2World, matrix);
        computeShader.SetTexture(0, Result, rt);
        
        var threadX = camera.pixelWidth / 8;
        var threadY = camera.pixelHeight / 8;
        
        computeShader.Dispatch(0, threadX, threadY, 1);
        
        return true;
    }
    
    /*
    private bool GetNormalDirections(Camera camera, RenderTexture triangleIndex, ref RenderTexture rt)
    {
        Shader.SetGlobalTexture(TriangleIndex, triangleIndex);
    }
    */

    private RenderTexture T_RenderRayDirection(ScriptableRenderContext context, Camera camera)
    {
        context.SetupCameraProperties(camera);
        var rt = new RenderTexture(camera.pixelWidth, camera.pixelHeight, 0, RenderTextureFormat.ARGB32);
        rt.enableRandomWrite = true;
        rt.Create();
        
        if (!GetRayDirections(camera, ref rt)) return rt;
        
        var cmd = CommandBufferPool.Get("Render Ray Direction");
        cmd.Blit(rt, BuiltinRenderTextureType.CameraTarget);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
        return rt;
    }
    
    private RenderTexture CreateRdmRenderTexture(int width, int height)
    {
        var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
        rt.enableRandomWrite = true;
        rt.Create();
        _rts.Add(rt);
        return rt;
    }
}
