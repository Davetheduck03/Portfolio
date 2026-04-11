// CircularWipe.shader
// Renders a full-screen black overlay with a circular "hole" punched in the centre.
// Used by SceneTransition.cs for iris-wipe transitions.
//
//  _Progress = 0  →  hole radius = 0  →  fully opaque (screen hidden)
//  _Progress = 1  →  hole radius = screen corner  →  fully transparent (screen visible)
//
// Compatible with Built-in Render Pipeline and URP (Screen Space Overlay canvas).

Shader "Custom/CircularWipe"
{
    Properties
    {
        // Required by Unity UI — not sampled in this shader
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        // Wipe colour (set via Image.color or material tint)
        _Color ("Tint", Color) = (0,0,0,1)

        // 0 = fully closed  /  1 = fully open
        _Progress ("Progress", Range(0,1)) = 0

        // Soft edge width (in UV-space, after aspect-ratio correction)
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.15)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Overlay"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
        }

        Cull      Off
        Lighting  Off
        ZWrite    Off
        ZTest     Always
        Blend     SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed4    _Color;
            float     _Progress;
            float     _EdgeSoftness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = TRANSFORM_TEX(v.uv, _MainTex);
                o.color  = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // UV (0,0)=bottom-left  (1,1)=top-right  centre=(0.5,0.5)
                float2 centered = i.uv - 0.5;

                // Stretch x so the wipe traces a true circle, not an ellipse
                float aspect = _ScreenParams.x / _ScreenParams.y;
                centered.x  *= aspect;

                // Distance from screen centre
                float dist = length(centered);

                // The furthest point from centre (i.e. a screen corner)
                float maxDist = length(float2(0.5 * aspect, 0.5));

                // Map _Progress to a radius that reaches the corners at exactly 1.0
                // Extend by one softness band so edges are clean at 0 and 1
                float radius = lerp(
                    -_EdgeSoftness,
                    maxDist + _EdgeSoftness,
                    _Progress
                );

                // alpha: 1 = opaque (outside circle), 0 = transparent (inside hole)
                float alpha = smoothstep(radius - _EdgeSoftness,
                                         radius + _EdgeSoftness,
                                         dist);

                fixed4 col = i.color;   // rgb = wipe colour (black by default)
                col.a      = alpha * i.color.a;
                return col;
            }
            ENDCG
        }
    }
}
