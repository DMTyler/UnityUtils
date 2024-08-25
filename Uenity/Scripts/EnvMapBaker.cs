using System.IO;
using UnityEditor;
using UnityEngine;


[RequireComponent(typeof(Renderer))]
public class EnvMapBaker : MonoBehaviour
{
    #region Fields
    [SerializeField] private Cubemap _envMap;
    [SerializeField] private BackgroundType _backgroundType;
    [SerializeField] private Color _backgroundColor = Color.black;
    
    [SerializeField] private float _diffuseStep = 0.025f;
    [SerializeField] private Cubemap _diffuseMap;
    
    [SerializeField] private Cubemap _specularMaps;
    private static float PI = Mathf.PI;

    private static readonly int Roughness = Shader.PropertyToID("_Roughness");
    private static readonly int MaxResolution = Shader.PropertyToID("_MaxResolution");

    private enum BackgroundType
    {
        Skybox,
        Color,
    }

    private ValueDropdownList<int> GetResolutions()
    {
        return new ValueDropdownList<int>()
        {
            { "128", 128 },
            { "256", 256 },
            { "512", 512 },
            { "1024", 1024 },
            { "2048", 2048 }
        };
    }
    
    private bool IsColor => _backgroundType == BackgroundType.Color;

    #endregion

    public void BakeEnvMap()
    {
        var camera = new GameObject("EnvCamera").AddComponent<Camera>();
        camera.transform.position = transform.position;
        camera.transform.rotation = Quaternion.identity;
        if (IsColor)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = _backgroundColor;
        }
        else
            camera.clearFlags = CameraClearFlags.Skybox;
        var cubemap = new Cubemap(2048, TextureFormat.RGBA32, false);
        camera.RenderToCubemap(cubemap);
        _envMap = cubemap;
        DestroyImmediate(camera.gameObject);
        
