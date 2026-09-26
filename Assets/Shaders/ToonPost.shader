// Toon look sebagai POST-PROCESSING (bukan ganti shader material satu-satu): posterize warna
// (kunci pencahayaan ke beberapa "step" kayak cel-shading) + outline hitam dari deteksi tepi
// depth/normal. Nempel ke SELURUH layar, gampang di-toggle lewat centang Active di Renderer
// Feature-nya kalau ternyata gak suka hasilnya — gak nyentuh satupun material asli.
Shader "Hidden/Blacksmith/ToonPost"
{
    Properties
    {
        [Header(Posterize)]
        _Levels ("Banyak Step Warna (makin dikit makin 'toon')", Range(2, 10)) = 4

        [Header(Outline)]
        _OutlineColor ("Warna Outline", Color) = (0, 0, 0, 1)
        _OutlineThickness ("Ketebalan Outline (texel)", Range(0.5, 4)) = 1.2
        _DepthThreshold ("Sensitivitas Depth", Range(0.0001, 0.05)) = 0.003
        _NormalThreshold ("Sensitivitas Normal", Range(0.01, 2)) = 0.4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "ToonPost"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _Levels;
            float4 _OutlineColor;
            float _OutlineThickness;
            float _DepthThreshold;
            float _NormalThreshold;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texel = (1.0 / _ScreenParams.xy) * _OutlineThickness;

                // ---- Posterize: kunci warna ke beberapa step, kesan cel-shading ----
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                color = floor(color * _Levels) / _Levels;

                // ---- Outline dari selisih depth & normal antar tetangga (Sobel sederhana) ----
                float depthCenter = SampleSceneDepth(uv);
                float3 normalCenter = SampleSceneNormals(uv);

                float depthDiff = 0;
                float normalDiff = 0;
                float2 offsets[4] = { float2(texel.x, 0), float2(-texel.x, 0), float2(0, texel.y), float2(0, -texel.y) };
                for (int i = 0; i < 4; i++)
                {
                    float2 sampleUv = uv + offsets[i];
                    depthDiff += abs(depthCenter - SampleSceneDepth(sampleUv));
                    normalDiff += distance(normalCenter, SampleSceneNormals(sampleUv));
                }

                bool isEdge = depthDiff > _DepthThreshold || normalDiff > _NormalThreshold;
                half3 result = isEdge ? _OutlineColor.rgb : color;

                return half4(result, 1);
            }
            ENDHLSL
        }
    }
}
