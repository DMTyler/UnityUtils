#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#ifndef EPSILON
#define EPSILON 1e-6
#endif
#define LIGHTMAP_ON
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

inline float NormalDistributionGGX(float3 N, float3 H, float roughness)
{
    // NDF_GGXTR(N, H, roughness) = roughness^2 / ( PI * ( dot(N, H))^2 * (roughness^2 - 1) + 1 )^2
    const float a = roughness * roughness;
    const float a2 = a * a;
    const float nh2 = pow(max(dot(N, H), 0), 2);
    float denom = (nh2 * (a2 - 1) + 1);
    float nom = a2;
    denom = PI * denom * denom + EPSILON;
    return nom / max(denom, 1e-6f);
}

inline float Geometry_SchlickGGX(float NdotV, float roughness)
{
    // G_SchlickGGX(NdotV, roughness) =
    // 2 * NdotV / (NdotV + sqrt(roughness^2 + (1 - roughness^2) * NdotV^2))
    const float r = (roughness + 1) * (roughness + 1) / 8;
    const float denom = (NdotV * (1 - r) + r) + 0.0001f;
    return NdotV / denom;
}

inline float Geometry_Smith(float3 N, float3 V, float3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0);
    float NdotL = max(dot(N, L), 0);
    float ggx2 = Geometry_SchlickGGX(NdotV, roughness);
    float ggx1 = Geometry_SchlickGGX(NdotL, roughness);
    return ggx1 * ggx2;
}

// F0 is the reflectance at normal incidence
inline float3 FresnelSchlick(float cosTheta, float3 F0)
{
    return F0 + (1 - F0) * pow(1 - cosTheta, 5);
}

// Schlick's approximation
inline float RadicalInverse_VdC(uint bits) 
{
    bits = (bits << 16u) | (bits >> 16u);
    bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
    bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
    bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
    bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
    return float(bits) * 2.3283064365386963e-10; // / 0x100000000
}

inline float VanDerCorpus(uint n, uint base)
{
    float invBase = 1.0 / float(base);
    float denom   = 1.0;
    float result  = 0.0;
    for(uint i = 0u; i < 32u; ++i)
    {
        if (n > 0u)
        {
            denom = fmod(float(n), 2.0);
            result += denom * invBase;
            invBase = invBase / 2.0;
            n = uint(float(n) / 2.0);
        }
    }
    return result;
}

inline float2 Hammersley(uint i, uint base)
{
    #ifdef USE_BIT_MANIPULATION
    return float2(float(i) / float(base), RadicalInverse_VdC(i));
    #else
    return float2((i) / float(base), VanDerCorpus(i, 2u));
    #endif
}

// This gives us a sample vector somewhat oriented around the expected microsurface's halfway vector
// based on some input roughness and the low-discrepancy sequence value Xi
// N: normal vector
float3 ImportanceSampleGGX(float2 Xi, float3 N, float roughness)
{
    float a = roughness * roughness; // note that this is squared in order to get better visual results
    float phi = 2 * PI * Xi.x;
    float cosTheta = sqrt((1 - Xi.y) / (1 + (a * a - 1) * Xi.y));
    float sinTheta = sqrt(1 - cosTheta * cosTheta);

    // from spherical coordinates to cartesian coordinates
    float3 H;
    H.x = cos(phi) * sinTheta;
    H.y = sin(phi) * sinTheta;
    H.z = cosTheta;

    // from tangent-space vector to world-space sample vector
    float3 up = abs(N.z) < 0.999 ? float3(0, 0, 1) : float3(1, 0, 0);
    float3 tangent = normalize(cross(up, N));
    float3 bitangent = cross(N, tangent);

    float3 sampleVec = tangent * H.x + bitangent * H.y + N * H.z;
    return normalize(sampleVec);
}

inline float Geometry_IBL_SchlickGGX(float NdotV,  float roughness)
{
    float k = (roughness * roughness) / 2;
    float denom = NdotV * (1 - k) + k;
    return NdotV / denom;
}

