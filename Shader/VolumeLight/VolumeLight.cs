

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VolumeLightVolume : VolumeComponent, IPostProcessComponent
{
    [Range(0, 3)]
    public FloatParameter Intensity = new FloatParameter(0f);
    public FloatParameter StepSize = new FloatParameter(0.1f);
    public FloatParameter MaxDistance = new FloatParameter(1000f);
    public IntParameter MaxSteps = new IntParameter(200);
    public FloatParameter ShadowIntensity = new FloatParameter(0.5f);
    
    public bool IsActive() => Intensity.value > 0f;
    public bool IsTileCompatible() => false;
}