        AssetDatabase.CreateAsset(cubemap, $"Assets/Cubemaps/Environment/{name}_Env.asset");
        AssetDatabase.SaveAssets();
    }
    
    private bool EnableBakeCondition()
    {
        var renderer = GetComponent<MeshRenderer>();
        if (renderer is null) return false;
        if (renderer.sharedMaterial is null) return false;
        if (renderer.sharedMaterial.shader.name != "Custom/Lit") return false;
        return true;
    }

    public void BakeDiffuseMap()
    {
        if (_envMap is null) BakeEnvMap();
        if (_envMap is null) return;
        const int mapWidth = 32;
        var map = new Cubemap(mapWidth, TextureFormat.RGBA32, false);
        for (var faceCount = 0; faceCount < 6; faceCount++)
        {
            for (var width = 0; width < mapWidth; width++)
            {
                for (var height = 0; height < mapWidth; height++)
                {
                    var uv = new Vector2((float)width / mapWidth, (float)height / mapWidth);
                    var N = GetCubeDirection(uv, (CubemapFace)faceCount).normalized;
                    var up = Vector3.up;
                    var right = Vector3.Cross(up, N);
                    up = Vector3.Cross(N, right);
                    var nrSamples = 0;
                    var irradiance = Color.black;
                    for (var phi = 0f; phi < 2 * PI; phi += _diffuseStep)
                    {
                        for (var theta = 0f; theta < 0.5f * PI; theta += _diffuseStep)
                        {
                            var tangentSample = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta));
                            var sampleVec = tangentSample.x * right + tangentSample.y * up + tangentSample.z * N;
                            var uvSample = GetCubeUVCoord(sampleVec);
                            var face = GetCubemapFace(sampleVec);
                            var sampleColor = _envMap.GetPixel(face, (int)(uvSample.x * _envMap.width), (int)(uvSample.y * _envMap.height));
                            irradiance += sampleColor;
                            nrSamples++;
                        }
                    }
                    irradiance /= nrSamples;
                    map.SetPixel((CubemapFace)faceCount, width, height, irradiance);
                }
            }
        }
        
        map.Apply();
       _diffuseMap = map;
       
       AssetDatabase.CreateAsset(map, $"Assets/Cubemaps/Diffuse/{name}_Diffuse.asset");
       AssetDatabase.SaveAssets();
       
       var material = GetComponent<MeshRenderer>()?.sharedMaterial;
       if (material is null) return;
       material.SetTexture("_DiffuseMap", _diffuseMap);
       
       
    }

    public void BakeSpecularMap()
    {
        if (_envMap is null) BakeEnvMap();
        if (_envMap is null) return;
        const uint iterationCount = 1024u;
        const int mipLevel = 6;
        const int _maxResolution = 128;
        // var map = new CubemapArray(_maxResolution, mipLevel, TextureFormat.RGBA32, false);
        var map = new Cubemap(_maxResolution, TextureFormat.RGBA32, mipLevel, false);
        map.filterMode = FilterMode.Trilinear;
        map.wrapMode = TextureWrapMode.Clamp;
        
        for (var currentMip = 0; currentMip < mipLevel; currentMip++)
        {
            for (var faceCount = 0; faceCount < 6; faceCount++)
            {
                for (var height = 0; height < _maxResolution; height += 1)
                {
                    for (var width = 0; width < _maxResolution; width += 1)
                    {
                        var N = GetCubeDirection(
                            new Vector2((float)width / _maxResolution, 
                                (float)height / _maxResolution),
                            (CubemapFace)faceCount);
                        var V = N;
                        var sum = Color.black;
                        var totalWeight = 0f;
                        var roughness = (float)currentMip / (mipLevel - 1);
                        for (var i = 0u; i < iterationCount; i++)
                        {
                            var Xi = Hammersley(i, iterationCount);
                            var H = ImportanceSampleGGX(Xi, N, roughness);
                            var L = Vector3.Reflect(-V, H);
                            var NoL = Mathf.Max(0, Vector3.Dot(N, L));
                            
                            if (NoL > 0)
                            {
                                var uv = GetCubeUVCoord(L);
                                var face = GetCubemapFace(L);
                                var sample = _envMap.GetPixel(face, (int)(uv.x * _envMap.width),
                                    (int)(uv.y * _envMap.height));
                                sum += sample * NoL;
                                totalWeight += NoL;
                            }
                        }

                        sum /= totalWeight;
                        map.SetPixel((CubemapFace)faceCount, width, height, sum, currentMip);
                    }
                }
            }
        }
        
        map.Apply();
        _specularMaps = map;
        
        AssetDatabase.CreateAsset(map, $"Assets/Cubemaps/Specular/{name}_Specular.asset");
        AssetDatabase.SaveAssets();
        
        var material = GetComponent<MeshRenderer>()?.sharedMaterial;
        if (material is null) return;
        material.SetTexture("_SpecularMaps", _specularMaps);
    }
    
    private CubemapFace GetCubemapFace(Vector3 dir)
    {
        dir = dir.normalized;

        // 计算方向向量x, y, z的绝对值
        var absX = Mathf.Abs(dir.x);
        var absY = Mathf.Abs(dir.y);
        var absZ = Mathf.Abs(dir.z);

        // 优先判断最主要轴
        if (absX >= absY && absX >= absZ)
            return dir.x > 0 ? CubemapFace.PositiveX : CubemapFace.NegativeX;
        else if (absY >= absX && absY >= absZ)
            return dir.y > 0 ? CubemapFace.PositiveY : CubemapFace.NegativeY;
        else
            return dir.z > 0 ? CubemapFace.PositiveZ : CubemapFace.NegativeZ;
    }

    private Vector3 GetCubeDirection(Vector2 uv, CubemapFace face)
    {
        var u = uv.x * 2 - 1;
        var v = uv.y * 2 - 1;
        var dir = Vector3.zero;

        switch (face)
        {
            case CubemapFace.PositiveX:
                dir = new Vector3(1, -v, -u);
                break;
            case CubemapFace.NegativeX:
                dir = new Vector3(-1, -v, u);
                break;
            case CubemapFace.PositiveY:
                dir = new Vector3(u, 1, v);
                break;
            case CubemapFace.NegativeY:
                dir = new Vector3(u, -1, -v);
                break;
            case CubemapFace.PositiveZ:
                dir = new Vector3(u, -v, 1);
                break;
            case CubemapFace.NegativeZ:
                dir = new Vector3(-u, -v, -1);
                break;
        }

        return dir.normalized;
    }
    
    private Vector2 GetCubeUVCoord(Vector3 dir)
    {
        var absDir = new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z));
        var uv = new Vector2();
        var face = GetCubemapFace(dir);

        switch (face)
        {
            case CubemapFace.PositiveX:
                uv.x = -dir.z / absDir.x;
                uv.y = -dir.y / absDir.x;
                break;
            case CubemapFace.NegativeX:
                uv.x = dir.z / absDir.x;
                uv.y = -dir.y / absDir.x;
                break;
            case CubemapFace.PositiveY:
                uv.x = dir.x / absDir.y;
                uv.y = dir.z / absDir.y;
                break;
            case CubemapFace.NegativeY:
                uv.x = dir.x / absDir.y;
                uv.y = -dir.z / absDir.y;
                break;
            case CubemapFace.PositiveZ:
                uv.x = dir.x / absDir.z;
                uv.y = -dir.y / absDir.z;
                break;
            case CubemapFace.NegativeZ:
                uv.x = -dir.x / absDir.z;
                uv.y = -dir.y / absDir.z;
                break;
        }
        
        uv = 0.5f * (uv + Vector2.one);
        
        return uv;
    }
    
    private Vector2 GetCubeUVCoord(Vector3 dir, CubemapFace face)
    {
        var absDir = new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z));
        var uv = new Vector2();

        switch (face)
        {
            case CubemapFace.PositiveX:
                uv.x = -dir.z / absDir.x;
                uv.y = -dir.y / absDir.x;
                break;
            case CubemapFace.NegativeX:
                uv.x = dir.z / absDir.x;
                uv.y = dir.y / absDir.x;
                break;
            case CubemapFace.PositiveY:
                uv.x = -dir.x / absDir.y;
                uv.y = dir.z / absDir.y;
                break;
            case CubemapFace.NegativeY:
                uv.x = dir.x / absDir.y;
                uv.y = -dir.z / absDir.y;
                break;
            case CubemapFace.PositiveZ:
                uv.x = dir.x / absDir.z;
                uv.y = -dir.y / absDir.z;
                break;
            case CubemapFace.NegativeZ:
                uv.x = -dir.x / absDir.z;
                uv.y = dir.y / absDir.z;
                break;
        }
        
        uv = 0.5f * (uv + Vector2.one);
        
        return uv;
    }
    
    private Vector2 Hammersley(uint i, uint N)
    {
        return new Vector2((float)i / N, RadicalInverse_VdC(i));
    }

    private float RadicalInverse_VdC(uint bits)
    {
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
        bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
        bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
        bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
        return bits * 2.3283064365386963e-10f; // / 0x100000000
    }
    
    private Vector3 ImportanceSampleGGX(Vector2 Xi, Vector3 N, float roughness)
    {
        var a = roughness * roughness;
        var phi = 2 * PI * Xi.x;
        var cosTheta = Mathf.Sqrt((1 - Xi.y) / (1 + (a * a - 1) * Xi.y));
        var sinTheta = Mathf.Sqrt(1 - cosTheta * cosTheta);
        var H = new Vector3(sinTheta * Mathf.Cos(phi), sinTheta * Mathf.Sin(phi), cosTheta);

        var V = N.z < 0.99? new Vector3(0, 1, 0) : new Vector3(1, 0, 0);
        var tangent = Vector3.Cross(N, V);
        var bitangent = Vector3.Cross(N, tangent);
        
        var sampleVec = tangent * H.x + bitangent * H.y + N * H.z;
        return sampleVec.normalized;
    }

    private void SaveCubemapAsPNG(Cubemap cubemap, string directory)
    {
        if (cubemap == null)
        {
            Debug.LogError("Cubemap is not assigned.");
            return;
        }

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        for (int i = 0; i < 6; i++)
        {
            CubemapFace face = (CubemapFace)i;
            Texture2D texture = new Texture2D(cubemap.width, cubemap.height, TextureFormat.RGBA32, false);
            texture.SetPixels(cubemap.GetPixels(face));
            texture.Apply();

            byte[] bytes = texture.EncodeToPNG();
            string filePath = Path.Combine(directory, $"{face}.png");
            File.WriteAllBytes(filePath, bytes);
            Debug.Log($"Saved {face} to {filePath}");

            // Clean up
            DestroyImmediate(texture);
        }
    }
}
