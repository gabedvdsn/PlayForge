// The Demo's outline: the object drawn once more, scaled up from its pivot and in one color, with its front faces
// culled, so only the rim past the object shows. Unlit and built-in, as the samples are (PUBLISHING.md).
Shader "PlayForge/Samples/Demo Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.25)) = 0.06
    }

    SubShader
    {
        Tags { "Queue" = "Geometry+1" "RenderType" = "Opaque" }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                // Scaled from the pivot rather than pushed along the normal, so a cube's hard edges don't split apart
                o.pos = UnityObjectToClipPos(float4(v.vertex.xyz * (1.0 + _OutlineWidth), 1));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
}
