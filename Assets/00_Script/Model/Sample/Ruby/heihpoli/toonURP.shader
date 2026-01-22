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

        // ★アウトライン太さ：ピクセル基準（目安 1～6）
        _OutlineWidth("Outline Width (px-ish)", Range(0,8)) = 3

        // ★疑似AA：フェード幅（大きいほど柔らかいが細く見える）
        _OutlineSoftness("Outline Softness", Range(0,2)) = 1

        // ★距離で薄くしてギザギザ抑制（0で無効）
        _OutlineFadeByDistance("Outline Fade By Distance", Range(0,1)) = 0.5
        _OutlineFadeStart("Outline Fade Start", Range(0,50)) = 10
        _OutlineFadeEnd("Outline Fade End", Range(0,200)) = 60
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

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadeColor;
                float4 _RimColor;

                float _ShadowSteps;
                float _ShadowFeather;
                float _ShadowOffset;

                float _RimPower;
                float _RimIntensity;
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
                steps = max(1.0, steps);
                float t = ndl * steps;
                float baseStep = floor(t) / steps;
                float nextStep = ceil(t) / steps;

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

                float4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float3 albedo = tex.rgb * _BaseColor.rgb;

                Light mainLight = GetMainLight(IN.shadowCoord);
                float3 L = normalize(mainLight.direction);

                float ndl = saturate(dot(N, L) * 0.5 + 0.5);
                ndl = saturate(ndl + _ShadowOffset);

                float shadowAtten = mainLight.shadowAttenuation;
                float toon = ToonStep(ndl, _ShadowSteps, _ShadowFeather);
                float litFactor = toon * shadowAtten;

                float3 shade = lerp(_ShadeColor.rgb, 1.0.xxx, litFactor);
                float3 color = albedo * shade * mainLight.color;

                color += SampleSH(N) * albedo;

                float rim = pow(saturate(1.0 - dot(N, V)), _RimPower) * _RimIntensity;
                color += rim * _RimColor.rgb;

                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }

        // ===== Outline with pseudo-AA =====
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            // ★MSAAが有効な場合に効く（URP Asset / Cameraの設定依存）
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;            // px-ish
                float _OutlineSoftness;         // fade width control
                float _OutlineFadeByDistance;   // 0..1
                float _OutlineFadeStart;
                float _OutlineFadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float  aa          : TEXCOORD1; // for alpha
            };

            // 画面上のピクセル量を「クリップ空間のオフセット」に近似変換する
            // _ScreenParams: (w,h,1+1/w,1+1/h)
            float2 PixelToNDC(float2 px)
            {
                return float2(px.x / _ScreenParams.x, px.y / _ScreenParams.y) * 2.0;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionWS = pos.positionWS;

                float3 nWS = normalize(TransformObjectToWorldNormal(IN.normalOS));

                // 視点方向に直交する成分だけで押し出す（不安定な膨張を抑える）
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(pos.positionWS));
                float3 nOrtho = normalize(nWS - viewDirWS * dot(nWS, viewDirWS));

                // ピクセル基準のオフセットをNDCに変換して、clip位置をずらす
                float2 ndcOffset = PixelToNDC(float2(_OutlineWidth, _OutlineWidth));

                // nOrtho を画面方向へ投影（近似）：View空間でXYとして扱う
                float3 nVS = normalize(TransformWorldToViewDir(nOrtho));
                float2 dir2 = normalize(nVS.xy + 1e-6);

                float2 offsetNDC = dir2 * ndcOffset.x;

                // clip空間でオフセット（wでスケール）
                float4 clip = pos.positionCS;
                clip.xy += offsetNDC * clip.w;

                OUT.positionHCS = clip;

                // 疑似AA用アルファ（端を少し薄くする）
                // _OutlineSoftness が大きいほど薄める
                OUT.aa = saturate(1.0 / max(1e-3, _OutlineSoftness));

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 距離フェード（遠方のジャギ対策）
                float alpha = 1.0;

                if (_OutlineFadeByDistance > 0.001)
                {
                    float dist = distance(_WorldSpaceCameraPos, IN.positionWS);
                    float t = saturate((dist - _OutlineFadeStart) / max(1e-3, (_OutlineFadeEnd - _OutlineFadeStart)));
                    // 遠いほど薄く
                    alpha *= lerp(1.0, 1.0 - t, _OutlineFadeByDistance);
                }

                // 疑似AAで少し薄め（AlphaToMaskが効く環境だと丸くなる）
                alpha *= IN.aa;

                return half4(_OutlineColor.rgb, _OutlineColor.a * alpha);
            }
            ENDHLSL
        }
    }
    
}
