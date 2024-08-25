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
        
        _DiffuseMap("Diffuse Map", Cube) = "" {}
        _SpecularMaps("Specular Maps", Cube) = "" {}
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
            #define SHADOW_RECEIVER_ON
            #pragma fragment frag
            #include "Assets/Shaders/BDRF.hlsl"
            #include "Assets/Shaders/Utils.hlsl"

            float4 _Albedo;
            TEXTURE2D(_AlbedoTexture);
            SAMPLER(sampler_AlbedoTexture);
            
            float _Roughness;
            TEXTURE2D(_RoughnessTexture);
            SAMPLER(sampler_RoughnessTexture);
            
            float _Metallic;
            TEXTURE2D(_MetallicTexture);
            SAMPLER(sampler_MetallicTexture);

            TEXTURE2D(_AO);
            SAMPLER(sampler_AO);

            TEXTURECUBE(_DiffuseMap);
            SAMPLER(sampler_DiffuseMap);

            TEXTURECUBE(_SpecularMaps);
            SAMPLER(sampler_SpecularMaps);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            struct VertIn
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 tangent : TANGENT;
            };

            struct VertOut
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
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
                output.uv = input.uv;
                output.tangent = TransformObjectToWorld(input.tangent);
                output.bitangent = cross(output.normalWS.xyz, output.tangent.xyz) * input.tangent.w;
                return output;
            }

            float4 frag(VertOut input) : SV_Target
            {
                IBLSurface surface;
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 normal = normalize(input.normalWS);
                float3 reflectionDir = normalize(reflect(-viewDir, normal));
                
                float4 albedoTex = SAMPLE_TEXTURE2D(_AlbedoTexture, sampler_AlbedoTexture, input.uv);
                surface.albedo = _Albedo * albedoTex;
                
                float roughnessTex = SAMPLE_TEXTURE2D(_RoughnessTexture, sampler_RoughnessTexture, input.uv).r;
                surface.roughness = _Roughness * roughnessTex;

                float metallicTex = SAMPLE_TEXTURE2D(_MetallicTexture, sampler_MetallicTexture, input.uv).r;
                surface.metallic = _Metallic * metallicTex;
                
                surface.worldPos = input.positionWS;

                float4 normalColor = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv);
                float3 normalTBN= UnpackNormal(normalColor);
                float3 tangent = input.tangent;
                float3 bitangent = input.bitangent;

                float3x3 TBN = float3x3(tangent, bitangent, normal);
                normalTBN = normalize(mul(normalTBN, TBN));
                
                surface.normalDir = normalTBN;
                surface.viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);

                float ao = SAMPLE_TEXTURE2D(_AO, sampler_AO, input.uv).r;
                surface.ao = ao;

                surface.irradiance = SAMPLE_TEXTURECUBE(_DiffuseMap, sampler_DiffuseMap, reflectionDir).rgb;

                const int MAX_LOD = 5;
                float mipLevel = MAX_LOD * surface.roughness;
                surface.specular = SAMPLE_TEXTURECUBE_LOD(_SpecularMaps, sampler_SpecularMaps, reflectionDir, mipLevel).rgb;
                
                return IBL(surface);
            }
            
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
    }
}
