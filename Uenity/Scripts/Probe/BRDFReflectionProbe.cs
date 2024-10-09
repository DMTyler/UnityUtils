using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

public class BRDFReflectionProbe : MonoBehaviour
{
    #region Fields
    [SerializeField] private BackgroundType _backgroundType;
    [SerializeField] private Color _backgroundColor = Color.black;
    [SerializeField] private int _envResolution = 1024; // choose only power of 2
    [SerializeField] private ComputeShader _inverseYComputeShader;
    [Space(20)]
    [SerializeField] private ComputeShader _diffuseComputeShader;
    [SerializeField] private int _diffuseMapResolution = 64; // choose only power of 2
    [SerializeField, Range(0.01f, 0.1f)] private float _diffuseStep = 0.025f;
    [SerializeField, Range(0.01f, 10)] private float _diffuseIntensity = 1;
    [Space(20)]
    [SerializeField] private ComputeShader _specularComputeShader;
    [SerializeField] private int _specularMapResolution = 1024;
    [SerializeField, Tooltip("Sample count per pixel")] private int _specularSPP = 1024; // choose only power of 2
    [SerializeField] private float _specularIntensity = 1;,
    }
    
    // Shader Properties
    private static readonly int Resolution = Shader.PropertyToID("_Resolution");
    private static readonly int Result = Shader.PropertyToID("Result");
    private static readonly int Step = Shader.PropertyToID("_Step");
    private static readonly int Intensity = Shader.PropertyToID("_Intensity");
    private static readonly int EnvMapFace0 = Shader.PropertyToID("_EnvMapFace0");
    private static readonly int EnvMapFace1 = Shader.PropertyToID("_EnvMapFace1");
    private static readonly int EnvMapFace2 = Shader.PropertyToID("_EnvMapFace2");
    private static readonly int EnvMapFace3 = Shader.PropertyToID("_EnvMapFace3");
    private static readonly int EnvMapFace4 = Shader.PropertyToID("_EnvMapFace4");
    private static readonly int EnvMapFace5 = Shader.PropertyToID("_EnvMapFace5");
    private static readonly int CurrentFace = Shader.PropertyToID("_CurrentFace");
    private static readonly int EnvMapResolution = Shader.PropertyToID("_EnvMapResolution");
    private static readonly int Source = Shader.PropertyToID("_Source");
    private static readonly int Spp = Shader.PropertyToID("_SPP");
    private static readonly int Roughness = Shader.PropertyToID("_Roughness");
    private static readonly int DiffuseMap = Shader.PropertyToID("_DiffuseMap");
    private static readonly int SpecularMaps = Shader.PropertyToID("_SpecularMaps");

    #endregion

    [Button]
    public void Test()
    {
        BakeEnvCubemap();
    }

    [Button]
    public void Bake()
    {
        var mat = gameObject.GetComponent<MeshRenderer>().sharedMaterial;
        if (!BakeDiffuse(mat)) Debug.LogWarning("Failed to bake diffuse map for probe " + name);
        if (!BakeSpecular(mat)) Debug.LogWarning("Failed to bake specular map for probe " + name);
    }

    public void BakeEnvCubemap()
    {
        var camera = new GameObject("EnvCamera").AddComponent<Camera>();
        
        camera.transform.position = transform.position;
        camera.allowHDR = true;
        if (IsColor)
        {
            camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = _backgroundColor;
        }
        else
            camera.clearFlags = CameraClearFlags.Skybox;
        
        var cubemap = new Cubemap(_envResolution, TextureFormat.RGBAFloat, false);
        camera.RenderToCubemap(cubemap);
        
        SaveCubeAsPNG(cubemap, "/Textures/EnvMaps/");
    }
    
    public List<RenderTexture> BakeEnv()
    {
        var camera = new GameObject("EnvCamera").AddComponent<Camera>();
        var envMapFaces = new List<RenderTexture>();
        camera.transform.position = transform.position;
        // camera.allowHDR = true;
        camera.fieldOfView = 90;
        
        if (IsColor)
        {
            camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = _backgroundColor;
        }
        else
            camera.clearFlags = CameraClearFlags.Skybox;
        
        var rotations = new[]
        {
            Quaternion.AngleAxis(90, Vector3.up), // +X
            Quaternion.AngleAxis(-90, Vector3.up), // -X
            Quaternion.AngleAxis(-90, Vector3.right), // +Y
            Quaternion.AngleAxis(90, Vector3.right), // -Y
            Quaternion.identity, // +Z
            Quaternion.AngleAxis(180, Vector3.up), // -Z
        };

        if (_inverseYComputeShader == null)
        {
            Debug.LogError("Inverse Y Compute Shader is null.");
            return envMapFaces;
        }
        
        
        for (var i = 0; i < 6; i++)
        {
            var temp = new RenderTexture(_envResolution, _envResolution, 0);
            temp.format = RenderTextureFormat.ARGBFloat;
            temp.enableRandomWrite = true;
            temp.Create();
            camera.transform.rotation = rotations[i];
            camera.targetTexture = temp;
            camera.Render();
            camera.targetTexture = null;
            var rt = new RenderTexture(_envResolution, _envResolution, 0);
            rt.format = RenderTextureFormat.ARGBFloat;
            rt.enableRandomWrite = true;
            rt.Create();
            var shader = _inverseYComputeShader;
            
            shader.SetInt(Resolution, _envResolution);
            shader.SetTexture(0, Source, temp);
            shader.SetTexture(0, Result, rt);
            shader.Dispatch(0, _envResolution / 8, _envResolution / 8, 1);
            temp.Release();
            DestroyImmediate(temp);
            
            envMapFaces.Add(rt);
            camera.targetTexture = null;
        }
        
        DestroyImmediate(camera.gameObject);
        return envMapFaces;
    }
    
    public bool BakeDiffuse(Material mat)
    {

        if (_diffuseComputeShader == null)
        {
            Debug.LogError("Diffuse Compute Shader is null.");
            return false;
        }
        
        var envMapFaces = BakeEnv();

        if (envMapFaces.Count != 6)
        {
            Debug.LogError("Failed to bake environment map.");
            return false;
        }
        var cube = new Cubemap(_diffuseMapResolution, TextureFormat.RGBAFloat, false);
        
        var shader = _diffuseComputeShader;
        shader.SetInt(Resolution, _diffuseMapResolution);
        shader.SetFloat(Step, _diffuseStep);
        shader.SetInt(EnvMapResolution, _envResolution);
        shader.SetFloat(Intensity, _diffuseIntensity);

        var kernel = shader.FindKernel("CSMain");
        shader.SetTexture(kernel, EnvMapFace0, envMapFaces[0]);
        shader.SetTexture(kernel, EnvMapFace1, envMapFaces[1]);
        shader.SetTexture(kernel, EnvMapFace2, envMapFaces[2]);
        shader.SetTexture(kernel, EnvMapFace3, envMapFaces[3]);
        shader.SetTexture(kernel, EnvMapFace4, envMapFaces[4]);
        shader.SetTexture(kernel, EnvMapFace5, envMapFaces[5]);
        
        var x = _diffuseMapResolution / 8;
        var y = _diffuseMapResolution / 8;

        for (var i = 0; i < 6; i++)
        {
            var rt = new RenderTexture(_diffuseMapResolution, _diffuseMapResolution, 0);
            rt.format = RenderTextureFormat.ARGBFloat;
            rt.enableRandomWrite = true;
            rt.Create();
            shader.SetInt(CurrentFace, i);
            shader.SetTexture(kernel, Result, rt);
            shader.Dispatch(kernel, x, y, 1);
            
            var texture = new Texture2D(_diffuseMapResolution, _diffuseMapResolution, TextureFormat.RGBAFloat, false);
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, _diffuseMapResolution, _diffuseMapResolution), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            
            rt.Release();
            DestroyImmediate(rt);
            var colors = texture.GetPixels();
            cube.SetPixels(colors, (CubemapFace)i);
            
            DestroyImmediate(texture);
        }
        cube.Apply();
        
        AssetDatabase.CreateAsset(cube, $"Assets/Cubemaps/Diffuse/{name}_Specular.asset");
        AssetDatabase.SaveAssets();

        mat.SetTexture(DiffuseMap, cube);
        envMapFaces.ForEach(rt =>
        {
            rt.Release();
            DestroyImmediate(rt);
        });
        envMapFaces.Clear();
        return true;
    }

    public bool BakeSpecular(Material mat)
    {
        if (_specularComputeShader == null)
        {
            Debug.LogError("Specular Compute Shader is null.");
            return false;
        }

        if (_specularMapResolution < 128)
        {
            Debug.LogError("Specular map resolution must be at least 128.");
            return false;
        }
        
        var invShader = _inverseYComputeShader;
        if (invShader == null)
        {
            Debug.LogError("Inverse Y Compute Shader is null.");
            return false;
        }
        
        var envMapFaces = BakeEnv();

        if (envMapFaces.Count < 6)
        {
            Debug.LogError("Failed to bake environment map.");
            return false;
        }

        const int maxLod = 5;

        var shader = _specularComputeShader;
        shader.SetInt(EnvMapResolution, _envResolution);
        shader.SetInt(Spp, _specularSPP);
        shader.SetFloat(Intensity, _specularIntensity);
        var kernel = shader.FindKernel("CSMain");
        shader.SetTexture(kernel, EnvMapFace0, envMapFaces[0]);
        shader.SetTexture(kernel, EnvMapFace1, envMapFaces[1]);
        shader.SetTexture(kernel, EnvMapFace2, envMapFaces[2]);
        shader.SetTexture(kernel, EnvMapFace3, envMapFaces[3]);
        shader.SetTexture(kernel, EnvMapFace4, envMapFaces[4]);
        shader.SetTexture(kernel, EnvMapFace5, envMapFaces[5]);
        
        var x = _specularMapResolution / 8;
        var y = _specularMapResolution / 8;

        var cubeArray = new CubemapArray(_specularMapResolution, maxLod + 1, TextureFormat.RGBAFloat, false);
        cubeArray.wrapMode = TextureWrapMode.Clamp;
        cubeArray.filterMode = FilterMode.Trilinear;
        
        var resolution = _specularMapResolution;
        for (var lod = 0; lod <= maxLod; lod++)
        {
            shader.SetFloat("_CurrentLOD", lod);
            
            for (var face = 0; face < 6; face++)
            {
                shader.SetInt(Resolution, resolution);
                shader.SetInt(CurrentFace, face);
                var rt = new RenderTexture(resolution, resolution, 0);
                rt.format = RenderTextureFormat.ARGBFloat;
                rt.enableRandomWrite = true;
                rt.Create();
                
                shader.SetTexture(kernel, Result, rt);
                shader.Dispatch(kernel, x, y, 1);
                
                var texture = new Texture2D(resolution, resolution, TextureFormat.RGBAFloat, false);
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                texture.Apply();
                RenderTexture.active = null;
                rt.Release();
                DestroyImmediate(rt);

                var colors = texture.GetPixels();
                cubeArray.SetPixels(colors, (CubemapFace)face, lod);
                DestroyImmediate(texture);
            }
        }
        cubeArray.Apply();
        mat.SetTexture(SpecularMaps, cubeArray);
        envMapFaces.ForEach(rt =>
        {
            rt.Release();
            DestroyImmediate(rt);
        });
        envMapFaces.Clear();
        
        AssetDatabase.CreateAsset(cubeArray, $"Assets/Cubemaps/Specular/{name}_Specular.asset");
        AssetDatabase.SaveAssets();
        
        return true;

    }
    
    public void SaveCubeAsPNG(Cubemap cubemap, string path)
    {
        var texture = new Texture2D(cubemap.width, cubemap.height, TextureFormat.RGBAFloat, false);
        for (var i = 0; i < 6; i++)
        {
            var colors = cubemap.GetPixels((CubemapFace)i);
            texture.SetPixels(colors);
            SaveTextureAsPNG(texture, Application.dataPath + path + i + ".png");
        }
        DestroyImmediate(texture);
    }

    public void SaveRTAsPNG(RenderTexture rt, string path)
    {
        var texture = new Texture2D(_envResolution, _envResolution, TextureFormat.RGBAFloat, false);
        RenderTexture.active = rt;
        texture.ReadPixels(new Rect(0, 0, _envResolution, _envResolution), 0, 0);
        SaveTextureAsPNG(texture, Application.dataPath + path);
        RenderTexture.active = null;
        DestroyImmediate(texture);
    }

    public void SaveTextureAsPNG(Texture2D texture, string path)
    {
        byte[] pngData = texture.EncodeToPNG();
        if (pngData != null)
        {
            System.IO.File.WriteAllBytes(path, pngData);
            Debug.Log("Texture saved to " + path);
        }
    }
}
