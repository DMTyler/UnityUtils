#ifndef DG_FRESNEL
#define DG_FRESNEL

#define DG_FresnelDielectric 0
#define DG_FresnelConductor 1
#define DG_FresnelSchlick 2
#define DG_FresnelConst 3

inline float _schlickWeight(float cosThetaI) {
    float m = clamp(1 - cosThetaI, 0, 1);
    return (m * m) * (m * m) * m;
}

/// \brief Schlick's approximation of Fresnel reflectance with adjustable power (changing the power may result to a EXTREMELLY
/// physically inaccurate results, do this only when necessary)
/// \param F0 The reflectance at normal incidence. For most of object, F0 can be set to (0.04, 0.04, 0.04)
/// \param cosThetaI The cosine of the angle between the half vector and the incident direction
/// \param power The power in Schlick's approximation equation. The smaller the value, the more pronounced the Fresnel effect. When the power is set to 5, it can approximate physical accuracy
inline float3 FrSchlickPower(float3 F0, float cosThetaI, float power)
{
    float m = clamp(1 - cosThetaI, 0, 1);
    return lerp(F0, float3(1, 1, 1), pow(m, power));
}

/// \brief Schlick's approximation of Fresnel reflectance
/// \param F0 The reflectance at normal incidence. For most of object, F0 can be set to (0.04, 0.04, 0.04)
/// \param cosThetaI The cosine of the angle between the half vector and the incident direction
inline float3 FrSchlick(float3 F0, float cosThetaI)
{
    return lerp(F0, float3(1, 1, 1), _schlickWeight(cosThetaI));
}

/// \brief The Fresnel reflectance for dielectric materials
/// \param cosThetaI The cosine of the angle between the normal and the incident direction
/// \param eta The ratio of the refractive indices
inline float3 FrDielectric(float cosThetaI, float3 eta)
{
    cosThetaI = clamp(cosThetaI, -1, 1);
    if (cosThetaI < 0)
    {
        cosThetaI = -cosThetaI;
        eta = 1 / eta;
    }
    float sinThetaTSq = eta * eta * (1 - cosThetaI * cosThetaI);
    if (sinThetaTSq > 1.f)
    {
        return 1.0f;
    }
    float cosThetaT = sqrt(max(1.f - sinThetaTSq, 0.f));
    
    //  S-polarization
    float Rs = (eta * cosThetaI - cosThetaT) / (eta * cosThetaI + cosThetaT);
    //  P-polarization
    float Rp = (cosThetaI - eta * cosThetaT) / (cosThetaI + eta * cosThetaT);

    return (Rs * Rs + Rp * Rp) * 0.5f;
}

/// \brief The Fresnel reflectance for conductors (metals)
/// https://seblagarde.wordpress.com/2013/04/29/memo-on-fresnel-equations/
/// \param cosThetaI The cosine of the angle between the normal and the incident direction
/// \param etaI The refractive index of the conductor
/// \param etaT The refractive index of the medium
/// \param K The extinction coefficient
inline float3 FrConductor(float cosThetaI, float3 etaI, float3 etaT, float3 K)
{
    cosThetaI = clamp(cosThetaI, -1, 1);
    float3 Rp;
    float3 Rs;

    float3 eta = etaT / etaI;
    float3 etaK = K / etaI;
    float3 eta2 = eta * eta;
    float3 etaK2 = etaK * etaK;

    float cosThetaI2 = cosThetaI * cosThetaI;
    float sinThetaI2 = 1.f - cosThetaI2;

    float3 t0 = eta2 - etaK2 - sinThetaI2;
    float3 a2plusb2 = sqrt(t0 * t0 + 4 * eta2 * etaK2);
    float3 t1 = a2plusb2 + cosThetaI2;
    float3 a = sqrt(0.5f * (a2plusb2 + t0));
    float3 t2 = 2 * cosThetaI * a;
    
    Rs = (t1 - t2) / (t1 + t2);

    float3 t3 = cosThetaI2 * a2plusb2 + sinThetaI2 * sinThetaI2;
    float3 t4 = t2 * sinThetaI2;

    Rp = Rs * (t3 - t4) / (t3 + t4);

    return (Rp + Rs) * 0.5f;
}

struct FresnelData
{
    int fresnelType;
    float3 etaI;
    float3 etaT; 
    float3 K; // The extinction coefficient
    float3 F0; // The reflectance at normal incidence

    float3 Evaluate(float cosThetaI)
    {
        if (fresnelType == DG_FresnelSchlick)
        {
            return FrSchlick(F0, cosThetaI);
        }
        else if (fresnelType == DG_FresnelDielectric)
        {
            return FrDielectric(cosThetaI, etaI/etaT);
        }
        else if (fresnelType == DG_FresnelConductor)
        {
            return FrConductor(cosThetaI, etaI, etaT, K);
        }
        else
        {
            return 1;
        }
    }
};


#endif