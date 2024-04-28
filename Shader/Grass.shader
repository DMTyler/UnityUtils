Shader "Custom/Grass"
{
    Properties
    {
        [HDR] _BottomColour("BottomColour", Color) = (0, 0, 0, 1)
        [HDR] _TopColour("TopColour", Color) = (1, 1, 1, 1)
        _BendRotationRandom("Bend Rotation Random", Range(-1, 1)) = 0.15
        
        _BladeWidth("Blade Width", Float) = 0.05
        _BladeWidthRandom("Blade Width Random", Float) = 0.02
        _BladeHeight("Blade Height", Float) = 0.5
        _BladeHeightRandom("Blade Height Random", Float) = 0.3
        
        _Tess("Tessellation", Range(1, 64)) = 20 // 细分数
        _MaxTessDistance("Max Tess Distance", Range(1, 32)) = 20 // 最大细分距离
        _MinTessDistance("Min Tess Distance", Range(1, 32)) = 1 // 最小细分距离
        
        _WindDistortionMap("Wind Disortion Map", 2D) = "white" {}
        _WindFrequency("Wind Frequency", Vector) = (0.05, 0.05, 0, 0)
        _WindStrength("Wind Strength", Range(-0.8, 0.8)) = 0.5
        
        _BladeForward("Blade Forward Amount", Float) = 0.38
        _BladeCurve("Blade Curvature Amount", Range(1, 4)) = 2
        
        _Shininess("Shininess", Range(0, 1)) = 0.5
        
        _ReactRadius("React Radius", Range(0, 2)) = 0.5
    }
    SubShader
    {
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex vert
            #pragma hull hull
            #pragma domain domain
            #pragma geometry geom
            #pragma fragment frag
            
            // 接收阴影 URP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN      // URP 主光阴影、联机阴影、屏幕空间阴影
            #pragma multi_compile_fragment _ _SHADOWS_SOFT      // URP 软阴影
           
            #include "Assets/06_Scripts/DmUtils/Shader/Utils.hlsl"

            float4 _BottomColour;
            float4 _TopColour;
            float _BendRotationRandom;

            float _BladeHeight;
            float _BladeHeightRandom;	
            float _BladeWidth;
            float _BladeWidthRandom;
            float _MaxTessDistance;
            float _MinTessDistance;
            float _Tess;

            sampler2D _WindDistortionMap;
            float4 _WindDistortionMap_ST;
            float2 _WindFrequency;
            float _WindStrength;

            float _BladeForward;
            float _BladeCurve;

            float _Shininess;

            float3 _ReactObject0;
            float3 _ReactObject1;
            float3 _ReactObject2;
            float3 _ReactObject3;
            float3 _ReactObject4;
            float3 _ReactObject5;
            float3 _ReactObject6;
            float3 _ReactObject7;
            float3 _ReactObject8;
            float3 _ReactObject9;
            float _ReactRadius;

            struct vertIn
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct vertOut
            {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct controlPoint
            {
                float4 vertex : INTERNALTESSPOS;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct tessellationFactors
            {
                float edge[3] : SV_TessFactor;
                float inside : SV_InsideTessFactor;
            };

            struct geomOut
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : NORMAL;
                float2 uv : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
            };

            controlPoint vert(vertIn i)
            {
                controlPoint o;
                o.vertex = i.vertex;
                o.normal = i.normal;
                o.tangent = i.tangent;
                return o;
            }

            vertOut afterVert(vertIn i)
            {
                vertOut o;
                o.vertex = i.vertex;
                o.normal = i.normal;
                o.tangent = i.tangent;
                return o;
            }

            [domain("tri")] // patch类型：tri(三角形)、quad（四边形）、isoline（线段，苹果的metal api不支持）
            [outputcontrolpoints(3)] // 输出控制点的数量
            [outputtopology("triangle_cw")] // 输出拓扑结构。triangle_cw（顺时针环绕三角形）、triangle_ccw（逆时针环绕三角形）、line（线段）。
            [partitioning("integer")]
            [patchconstantfunc("patchConstantFunction")]
            controlPoint hull(InputPatch<controlPoint, 3> patch, uint id : SV_OutputControlPointID)
            {
                return patch[id];
            }

            // 随着距相机的距离减少细分数
            inline float CalcDistanceTessFactor(float4 vertex, float minDist, float maxDist, float tess)
            {
                float3 worldPosition = TransformObjectToWorld(vertex.xyz);
                float dist = distance(worldPosition,  GetCameraPositionWS());
                float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
                return (f);
            }

            tessellationFactors patchConstantFunction(InputPatch<controlPoint, 3> patch)
            {
                tessellationFactors f;

                float min = _MinTessDistance;
                float max = _MaxTessDistance;
                float edge0 = CalcDistanceTessFactor(patch[0].vertex, min, max, _Tess);
                float edge1 = CalcDistanceTessFactor(patch[1].vertex, min, max, _Tess);
                float edge2 = CalcDistanceTessFactor(patch[2].vertex, min, max, _Tess);
                
                // 求细分段数平均来保证不同距离下的细分数没有隔阂
                f.edge[0] = (edge1 + edge2) / 2;
                f.edge[1] = (edge2 + edge0) / 2;
                f.edge[2] = (edge0 + edge1) / 2;
                f.inside = (edge0 + edge1 + edge2) / 3;
	            return f;
            }

            [domain("tri")]
            vertOut domain(tessellationFactors factors, OutputPatch<controlPoint, 3> patch, float3 barycentricCoordinates : SV_DomainLocation)
            {
                vertIn v;

                //为了找到该顶点的位置，我们必须使用重心坐标在原始三角形范围内进行插值。
                //X，Y和Z坐标确定第一，第二和第三控制点的权重。
                //以相同的方式插值所有顶点数据。让我们为此定义一个方便的宏，该宏可用于所有矢量大小。
                #define DomainInterpolate(fieldName) v.fieldName = \
                patch[0].fieldName * barycentricCoordinates.x + \
                patch[1].fieldName * barycentricCoordinates.y + \
                patch[2].fieldName * barycentricCoordinates.z
                
                DomainInterpolate(vertex);
                DomainInterpolate(normal);
                DomainInterpolate(tangent);

                return afterVert(v);
                
            }

            inline float3 Offset(float3 origin, float3x3 rotaiton, float3x3 tbn)
            {
                return mul(rotaiton, mul(origin, tbn));
            }

            inline float3 ReactOffset(float3 arg)
            {
                return float3(arg.x, 0, arg.z);
            }

            inline geomOut GetGeometryOutput(float3 pos, float3 origin, float2 uv, float3 reactUV, float3x3 rotationMatrix, float3x3 tbn_OS)
            {
                geomOut o;
                float3 offset = Offset(origin, rotationMatrix, tbn_OS);
                o.positionCS = TransformObjectToHClip(pos + offset + ReactOffset(reactUV));
                o.uv = uv;
                float3 positionWS = TransformObjectToWorld(pos + offset);
                o.shadowCoord = TransformWorldToShadowCoord(positionWS);
                o.normalWS = TransformObjectToWorld(mul(rotationMatrix, mul(float3(0, 0, 1), tbn_OS)));
                o.positionWS = positionWS;
                return o;
            }

            #define BLADE_SEGMENT 3
            [maxvertexcount(BLADE_SEGMENT * 2 + 1)]
            void geom(triangle vertOut IN[3]: SV_POSITION, inout TriangleStream<geomOut> stream)
            {
                float3 pos = IN[0].vertex;
                float3 vNormal = IN[0].normal;
                float4 vTangent = IN[0].tangent;
                float3 vBinormal = cross(vNormal, vTangent) * vTangent.w;
                // tbn用于将模型空间转换到切线空间
                float3x3 tbn_OS = float3x3(vTangent.xyz, vBinormal, vNormal);

                // 采样扰动贴图，随时间偏移
                float2 uv = pos.xz * _WindDistortionMap_ST.xy + _WindDistortionMap_ST.zw + _WindFrequency * _Time.y;
                float2 texColor = tex2Dlod(_WindDistortionMap, float4(uv, 0, 0)).xy;
                float2 windSample = float2(saturate(texColor.x) * 2 - 1, saturate(texColor.y * 2) - 1) * _WindStrength;
                float3 wind = float3(windSample.x, 0, windSample.y);
                
                // 计算旋转矩阵
                // 切线空间中的Z方向是模型空间里的法线方向
                // float3x3 rotationZMatrix = RotationZ(rand(pos) * 2 * PI); // 朝向旋转
                // float3x3 rotationXMatrix = RotationX(-rand(pos.zxy) * 0.5 * PI * _BendRotationRandom); // 随风飘摇

                // float3x3 rotationMatrix = mul(rotationXMatrix, rotationZMatrix); // 先转z轴再转x轴让大家旋转的方向一致

                // 风动矩阵
                // float3x3 windMatrix = RotationEulerAngle(wind * PI);

                float3 rotationEuler = float3(-rand(pos.zxy) * HALF_PI * _BendRotationRandom, 0, rand(pos) * TWO_PI);
                float3x3 rotationMatrix = RotationQuaternion(rotationEuler);

                float3 windRotationEuler = rotationEuler + wind * PI;
                float3x3 windMatrix = RotationQuaternion(windRotationEuler);

                // 计算宽度及高度
                float height = (rand(pos.zyx) * 2 - 1) * _BladeHeightRandom + _BladeHeight;
                float width = ((rand(pos.xzy) * 2 - 1) * _BladeWidthRandom + _BladeWidth) / 2;
                float forward = rand(pos.zxy) * _BladeForward / 10;
                float3 worldPos = TransformObjectToWorld(pos);
                 // 计算偏移量
                // float3 offset1 = Offset(float3(width, 0, 0), rotationMatrix, tbn_OS);
                // float3 offset2 = Offset(float3(-width, 0, 0), rotationMatrix, tbn_OS);
                // float3 offset3 = Offset(float3(0, 0, height), mul(windMatrix, rotationMatrix), tbn_OS); // 对顶点应用风动
                float3 totalReact = float3(0, 0, 0);

                #define GetReact(i) totalReact += distance(_ReactObject##i, worldPos) * normalize(worldPos - _ReactObject##i) * saturate(1 - distance(_ReactObject##i, worldPos) / _ReactRadius) * 0.2;
                
                GetReact(0)
                GetReact(1)
                GetReact(2)
                GetReact(3)
                GetReact(4)
                GetReact(5)
                GetReact(6)
                GetReact(7)
                GetReact(8)
                GetReact(9)
                

                // 添加顶点
                for (int i = 0; i < BLADE_SEGMENT; i++)
                {
                    float t = i / (float)BLADE_SEGMENT;
                    
                    float segmentHeight = height * t;
                    float segmentWidth = width * (1 - t);
                    float segmentForward = pow(t, _BladeCurve) * forward; // 曲线
                    float3 segmentReact = pow(t, 2) * totalReact;
                    float3x3 transformMatrix = i == 0? rotationMatrix : windMatrix; // 是否应用风动
                    stream.Append(GetGeometryOutput(pos, float3(segmentWidth, segmentForward, segmentHeight), float2(0, t), segmentReact, transformMatrix, tbn_OS));
                    stream.Append(GetGeometryOutput(pos, float3(-segmentWidth, segmentForward, segmentHeight), float2(1, t), segmentReact, transformMatrix, tbn_OS));
                }
                
                // 沿切线方向偏移
                // stream.Append(GetGeometryOutput(pos, float3(width, 0, 0), float2(0, 0), rotationMatrix, tbn_OS));
                // stream.Append(GetGeometryOutput(pos, float3(-width, 0, 0), float2(1, 0), rotationMatrix, tbn_OS));
                stream.Append(GetGeometryOutput(pos, float3(0, forward, height), float2(0.5, 1), totalReact, windMatrix, tbn_OS)); // 对顶点应用风动
            }

            float4 frag(geomOut i) : SV_Target
            {
                float4 c = lerp(_BottomColour, _TopColour, i.uv.y);
                Light light = GetMainLight(i.shadowCoord);
                return HalfLambert(i.normalWS, c * (light.shadowAttenuation * 0.5 + 0.5));
            }
            ENDHLSL
        }
    }
    FallBack "Packages/com.unity.render-pipelines.universal/FallbackError" 
}
