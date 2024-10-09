Shader "Custom/HDR"
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
        [HDR] _Emission("Emission", Color) = (0, 0, 0, 1)
        _EmissionTexture("Emission Texture", 2D) = "white" {}
        
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
            #include "Assets/Shaders/BRDF/BDRF.hlsl"
            #include "Assets/Shaders/Utils.hlsl"
            #pragma shader_feature_local_fragment _EMISSION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
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
            // Dynamic Lightmap
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            // Fog
            #pragma multi_compile_fog

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

            float4 _Emission;
            TEXTURE2D(_EmissionTexture);
            SAMPLER(sampler_EmissionTexture);

            struct VertIn
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 lightUV : TEXCOORD1;
            };

            struct VertOut
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float2 lightUV : TEXCOORD4;
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
                output.lightUV = input.lightUV;
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
                surface.realNormalDir = input.normalWS;
                surface.viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);

                float ao = SAMPLE_TEXTURE2D(_AO, sampler_AO, input.uv).r;
                surface.ao = ao;

                surface.diffuse = SAMPLE_TEXTURECUBE(_DiffuseMap, sampler_DiffuseMap, reflectionDir).rgb;

                const int MAX_LOD = 5;
                float mipLevel = MAX_LOD * surface.roughness;
                surface.specular = SAMPLE_TEXTURECUBE_LOD(_SpecularMaps, sampler_SpecularMaps, reflectionDir, mipLevel).rgb;
                surface.lightUV = input.lightUV;
                

                EmissionIBLSurface emission;
                emission.emission = _Emission * SAMPLE_TEXTURE2D(_EmissionTexture, sampler_EmissionTexture, input.uv);
                emission.iblSurface = surface;
                
                return EmissionBRDF(emission);
            }
            
            ENDHLSL
        }

        // This pass it not used during regular rendering, only for lightmap baking.
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }
            
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SPECGLOSSMAP
            #pragma shader_feature EDITOR_VISUALIZATION
            
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/MetaPass.hlsl"

            #define MetaInput UnityMetaInput
            #define MetaFragment UnityMetaFragment

            float4 _Albedo;
            TEXTURE2D(_AlbedoTexture);
            SAMPLER(sampler_AlbedoTexture);

            float4 _Emission;
            TEXTURE2D(_EmissionTexture);
            SAMPLER(sampler_EmissionTexture);

            struct VertIn
            {
                float4 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
            };

            struct VertOut
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                #ifdef EDITOR_VISUALIZATION
                float4 VizUV : TEXCOORD1;
                float4 LightCoord : TEXCOORD2;
                #endif
            };
            
            VertOut UniversalVertexMeta(VertIn input)
            {
                VertOut output = (VertOut)0;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
                output.uv = TRANSFORM_TEX(input.uv0, _BaseMap);
            #ifdef EDITOR_VISUALIZATION
                UnityEditorVizData(input.positionOS.xyz, input.uv0, input.uv1, input.uv2, output.VizUV, output.LightCoord);
            #endif
                return output;
            }

            half4 UniversalFragmentMeta(VertOut fragIn, MetaInput metaInput)
            {
            #ifdef EDITOR_VISUALIZATION
                metaInput.VizUV = fragIn.VizUV;
                metaInput.LightCoord = fragIn.LightCoord;
            #endif

                return UnityMetaFragment(metaInput);
            }

            float4 UniversalFragmentMetaLit(VertOut input) : SV_Target
            {
                MetaInput metaInput;
                metaInput.Albedo = _Albedo * SAMPLE_TEXTURE2D(_AlbedoTexture, sampler_AlbedoTexture, input.uv);
                metaInput.Emission = _Emission * SAMPLE_TEXTURE2D(_EmissionTexture, sampler_EmissionTexture, input.uv);
                return UniversalFragmentMeta(input, metaInput);
            }

            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
    }
}
