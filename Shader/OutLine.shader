Shader "Custom/OutLine"
{
    Properties 
    {
        _Width("Width", Range(0, 10)) = 1
        _Color("Color", Color) = (0, 0, 0, 1)
    }
    
    SubShader
    {
        Pass
        {
            Cull Front
            ZWrite Off
            
            HLSLPROGRAM
            #include "Assets/06_Scripts/DmUtils/Shader/Utils.hlsl"
            #pragma vertex vert
            #pragma fragment frag

            float _Width;
            float4 _Color;

            struct a2v
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tagentOS : TANGENT;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 uv7 : TEXCOORD7;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
            };

            v2f vert(a2v v)
            {
                
                float4 scaledScreenParams = GetScaledScreenParams();
                float ScaleX = abs(scaledScreenParams.x / scaledScreenParams.y); // 求得X因屏幕比例缩放的倍数
                
		        v2f output;
                
		        VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(v.normalOS, v.tagentOS);
                
                float3x3 tbn_ws = float3x3(normalInput.tangentWS, normalInput.bitangentWS, normalInput.normalWS);
                float3 smoothNorWS = normalize(mul(v.uv7.xyz, tbn_ws)); // 转换回世界空间
                // float3 vertexWS = vertexInput.positionWS;
                float3 normalCS = TransformWorldToHClipDir(smoothNorWS); // 法线转换到裁剪空间
                float2 extendDis = normalize(normalCS.xy) * (_Width * 0.005); // 根据法线和线宽计算偏移量
                
                extendDis.x /= ScaleX ; // 由于屏幕比例可能不是1:1，所以偏移量会被拉伸显示，根据屏幕比例把x进行修正
                output.positionCS = vertexInput.positionCS;

                #if _OLWVWD_ON
                    // 屏幕下描边宽度会变
                    output.positionCS.xy +=extendDis;
                #else
                    // 屏幕下描边宽度不变，则需要顶点偏移的距离在NDC坐标下为固定值
                    // 因为后续会转换成NDC坐标，会除w进行缩放，所以先乘一个w，那么该偏移的距离就不会在NDC下有变换
                    output.positionCS.xy += extendDis * clamp(output.positionCS.w, 0, 20); // clamp避免远处描边诡异
                #endif
		        return output;
            }

           float4 frag(v2f i) : SV_TARGET
            {
                return _Color;
            }
            
            ENDHLSL
        }
    }
        
    
    FallBack "Diffuse"
}
