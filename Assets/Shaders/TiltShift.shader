// Tilt-shift/miniature look: BUKAN Depth of Field beneran (gak butuh depth scene sama sekali),
// murni blur berdasarkan posisi di LAYAR. Jadi pita tajamnya bisa diputer manual (_Angle) biar
// pas sama sudut kamera isometrik-mu, gak ngikutin garis jarak scene yang suka diagonal aneh.
Shader "Hidden/Blacksmith/TiltShift"
{
    Properties
    {
        [Header(Blur)]
        _BlurRadius ("Blur Radius", Range(0, 0.02)) = 0.006
        _Samples ("Blur Quality (samples)", Range(4, 32)) = 12

        [Header(Focus Band)]
        _FocusCenter ("Focus Center (0 = tepi awal, 1 = tepi akhir)", Range(0,1)) = 0.5
        _FocusWidth ("Focus Width (lebar area tajam)", Range(0,1)) = 0.18
        _Feather ("Feather (kehalusan transisi)", Range(0.01,1)) = 0.3
        _Angle ("Band Angle (derajat, 0 = horizontal)", Range(-90,90)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "TiltShift"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Core.hlsl URP ini yang beneran ngedefinisiin TEXTURE2D_X/SAMPLE_TEXTURE2D_X buat versi
            // package ini — Blit.hlsl sendiri gak nge-include itu, cuma pakai asumsi udah ada.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BlurRadius;
            float _Samples;
            float _FocusCenter;
            float _FocusWidth;
            float _Feather;
            float _Angle;

            #define TS_TWO_PI 6.28318530718

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Proyeksiin UV ke sumbu yang udah diputer _Angle derajat, biar pita tajamnya
                // bisa dimiringin manual ngikutin sudut kamera isometrik-mu.
                float rad = radians(_Angle);
                float s = sin(rad);
                float c = cos(rad);
                float2 centered = uv - 0.5;
                float projected = centered.x * (-s) + centered.y * c;

                float dist = abs(projected - (_FocusCenter - 0.5));
                float blend = smoothstep(_FocusWidth, _FocusWidth + _Feather, dist);

                half3 sharpColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                // Blur murah: sampling melingkar (poor-man's disc blur), radiusnya di-jitter dikit
                // biar gak keliatan kayak cincin.
                half3 blurColor = 0;
                int samples = (int)_Samples;
                for (int i = 0; i < samples; i++)
                {
                    float a = (i / (float)samples) * TS_TWO_PI;
                    float r = _BlurRadius * (0.4 + 0.6 * frac(i * 0.61803398875));
                    float2 offset = float2(cos(a), sin(a)) * r;
                    blurColor += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset).rgb;
                }
                blurColor /= max(samples, 1);

                half3 result = lerp(sharpColor, blurColor, blend);
                return half4(result, 1);
            }
            ENDHLSL
        }
    }
}
