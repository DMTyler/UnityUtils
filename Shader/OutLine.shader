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
                float ScaleX = abs(scaledScreenParams.x / scaledScreenParams.y); // ���X����Ļ�������ŵı���
                
		        v2f output;
                
		        VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(v.normalOS, v.tagentOS);
                
                float3x3 tbn_ws = float3x3(normalInput.tangentWS, normalInput.bitangentWS, normalInput.normalWS);
                float3 smoothNorWS = normalize(mul(v.uv7.xyz, tbn_ws)); // ת��������ռ�
                // float3 vertexWS = vertexInput.positionWS;
                float3 normalCS = TransformWorldToHClipDir(smoothNorWS); // ����ת�����ü��ռ�
                float2 extendDis = normalize(normalCS.xy) * (_Width * 0.005); // ���ݷ��ߺ��߿����ƫ����
                
                extendDis.x /= ScaleX ; // ������Ļ�������ܲ���1:1������ƫ�����ᱻ������ʾ��������Ļ������x��������
                output.positionCS = vertexInput.positionCS;

                #if _OLWVWD_ON
                    // ��Ļ����߿�Ȼ��
                    output.positionCS.xy +=extendDis;
                #else
                    // ��Ļ����߿�Ȳ��䣬����Ҫ����ƫ�Ƶľ�����NDC������Ϊ�̶�ֵ
                    // ��Ϊ������ת����NDC���꣬���w�������ţ������ȳ�һ��w����ô��ƫ�Ƶľ���Ͳ�����NDC���б任
                    output.positionCS.xy += extendDis * clamp(output.positionCS.w, 0, 20); // clamp����Զ����߹���
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
