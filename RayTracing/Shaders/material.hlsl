#ifndef DG_MATERIAL
#define DG_MATERIAL

#include "structs.hlsl"
#include "fresnel.hlsl"
#include "bxdf.hlsl"

void UnpackFresnel(GPUMaterial gpuMat, out FresnelData fresnelData)
{
    fresnelData.F0 = float3(0.04, 0.04, 0.04);
    fresnelData.etaI = gpuMat.etaI;
    fresnelData.etaT = gpuMat.etaT;
    fresnelData.K = gpuMat.K;
    fresnelData.fresnelType = gpuMat.fresnelType;
}

float3 ComputeBxDFMicrofacetReflection(GPUMaterial gpuMat, float3 wi, float3 wo)
{
    BxDFMicrofacetReflection bxdf;
    bxdf.R = gpuMat.reflectance;
    bxdf.alphax = gpuMat.roughness;
    bxdf.alphay = gpuMat.roughnessV;
    UnpackFresnel(gpuMat, bxdf.fresnel);
    float pdf;
    return bxdf.F(wi, wo, pdf);
}

float3 ComputeBxDFMicrofacetTransmission(GPUMaterial gpuMat, float3 wi, float3 wo)
{
    BxDFMicrofacetTransmission bxdf;
    bxdf.T = gpuMat.transmittance;
    bxdf.alphax = gpuMat.roughness;
    bxdf.alphay = gpuMat.roughnessV;
    bxdf.etaI = 1;
    bxdf.etaO = 1.5;
    UnpackFresnel(gpuMat, bxdf.fresnel);
    float pdf;
    return bxdf.F(wi, wo, pdf);
}

float3 ComputeLambertianDiffuse(GPUMaterial gpuMat, float3 wi, float3 wo)
{
    BxDFLambertian bxdf;
    bxdf.R = gpuMat.reflectance;
    UnpackFresnel(gpuMat, bxdf.fresnel);
    float pdf;
    return bxdf.F(wi, wo, pdf);
}

#endif