inline float Geometry_IBL_Smith(float3 N, float3 V, float3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0);
    float NdotL = max(dot(N, L), 0);
    float ggx2 = Geometry_IBL_SchlickGGX(NdotV, roughness);
    float ggx1 = Geometry_IBL_SchlickGGX(NdotL, roughness);
    return ggx1 * ggx2;
}

// global texture for IBL
TEXTURE2D(_GlobalIntegrateMap);
SAMPLER(sampler_GlobalIntegrateMap);

// calculate the scale and bias (two integrals) of F0 (x is scale, y is bias)
// https://www.cnblogs.com/bitzhuwei/p/specular-IBL.html#_label2_0_0
inline float2 IntegrateBRDF(float3 NdotV, float roughness)
{
    float3 V;
    V.x = sqrt(1 - NdotV * NdotV);
    V.y = 0;
    V.z = NdotV;

    float scale = 0; // Integral1
    float bias = 0; // Integral2
    const uint SAMPLE_COUNT = 1024u;
    const float3 N = float3(0, 0, 1);
    for(uint i = 0u; i < SAMPLE_COUNT; ++i)
    {
        float2 Xi = Hammersley(i, SAMPLE_COUNT);
        float3 H = ImportanceSampleGGX(Xi, N, roughness);
        float3 L = 2 * dot(V, H) * H - V;

        float NdotL = max(L.z, 0);
        float NdotH = max(H.z, 0);
        float VdotH = max(dot(V, H), 0);
        
        if (NdotL > 0)
        {
            float G = Geometry_IBL_Smith(N, V, L, roughness);
            float G_Vis = G * VdotH / (NdotH * NdotV + 1e-4f);
            float Fc = pow(1 - VdotH, 5);
            
            scale += (1 - Fc) * G_Vis;
            bias += Fc * G_Vis;
        }
    }

    scale = scale / float(SAMPLE_COUNT);
    bias = bias / float(SAMPLE_COUNT);
    
    return float2(scale, bias);
}

struct BRDFSurface
{
    float3 worldPos;
    float3 normalDir;
    float3 viewDir; 
    float3 albedo; 
    float metallic; 
    float roughness;
    float ao; // ambient occlusion
};

float4 BRDF(BRDFSurface surface)
{
    float3 N = normalize(surface.normalDir);
    float3 V = normalize(surface.viewDir);
    float3 f0 = float3(0.04, 0.04, 0.04);
    f0 = lerp(f0, surface.albedo, surface.metallic);
    float4 shadowCoord = TransformWorldToShadowCoord(surface.worldPos);

    Light mainLight = GetMainLight(shadowCoord);
    float3 L = mainLight.direction;
    float3 H = normalize(V + L);
    float3 Lo = float3(0, 0, 0);

    // 1. main light
    float3 radiance = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
    float NDF = NormalDistributionGGX(N, H, surface.roughness);
    float G = Geometry_Smith(N, V, L, surface.roughness);
    float3 F = FresnelSchlick(clamp(dot(H, V), 0, 1), f0);

    float3 nomin = NDF * G * F;
    float denom = 4 * max(dot(N, V), 0) * max(dot(N, L), 0) + 1e-4f; // prevent divided by zero
    float3 specular = nomin / denom;
    float3 ks = F;
    float3 kd = float3(1, 1, 1) - ks;
    kd *= 1 - surface.metallic; // a linear blend for partly metal & metal have no diffuse reflection
    float NdotL = max(dot(N, L), 0);
    Lo += (kd * surface.albedo / PI + specular) * radiance * NdotL; // ks has been included in specular


    // 2. additional lights
    for (int i = 0; i < GetAdditionalLightsCount(); i++)
    {
        Light additionalLight = GetAdditionalLight(i, surface.worldPos, shadowCoord);
        float3 L = additionalLight.direction;
        float3 H = normalize(V + L);
        float3 radiance = additionalLight.color * additionalLight.distanceAttenuation * additionalLight.shadowAttenuation;
        float NDF = NormalDistributionGGX(N, H, surface.roughness);
        float G = Geometry_Smith(N, V, L, surface.roughness);
        float3 F = FresnelSchlick(clamp(dot(H, V), 0 ,1), f0);

        float3 nomin = NDF * G * F;
        float denom = 4 * max(dot(N, V), 0) * max(dot(N, L), 0) + 1e-4f; // prevent divided by zero
        float3 specular = nomin / denom;
        float3 ks = F;
        float3 kd = float3(1, 1, 1) - ks;
        kd *= 1 - surface.metallic; // a linear blend for partly metal & metal have no diffuse reflection
        float NdotL = max(dot(N, L), 0);
        Lo += (kd * surface.albedo / PI + specular) * radiance * NdotL; // ks has been included in specular
    }

    float3 ambient = float3(0.5, 0.5, 0.5) * surface.albedo * surface.ao;
    float3 color = ambient + Lo;
    color = MixFogColor(color.rgb, unity_FogColor, unity_FogParams.x);
    
    return float4(color, 1);
}

