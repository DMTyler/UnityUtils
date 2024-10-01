using System;
using System.Collections;
using System.Collections.Generic;
using DM.Utils;
using UnityEngine;

[ExecuteInEditMode]
public class VolumetricFog : MonoBehaviour
{
    [SerializeField] private Material material;
    private static readonly int BoundsCentre = Shader.PropertyToID("_BoundsCentre");
    private static readonly int BoundsExtents = Shader.PropertyToID("_BoundsExtents");
    private static readonly int WindDirection = Shader.PropertyToID("_WindDirection");
    private static readonly int BoundsBorder = Shader.PropertyToID("_BoundsBorder");
    
    [SerializeField] private Vector3 windDirection;
    [SerializeField] private float windStrength = 0.1f;
    [SerializeField] private float border;
    
    private Vector3 boundsCentreBuffer;
    private Vector3 boundsExtentsBuffer;
    private Vector3 windDirectionBuffer;

    private void Start()
    {
        if (material.IsNull())
            material = GetComponent<Renderer>().material;
        if (material.IsNull() || material.shader.name != "Custom/VolumetricFog")
            throw new NullReferenceException("Material or Shader not found.");
    }

    private void Update()
    {
        if (transform.position != boundsCentreBuffer)
        {
            material.SetVector(BoundsCentre, transform.position);
            boundsCentreBuffer = transform.position;
        }
        
        if (transform.lossyScale / 2 != boundsExtentsBuffer)
        {
            material.SetVector(BoundsExtents, transform.lossyScale / 2);
            boundsExtentsBuffer = transform.lossyScale / 2;
        }
        
        var boundsBorder = new Vector4(boundsExtentsBuffer.x * border + 0.0001f, boundsExtentsBuffer.x * (1f - border), 
            boundsExtentsBuffer.z * border + 0.0001f, boundsExtentsBuffer.z * (1f - border));
        material.SetVector(BoundsBorder, boundsBorder);

        windDirectionBuffer += windDirection * (Time.deltaTime * windStrength);
        windDirectionBuffer.x %= 10000;
        windDirectionBuffer.y %= 10000;
        windDirectionBuffer.z %= 10000;
        
        material.SetVector(WindDirection, windDirectionBuffer);
    }
}
