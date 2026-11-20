Shader "Custom/UI_BackdropBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        // 模糊强度
        _BlurSize ("Blur Size", Range(0, 10)) = 5
        // 模糊迭代次数（越高越糊，但也越吃性能）
        _BlurIterations ("Blur Iterations", Range(1, 4)) = 2
        // 背景暗化程度
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
                float _BlurIterations;
                float _Darkness;
            CBUFFER_END

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

            // 简单的屏幕采样函数
            half3 SampleScreenBlur(float2 uv, float2 texelSize, float blurSize)
            {
                half3 color = 0;
                float2 offset1 = float2(1.0, 1.0) * texelSize * blurSize;
                float2 offset2 = float2(1.0, -1.0) * texelSize * blurSize;

                // 九宫格采样（性能好，效果尚可）
                color += SampleSceneColor(uv);
                color += SampleSceneColor(uv + float2(offset1.x, 0));
                color += SampleSceneColor(uv - float2(offset1.x, 0));
                color += SampleSceneColor(uv + float2(0, offset1.y));
                color += SampleSceneColor(uv - float2(0, offset1.y));
                color += SampleSceneColor(uv + offset1);
                color += SampleSceneColor(uv - offset1);
                color += SampleSceneColor(uv + offset2);
                color += SampleSceneColor(uv - offset2);
                
                return color / 9.0;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 获取屏幕 UV
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float2 texelSize = 1.0 / _ScreenParams.xy; // 屏幕像素大小

                // 多次迭代采样，增加模糊度
                half3 blurredColor = 0;
                float currentBlurSize = _BlurSize;
                
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    if (i >= _BlurIterations) break;
                    // 在每次迭代中，采样范围逐渐扩大
                    blurredColor += SampleScreenBlur(screenUV, texelSize, currentBlurSize);
                    currentBlurSize *= 1.5; // 扩散系数
                }
                blurredColor /= _BlurIterations;

                // 与 UI 原图（比如圆角矩形贴图）混合
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // 将屏幕模糊颜色乘上 UI 的原图颜色（保留UI的透明度、圆角和颜色）
                half3 finalColor = blurredColor * texColor.rgb * input.color.rgb;
                
                // 暗化处理，让文字更清晰
                finalColor *= (1.0 - _Darkness);

                // 最终透明度由 UI 自身的 Alpha 决定
                return half4(finalColor, texColor.a * input.color.a);
            }
            ENDHLSL
        }
    }
}