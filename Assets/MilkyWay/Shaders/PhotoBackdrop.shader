Shader "MilkyWay/PhotoBackdrop"
{
    // A real deep-sky photograph (DSS2 survey cutout) on a camera-facing quad.
    // Used two ways in the nebula showcase:
    //  - dimmed wide-field backdrop behind each volumetric specimen, with a
    //    radial vignette so it melts into the panoramic sky, and
    //  - full-frame photo exhibit (the Horsehead), vignette pushed past 1 so
    //    the photograph runs edge to edge.
    // Unlit, transparent, drawn just before the nebula volumes (Queue-20).
    Properties
    {
        _MainTex("Photo", 2D) = "black" {}
        _Exposure("Exposure", Range(0, 4)) = 1
        _Tint("Tint", Color) = (1, 1, 1, 1)
        // Vignette in centre-normalized UV distance (1 = edge midpoint).
        _VignetteInner("Vignette Inner", Range(0, 3)) = 0.5
        _VignetteOuter("Vignette Outer", Range(0, 3)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "PhotoBackdrop"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Tint;
                float _Exposure, _VignetteInner, _VignetteOuter;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                c *= _Exposure * _Tint.rgb;
                float d = length(i.uv - 0.5) * 2.0;
                half a = 1.0 - smoothstep(_VignetteInner, _VignetteOuter, d);
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
