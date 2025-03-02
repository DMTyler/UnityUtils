#ifndef DG_RANDOM
#define DG_RANDOM

inline float random(float min, float max, float seed)
{
    return lerp(min, max, frac(sin(seed) * 43758.5453));
}

inline float random(float min, float max, float2 seed)
{
    float dotProduct = dot(seed, float2(12.9898, 78.233));
    return lerp(min, max, frac(sin(dotProduct) * 43758.5453));
}

inline float random(float min, float max, float3 seed)
{
    float dotProduct = dot(seed, float3(12.9898, 78.233, 45.543));
    return lerp(min, max, frac(sin(dotProduct) * 43758.5453));
}
#endif