using System;
using System.Collections;
using System.Collections.Generic;
using DM.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VolumeLightFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Settings
    {
        public RenderPassEvent Event = RenderPassEvent.AfterRenderingOpaques;
    }
    
    public Settings settings = new Settings();
    
    private class VolumeLightPass : ScriptableRenderPass
    {
        private RenderTargetIdentifier m_currentTarget;
        private VolumeLightVolume m_volume;
        private Material m_material;
        
        private static readonly string renderTag = "k_VolumeLight";
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int TempTargetID = Shader.PropertyToID("_TempTargetVolumeLight");
        private static readonly int MaxStep = Shader.PropertyToID("_MaxStep");
        private static readonly int MaxDistance = Shader.PropertyToID("_MaxDistance");
        private static readonly int LightIntensity = Shader.PropertyToID("_LightIntensity");
        private static readonly int StepSize = Shader.PropertyToID("_StepSize");
        private static readonly int ShadowIntensity = Shader.PropertyToID("_ShadowIntensity");

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            m_currentTarget = renderingData.cameraData.renderer.cameraColorTarget;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!renderingData.cameraData.postProcessEnabled || m_material.IsNull()) return;
            var stack = VolumeManager.instance.stack;
            var volume = stack.GetComponent<VolumeLightVolume>();
            if (volume.IsNull() || !volume.IsActive()) return;
            m_volume = volume;
            if (!m_volume.IsActive()) return;
            var cmd = CommandBufferPool.Get(renderTag);
            Render(cmd, ref renderingData);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        private void Render(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (m_volume.IsNull() || !m_volume.IsActive()) return;
            if (m_material.IsNull()) return;
            
            ref var cameraData = ref renderingData.cameraData;
            var source = m_currentTarget;
            var destination = TempTargetID;
            
            var w = cameraData.camera.pixelWidth;
            var h = cameraData.camera.pixelHeight;
            
            m_material.SetInt(MaxStep, m_volume.MaxSteps.value);
            m_material.SetFloat(MaxDistance, m_volume.MaxDistance.value);
            m_material.SetFloat(LightIntensity, m_volume.Intensity.value);
            m_material.SetFloat(StepSize, m_volume.StepSize.value);
            m_material.SetFloat(ShadowIntensity, m_volume.ShadowIntensity.value);

            var shaderPass = 0;
            cmd.SetGlobalTexture(MainTex, source);
            cmd.GetTemporaryRT(destination, w, h, 0, FilterMode.Point, RenderTextureFormat.Default);
            cmd.Blit(source, destination);
            cmd.Blit(destination, source, m_material, shaderPass);
        }
        
        public VolumeLightPass(RenderPassEvent eve)
        {
            renderPassEvent = eve;
            var shader = Shader.Find("Custom/VolumeLight");
            if (shader.IsNull())
            {
#if UNITY_EDITOR
                Debug.LogError($"{nameof(VolumeLightPass)}: Shader not found");
#endif
                return;
            }
            m_material = CoreUtils.CreateEngineMaterial(shader);
        }
        
        
    }
    
    VolumeLightPass m_VolumeLightPass;
    public override void Create()
    {
        m_VolumeLightPass = new VolumeLightPass(settings.Event);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_VolumeLightPass);
    }
}
