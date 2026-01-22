Shader "Custom/toonURP"
{
   Properties
    {
        [MainTexture]_BaseMap("Base Map", 2D) = "white" {}
        [MainColor]_BaseColor("Base Color", Color) = (1,1,1,1)

        _ShadeColor("Shade Color", Color) = (0.6,0.6,0.6,1)
        _ShadowSteps("Shadow Steps", Range(1,5)) = 2
        _ShadowFeather("Shadow Feather", Range(0.0,0.2)) = 0.03
        _ShadowOffset("Shadow Offset", Range(-1,1)) = 0

        _RimColor("Rim Color", Color) = (1,1,1,1)
        _RimPower("Rim Power", Range(0.5,8)) = 3
        _RimIntensity("Rim Intensity", Range(0,2)) = 0.6

        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth("Outline Width", Range(0,0.02)) = 0.004
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "ForwardLitToon"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadeColor;
                float4 _RimColor;
                float4 _OutlineColor;

                float _ShadowSteps;
                float _ShadowFeather;
                float _ShadowOffset;

                float _RimPower;
                float _RimIntensity;

                float _OutlineWidth;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            float ToonStep(float ndl, float steps, float feather)
            {
                // ndl: 0..1
                steps = max(1.0, steps);
                float t = ndl * steps;
                float baseStep = floor(t) / steps;
                float nextStep = ceil(t) / steps;

                // feathered edge around transition
                float edge = frac(t);
                float w = max(1e-5, feather * steps);
                float s = smoothstep(0.5 - w, 0.5 + w, edge);

                return lerp(baseStep, nextStep, s);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrmInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = NormalizeNormalPerVertex(nrmInputs.normalWS);
                OUT.uv          = IN.uv;

                OUT.shadowCoord = GetShadowCoord(posInputs);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // Base
                float4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float3 albedo = tex.rgb * _BaseColor.rgb;

                // Main Light
                Light mainLight = GetMainLight(IN.shadowCoord);
                float3 L = normalize(mainLight.direction);

                // NdotL mapped to 0..1
                float ndl = saturate(dot(N, L) * 0.5 + 0.5);
                ndl = saturate(ndl + _ShadowOffset);

                // Shadow attenuation (URP shadow maps)
                float shadowAtten = mainLight.shadowAttenuation;

                // Toon shade factor
                float toon = ToonStep(ndl, _ShadowSteps, _ShadowFeather);

                // Combine: if in shadow, push towards shade
                float litFactor = toon * shadowAtten;

                float3 shade = lerp(_ShadeColor.rgb, 1.0.xxx, litFactor);
                float3 color = albedo * shade * mainLight.color;

                // Simple ambient (SH)
                float3 ambient = SampleSH(N) * albedo;
                color += ambient;

                // Rim light
                float rim = pow(saturate(1.0 - dot(N, V)), _RimPower) * _RimIntensity;
                color += rim * _RimColor.rgb;

                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }

        // Outline (backface + expand along normals)
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 n = normalize(IN.normalOS);
                float3 p = IN.positionOS.xyz + n * _OutlineWidth;

                VertexPositionInputs posInputs = GetVertexPositionInputs(p);
                OUT.positionHCS = posInputs.positionCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }}
