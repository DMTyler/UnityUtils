Shader "Custom/VolumetricFog"
{
    Properties
    {
        _BoundsCentre("Bounds Centre", Vector) = (0, 0, 0)
        _BoundsExtents("Bounds Extents", Vector) = (1, 1, 1)
        _BoundsBorder("Bounds Border", Vector) = (1, 1, 1, 1)
        _StepSize("Step Size", Range(1, 20)) = 1
        _Dithering("Dittering", Range(0, 1)) = 0.6
        _Color("Color", Color) = (1, 1, 1, 1)
        _NoiseTexture("Noise Texture", 2D) = "White"{}
        _DetailTexture("Detail Texture", 3D) = "White"{}
        _DetailColor("Detail Color", Color) = (1, 1, 1, 1)
        _WindDirection("Wind Direction", Vector) = (1, 0, 0, 0)
        _NoiseScale("Noise Scale", Range(0, 1)) = 0.5
        _Density("Density", Float) = 1.01
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Off
        
        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #include "Assets/Shaders/Utils.hlsl"

            #define BORDER_SIZE_SPHERE _BoundsBorder.x
            #define BORDER_START_SPHERE _BoundsBorder.y
            #define BORDER_SIZE_BOX _BoundsBorder.xz
            #define BORDER_START_BOX _BoundsBorder.yw

            #define DETAIL_OFFSET 0
            #define DETAIL_STRENGTH 1
            #define DETAIL_SCALE 1
            
            struct VertIn
            {
                float4 PositionOS : POSITION;
            };

            struct VertOut
            {
                float4 PositionCS : SV_POSITION;
                float3 PositionWS : TEXCOORD0;
            };

            float3 _BoundsCentre;
            float3 _BoundsExtents;
            float _StepSize;
            float _Dithering;
            float4 _BoundsBorder;
            float4 _Color;
            float4 _DetailColor;
            float3 _WindDirection;
            float _NoiseScale;
            float _Density;

            TEXTURE2D(_NoiseTexture);
            SAMPLER(sampler_NoiseTexture);

            TEXTURE3D(_DetailTexture);
            SAMPLER(sampler_DetailTexture);

            VertOut vert(VertIn input)
            {
                VertOut output;
                output.PositionCS = TransformObjectToHClip(input.PositionOS);
                output.PositionWS = TransformObjectToWorld(input.PositionOS);
                return output;
            }
            
            float2 rayBoxDist(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRayDir)
            {
                float3 tMin = (boundsMin - rayOrigin) * invRayDir;
                float3 tMax = (boundsMax - rayOrigin) * invRayDir;
                float3 t1 = min(tMin, tMax);
                float3 t2 = max(tMin, tMax);
                float tNear = max(max(t1.x, t1.y), t1.z);
                float tFar = min(min(t2.x, t2.y), t2.z);
                return float2(tNear, tFar);
            }

            float SampleDensity(float3 position)
            {
                //采样3D噪音图
	            half detail = SAMPLE_TEXTURE3D(_DetailTexture, sampler_DetailTexture, float4(position * DETAIL_SCALE - _WindDirection, 0)).r;
	            //这时wpos变成一个相对坐标了
	            position.xyz -= _BoundsCentre;
	            //缩放
	            position.y /= _BoundsExtents.y;
	            float density = SAMPLE_TEXTURE2D(_NoiseTexture, sampler_NoiseTexture, float4(position.xz * _NoiseScale - _WindDirection.xz, 0, 0));
	            density -= abs(position.y);
	            density += (detail + DETAIL_OFFSET) * DETAIL_STRENGTH;
	            return density;
            }

            void AddFog(float3 wpos, float rayStep, inout half4 sum)
            {
                float density = SampleDensity(wpos);
                float2 dist2 = abs(wpos.xz - _BoundsCentre.xz);
                float2 border2 = saturate((dist2 - BORDER_START_BOX) / BORDER_SIZE_BOX);
                float border = 1.0 - max(border2.x, border2.y);
                density *= border * border;

                if (density > 0)
                {
                    half4 fgCol = _Color * half4(1, 1, 1, density);
                    // fgCol.rgb *= density * fgCol.a;
                    fgCol *= min(1.0, _Density * rayStep);
                    sum += fgCol * (1.0 - sum.a);
                }
	        }


            float4 frag(VertOut input) : SV_TARGET
            {
                float3 ray = normalize(input.PositionWS - _WorldSpaceCameraPos);
                half3 invRayDir = 1.0 / ray;
                float2 t = rayBoxDist(_BoundsCentre - _BoundsExtents, _BoundsCentre + _BoundsExtents, _WorldSpaceCameraPos, invRayDir);
                float distInsideBox = t.y;

                float dithering = InterleavedGradientNoise(input.PositionCS.xy, 0);
                float rayLenInsideBox = clamp(distInsideBox - dithering * _Dithering, 0, distInsideBox);
                float stepCount = rayLenInsideBox / _StepSize;
                stepCount = clamp(stepCount, 1, 100);
                float4 sum = float4(0, 0, 0, 0);
                float3 startPos = input.PositionWS;
                for (int i = 0; i < stepCount; i++)
                {
                    AddFog(startPos, i, sum);
                    startPos += ray * _StepSize;
                }
                return sum;
            }

            
            ENDHLSL
        }
    }
}
