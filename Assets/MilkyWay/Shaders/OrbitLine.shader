Shader "MilkyWay/OrbitLine"
{
    // The orbit line as a drawn object instead of a flat ribbon: a soft
    // feathered cross-section with a bright hairline core, and a comet-tail
    // gradient that glows at the planet and fades around the ellipse behind
    // it — so every orbit shows where its planet is and which way it runs.
    //
    // Rendered by a LineRenderer in Stretch texture mode: uv.x runs 0..1
    // around the loop (the same parameter as the orbit angle), uv.y runs
    // 0..1 across the width.
    Properties
    {
        _Color("Tint", Color) = (0.65, 0.75, 0.95, 0.6)
        _HeadPhase("Planet Phase (0..1 along loop)", Range(0, 1)) = 0.0
        _TrailFalloff("Trail Falloff", Float) = 7.0
        _TrailBoost("Trail Brightness", Float) = 1.6
        _BaseGlow("Base Glow", Range(0, 1)) = 0.30
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "OrbitLine"
            Tags { "LightMode" = "UniversalForward" }
            // Premultiplied: glows over black space, stays a soft veil over
            // a bright planet instead of the old opaque grey ribbon.
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _HeadPhase, _TrailFalloff, _TrailBoost, _BaseGlow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionHCS = TransformWorldToHClip(TransformObjectToWorld(v.positionOS.xyz));
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Cross-section: a wide soft feather with a thin bright core.
                float across = abs(i.uv.y * 2.0 - 1.0);
                float feather = pow(saturate(1.0 - across), 2.2);
                float core = exp(-across * across * 10.0);

                // How far BEHIND the planet this point sits (the planet runs
                // toward increasing uv.x). 0 at the planet, 1 a full lap back.
                float behind = frac(_HeadPhase - i.uv.x);
                float trail = _BaseGlow + _TrailBoost * exp(-behind * _TrailFalloff);

                float3 col = _Color.rgb * (feather * 0.7 + core * 1.1) * trail;
                float a = saturate((feather * 0.55 + core * 0.45) * trail * _Color.a);
                return half4(col * _Color.a, a);
            }
            ENDHLSL
        }
    }
}
