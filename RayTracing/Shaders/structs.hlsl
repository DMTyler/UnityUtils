#ifndef DG_STRUCTS
#define DG_STRUCTS

struct GPUMaterial
{
    float3 reflectance; // albedo
    float3 transmittance;
    float3 specular;
    float3 normal;

    float3 K; // metal material absorption 

    float roughness; // The roughness in x direction
    float roughnessV; // The roughness in y direction

    float etaT;
    float etaI;

    int fresnelType;
    int materialType;
};

#endif