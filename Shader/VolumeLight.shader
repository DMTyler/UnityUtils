Shader "Custom/VolumeLight"
{
    Properties
    {
        _MainTex ("MainTex", 2D) = "white" {}  
        _MaxStep ("MaxStep", float) = 200      // �����
        _MaxDistance ("MaxDistance",float) = 1000   // ��󲽽�����
        _LightIntensity ("LightIntensity",float) = 0.01 // ÿ�β������ӵĹ���ǿ��
        _StepSize ("StepSize" , float) = 0.1	 // ÿ�β������� 
        _ShadowIntensity ("ShadowIntensity", float) = 0.8 // ��Ӱǿ��
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" } 
        ZWrite Off
        ZTest Always
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)

        CBUFFER_END
        ENDHLSL

        Pass
        {
            HLSLPROGRAM

            // ���ùؼ���
            #pragma shader_feature _AdditionalLights

            // ������Ӱ����ؼ���
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT

            #pragma vertex vert
            #pragma fragment frag

            #include "Assets/06_Scripts/DmUtils/Shader/Utils.hlsl"

            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _MaxDistance;
            float _MaxStep;
            float _LightIntensity;
            float _StepSize;
            float4 _LightColor0;
            float _ShadowIntensity;

            struct vertIn
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct vertOut
            {
                float4 positionCS: SV_POSITION;
                float2 uv : TEXCOORD4;
            };

            vertOut vert(vertIn i)
            {
                vertOut o;
                o.positionCS = TransformObjectToHClip(i.positionOS);
                o.uv = i.uv;
                return o;
            }

            inline float4 GetWorldPos(float2 ScreenUV, float Depth)
            {
                float4 screenPos = float4(ScreenUV.x * 2 - 1, ScreenUV.y * 2 - 1, Depth * 2 -1, 1);
                float4 ndcPos = mul(unity_CameraInvProjection, screenPos);
                ndcPos = float4(ndcPos.xyz / ndcPos.w, 1);
                float4 worldPos = mul(unity_CameraToWorld, ndcPos * float4(1, 1, -1, 1));
                worldPos = float4(worldPos.xyz, 1);
                return worldPos;
            }

            inline float GetShadow(float3 worldPos)
            {
                // ShadowSamplingData spd = GetMainLightShadowSamplingData();
                // float4 params = GetMainLightShadowParams();
                // int shadowSliceIndex = params.w;
                // if (shadowSliceIndex < 0)
                //    return 1.0;

                // float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
                // return SampleShadowmap(TEXTURE2D_ARGS(_MainLightShadowmapTexture, sampler_LinearClampCompare), shadowCoord, spd, params, true);
                float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
                float shadow = MainLightRealtimeShadow(shadowCoord);
                return shadow * _ShadowIntensity + 1 - _ShadowIntensity;
            }

            float4 frag(vertOut i) : SV_Target
            {
                float2 uv = i.uv;
                float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
                depth = 1.0 - depth;
                float3 ro = _WorldSpaceCameraPos;
                float3 worldPos = GetWorldPos(uv, depth).xyz;
                float3 rd = normalize(worldPos - ro);
                float3 currentPos = ro;
                float max_len = min(length(worldPos - ro), _MaxDistance); // ��󲽽�����
                float step = _StepSize; // ÿ�β�������
                float totalIntensity = 0;
                float d = 0;// ��ǰ��������
                for (int c = 0; c < _MaxStep; c++)
                {
                    d += step;
                    if (d > max_len)
                    {
                        break;
                    }
                    currentPos += rd * step;
                    totalIntensity += _LightIntensity * GetShadow(currentPos);
                }
                float4 lightColor = float4(totalIntensity * _LightColor0.rgb, totalIntensity);
                float3 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                float3 finalColor = saturate(texColor * lightColor);
                return float4(finalColor.xyz, 1);
            }

            ENDHLSL
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            // ���ùؼ���
            #pragma shader_feature _ALPHATEST_ON

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS: POSITION;
                float3 normalOS: NORMAL;
            };

            struct Varyings
            {
                float4 positionCS: SV_POSITION;
            };

            // ��ȡ�ü��ռ��µ���Ӱ����
            float4 GetShadowPositionHClips(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                // ��ȡ��Ӱר�òü��ռ��µ�����
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                // �ж��Ƿ�����DirectXƽ̨��ת������
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetShadowPositionHClips(input);
                return output;
            }


            half4 frag(Varyings input): SV_TARGET
            {
                return half4(0,0,0,1);
            }

            ENDHLSL

        }
    }
    FallBack "Packages/com.unity.render-pipelines.universal/FallbackError" 
}
