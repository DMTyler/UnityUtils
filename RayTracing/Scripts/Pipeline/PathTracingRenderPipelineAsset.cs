using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Path Tracing Render Pipeline", fileName = "New PathTracingRenderPipelineAsset")]
public class PathTracingRenderPipelineAsset : RenderPipelineAsset
{
    protected override RenderPipeline CreatePipeline()
    {
        return new PathTracingRenderPipeline();
    }
}
