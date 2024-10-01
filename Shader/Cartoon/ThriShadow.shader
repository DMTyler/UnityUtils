
Shader "Custom/ThriShadow"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Threshold1 ("Threshold 1", Range(0, 1)) = 0.2
        _Threshold2 ("Threshold 2", Range(0, 1)) = 0.7
        _Strength1("Strength 1", Range(0, 1)) = 0.3
        _Strength2("Strength 2", Range(0, 1)) = 0.5
        _ShadowStrength("Shadow Strength", Range(0, 1)) = 0.5
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            
            // 接收阴影 URP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN      // URP 主光阴影、联机阴影、屏幕空间阴影
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT      // URP 软阴影
            
            #include "Assets/Shaders/Utils.hlsl"
            #pragma vertex vert
            #pragma fragment frag

            struct VertIn
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct VertOut
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            float4 _Color;
            float _Threshold1;
            float _Threshold2;
            float _Strength1;
            float _Strength2;
            float _ShadowStrength;

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            VertOut vert(VertIn input)
            {
                VertOut output;
                output.normalWS = TransformObjectToWorldDir(input.normalOS);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.shadowCoord = TransformWorldToShadowCoord(TransformObjectToWorld(input.positionOS));
                output.positionWS = TransformObjectToWorld(input.positionOS);
                #if UNITY_REVERSED_Z
    			output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
    			output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            float GetDistanceFade(float3 positionWS)
			{
			    float4 posVS = mul(GetWorldToViewMatrix(), float4(positionWS, 1));
			#if UNITY_REVERSED_Z
			    float vz = -posVS.z;
			#else
			    float vz = posVS.z;
			#endif
			    float fade = 1 - smoothstep(30.0, 40.0, vz);
			    return fade;
			}

            float4 frag(VertOut input) : SV_Target
            {
                Light mainLight = GetMainLight();
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 mainColor = _Color * tex * float4(mainLight.color, 1);

                float shadowFadeOut = GetDistanceFade(input.positionWS);
                float shadow = MainLightRealtimeShadow(input.shadowCoord);
                shadow = lerp(1, shadow, shadowFadeOut);
                float shadowStrength = shadow * _ShadowStrength + (1 - _ShadowStrength);

                float addColor = 0;
            	for (int i = 0; i < GetAdditionalLightsCount(); i ++)
                {
                    Light light = GetAdditionalLight(i, input.positionWS, half4(1, 1, 1, 1));
            		addColor += (dot(input.normalWS, mainLight.direction) * 0.5 + 0.5) * _Color * tex * float4(light.color, 1)
            		* light.shadowAttenuation * light.distanceAttenuation;
                }
                
                half3 normal = normalize(input.normalWS);
                float3 lightDir = mainLight.direction;
                float angle = (dot(normalize(lightDir), normal) + 1) / 2;

                
                float strength1 = step(angle, _Threshold1);
                float strength2 = step(angle, _Threshold2);

                float strength = max(1 - shadowStrength, clamp(strength1 * _Strength1 + strength2 * (_Strength2 - _Strength1), 0.0, _Strength2));
                
                half4 output = (UNITY_LIGHTMODEL_AMBIENT + mainColor + addColor) * (1 - strength);
                
                return output;
            }
            
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
    }
}
