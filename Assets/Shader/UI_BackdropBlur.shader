Shader "Custom/UI_BackdropBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // 建议默认值：radius = _Sigma * _BlurSize ≈ 4~6 背景像素
        _BlurSize ("Blur Size", Range(0, 8)) = 3
        _Sigma ("Sigma", Range(0.5, 4)) = 1.5
        _Darkness ("Darkness", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "UI_BACKDROP_BLUR"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            // 采样点数：32 在半径 <= 6 时已足够密；半径越小可以降到 16
            #define UI_BLUR_TAPS 32
            #define GOLDEN_ANGLE 2.39996323

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 screenPos : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _BlurSize;
                float _Sigma;
                float _Darkness;
            CBUFFER_END

            // Vogel 螺旋圆盘采样。
            // r_i = sqrt((i+0.5)/N)  —— 从中心开始均匀铺满整个圆盘（原表 r 从 0.177 起，中心有空环）
            // 权重 exp(-2*r^2) 与 sigma 解耦：半径取 2*sigma 时高斯权重恒等于 exp(-2*r^2)
            // [unroll] 后编译器会把 cos/sin/exp 的常量参数折叠掉
            half3 DiscBlur(float2 uv, float2 texel, float radius)
            {
                half3 sum  = SampleSceneColor(uv);
                float wsum = 1.0;

                [unroll]
                for (int i = 0; i < UI_BLUR_TAPS; i++)
                {
                    float  r     = sqrt((i + 0.5) / UI_BLUR_TAPS);
                    float  theta = i * GOLDEN_ANGLE;
                    float2 dir   = float2(cos(theta), sin(theta)) * r;
                    float  w     = exp(-2.0 * r * r);

                    sum  += SampleSceneColor(uv + dir * radius * texel) * w;
                    wsum += w;
                }
                return sum / wsum;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

#if defined(UNITY_PRETRANSFORM_TO_DISPLAY_ORIENTATION)
                screenUV = RemovePretransformRotation(screenUV);
#endif

                float2 texel = _CameraOpaqueTexture_TexelSize.xy;

                // 关键：去掉 2 倍，半径 = sigma * blurSize
                // 默认 1.5 * 3 = 4.5 背景像素，32 个点间距 ≈ 1.8 像素，基本无重影
                float radius = max(_Sigma, 0.5) * max(_BlurSize, 0.0);

                half3 blurred = DiscBlur(screenUV, texel, radius);

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 finalColor = blurred * texColor.rgb * input.color.rgb;
                finalColor *= (1.0 - _Darkness);

                return half4(finalColor, texColor.a * input.color.a);
            }
            ENDHLSL
        }
    }
}