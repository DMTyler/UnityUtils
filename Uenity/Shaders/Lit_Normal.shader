// A reimplementation of BRDF model in UE4
// check readme.docx for more details

Shader "Custom/Lit_Normal"
{
    Properties
    {
        _Albedo("Albedo", Color) = (1, 1, 1, 1)
        _AlbedoTexture("Albedo Texture", 2D) = "white" {}
        _Roughness("Roughness", Range(0, 1)) = 0.5
        _RoughnessTexture("Roughness Texture", 2D) = "white" {}
        _Metallic("Metallic", Range(0, 1)) = 0
        _MetallicTexture("Metallic Texture", 2D) = "white" {}
        _AOTexture("AO Texture", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "white" {}
        _NormalMapScale("Normal Map Scale", Range(0, 10)) = 1
        _DiffuseMap("Diffuse Map", Cube) = "" {}
        _SpecularMaps("Specular Maps", CubeArray) = "" {}
    }
    
    SubShader
    {
        Tags
        {
            
        }
        
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

            #include_with_pragmas "Assets/Shaders/BRDF/BDRF.hlsl"
            #include "Assets/Shaders/Utils.hlsl"
            #include "Assets/Shaders/BRDF/Probe/Probe.hlsl"

            float4 _Albedo;
            TEXTURE2D(_AlbedoTexture);
            SAMPLER(sampler_AlbedoTexture);
            
            float _Roughness;
            TEXTURE2D(_RoughnessTexture);
            SAMPLER(sampler_RoughnessTexture);
            
            float _Metallic;
            TEXTURE2D(_MetallicTexture);
            SAMPLER(sampler_MetallicTexture);

            TEXTURE2D(_AOTexture);
            SAMPLER(sampler_AOTexture);

            TEXTURECUBE(_DiffuseMap);
            SAMPLER(sampler_DiffuseMap);

            TEXTURECUBE_ARRAY(_SpecularMaps);
            SAMPLER(sampler_SpecularMaps);

            float _NormalMapScale;
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            struct VertIn
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 lightUV : TEXCOORD1;
                float4 tangent : TANGENT;
            };

            struct VertOut
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float3 tangent : TEXCOORD4;
                float3 bitangent : TEXCOORD5;
            };

            VertOut vert(VertIn input)
            {
                VertOut output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.positionOS = input.positionOS;
                output.uv.xy = input.uv;
                output.uv.zw = input.lightUV;
                output.tangent = TransformObjectToWorld(input.tangent);
                output.bitangent = cross(output.normalWS.xyz, output.tangent.xyz) * input.tangent.w;
                return output;
            }

            float4 frag(VertOut input) : SV_Target
            {
                float2 uv = input.uv.xy;
                IBLSurface surface;
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 normal = normalize(input.normalWS);
                float3 reflectionDir = normalize(reflect(-viewDir, normal));
                
                float4 albedoTex = SAMPLE_TEXTURE2D(_AlbedoTexture, sampler_AlbedoTexture, uv);
                surface.albedo = _Albedo * albedoTex;
                
                float roughnessTex = SAMPLE_TEXTURE2D(_RoughnessTexture, sampler_RoughnessTexture, uv).r;
                surface.roughness = _Roughness * roughnessTex;

                float metallicTex = SAMPLE_TEXTURE2D(_MetallicTexture, sampler_MetallicTexture, uv).r;
                surface.metallic = _Metallic * metallicTex;
                
                surface.worldPos = input.positionWS;

                float4 normalColor = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);
                float3 normalTBN= UnpackNormalScale(normalColor, _NormalMapScale);
                float3 tangent = normalize(input.tangent);
                float3 bitangent = normalize(input.bitangent);

                float3x3 TBN = float3x3(tangent, bitangent, normal);
                normalTBN = normalize(mul(normalTBN, TBN));
                
                surface.normalDir = normalTBN;
                surface.realNormalDir = normal;
                surface.viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);

                float ao = SAMPLE_TEXTURE2D(_AOTexture, sampler_AOTexture, uv).r;
                surface.ao = ao;

                float4 diffuse = SAMPLE_TEXTURECUBE(_DiffuseMap, sampler_DiffuseMap, reflectionDir);
                surface.diffuse = diffuse;

                float lod = surface.roughness * 5;
                float4 specular = SAMPLE_TEXTURECUBE_ARRAY(_SpecularMaps, sampler_SpecularMaps, reflectionDir, lod);
                surface.specular = specular;
                surface.lightUV = input.uv.zw;
                return IBL(surface);
            }
            
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
    }
}
