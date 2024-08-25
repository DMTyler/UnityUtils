using UnityEngine;
using UnityEngine.Rendering;

public class IntegrateBaker : MonoBehaviour
{
    [SerializeField] private string _path;
    [SerializeField] private ComputeShader _computeShader;
    const int RESOLUTION = 1024;

    public void Bake()
    {
        if (_computeShader is null)
        {
            Debug.LogError("Compute shader is not assigned");
            return;
        }
        
        var dataTexture = new Texture2D(RESOLUTION, RESOLUTION, TextureFormat.RGBA32, false);
        for (var height = 0; height < RESOLUTION; height++)
        {
            var roughness = (float)height / (RESOLUTION - 1);
            for (var width = 0; width < RESOLUTION; width++)
            {
                var NoV = (float)width / (RESOLUTION - 1);
                var color = new Color(NoV, roughness, 0, 1);
                dataTexture.SetPixel(width, height, color);
            }
        }
        
        dataTexture.Apply();
        var kernel = _computeShader.FindKernel("CSMain");
        _computeShader.SetTexture(kernel, "Data", dataTexture);
        
        var rdTexture = new RenderTexture(RESOLUTION, RESOLUTION, 0);
        rdTexture.enableRandomWrite = true;
        rdTexture.Create();
        
        _computeShader.SetTexture(kernel, "Result", rdTexture);
        _computeShader.Dispatch(kernel, RESOLUTION / 8, RESOLUTION / 8, 1);

        AsyncGPUReadback.Request(rdTexture, 0, TextureFormat.RGBA32, Save);
    }

    private void Save(AsyncGPUReadbackRequest request)
    {
        if (request.hasError)
        {
            Debug.LogError("Error during GPU readback.");
        }
        else
        {
            Debug.Log("GPU computation completed.");
            
            var resultTexture = new Texture2D(RESOLUTION, RESOLUTION, TextureFormat.RGBA32, false);
            resultTexture.LoadRawTextureData(request.GetData<uint>());
            resultTexture.Apply();
            
            // 保存到硬盘
            var pngData = resultTexture.EncodeToPNG();
            if (pngData != null)
            {
                var path = _path + "/IntegrateMap.png";
                System.IO.File.WriteAllBytes(path, pngData);
                Debug.Log(path + _path);
            }
            
            Shader.SetGlobalTexture("_GlobalIntegrateMap", resultTexture);
        }
    }
}
