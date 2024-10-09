using System.Collections;
using System.Collections.Generic;
using DM.Utils;
using UnityEngine;
using UnityEngine.Serialization;

public class Pixelizer : MonoBehaviour
{
    [SerializeField] private Texture2D _source;
    [SerializeField, FolderPath] private string _folderPath;
    [SerializeField] private string _suffix = "_P";
    [SerializeField] private bool _avg = false;
    [SerializeField]private int _targetSize = 256; // choose numbers < origin resolution and only power of 2
    
    public void Bake()
    {
        if (_source is null)
        {
            Debug.Log("No source texture set in Pixelizer");
            return;
        }

        var source = _source;
        if (source.format == TextureFormat.DXT1 || source.format == TextureFormat.DXT5)
        {
            source = DGraphics.DecodeTexture(_source);
        }
        if (_avg)
            BakeAvg(source);
        else
            BakeVote(source);
    }
    private void BakeVote(Texture2D source)
    {
        var blockSize = source.width / _targetSize;
        var target = new Texture2D(_targetSize, _targetSize, TextureFormat.RGBA32, false);
        target.filterMode = FilterMode.Point;
        var colors = new Color[_targetSize * _targetSize];
        // iterate through the target texture
        for (var y = 0; y < _targetSize; y++)
        {
            for (var x = 0; x < _targetSize; x++)
            {
                var colorDict = new Dictionary<Color, int>();
                for (var j = 0; j < blockSize; j++)
                {
                    for (var i = 0; i < blockSize; i++)
                    {
                        var color = source.GetPixel(x * blockSize + i, y * blockSize + j);
                        
                        // decrease the precision of color
                        color.r = Mathf.Floor(color.r * 16) / 16;
                        color.g = Mathf.Floor(color.g * 16) / 16;
                        color.b = Mathf.Floor(color.b * 16) / 16;
                        color.a = Mathf.Floor(color.a * 16) / 16;
                        
                        if (colorDict.ContainsKey(color))
                        {
                            colorDict[color]++;
                        }
                        else
                        {
                            colorDict[color] = 1;
                        }
                    }
                }
                // find the most frequent color
                var max = 0;
                var maxColor = Color.clear;
                foreach (var pair in colorDict)
                {
                    if (pair.Value > max)
                    {
                        max = pair.Value;
                        maxColor = pair.Key;
                    }
                }
                colors[y * _targetSize + x] = maxColor;
            }
        }
        target.SetPixels(colors);
        target.Apply();
        DGraphics.SaveTextureAsPNG(target, $"{_folderPath}/{source.name}{_suffix}.png");
    }
    private void BakeAvg(Texture2D source)
    {
        var blockSize = source.width / _targetSize;
        var target = new Texture2D(_targetSize, _targetSize, TextureFormat.RGBA32, false);
        target.filterMode = FilterMode.Point;
        var colors = new Color[_targetSize * _targetSize];
        // iterate through the target texture
        for (var y = 0; y < _targetSize; y++)
        {
            for (var x = 0; x < _targetSize; x++)
            {
                var sum = Color.clear;
                var step = 0;
                for (var j = 0; j < blockSize; j++)
                {
                    for (var i = 0; i < blockSize; i++)
                    {
                        var color = source.GetPixel(x * blockSize + i, y * blockSize + j);
                        sum += color;
                        step++;
                    }
                }
                var avg = sum / step;
                colors[y * _targetSize + x] = avg;
            }
        }
        target.SetPixels(colors);
        target.Apply();
        DGraphics.SaveTextureAsPNG(target, $"{_folderPath}/{source.name}{_suffix}.png");
    }
}
