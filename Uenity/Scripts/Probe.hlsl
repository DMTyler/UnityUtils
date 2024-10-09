float3 GetCubeDirection(float2 uv, int face)
{
    float u = uv.x * 2 - 1;
    float v = uv.y * 2 - 1;
    float3 dir;

    switch (face)
    {
        case 0: dir = float3(1, -v, -u); break;
        case 1: dir = float3(-1, -v, u); break;
        case 2: dir = float3(u, 1, v); break;
        case 3: dir = float3(u, -1, -v); break;
        case 4: dir = float3(u, -v, 1); break;
        case 5: dir = float3(-u, -v, -1); break;
        default: dir = float3(0, 0, 0); break;
    }
    
    return normalize(dir);
}

int GetFace(float3 dir)
{
    dir = normalize(dir);

    float absX = abs(dir.x);
    float absY = abs(dir.y);
    float absZ = abs(dir.z);

    if (absX > absY && absX > absZ)
    {
        if (dir.x > 0)
            return 0;
        else
            return 1;
    }
    else if (absY > absX && absY > absZ)
    {
        if (dir.y > 0)
            return 2;
        else
            return 3;
    }
    else
    {
        if (dir.z > 0)
            return 4;
        else
            return 5;
    }
}

float2 GetUV(float3 dir)
{
    float absX = abs(dir.x);
    float absY = abs(dir.y);
    float absZ = abs(dir.z);
    float2 uv;
    int face = GetFace(dir);

    switch (face)
    {
        case 0: uv = float2(-dir.z, -dir.y) / absX; break;
        case 1: uv = float2(dir.z, -dir.y) / absX; break;
        case 2: uv = float2(dir.x, dir.z) / absY; break;
        case 3: uv = float2(dir.x, -dir.z) / absY; break;
        case 4: uv = float2(dir.x, -dir.y) / absZ; break;
        case 5: uv = float2(-dir.x, -dir.y) / absZ; break;
        default: uv = float2(0, 0); break;
    }

    uv = 0.5f * (uv + 1);

    return uv;
    
}