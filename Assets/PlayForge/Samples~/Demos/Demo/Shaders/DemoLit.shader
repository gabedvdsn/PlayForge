// The Demos' surface. In the Built-in pipeline it is Standard, borrowing Standard's own passes, so the Demos look as
// they always have. Under URP, Standard renders magenta, so a pass of its own lights the surface by URP's main light.
// That pass declares the URP globals it reads instead of including URP's files, so the shader compiles in a project
// without URP as well (SP-23).
Shader "PlayForge/Samples/Demo Lit"
{
    // Standard's properties, every one: its borrowed passes read them all, and an undeclared one reads 0. Blend and
    // depth state above all: Blend [_SrcBlend] [_DstBlend] with both at 0 draws black
    Properties
    {
        _Color("Color", Color) = (1, 1, 1, 1)
        _MainTex("Albedo", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
        _GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
        [Enum(Metallic Alpha,0,Albedo Alpha,1)] _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0
        [Gamma] _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}
        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _GlossyReflections("Glossy Reflections", Float) = 1.0
        _BumpScale("Scale", Float) = 1.0
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _Parallax("Height Scale", Range(0.005, 0.08)) = 0.02
        _ParallaxMap("Height Map", 2D) = "black" {}
        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _EmissionColor("Color", Color) = (0, 0, 0)
        _EmissionMap("Emission", 2D) = "white" {}
        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}
        _DetailNormalMapScale("Scale", Float) = 1.0
        [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}
        [Enum(UV0,0,UV1,1)] _UVSec("UV Set for secondary textures", Float) = 0

        [HideInInspector] _Mode("__mode", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
    }

    // URP: the main light, its highlight and a flat ambient term. It casts and receives no shadows
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Glossiness;
            float _Metallic;

            // URP sets these each frame: the main light's direction, and its color
            float4 _MainLightPosition;
            float4 _MainLightColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 albedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                float3 n = normalize(i.normal);
                float3 l = normalize(_MainLightPosition.xyz);
                float3 h = normalize(l + normalize(_WorldSpaceCameraPos - i.worldPos));

                // Lambert diffuse, and a Blinn-Phong highlight that tightens as smoothness rises. Metal tints its
                // highlight with its color and loses its diffuse, as Standard's does
                float diffuse = saturate(dot(n, l));
                float highlight = pow(saturate(dot(n, h)), exp2(10 * _Glossiness + 1)) * _Glossiness;
                float3 diffuseColor = albedo * (1 - _Metallic);
                float3 specularColor = lerp(0.04, albedo, _Metallic);

                float3 lit = (diffuseColor * diffuse + specularColor * highlight) * _MainLightColor.rgb;
                return float4(lit + albedo * 0.25, 1);
            }
            ENDHLSL
        }
    }

    // Built-in: Standard's own passes
    SubShader
    {
        Tags { "RenderType" = "Opaque" "PerformanceChecks" = "False" }
        UsePass "Standard/FORWARD"
        UsePass "Standard/FORWARD_DELTA"
        UsePass "Standard/SHADOWCASTER"
        UsePass "Standard/DEFERRED"
        UsePass "Standard/META"
    }

    FallBack "Diffuse"
    CustomEditor "StandardShaderGUI"
}
