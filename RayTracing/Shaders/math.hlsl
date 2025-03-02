#ifndef DG_MATH
#define  DG_MATH

#ifndef PI
#define PI 3.14159265358979323846
#endif

#ifndef INV_PI
#define INV_PI 0.31830988618379067154
#endif

/// \brief The reflection under tangent space
inline float3 Reflect(float3 wo)
{
    return float3(wo.x, wo.y, -wo.z);
}

/// \param w The tagent space(z-up) vector 
inline float AbsCosTheta(float3 w)
{
    return abs(w.z);
}

/// \brief The cosine of the angle between the normal and the vector
/// \param w The tangent space(z-up) vector
inline float CosTheta(float3 w)
{
    return w.z;
}

/// \brief The cosine square of the angle between the normal and the vector
/// \param w The tangent space(z-up) vector
inline float Cos2Theta(float3 w)
{
    return w.z * w.z;
}

/// \brief The sine square of the angle between the normal and the vector
/// \param w The tangent space(z-up) vector
inline float Sin2Theta(float3 w)
{
    return max(0, 1.0 - Cos2Theta(w));
}

/// \brief The sine of the angle between the normal and the vector
/// \param w The tangent space(z-up) vector
inline float SinTheta(float3 w)
{
    return sqrt(Sin2Theta(w));
}

/// \brief The tangent of the angle between the normal and the vector
/// \param w The tangent space(z-up) vector
inline float TanTheta(float3 w)
{
    return SinTheta(w) / CosTheta(w);
}

/// \brief The tangent square of the angle between the normal and the vector
/// \param w The tangent space(z-up) vector
inline float Tan2Theta(float3 w)
{
    return Sin2Theta(w) / Cos2Theta(w);
}

/// \brief The cosine of the azimuthal angle of the vector 切线空间（Tangent Space）中的方位角（Azimuth Angle）的 cosine 值
/// \param w The tangent space(z-up) vector
inline float CosPhi(float3 w)
{
    float sinTheta = SinTheta(w);
    return sinTheta == 0 ? 1 : clamp(w.x / sinTheta, -1, 1);
}

/// \brief The sine of the azimuthal angle of the vector 切线空间（Tangent Space）中的方位角（Azimuth Angle）的 sine 值
/// \param w The tangent space(z-up) vector
inline float SinPhi(float3 w)
{
    float sinTheta = SinTheta(w);
    return sinTheta == 0 ? 0 : clamp(w.y / sinTheta, -1, 1);
}

/// \brief The cosine square of the azimuthal angle of the vector 切线空间（Tangent Space）中的方位角（Azimuth Angle）的 cosine 平方值
/// \param w The tangent space(z-up) vector
inline float Cos2Phi(float3 w)
{
    const float cosPhi = CosPhi(w);
    return cosPhi * cosPhi;
}

/// \brief The sine square of the azimuthal angle of the vector 切线空间（Tangent Space）中的方位角（Azimuth Angle）的 sine 平方值
/// \param w The tangent space(z-up) vector
inline float Sin2Phi(float3 w)
{
    const float sinPhi = SinPhi(w);
    return sinPhi * sinPhi;
}

/// \brief Whether two vectors are in the same hemisphere
/// \param w The tangent space(z-up) vector
/// \param wp The tangent space(z-up) vector
inline bool SameHemisphere(float3 w, float3 wp)
{
    return w.z * wp.z > 0;
}

/// \brief Geometric correction of the normal distribution (to get the pdf)
/// \param D The normal distribution value
/// \param wh The half vector in tangent space
inline float Pdf_Wh(float D, float3 wh)
{
    return D * AbsCosTheta(wh);
}

/// \brief Set the direction of w into the same hemishpere of v
/// \param w The tangent space(z-up) vector to be set
/// \param v The target direction
inline float3 Faceforward(float3 w, float3 v)
{
    return (dot(w, v) < 0.0f) ? -w : w;
}

/// \brief Transform a direction from world space into tangent space
inline float3 TransformWorldToTangent(float3 worldDir, float3 normalWS, float3 tangentWS, float3 bitangentWS)
{
    return float3(
        dot(worldDir, tangentWS),
        dot(worldDir, bitangentWS),
        dot(worldDir, normalWS)
    );
}

/// \brief Transform a direction from object space into tangent space
inline float3 TransformObjectToTangent(float3 objectDir, float3 normalOS, float3 tangentOS, float3 bitangentOS)
{
    return mul(objectDir, float3x3(tangentOS, bitangentOS, normalOS));
}

/// \brief Transform a direction from tangent space into world space
inline float3 TransformTangentToWorld(float3 tangentDir, float3 normalWS, float3 tangentWS, float3 bitangentWS)
{
    return tangentDir.x * tangentWS + tangentDir.y * bitangentWS + tangentDir.z * normalWS;
}

/// \brief Transform a direction from tangent space into object space
inline float3 TransformTangentToObject(float3 tangentDir, float3 normalOS, float3 tangentOS, float3 bitangentOS)
{
    return tangentDir.x * tangentOS + tangentDir.y * bitangentOS + tangentDir.z * normalOS;
}

/// \brief The reflection under tangent space
inline bool Refract(float3 wi, float eta, out float3 wt)
{
    wi = normalize(wi);
    float CosThetaI = CosTheta(wi);
    float sin2ThetaI = max(0, 1 - CosThetaI * CosThetaI);
    float sin2ThetaT = eta * eta * sin2ThetaI;
    if (sin2ThetaT >= 1)
    {
        wt = float3(0, 0, 0);
        return false;
    }
    float cosThetaT = sqrt(max(0, 1 - sin2ThetaT));
    wt = eta * -wi + (eta * CosThetaI - cosThetaT) * float3(0, 0, 1) ; // (eta * CosThetaI - cosThetaT) * float3(0, 0, 1)
    return true;
}
#endif