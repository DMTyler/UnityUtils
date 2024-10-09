Shader "Custom/Sprite"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Albedo ("Albedo (RGB)", 2D) = "white" {}
        _Roughness("Roughness", Range(0, 1)) = 0.5
        _RoughnessTexture("Roughness Texture", 2D) = "white" {}
        _Metallic("Metallic", Range(0, 1)) = 0
        _MetallicTexture("Metallic Texture", 2D) = "white" {}
        _AOTexture("AO Texture", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalMapScale ("Normal Map Scale", Range(0, 10)) = 1
    }
    SubShader
    {
        Cull Off
        Pass
        {
            HLSLPROGRAM
            
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma shader_feature_local_fragment _EMISSION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #define _ADDITIONAL_LIGHT_CALCULATE_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTERED_RENDERING
            // Soft Shadows
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            // SSAO
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            // Mix
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            // Shadowmask
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            // Lightmap
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            // Fog
            #define FOG_EXP2

            #include "Assets/Shaders/BRDF/BDRF.hlsl"

            float4 _Color;
            float _NormalMapScale;

            TEXTURE2D(_Albedo);
            SAMPLER(sampler_Albedo);

            
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            float _Roughness;
            TEXTURE2D(_RoughnessMap);
            SAMPLER(sampler_RoughnessMap);

            float _Metallic;
            TEXTURE2D(_MetallicMap);
            SAMPLER(sampler_MetallicMap);

            struct VertIn
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };

            struct VertOut
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
            };

            VertOut vert (VertIn input)
            {
                VertOut o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.positionWS = TransformObjectToWorld(input.positionOS);
                o.uv = input.uv;
                o.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                o.tangentWS = float4(normalize(TransformObjectToWorldNormal(input.tangentOS)), input.tangentOS.w);
                return o;
            }

            float4 frag (VertOut input) : SV_Target
            {
                float4 albedo = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv);
                float4 color = albedo * _Color;
                clip(color.a - 0.1);
                
                float3 normal = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                normal *= _NormalMapScale;
                float3 T = normalize(input.tangentWS);
                float3 N = normalize(input.normalWS);
                float3 B = cross(N, T) * input.tangentWS.w;
                float3x3 TBN = float3x3(T, B, N);
                normal = mul(normal, TBN);

                BRDFSurface surface;
                surface.albedo = color;

                float roughness = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, input.uv).r * _Roughness;
                surface.roughness = 0.8f;

                float metallic = SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, input.uv).r * _Metallic;
                surface.metallic = 0.2f;
                surface.normalDir = normal;
                surface.worldPos = input.positionWS;
                surface.viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                surface.ao = 1;
                
                return BRDF(surface);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            HLSLPROGRAM

            #define _ALPHATEST_ON
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 _Color;
            TEXTURE2D(_Albedo);
            SAMPLER(sampler_Albedo);
            
            struct VertIn
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 texcoord     : TEXCOORD0;
            };

            struct VertOut
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
            };

            float4 GetShadowPositionHClip(VertIn input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                return positionCS;
            }

            VertOut ShadowPassVertex(VertIn input)
            {
                VertOut output;

                output.uv = input.texcoord;
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragment(VertOut input) : SV_TARGET
            {
                float alpha = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, input.uv).a;
                clip(alpha - 0.1);

            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
            #endif

                return 0;
            }
            ENDHLSL
        }
    }
}
