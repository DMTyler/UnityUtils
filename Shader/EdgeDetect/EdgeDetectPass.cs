using DM.Utils;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public class EdgeDetectVolumeFeature : ScriptableRendererFeature
{
    private EdgeDetectVolumePass m_EdgeDetectVolumePass;
    
    public override void Create()
    {
        m_EdgeDetectVolumePass = new EdgeDetectVolumePass(RenderPassEvent.AfterRenderingOpaques);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_EdgeDetectVolumePass);
    }
}

public class EdgeDetectVolumePass : ScriptableRenderPass
{
    private static readonly string k_RenderTag = "EdgeDetectVolume";
    private static readonly int TempTarget = Shader.PropertyToID("_TempTargetEdgeDetectVolume");
    private static readonly int MainTex = Shader.PropertyToID("MainTex");
    private static readonly int EdgeOnly = Shader.PropertyToID("_EdgeOnly");
    private static readonly int EdgeWidth = Shader.PropertyToID("_Width");
    private static readonly int EdgeColour = Shader.PropertyToID("_EdgeColor");
    private static readonly int BackgroundColour = Shader.PropertyToID("_BackgroundColor");
    
    private EdgeDetectVolume m_EdgeDetectVolume;
    private Material m_Material;
    private RenderTargetIdentifier m_Source;

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        m_Source = renderingData.cameraData.renderer.cameraColorTarget;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (!renderingData.cameraData.postProcessEnabled || m_Material.IsNull()) return;
        var stack = VolumeManager.instance.stack;
        var edgeDetectVolume = stack.GetComponent<EdgeDetectVolume>();
        if (edgeDetectVolume.IsNull() || !edgeDetectVolume.IsActive()) return;
        m_EdgeDetectVolume = edgeDetectVolume;
        if (!m_EdgeDetectVolume.active) return;
        
        var cmd = CommandBufferPool.Get(k_RenderTag);
        Render(cmd, ref renderingData);
        
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    private void Render(CommandBuffer cmd, ref RenderingData renderingData)
    {
        ref var cameraData = ref renderingData.cameraData;
        var source = m_Source;
        var destination = TempTarget;
        
        var w = cameraData.camera.pixelWidth;
        var h = cameraData.camera.pixelHeight;

        const int shaderPass = 0;
        
        m_Material.SetVector(EdgeColour, m_EdgeDetectVolume.EdgeColour.value);
        m_Material.SetVector(BackgroundColour, m_EdgeDetectVolume.BackgroundColour.value);
        m_Material.SetFloat(EdgeOnly, m_EdgeDetectVolume.EdgeOnly.value ? 1 : 0);
        m_Material.SetFloat(EdgeWidth, m_EdgeDetectVolume.EdgeWidth.value);
        
        cmd.SetGlobalTexture(MainTex, source);
        cmd.GetTemporaryRT(destination, w, h, 0, FilterMode.Point, RenderTextureFormat.Default);
        cmd.Blit(source, destination);
        cmd.Blit(destination, source, m_Material, shaderPass);
    }
    
    public EdgeDetectVolumePass(RenderPassEvent eve)
    {
        renderPassEvent = eve;
        var shader = Shader.Find("PostEffect/EdgeDetect");
        if (shader.IsNull())
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(EdgeDetectVolumePass)}: Shader Not Found");
#endif
            return;
        }
        m_Material = CoreUtils.CreateEngineMaterial(shader);
    }
}
