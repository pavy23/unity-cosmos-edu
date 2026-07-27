Shader "MilkyWay/PlanetRing"
{
    // Saturn's rings on a flat annulus mesh (built by SolarSystemRig): the
    // banding comes from 1D noise over the radial coordinate (uv.x = 0 at the
    // inner edge, 1 at the outer), with the Cassini division authored in as a
    // hard gap. Two-sided and lit from _SunPos like PlanetSurface.
    Properties
    {
        _RingColor("Ring Colour", Color) = (0.82, 0.74, 0.58, 1)
        _RingTex("Ring Strip (x = radius, rgba)", 2D) = "black" {}
        _RingTexStrength("Ring Strip Strength", Range(0, 1)) = 0.0
        _SunPos("Sun Position (world)", Vector) = (0, 0, 0, 0)
        _Ambient("Ambient Floor", Range(0, 1)) = 0.15
        _Opacity("Opacity", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-150" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "PlanetRing"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RingColor, _SunPos;
                float _Ambient, _Opacity;
                float _RingTexStrength;
            CBUFFER_END

            TEXTURE2D(_RingTex); SAMPLER(sampler_RingTex);

            float ring_hash(float p)
            {
                return frac(sin(p * 127.1) * 43758.5453);
            }

            float ring_noise(float p)
            {
                float i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(ring_hash(i), ring_hash(i + 1.0), f);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float r = i.uv.x;

                // Fine banding at two scales, then the Cassini division: a
                // clean dark gap two-thirds of the way out.
                float bands = 0.55 + 0.45 * ring_noise(r * 26.0);
                bands *= 0.7 + 0.3 * ring_noise(r * 90.0 + 7.0);
                float cassini = 1.0 - smoothstep(0.62, 0.64, r) * (1.0 - smoothstep(0.68, 0.70, r));
                float edge = smoothstep(0.0, 0.06, r) * (1.0 - smoothstep(0.93, 1.0, r));

                // The observed ring strip (Cassini imagery reduced to a radial
                // profile): rgb is the ring colour, alpha its optical depth —
                // the real gaps and the real grading replace the noise bands.
                half4 strip = SAMPLE_TEXTURE2D(_RingTex, sampler_RingTex, float2(r, 0.5));

                float alpha = lerp(bands * cassini * edge, strip.a, _RingTexStrength) * _Opacity;
                float3 baseCol = lerp(_RingColor.rgb * (0.85 + 0.3 * bands), strip.rgb, _RingTexStrength);

                // Flat ring: both faces catch light by |N·L|.
                float3 N = normalize(i.normalWS);
                float3 L = normalize(_SunPos.xyz - i.positionWS);
                float light = _Ambient + (1.0 - _Ambient) * abs(dot(N, L));

                return half4(baseCol * light, alpha);
            }
            ENDHLSL
        }
    }
}
