#ifndef DG_BXGF
#define DG_BXGF
#include "math.hlsl"
#include "fresnel.hlsl"

#define DG_BxDF_Lambertian 1 // 1
#define DG_BxDF_OrenNayar 2  // 1 << 1
#define DG_BxDF_MicrofacetReflection 4 // 1 << 2
#define DG_BxDF_MicrofacetTransmission 8 // 1 << 3

/// \brief Anisotropic Trowbridge-Reitz (GGX) normal distribution function 双轴粗糙度 Trowbridge-Reitz 分布函数
/// \param wh The half vector in tangent space
/// \param alphax The roughness in x direction
/// \param alphay The roughness in y direction
float TrowbridgeReitzD(float3 wh, float alphax, float alphay)
{
    float tan2Theta = Tan2Theta(wh);
    const float cos2Theta = Cos2Theta(wh);
    const float cos4Theta = cos2Theta * cos2Theta;
    float e =
        (Cos2Phi(wh) / (alphax * alphax) + Sin2Phi(wh) / (alphay * alphay)) * tan2Theta;
    return 1 / (PI * alphax * alphay * cos4Theta * (1 + e) * (1 + e));
}

/// \brief Microfacet function on one direction 单方向遮挡项
/// \param w The direction in tangent space
/// \param alphax The roughness in x direction
/// \param alphay The roughness in y direction
float TrowbridgeReitzLambda(float3 w, float alphax, float alphay)
{
    float absTanTheta = abs(TanTheta(w));
    float alpha =
        sqrt(Cos2Phi(w) * alphax * alphax + Sin2Phi(w) * alphay * alphay);
    float alpha2Tan2Theta = (alpha * absTanTheta) * (alpha * absTanTheta);
    return (sqrt(1.0 + alpha2Tan2Theta) - 1) * 0.5;
}

/// \brief Trowbridge-Reitz Microfacet model 几何遮挡模型
/// \param wo The outgoing direction in tangent space
/// \param wi The incoming direction in tangent space
/// \param alphax The roughness in x direction
/// \param alphay The roughness in y direction
float MicrofacetG(float3 wi, float3 wo, float alphax, float alphay)
{
    float3 wh = normalize(wi + wo);
    if (dot(wo, wh) / CosTheta(wo) <= 0)
        return 0;
    return 1.f / (1 + TrowbridgeReitzLambda(wo, alphax, alphay) + TrowbridgeReitzLambda(wi, alphax, alphay));
}

struct BxDFLambertian
{
    FresnelData fresnel;
    float3 R; // The reflectance (albedo)
    
    /// \brief Lambertian Diffuse BRDF 兰伯特漫反射模型
    /// \param wi The incident direction in tangent space
    /// \param wo The outgoing direction in tangent space
    float3 F(float3 wi, float3 wo, out float pdf)
    {
        float3 wh = normalize(wi + wo);
        pdf = SameHemisphere(wi, wo)? AbsCosTheta(wo) * INV_PI : 0;
        return wo.z == 0 ? 0 : (1.f - fresnel.Evaluate(dot(wi, wh))) * R * INV_PI * saturate(CosTheta(wi));
    }

};

// The oren-nayar diffuse BRDF
struct BxDFOrenNayar
{
    FresnelData fresnel;
    float alphax;
    float alphay;
    float3 R;

