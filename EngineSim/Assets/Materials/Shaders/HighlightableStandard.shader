Shader "Custom/HighlightableStandard"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
        _GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
        [Enum(Metallic Alpha,0,Albedo Alpha,1)] _SmoothnessTextureChannel ("Smoothness texture channel", Float) = 0
        [Gamma] _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}
        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _GlossyReflections("Glossy Reflections", Float) = 1.0
        
        // === OUTLINE PROPERTIES ===
        _OutlineColor ("Outline Color", Color) = (1, 0.93, 0, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.05)) = 0.005

        _BumpScale("Scale", Float) = 1.0
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _Parallax ("Height Scale", Range (0.005, 0.08)) = 0.02
        _ParallaxMap ("Height Map", 2D) = "black" {}
        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}
        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}
        _DetailNormalMapScale("Scale", Float) = 1.0
        [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}
        [Enum(UV0,0,UV1,1)] _UVSec ("UV Set for secondary textures", Float) = 0

        // Blending state
        [HideInInspector] _Mode ("__mode", Float) = 0.0
        [HideInInspector] _SrcBlend ("__src", Float) = 1.0
        [HideInInspector] _DstBlend ("__dst", Float) = 0.0
        [HideInInspector] _ZWrite ("__zw", Float) = 1.0
    }

    CGINCLUDE
        #define UNITY_SETUP_BRDF_INPUT MetallicSetup
    ENDCG

    // ============================================================================
    // SubShader для ПК/мобилок (LOD 300)
    // ============================================================================
    SubShader
    {
        Tags { "RenderType"="Opaque" "PerformanceChecks"="False" }
        LOD 300

        // --- Стандартные пассы Standard Shader (без изменений) ---
        Pass 
        {
             Name "FORWARD" Tags { "LightMode"="ForwardBase" }
            Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite]
            CGPROGRAM
            #pragma target 3.0
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_fragment _EMISSION
            #pragma shader_feature_local _METALLICGLOSSMAP
            #pragma shader_feature_local_fragment _DETAIL_MULX2
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _GLOSSYREFLECTIONS_OFF
            #pragma shader_feature_local _PARALLAXMAP
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma vertex vertBase
            #pragma fragment fragBase
            #include "UnityStandardCoreForward.cginc"
            ENDCG
        }
        Pass { Name "FORWARD_DELTA" Tags { "LightMode"="ForwardAdd" }
            Blend [_SrcBlend] One Fog { Color (0,0,0,0) } ZWrite Off ZTest LEqual
            CGPROGRAM
            #pragma target 3.0
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _METALLICGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _DETAIL_MULX2
            #pragma shader_feature_local _PARALLAXMAP
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            #pragma vertex vertAdd
            #pragma fragment fragAdd
            #include "UnityStandardCoreForward.cginc"
            ENDCG
        }
        Pass { Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual
            CGPROGRAM
            #pragma target 3.0
            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _METALLICGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _PARALLAXMAP
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #pragma vertex vertShadowCaster
            #pragma fragment fragShadowCaster
            #include "UnityStandardShadow.cginc"
            ENDCG
        }
        Pass { Name "DEFERRED" Tags { "LightMode"="Deferred" }
            CGPROGRAM
            #pragma target 3.0
            #pragma exclude_renderers nomrt
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_fragment _EMISSION
            #pragma shader_feature_local _METALLICGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _DETAIL_MULX2
            #pragma shader_feature_local _PARALLAXMAP
            #pragma multi_compile_prepassfinal
            #pragma multi_compile_instancing
            #pragma vertex vertDeferred
            #pragma fragment fragDeferred
            #include "UnityStandardCore.cginc"
            ENDCG
        }
        Pass { Name "META" Tags { "LightMode"="Meta" }
            Cull Off
            CGPROGRAM
            #pragma vertex vert_meta
            #pragma fragment frag_meta
            #pragma shader_feature_fragment _EMISSION
            #pragma shader_feature_local _METALLICGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _DETAIL_MULX2
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "UnityStandardMeta.cginc"
            ENDCG
        }

        // === OUTLINE PASS (для LOD 300) ===
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Offset 10, 10
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #include "UnityCG.cginc"
            
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            
            float4 _OutlineColor;
            float _OutlineWidth;

            v2f vertOutline(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                worldPos += worldNormal * _OutlineWidth;
                o.vertex = UnityWorldToClipPos(worldPos);
                return o;
            }

            fixed4 fragOutline(v2f i) : SV_Target
            {
                // Если ширина <= 0 — не рисуем контур
                if (_OutlineWidth <= 0.0001) clip(-1);
                return _OutlineColor;
            }
            ENDCG
        }
    }

    // ============================================================================
    // SubShader для слабых устройств (LOD 150) - ТОЖЕ ДОБАВЛЯЕМ OUTLINE!
    // ============================================================================
    SubShader
    {
        Tags { "RenderType"="Opaque" "PerformanceChecks"="False" }
        LOD 150

        // --- Упрощенные стандартные пассы ---
        Pass { Name "FORWARD" Tags { "LightMode"="ForwardBase" }
            Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite]
            CGPROGRAM #pragma target 2.0
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_fragment _EMISSION
            #pragma shader_feature_local _METALLICGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _GLOSSYREFLECTIONS_OFF
            #pragma skip_variants SHADOWS_SOFT DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fwdbase #pragma multi_compile_fog
            #pragma vertex vertBase #pragma fragment fragBase
            #include "UnityStandardCoreForward.cginc"
            ENDCG
        }
        Pass { Name "FORWARD_DELTA" Tags { "LightMode"="ForwardAdd" }
            Blend [_SrcBlend] One Fog{Color(0,0,0,0)} ZWrite Off ZTest LEqual
            CGPROGRAM #pragma target 2.0
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _METALLICGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _DETAIL_MULX2
            #pragma skip_variants SHADOWS_SOFT
            #pragma multi_compile_fwdadd_fullshadows #pragma multi_compile_fog
            #pragma vertex vertAdd #pragma fragment fragAdd
            #include "UnityStandardCoreForward.cginc"
            ENDCG
        }
        Pass { Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual
            CGPROGRAM #pragma target 2.0
            #pragma shader_feature_local _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local _METALLICGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma skip_variants SHADOWS_SOFT
            #pragma multi_compile_shadowcaster
            #pragma vertex vertShadowCaster #pragma fragment fragShadowCaster
            #include "UnityStandardShadow.cginc"
            ENDCG
        }
        Pass { Name "META" Tags { "LightMode"="Meta" }
            Cull Off
            CGPROGRAM
            #pragma vertex vert_meta #pragma fragment frag_meta
            #pragma shader_feature_fragment _EMISSION
            #pragma shader_feature_local _METALLICGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _DETAIL_MULX2
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "UnityStandardMeta.cginc"
            ENDCG
        }

        // === OUTLINE PASS (для LOD 150) ===
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front ZWrite Off ZTest LEqual Offset 10, 10 Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 vertex:SV_POSITION; };
            float4 _OutlineColor; float _OutlineWidth;
            v2f vertOutline(appdata v) {
                v2f o;
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                worldPos += worldNormal * _OutlineWidth;
                o.vertex = UnityWorldToClipPos(worldPos);
                return o;
            }
            fixed4 fragOutline(v2f i) : SV_Target {
                if (_OutlineWidth <= 0.0001) clip(-1);
                return _OutlineColor;
            }
            ENDCG
        }
    }

    FallBack "VertexLit"
    // CustomEditor "StandardShaderGUI"  <-- УБРАТЬ или закомментировать!
}