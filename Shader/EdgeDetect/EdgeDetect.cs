using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class EdgeDetectVolume : VolumeComponent, IPostProcessComponent
{
    public BoolParameter EdgeOnly = new (false);
    public ColorParameter EdgeColour = new(new Color(0, 0, 0, 1));
    public ColorParameter BackgroundColour = new (new Color(1, 1, 1, 1));
    [Range(0, 1.9f)]
    public FloatParameter EdgeWidth = new (0);

    public bool IsActive() => EdgeWidth.value is > 0 and < 2;

    public bool IsTileCompatible() => false;
}