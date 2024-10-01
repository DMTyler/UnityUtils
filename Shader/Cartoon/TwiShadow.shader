Shader "Custom/TwiShadow"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MinThreshold ("Min Threshold", Range(0, 1)) = 0.2
        _MaxThreshold ("Max Threshold", Range(0, 1)) = 0.7
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.5
        _MainTex ("Texture", 2D) = "white" {}
    	_SpecularRange ("Specular Range", Range(0, 100)) = 10
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
            struct BPStruct
			{
				Light light;
				float3 normalWS;
				float3 viewDirWS;
				float3 positionWS;
				float2 uv;
			};

            float4 _Color;
            float _MinThreshold;
            float _MaxThreshold;
            float _ShadowStrength;
            float _SpecularRange;

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
			    float fade = 1 - smoothstep(30.0, 40.0, vz); // 模拟阴影自然消失
			    return fade;
			}

            float4 CalculateLight(BPStruct bp)
            {
            	float NdotL = max(0, dot(bp.normalWS, bp.light.direction));
	            float3 h = normalize(bp.light.direction + bp.viewDirWS);
            	float spec = pow(max(0, dot(h, bp.normalWS)), _SpecularRange);
            	real4 lightColor = real4(bp.light.color, 1.0) * ((bp.light.shadowAttenuation * bp.light.distanceAttenuation) * NdotL);

            	// real4 diffuse = texColor * nDotL * lightColor;
            	// 计算线性衰减
            	real4 specular = spec * lightColor;

            	return specular;
            }

            float4 frag(VertOut input) : SV_Target
            {
                Light mainLight = GetMainLight();
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            	float shadow = MainLightRealtimeShadow(input.shadowCoord);
                half4 mainColor = _Color * tex * (float4(mainLight.color, 1) * lerp((1 - _ShadowStrength), 1, shadow));

            	float addColor = 0;
            	for (int i = 0; i < GetAdditionalLightsCount(); i ++)
                {
                    Light light = GetAdditionalLight(i, input.positionWS, half4(1, 1, 1, 1));
                	addColor += (dot(input.normalWS, mainLight.direction)) * _Color * tex * float4(light.color, 1)
            		* light.shadowAttenuation * light.distanceAttenuation; 
                }

            	mainColor = mainColor + addColor;
                half3 normal = normalize(input.normalWS);
                float3 lightDir = mainLight.direction;
                float angle = dot(normalize(lightDir), normal);
                float dot = smoothstep(_MinThreshold, _MaxThreshold, angle);
                half halfLambertFactor = saturate(dot * _ShadowStrength + (1 - _ShadowStrength));
                half4 diffuseColor = halfLambertFactor;
                half4 output = (UNITY_LIGHTMODEL_AMBIENT + mainColor) * diffuseColor;
            	
                return output;
            }
            
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
    }
}