    float3 F(float3 wi, float3 wo, out float pdf)
    {
        pdf = 0;
        float sigma = sqrt((alphax * alphax + alphay * alphay) * 0.5f);
        float sigma2 = sigma * sigma;
        
        float A = 1 - 0.5f * sigma2 / (sigma2 + 0.33f);
        float B = 0.45f * sigma2 / (sigma2 + 0.09f);
        float3 wh = normalize(wi + wo);

        float sinAlpha, tanBeta;
        if (AbsCosTheta(wi) > AbsCosTheta(wo)) // if theta_i > theta_o
            {
            sinAlpha = SinTheta(wo);
            tanBeta = TanTheta(wi);
            }
        else
        {
            sinAlpha = SinTheta(wi);
            tanBeta = TanTheta(wo);
        }

        float maxCos = 0;
        if (SinTheta((wi)) > 1e-4 && SinTheta(wo) > 1e-4)
        {
            float sinPhiI = SinPhi(wi), cosPhiI = CosPhi(wi);
            float sinPhiO = SinPhi(wo), cosPhiO = CosPhi(wo);
            float dCos = cosPhiI * cosPhiO + sinPhiI * sinPhiO;
            maxCos = max(0.0f, dCos);
        }

        float factor = A + B * maxCos * sinAlpha * tanBeta;
        // integral of oren-nayar on the hemisphere is R
        pdf = factor * INV_PI;
        return fresnel.Evaluate(dot(wi, wh)) * R * pdf;
    }
};

// The microfacet reflection BRDF
struct BxDFMicrofacetReflection
{
    FresnelData fresnel;
    float alphax;
    float alphay;
    float3 R; // the reflectance

    float3 F(float3 wi, float3 wo, out float pdf)
    {
        pdf = 0;
        float3 wh = wi + wo;
        
        float cosThetaO = CosTheta(wo);
        float cosThetaI = CosTheta(wi);
        if (cosThetaI <= 0 || cosThetaO <= 0) return float3(0, 0, 0);
        
        // Handle degenerate cases for microfacet reflection
        if (cosThetaI == 0 || cosThetaO == 0)
            return float3(0, 0, 0);
        if (wh.x == 0 && wh.y == 0 && wh.z == 0)
            return float3(0, 0, 0);
        
        wh = normalize(wh);
        wh = Faceforward(wh, float3(0, 0, 1));

        float3 F = fresnel.Evaluate(abs(dot(wi, wh)));

        float D = TrowbridgeReitzD(wh, alphax, alphay);
        float G = MicrofacetG(wi, wo, alphax, alphay);
        pdf = Pdf_Wh(D, wh) * 0.25 / (dot(wo, wh));
        
        return F * D * G * 0.25 /
            (cosThetaI * cosThetaO);
    }
};

// The microfacet transmission BTDF
struct BxDFMicrofacetTransmission
{
    FresnelData fresnel;
    float alphax;   // The roughness in x direction
    float alphay;   // The roughness in y direction
    float etaI;     // The refractive index of the medium A
    float etaO;     // The refractive index of the medium B
    float3 T;       // The transmittance

    /// \brief Microfacet transmission BTDF
    /// https://www.cs.cornell.edu/~srm/publications/EGSR07-btdf.pdf
    /// Formula (21)
    float3 F(float3 wi, float3 wo, out float pdf)
    {
        pdf = 0;

        if (SameHemisphere(wo, wi)) 
            return 0;

        float cosThetaO = CosTheta(wo);
        float cosThetaI = CosTheta(wi);
        if (cosThetaI == 0 || cosThetaO == 0) 
            return 0;

        float eta = cosThetaO > 0 ? (etaO / etaI) : (etaI / etaO);
        
        // By Snell's law
        float3 wh = normalize(wo + wi * eta);
        wh = wh.z < 0 ? -wh : wh;
        
        float3 F = fresnel.Evaluate(dot(wo, wh));
        float D = TrowbridgeReitzD(wh, alphax, alphay);
        float G = MicrofacetG(wi, wo, alphax, alphay);
        float sqrtDenom = etaI * dot(wi, wh) + etaO * dot(wo, wh);
        
        float dwh_dwi = abs((eta * eta * dot(wi, wh)) / (sqrtDenom * sqrtDenom));
        pdf = Pdf_Wh(D, wh) * dwh_dwi;

        return abs(dot(wi, wh)) * abs(dot(wo, wh)) * etaO * etaO * (1 - F) * G * D / (abs(cosThetaI) * abs(cosThetaO) * sqrtDenom * sqrtDenom);
    }
};

#endif