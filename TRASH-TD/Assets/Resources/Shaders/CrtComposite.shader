// Lays the UI texture over the game texture in one pass, for CrtDisplay (Classic and Full TV), so the CRT shader
// reads a single texture per tap instead of two. With CRT_HEAT the game is first rippled by the Stage 4 heat shimmer
// (the UI never is), exactly as CrtScreen.shader does in its own two-texture path: HeatOffset below mirrors it.
// The UI texture is rendered with normal alpha blending onto transparent black, so its colour is already multiplied by
// coverage and its alpha is coverage squared (single layer).
Shader "Hidden/TrashTD/CrtComposite"
{
    Properties
    {
        _MainTex ("Game", 2D) = "black" {}
        _UiTex ("UI", 2D) = "black" {}
        _HeatWarp ("Heat Warp (strength in uv, band height 0-1)", Vector) = (0, 0.4, 0, 0)
        _HeatVentX ("Heat Vent X (uv, negative = off)", Vector) = (-1, -1, -1, -1)
        _HeatVentY ("Heat Vent Y (uv)", Vector) = (0, 0, 0, 0)
        _CrtTime ("Time (unscaled, set by script)", Float) = 0
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            Name "Composite"

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ CRT_HEAT

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _UiTex;
            float4 _HeatWarp;
            float4 _HeatVentX;
            float4 _HeatVentY;
            float _CrtTime;

            // Same as HeatOffset in CrtScreen.shader: how far the game picture is shoved sideways and up/down at uv.
            float2 HeatOffset(float2 uv)
            {
                float strength = _HeatWarp.x;
                if (strength < 0.00001) return float2(0.0, 0.0);

                float t = _CrtTime;
                float band = saturate(1.0 - uv.y / max(_HeatWarp.y, 0.05));
                float amount = band * band;

                float4 dx = uv.x - _HeatVentX;
                float4 rise = uv.y - _HeatVentY;
                float4 column = exp(-dx * dx * 700.0) * step(0.0, rise) * saturate(1.0 - rise / 0.5) * step(0.0, _HeatVentX);
                amount += (column.x + column.y + column.z + column.w) * 0.9;

                float w1 = sin(uv.y * 85.0 - t * 2.8 + sin(uv.x * 11.0 + t * 0.9) * 2.2);
                float w2 = sin(uv.y * 41.0 - t * 1.9 + uv.x * 23.0 + t * 0.4);
                float2 offset;
                offset.x = (w1 * 0.65 + w2 * 0.35) * amount;
                offset.y = sin(uv.x * 37.0 + t * 1.6 + uv.y * 20.0) * 0.3 * amount;
                return offset * strength;
            }

            half4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
            #if defined(CRT_HEAT)
                half4 picture = tex2Dlod(_MainTex, float4(uv + HeatOffset(uv), 0.0, 0.0));
            #else
                half4 picture = tex2Dlod(_MainTex, float4(uv, 0.0, 0.0));
            #endif
                half4 ui = tex2Dlod(_UiTex, float4(uv, 0.0, 0.0));
                half coverage = sqrt(saturate(ui.a));
                picture.rgb = ui.rgb + picture.rgb * (1.0 - coverage);
                picture.a = max(picture.a, coverage);
                return picture;
            }
            ENDCG
        }
    }
}