struct IBLSurface
{
    float3 worldPos;
    float3 normalDir;
    float3 realNormalDir; // the normal dir of mesh
    float3 viewDir; 
    float4 albedo; 
    float metallic; 
    float roughness;
    float ao; // ambient occlusion
    float2 lightUV; // lightmap UV
    float3 diffuse; // ambient color, should from IBL
    float3 specular;
};

float3 FresnelSchlickRoughness(float cosTheta, float3 F0, float roughness)
{
    return F0 + (max(float4(1, 1, 1, 1) - F0, 0) * pow(1 - cosTheta, 5)) * (1 - roughness);
}

inline float GetMipmapLevelFromRoughness(float roughness)
{
    return 5 * roughness;
}

float3 TransformNormalMapToWorldNormal(float4 inputColor, float3 normalWS, float4 tangent)
{
    float3 bitangent = cross(normalWS, tangent) * tangent.w;
    float3 tbnNormal = float3(inputColor.r, inputColor.g, inputColor.b) * 2 - 1;
    return normalize(tbnNormal.x * tangent + tbnNormal.y * bitangent + tbnNormal.z * normalWS);
}

float4 IBL(IBLSurface surface)
{
    float3 N = normalize(surface.normalDir);
    float3 V = normalize(surface.viewDir);
    float3 f0 = float3(0.04, 0.04, 0.04);
    f0 = lerp(f0, surface.albedo, surface.metallic);
    float4 shadowCoord = TransformWorldToShadowCoord(surface.worldPos);

    Light mainLight = GetMainLight(shadowCoord);
    float3 L = mainLight.direction;
    float3 H = normalize(V + L);
    float3 DirectLo = float3(0, 0, 0);

    // Begin Of Direct Light Part
    // 1. main light
    float3 radiance = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
    float D = NormalDistributionGGX(N, H, surface.roughness);
    float G = Geometry_Smith(N, V, L, surface.roughness);
    float3 F = FresnelSchlick(clamp(dot(H, V), 0, 1), f0);

    float3 nomin = D * G * F;
    float denom = 4 * max(dot(N, V), 0) * max(dot(N, L), 0) + 1e-4f; // prevent divided by zero
    float3 specular = nomin / denom;
    float3 ks = F;
    float3 kd = float3(1, 1, 1) - ks;
    kd *= 1 - surface.metallic; // a linear blend for partly metal & metal have no diffuse reflection
    float NdotL = max(dot(N, L), 0);
    DirectLo += (kd * surface.albedo * surface.ao / PI + specular) * radiance * NdotL; // ks has been included in specular

    // 2. additional lights
    for (int i = 0; i < GetAdditionalLightsCount(); i++)
    {
        Light additionalLight = GetAdditionalLight(i, surface.worldPos, shadowCoord);
        float3 L = additionalLight.direction;
        float3 H = normalize(V + L);
        #ifdef SHADOW_RECEIVER
        float3 radiance = additionalLight.color * additionalLight.shadowAttenuation;
        #else
        float3 radiance = additionalLight.color * additionalLight.distanceAttenuation * additionalLight.shadowAttenuation;
        #endif
        float NDF = NormalDistributionGGX(N, H, surface.roughness);
        float G = Geometry_Smith(N, V, L, surface.roughness);
        float3 F = FresnelSchlick(clamp(dot(H, V), 0 ,1), f0);

        float3 nomin = NDF * G * F;
        float denom = 4 * max(dot(N, V), 0) * max(dot(N, L), 0) + 1e-4f; // prevent divided by zero
        float3 specular = nomin / denom;
        float3 ks = F;
        float3 kd = float3(1, 1, 1) - ks;
        kd *= 1 - surface.metallic; // a linear blend for partly metal & metal have no diffuse reflection
        float NdotL = max(dot(N, L), 0);
        DirectLo += (kd * surface.albedo * surface.ao / PI + specular) * radiance * NdotL; // ks has been included in specular
    }

    // 3. Lightmap
    float2 lightUV;
    OUTPUT_LIGHTMAP_UV(surface.lightUV, unity_LightmapST, lightUV);
    float3 gi = SAMPLE_GI(lightUV, 0, surface.realNormalDir);
    L = surface.realNormalDir; // the light direction is the reflection of view direction
    H = normalize(V + L);
    D = NormalDistributionGGX(N, H, surface.roughness);
    G = Geometry_Smith(N, V, L, surface.roughness);
    F = FresnelSchlick(clamp(dot(H, V), 0, 1), f0);
    nomin = D * G * F;
    denom = 4 * max(dot(N, V), 0) * max(dot(N, L), 0) + 1e-4f; // prevent divided by zero
    specular = nomin / denom;
    ks = F;
    kd = float3(1, 1, 1) - ks;
    kd *= 1 - surface.metallic; // a linear blend for partly metal & metal have no diffuse reflection
    NdotL = max(dot(N, L), 0);
    DirectLo += (kd * surface.albedo / PI + specular) * gi * NdotL; // ks has been included in specular
    // End Of Direct Light Part

    // Begin Of IBL Part
    float3 IBLLo = float3(0, 0, 0);
    F = FresnelSchlickRoughness(max(dot(N, V), 0), f0, surface.roughness);
    ks = F;
    kd = float3(1, 1, 1) - ks;
    kd *= 1 - surface.metallic;
    float3 diffuse = surface.diffuse * surface.albedo / PI;
    IBLLo += kd * diffuse * surface.ao;
    float2 envBRDF = SAMPLE_TEXTURE2D(_GlobalIntegrateMap, sampler_GlobalIntegrateMap, float2(max(dot(N, V), 0), surface.roughness)).rg;
    IBLLo += surface.specular * (F * envBRDF.x + envBRDF.y);
    float3 color = IBLLo + DirectLo;
;
    return float4(color, 1);
}

