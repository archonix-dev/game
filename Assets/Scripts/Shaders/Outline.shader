Shader "Custom/AdvancedOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.1)) = 0.01
        _OutlineTexture ("Outline Texture", 2D) = "white" {}
        _OutlineTextureScale ("Texture Scale", Float) = 1.0
        _OutlineIntensity ("Outline Intensity", Range(0.0, 100.0)) = 1.0
        _OutlineGlow ("Outline Glow", Range(0.0, 100.0)) = 0.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            
            Cull Front
            ZWrite On
            ZTest LEqual
            Blend One OneMinusSrcAlpha
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float4 _OutlineTexture_ST;
                float _OutlineTextureScale;
                float _OutlineIntensity;
                float _OutlineGlow;
            CBUFFER_END
            
            TEXTURE2D(_OutlineTexture);
            SAMPLER(sampler_OutlineTexture);
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Получаем нормаль в мировом пространстве
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                // Получаем позицию в мировом пространстве
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                
                // Расширяем позицию по нормали
                positionWS += normalWS * _OutlineWidth;
                
                // Преобразуем в пространство клипа
                output.positionCS = TransformWorldToHClip(positionWS);
                output.worldPos = positionWS;
                
                // UV координаты
                output.uv = TRANSFORM_TEX(input.uv, _OutlineTexture) * _OutlineTextureScale;
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Сэмплируем текстуру
                half4 outlineTex = SAMPLE_TEXTURE2D(_OutlineTexture, sampler_OutlineTexture, input.uv);
                
                // Применяем цвет и интенсивность
                half4 finalColor = _OutlineColor * outlineTex * _OutlineIntensity;
                
                // Добавляем свечение
                if (_OutlineGlow > 0)
                {
                    float glow = sin(_Time.y * 2.0) * 0.5 + 0.5;
                    finalColor.rgb += _OutlineColor.rgb * _OutlineGlow * glow;
                }
                
                return finalColor;
            }
            ENDHLSL
        }
    }
    
    Fallback "Universal Render Pipeline/Lit"
}