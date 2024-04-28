#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#ifndef PI
#define PI 3.14159265359
#endif

half4 Lambert(float3 normalWS, float4 tex, float4 color)
{
    Light mainLight = GetMainLight();
    float4 lightColor = float4(mainLight.color, 1);
    float3 lightDir = mainLight.direction;
    float lightAten = saturate(dot(lightDir, normalWS));
    return tex * color * lightAten * lightColor;
}

half4 Lambert(float3 normalWS, float4 color)
{
    Light mainLight = GetMainLight();
    float4 lightColor = float4(mainLight.color, 1);
    float3 lightDir = mainLight.direction;
    float lightAten = saturate(dot(lightDir, normalWS));
    return color * lightAten * lightColor;
}

half4 HalfLambert(float3 normalWS, float4 tex, float4 color)
{
    Light mainLight = GetMainLight();
    half4 ambientColor = UNITY_LIGHTMODEL_AMBIENT;
    half4 lightColor = float4(mainLight.color, 1);
    half3 normal = normalize(normalWS);
    float3 lightDir = mainLight.direction;
    half4 halfLambertFactor = saturate(dot(normalize(lightDir), normal) * 0.5 + 0.5);
    half4 diffuseColor = halfLambertFactor * lightColor;
    half4 output = tex * color * (diffuseColor + ambientColor);
    return output;
}

half4 HalfLambert(float3 normalWS, float4 color)
{
    Light mainLight = GetMainLight();
    half4 ambientColor = UNITY_LIGHTMODEL_AMBIENT;
    half4 lightColor = float4(mainLight.color, 1);
    half3 normal = normalize(normalWS);
    float3 lightDir = mainLight.direction;
    half4 halfLambertFactor = saturate(dot(normalize(lightDir), normal) * 0.5 + 0.5);
    half4 diffuseColor = halfLambertFactor * lightColor;
    half4 output = color * (diffuseColor + ambientColor);
    return output;
}

inline half Pow5(half x)
{
    half x2 = x * x;
    return x2 * x2 * x;
}

// Diffuse ��
// NdotV: dot(normal, viewDir)
// NdotL: dot(normal, lightDir)
// LdotH: dot(lightDir, halfDir)
// halfDir = normalize(lightDir + viewDir)
half DisneyDiffuseTerm(half NdotV, half NdotL, half LdotH, half roughness)
{
    half fd90 = 0.5 + 2 * LdotH * LdotH * roughness;

    // fresnel
    half lightScatter = 1.0 + (fd90 - 1.0) * Pow5(1 - NdotL);
    half viewScatter = 1.0 + (fd90 - 1.0) * Pow5(1 - NdotV);

    return lightScatter * viewScatter;
}

// GTR ��
// ����Ĭ�� y = 2���� GGX �ֲ�
float DisneyGGXTerm(float NdotH, float roughness)
{
    float a2 = roughness * roughness;
    float d = (NdotH * a2 - NdotH) * NdotH + 1.0;
    return PI * a2 / (d * d + 1e-7f); // 1e-7f �����0
}

// F ��
half3 DisneyFresnelTerm(half3 F0, half LdotH)
{
    return F0 + (1.0 - F0) * Pow5(1.0 - LdotH);
}

half3 DisneyFresnelLerp(half3 F0, half3 F90, half LdotH)
{
    half t = Pow5(1.0 - LdotH);
    return lerp(F0, F90, t);
}

// V ��
// Ref: http://jcgt.org/published/0003/02/03/paper.pdf  2014������
// �ɼ�����������κ�������ƽϵ��һ�𣩵ļ���
float DisneyVisibilityTerm (float NdotL, float NdotV, float roughness)
{
    #if 0  //Ĭ�Ϲرգ���ע�������� Frostbite��GGX-Smith Joint��������ȷ��������Ҫ�������Σ��ܲ����ã�
        // ԭʼ�䷽:
        //  lambda_v    = (-1 + sqrt(a2 * (1 - NdotL2) / NdotL2 + 1)) * 0.5f;
        //  lambda_l    = (-1 + sqrt(a2 * (1 - NdotV2) / NdotV2 + 1)) * 0.5f;
        //  G           = 1 / (1 + lambda_v + lambda_l);
        // �������������ʹ����Ż�
        half a          = roughness;
        half a2         = a * a;
        half lambdaV    = NdotL * sqrt((-NdotV * a2 + NdotV) * NdotV + a2);
        half lambdaL    = NdotV * sqrt((-NdotL * a2 + NdotL) * NdotL + a2);
        // �򻯿ɼ�������: (2.0f * NdotL * NdotV) /  ((4.0f * NdotL * NdotV) * (lambda_v + lambda_l + 1e-5f));
        return 0.5f / (lambdaV + lambdaL + 1e-5f);  // �˹��ܲ��ʺ���Mobile�����У�
                                                    // ���epsilonС�ڿ��Ա�ʾΪһ���ֵ
    #else
        // ����ֵ����sqrt������ѧ�ϲ���ȷ�����㹻�ӽ���
        // ���������Respawn Entertainment�� GGX-Smith Joint���Ʒ���
        float a = roughness;
        float lambdaV = NdotL * (NdotV * (1 - a) + a);
        float lambdaL = NdotV * (NdotL * (1 - a) + a);
        return 0.5f / (lambdaV + lambdaL + 1e-4f); // 1e-4f���hlslcc�������Ľ������
    #endif
}

float magnitude(float2 v)
{
    return sqrt(dot(v, v));
}

float magnitude(float3 v)
{
    return sqrt(dot(v, v));
}

float magnitude(float4 v)
{
    return sqrt(dot(v, v));
}

float3x3 RotationX(const float angle)
{
    return float3x3(
        1, 0, 0,
        0, cos(angle), -sin(angle),
        0, sin(angle), cos(angle)
        );
}

float3x3 RotationY(const float angle)
{
    return float3x3(
        cos(angle), 0, sin(angle),
        0, 1, 0,
        -sin(angle), 0, cos(angle)
    );
}

float3x3 RotationZ(const float angle)
{
    return float3x3(
        cos(angle), -sin(angle), 0,
        sin(angle), cos(angle), 0,
        0, 0, 1
    );
}

float3x3 RotationQuaternion(const float3 angle)
{
    const float a = angle.y;
    const float b = angle.x;
    const float c = angle.z;

    const float x = cos(a/2) * sin(b/2) * cos(c/2) + sin(a/2) * cos(b/2) * sin(c/2);
    const float y = sin(a/2) * cos(b/2) * cos(c/2) - cos(a/2) * sin(b/2) * sin(c/2);
    const float z = cos(a/2) * cos(b/2) * sin(c/2) - sin(a/2) * sin(b/2) * cos(c/2);
    const float w = cos(a/2) * cos(b/2) * cos(c/2) + sin(a/2) * sin(b/2) * sin(c/2);

    return float3x3(
        1 - 2*y*y - 2*z*z, 2*x*y - 2*z*w, 2*x*z + 2*y*w,
        2*x*y + 2*z*w, 1 - 2*x*x - 2*z*z, 2*y*z - 2*x*w,
        2*x*z - 2*y*w, 2*y*z + 2*x*w, 1 - 2*x*x - 2*y*y
        );
}

// ʹ��һ����ά��������һ��(0, 1)�������
float rand(float3 se)
{
    return frac(sin(dot(se.xyz, float3(12.9898, 78.233, 53.539))) * 43758.5453);
}


float4 TransformHClipToViewPortPos(float4 positionCS)
{
    float4 o = positionCS * 0.5f;
    o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
    o.zw = positionCS.zw;
    return o / o.w;
}



