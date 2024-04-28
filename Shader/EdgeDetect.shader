Shader "PostEffect/EdgeDetect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _EdgeOnly ("EdgeOnly", float) = 1.0
        _EdgeColor ("EdgeColor", Color) = (0, 0, 0, 1)
        _BackgroudColor ("BackgroundColor", Color) = (1, 1, 1, 1)
        _Width ("Width", Range(0, 1)) = 0
    }
    SubShader
    {
        Pass
        {
            ZTest Always 
            Cull Off 
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Assets/06_Scripts/DmUtils/Shader/Utils.hlsl"
 
            //properties
            sampler2D _MainTex;
            half4 _MainTex_TexelSize;
            float _EdgeOnly;
            float4 _EdgeColor;
            float4 _BackgroudColor;
            float _Width;

            struct a2v
            {
                float4 vertex : POSITION;
                half2 texcoord : TEXCOORD0;
            };
 
            struct v2f
            {
                float4 pos : SV_POSITION;
                half2 uv[9] : TEXCOORD0; 
            };
 
            v2f vert(a2v v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex);
 
                half2 uv = v.texcoord;

                // 九宫格采样
                o.uv[0] = uv + _MainTex_TexelSize.xy * half2(-1, -1);
                o.uv[1] = uv + _MainTex_TexelSize.xy * half2(0, -1);
                o.uv[2] = uv + _MainTex_TexelSize.xy * half2(1, -1);
                o.uv[3] = uv + _MainTex_TexelSize.xy * half2(-1, 0);
                o.uv[4] = uv + _MainTex_TexelSize.xy * half2(0, 0);
                o.uv[5] = uv + _MainTex_TexelSize.xy * half2(1, 0);
                o.uv[6] = uv + _MainTex_TexelSize.xy * half2(-1, 1);
                o.uv[7] = uv + _MainTex_TexelSize.xy * half2(0, 1);
                o.uv[8] = uv + _MainTex_TexelSize.xy * half2(1, 1);
 
                return o;
            }

            // 置灰
            float luminance(float4 color)
            {
                return 0.2125 * color.r + 0.7154 * color.g + 0.0721 * color.b;
            }
            
            // Sobel算子
            half Sobel(v2f i)
            {
                // 定义卷积核：
                const half Gx[9] = 
                {
                    -1, 0, 1,
                    -2, 0, 2,
                    -1, 0, 1
                };
                const half Gy[9] =
                {
                    -1, -2, -1,
                    0, 0, 0, 
                    1, 2, 1
                };
                
                half texColor;
                half edgeX = 0;
                half edgeY = 0;
                
                for(int j = 0; j < 9; j++) {
                    texColor = luminance(tex2D(_MainTex, i.uv[j]));  // 依次对9个像素采样
                    edgeX += texColor * Gx[j];
                    edgeY += texColor * Gy[j];
                }
 
                half edge = 1 - abs(edgeX) - abs(edgeY); // 绝对值代替开根号求模，节省开销
                // half edge = 1 - pow(edgeX*edgeX + edgeY*edgeY, 0.5);
                return edge;
            }
 
            float4 frag(v2f i) : SV_Target
            {
                half edge = Sobel(i);
                if (edge > _Width) {
                    return tex2D(_MainTex, i.uv[4]);
                }
 
                float4 withEdgeColor = lerp(_EdgeColor, tex2D(_MainTex, i.uv[4]), saturate(edge - _Width));  // 4是原始像素位置
                float4 onlyEdgeColor = lerp(_EdgeColor, _BackgroudColor, edge);
                return lerp(withEdgeColor, onlyEdgeColor, _EdgeOnly);
            }
 
            
            ENDHLSL
        }
    }
    Fallback "Diffuse"
}
