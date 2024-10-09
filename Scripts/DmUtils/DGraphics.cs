using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DM.Utils
{
    public static class DGraphics
    {
        public static void SaveRTAsPNG(RenderTexture rt, string path)
        {
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false);
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            SaveTextureAsPNG(texture, path);
            RenderTexture.active = null;
            Object.DestroyImmediate(texture);
        }
        
        public static void SaveTextureAsPNG(Texture2D texture, string path)
        {
            var pngData = texture.EncodeToPNG();
            if (pngData != null)
            {
                System.IO.File.WriteAllBytes(path, pngData);
                Debug.Log("Texture saved to " + path);
            }
        }

        public static Texture2D DecodeTexture(Texture2D source, int mipmap = 0)
        {
            var height = source.height >> mipmap;
            var width = source.width >> mipmap;
            
            if (source.format != TextureFormat.DXT1 && source.format != TextureFormat.DXT5)
            {
                throw new Exception($"Texture format {source.format} not supported");
            }

            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            GCHandle handle;
            IntPtr ptr;
            var output = new byte[4 * width * height];
            var flag = source.format == TextureFormat.DXT1 ? CompressionType.kDxt1 : CompressionType.kDxt5;
            
            if (source.format == TextureFormat.DXT1)
            {
                handle = GCHandle.Alloc(source.GetRawTextureData(), GCHandleType.Pinned);
                ptr = handle.AddrOfPinnedObject();
            }
            else
            {
                var nArray = source.GetRawTextureData<short>().ToArray();
                var array = new short[nArray.Length];
                for (var i = 0; i < nArray.Length; i++)
                {
                    array[i] = nArray[i]; // copy to new array
                }
                handle = GCHandle.Alloc(array, GCHandleType.Pinned);
                ptr = handle.AddrOfPinnedObject();
            }
            
            DecodeTexture(output, ptr, width, height, (int)flag);
            
            var colors = new Color[width * height];
            for (var i = 0; i < 4 * width * height; i+=4)
            {
                colors[i / 4] = new Color(output[i] / 255f, output[i + 1] / 255f, output[i + 2] / 255f, output[i + 3] / 255f);
            }
            
            handle.Free();
            result.SetPixels(colors);
            result.Apply();
            return result;
        }
        
        public static Texture2D EncodeTexture(Texture2D source, CompressionType type)
        {
            if (type != CompressionType.kDxt1 && type != CompressionType.kDxt5)
            {
                throw new Exception("Texture format not supported");
            }
            
            var height = source.height;
            var width = source.width;
            var outputs = Marshal.AllocHGlobal(4 * width * height);
            var inputs = Marshal.AllocHGlobal(4 * width * height);
            for (var i = 0; i < width * height; i += 4)
            {
                Marshal.WriteByte(inputs, i, (byte)source.GetPixel(i % width, i / width).r);
                Marshal.WriteByte(inputs, i + 1, (byte)source.GetPixel(i % width, i / width).g);
                Marshal.WriteByte(inputs, i + 2, (byte)source.GetPixel(i % width, i / width).b);
                Marshal.WriteByte(inputs, i + 3, (byte)source.GetPixel(i % width, i / width).a);
            }
            
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);

            EncodeTexture(inputs, outputs, width, height, (int)type);
                
            var colors = new Color[width * height];
            for (var i = 0; i < width * height; i+=4)
            {
                var r = Marshal.ReadByte(outputs, i);
                var g = Marshal.ReadByte(outputs, i + 1);
                var b = Marshal.ReadByte(outputs, i + 2);
                var a = Marshal.ReadByte(outputs, i + 3);
                    
                colors[i] = new Color(r, g, b, a);
            }
                
            result.SetPixels(colors);
            result.Apply();

            Marshal.FreeHGlobal(inputs);
            Marshal.FreeHGlobal(inputs);
            return result;
        }
        
        public static Vector3 UnpackNormal(Color packedNormal)
        {
            packedNormal.a *= packedNormal.r;
            var x = packedNormal.a * 2 - 1;
            var y = packedNormal.g * 2 - 1;
            var xy = new Vector2(x, y);
            var z = Mathf.Max(1e-10f, Mathf.Sqrt(Mathf.Max(0, Vector2.Dot(xy, xy))));
            return new Vector3(x, y, z);
        }

        public static Color PackNormal(Vector3 normal)
        {
            var x = normal.x * 0.5f + 0.5f;
            var y = normal.y * 0.5f + 0.5f;
            return new Color(1, y, 0, x);
        }

        /// <summary>
        /// Decompress DTX5 data
        /// The decompressed pixels will be written as a contiguous array of width * height
        /// 16 rgba values, with each component as 1 byte each. In memory this is:
        /// { r1, g1, b1, a1, .... , rn, gn, bn, an } for n = width*height
        /// </summary>
        /// <param name="decompressedData">Storage for the decompressed pixels.</param>
        /// <param name="compressedData">The compressed DXT blocks.</param>
        /// <param name="width">The width of the source image.</param>
        /// <param name="height">The height of the source image.</param>
        [DllImport("squishlib")]
        public static extern void DecodeTexture(byte[] decompressedData, IntPtr compressedData, int width, int height, int flag = 0);

        /// <summary>
        /// Compresses an image to DTX5
        /// The source pixels should be presented as a contiguous array of width*height
        /// rgba values, with each component as 1 byte each. In memory this should be:
        /// { r1, g1, b1, a1, .... , rn, gn, bn, an } for n = width*height
        /// </summary>
        /// <param name="decompressedData">The pixels of the source.</param>
        /// <param name="compressedData">Storage for the compressed output.</param>
        /// <param name="width">The width of the source image.</param>
        /// <param name="height">The height of the source image.</param>
        [DllImport("squishlib")]
        public static extern void EncodeTexture(IntPtr decompressedData, IntPtr compressedData, int width, int height, int flag = 0);
    }

    public enum CompressionType
    {
        kDxt1 = 1 << 0,
        kDxt3 = 1 << 1,
        kDxt5 = 1 << 2,
    }
}