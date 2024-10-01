// A reimplementation of BRDF model in UE4
// check readme.docx for more details

Shader "Custom/Lit"
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
            #pragma fragment frag
            #include_with_pragmas "Assets/Shaders/BRDF/BDRF.hlsl"
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

            struct VertIn
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
            };

            struct VertOut
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float2 lightmapUV : TEXCOORD4;
            };

            inline float4 SampleEnvironmentMap(VertOut input, float roughness)
            {
                float3 N = normalize(input.positionOS);
                float3 V = N;
                
                const uint SAMPLE_COUNT = 1024u;
                float totalWeight = 0;
                float3 result = float3(0, 0, 0);
                for(uint i = 0; i < SAMPLE_COUNT; i++)
                {
                    float2 Xi = Hammersley(i, SAMPLE_COUNT);
                    float3 H = ImportanceSampleGGX(Xi, N, roughness);
                    float3 L = 2 * dot(V, H) * H - V;
                    float NdotL = max(dot(N, L), 0);
                    
                    if(NdotL > 0)
                    {
                        float4 envCol = SAMPLE_TEXTURECUBE(unity_SpecCube0, samplerunity_SpecCube0, L);
                        float3 envHDRCol = DecodeHDREnvironment(envCol, unity_SpecCube0_HDR).rgb;
                        result += envHDRCol * NdotL;
                        totalWeight += NdotL;
                    }
                }
                return float4(result / totalWeight, 1);
            }

            VertOut vert(VertIn input)
            {
                VertOut output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.positionOS = input.positionOS;
                output.uv = input.uv;
                output.lightmapUV = input.lightmapUV;
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
                
                surface.normalDir = input.normalWS;
                surface.realNormalDir = normal;
                surface.viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);

                float ao = SAMPLE_TEXTURE2D(_AO, sampler_AO, input.uv).r;
                surface.ao = ao;

                surface.irradiance = SAMPLE_TEXTURECUBE(_DiffuseMap, sampler_DiffuseMap, reflectionDir).rgb;

                const int MAX_LOD = 5;
                float mipLevel = MAX_LOD * surface.roughness;
                surface.specular = SAMPLE_TEXTURECUBE_LOD(_SpecularMaps, sampler_SpecularMaps, reflectionDir, mipLevel).rgb;

                surface.lightUV = input.lightmapUV;
                
                return IBL(surface);
            }
            
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
    }
}