inline float4 SampleEnvironmentMap(float3 positionOS, float roughness)
{
    float3 N = normalize(positionOS);
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

struct SSSSurface
{
    BRDFSurface brdfSurface;
    float distortion;
    float power;
    float scale;
    float thickness;
    
};

float4 FastSSS(SSSSurface surface)
{
    BRDFSurface brdfSurface = surface.brdfSurface;
    float3 N = normalize(brdfSurface.normalDir);
    float3 V = normalize(brdfSurface.viewDir);
    float3 f0 = float3(0.04, 0.04, 0.04);
    f0 = lerp(f0, brdfSurface.albedo, brdfSurface.metallic);

    Light mainLight = GetMainLight(TransformWorldToShadowCoord(brdfSurface.worldPos));
    float3 L = mainLight.direction;
    float3 H = normalize(V + L);
    float3 Lo = float3(0, 0, 0);

    // 1. main light
    float attenuation = mainLight.shadowAttenuation;
    float3 radiance = mainLight.color * attenuation;
    //float3 radiance = mainLight.color * mainLight.distanceAttenuation;
    float NDF = NormalDistributionGGX(N, H, brdfSurface.roughness);
    float G = Geometry_Smith(N, V, L, brdfSurface.roughness);
    float3 F = FresnelSchlick(clamp(dot(H, V), 0, 1), f0);

    float3 nomin = NDF * G * F;
    float denom = 4 * max(dot(N, V), 0) * max(dot(N, L), 0) + 1e-4f; // prevent divided by zero
    float3 specular = nomin / denom;
    float3 ks = F;
    float3 kd = float3(1, 1, 1) - ks;
    kd *= 1 - brdfSurface.metallic; // a linear blend for partly metal & metal have no diffuse reflection
    float NdotL = max(dot(N, L), 0);
    Lo += (kd * brdfSurface.albedo / PI + specular) * radiance * NdotL; // ks has been included in specular

    // calculate the SSS color
    float3 TH = normalize(L + N * surface.distortion);
    float intensity = pow(max(dot(V, -TH), 0), surface.power) * surface.scale;
    intensity += mainLight.shadowAttenuation * surface.thickness;
    float3 sssColor = brdfSurface.albedo * mainLight.color * intensity;

    // 2. additional lights
    for (int i = 0; i < GetAdditionalLightsCount(); i++)
    {
        Light additionalLight = GetAdditionalLight(i, brdfSurface.worldPos, TransformWorldToShadowCoord(brdfSurface.worldPos));
        float3 L = additionalLight.direction;
        float3 H = normalize(V + L);
        float3 radiance = additionalLight.color * additionalLight.shadowAttenuation * additionalLight.distanceAttenuation;
        // float3 radiance = additionalLight.color * additionalLight.distanceAttenuation;
        float NDF = NormalDistributionGGX(N, H, brdfSurface.roughness);
        float G = Geometry_Smith(N, V, L, brdfSurface.roughness);
        float3 F = FresnelSchlick(clamp(dot(H, V), 0 ,1), f0);

        float3 nomin = NDF * G * F;
        float denom = 4 * max(dot(N, V), 0) * max(dot(N, L), 0) + 1e-4f; // prevent divided by zero
        float3 specular = nomin / denom;
        float3 ks = F;
        float3 kd = float3(1, 1, 1) - ks;
        kd *= 1 - brdfSurface.metallic; // a linear blend for partly metal & metal have no diffuse reflection
        float NdotL = max(dot(N, L), 0);
        Lo += (kd * brdfSurface.albedo / PI + specular) * radiance * NdotL; // ks has been included in specular

        float3 TH = normalize(L + N * surface.distortion);
        float intensity = pow(max(dot(V, -TH), 0), surface.power) * surface.scale;
        intensity *= additionalLight.shadowAttenuation * additionalLight.distanceAttenuation * (1 - surface.thickness);
        float3 sssColor = brdfSurface.albedo * additionalLight.color * intensity;

        Lo += sssColor;
    }

    float3 ambient = float3(0.5, 0.5, 0.5) * brdfSurface.albedo * brdfSurface.ao;
    float3 ambSssColor = brdfSurface.albedo.rgb * ambient * intensity;
    ambient += ambSssColor;
    return float4(ambient + Lo, 1);
}

struct EmissionIBLSurface
{
    IBLSurface iblSurface;
    float3 emission;
};

float4 EmissionBRDF(EmissionIBLSurface eSurface)
{
    IBLSurface surface = eSurface.iblSurface;
    float3 N = normalize(surface.normalDir);
    float3 V = normalize(surface.viewDir);
    float3 f0 = float3(0.04, 0.04, 0.04);
    f0 = lerp(f0, surface.albedo, surface.metallic);
    float4 shadowCoord = TransformWorldToShadowCoord(surface.worldPos);

    Light mainLight = GetMainLight(shadowCoord);
    float3 L = mainLight.direction;
    float3 H = normalize(V + L);
    float3 Lo = float3(0, 0, 0);

    // 1. main light
    float3 radiance = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
    float NDF = NormalDistributionGGX(N, H, surface.roughness);
    float G = Geometry_Smith(N, V, L, surface.roughness);
    float3 F = FresnelSchlick(clamp(dot(H, V), 0, 1), f0);

    float3 nomin = NDF * G * F;
    float denom = 4 * max(dot(N, V), 0) * max(dot(N, L), 0) + 1e-4f; // prevent divided by zero
    float3 specular = nomin / denom;
    float3 ks = F;
    float3 kd = float3(1, 1, 1) - ks;
    kd *= 1 - surface.metallic; // a linear blend for partly metal & metal have no diffuse reflection
    float NdotL = max(dot(N, L), 0);
    Lo += (kd * surface.albedo / PI + specular) * radiance * NdotL; // ks has been included in specular

    // 2. additional lights
    for (int i = 0; i < GetAdditionalLightsCount(); i++)
    {
        Light additionalLight = GetAdditionalLight(i, surface.worldPos);
        float3 L = additionalLight.direction;
        float3 H = normalize(V + L);
        #ifdef SHADOW_RECEIVER
        float3 radiance = additionalLight.color * additionalLight.shadowAttenuation;
        #else
        float3 radiance = additionalLight.color * additionalLight.distanceAttenuation * additionalLight.shadowAttenuation;
        #endif
        float NDF = NormalDistributionGGX(N, H, surface.roughness);
        float G = Geometry_Smith(N, V, L, surface.roughness);
        float3 F = FresnelSchlick(clamp(dot(H, V), 0 ,1), f0);

        float3 nomin = NDF * G * F;
        float denom = 4 * max(dot(N, V), 0) * max(dot(N, L), 0) + 1e-4f; // prevent divided by zero
        float3 specular = nomin / denom;
        float3 ks = F;
        float3 kd = float3(1, 1, 1) - ks;
        kd *= 1 - surface.metallic; // a linear blend for partly metal & metal have no diffuse reflection
        float NdotL = max(dot(N, L), 0);
        Lo += (kd * surface.albedo / PI + specular) * radiance * NdotL; // ks has been included in specular
    }

    // 3. Lightmap
    float2 lightUV;
    OUTPUT_LIGHTMAP_UV(surface.lightUV, unity_LightmapST, lightUV);
    float3 gi = SAMPLE_GI(lightUV, 0, surface.realNormalDir);
    L = surface.realNormalDir; // the light direction is the reflection of view direction
    H = normalize(V + L);
    NDF = NormalDistributionGGX(N, H, surface.roughness);
    G = Geometry_Smith(N, V, L, surface.roughness);
    F = FresnelSchlick(clamp(dot(H, V), 0, 1), f0);
    nomin = NDF * G * F;
    denom = 4 * max(dot(N, V), 0) * max(dot(N, L), 0) + 1e-4f; // prevent divided by zero
    specular = nomin / denom;
    ks = F;
    kd = float3(1, 1, 1) - ks;
    kd *= 1 - surface.metallic; // a linear blend for partly metal & metal have no diffuse reflection
    NdotL = max(dot(N, L), 0);
    Lo += (kd * surface.albedo / PI + specular) * gi * NdotL; // ks has been included in specular
    
    float3 F2 = FresnelSchlickRoughness(max(dot(N, V), 0), f0, surface.roughness);
    float3 ks2 = F2;
    float3 kd2 = float3(1, 1, 1) - ks2;
    kd2 *= 1 - surface.metallic;
    float3 diffuse = surface.diffuse * surface.albedo;
    float NdotV = max(dot(N, V), 0);
    float2 envBRDF = SAMPLE_TEXTURE2D(_GlobalIntegrateMap, sampler_GlobalIntegrateMap, float2(NdotV, surface.roughness)).rg;
    float3 specularInIBL = surface.specular * (F2 * envBRDF.x + envBRDF.y);
    float3 ambient = (kd2 * diffuse + specularInIBL) * surface.ao;
    // float3 ambient = float3(1, 1, 1) * surface.albedo * surface.ao;
    float3 color = ambient + Lo;
    color.rgb += eSurface.emission;
    color = MixFogColor(color, unity_FogColor, unity_FogParams.x);
    return float4(color, 1);
}