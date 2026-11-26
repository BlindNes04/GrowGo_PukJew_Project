// =====================================================================================
//  GrowGoBGWave.shader  🌳🌊 ทำให้รูปพื้นหลังรูปเดียว "ขยับ" ได้ (ใช้กับ UI Image)
// -------------------------------------------------------------------------------------
//  _Mode = 0 : ต้นไม้ไหวตามลม (MainForest / FriendVisit)
//              ใช้รูป mask (ขาว = ต้นไม้) + รูปฟ้าที่ลบต้นไม้ออกแล้ว (Resources/GrowGoSway)
//              -> ขยับเฉพาะต้นไม้ ฟ้า/เมฆ/พื้นหญ้านิ่ง, ยอดไม้ไหวมากกว่าโคน
//              ถ้าไม่มีรูป mask (_HasMask = 0) จะไหวทั้งส่วนบนของรูปแทน
//  _Mode = 2 : น้ำทะเลซัดเข้า-ออกฝั่ง (BeachIsland) แบบเดียวกับต้นไม้: mask R = น้ำ+ฟอง, G = หิน (อยู่บนสุดเสมอ)
//              + รูปทรายที่ลบน้ำออกแล้ว -> ทั้งผืนน้ำเลื่อนขึ้นฝั่งแล้วถอยกลับ ใกล้ขอบฟ้านิ่ง
//  _Mode = 1 : (เลิกใช้แล้ว) น้ำเป็นระลอกในตัว
//              เฉพาะพิกเซลที่ "สีฟ้า" (น้ำ) และอยู่ใต้เส้นขอบฟ้า เท่านั้นที่ขยับ + มีประกายแดด
//  _Amp / _Phase / _Shimmer ตั้งจาก GrowGoWeather.cs ทุกเฟรม
//  ต่อจาก UI/Default ของ Unity (รองรับ Mask / RectMask2D เหมือนเดิม)
// =====================================================================================
Shader "GrowGo/BGWave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Mode ("Mode (0 trees, 1 water)", Float) = 0
        _MaskTex ("Tree Mask", 2D) = "black" {}
        _SkyTex ("Sky Plate (no trees)", 2D) = "white" {}
        _HasMask ("Has Mask", Float) = 0
        _Amp ("Amplitude", Float) = 0.003
        _Phase ("Phase", Float) = 0
        _Shimmer ("Water Shimmer", Float) = 0.05
        _GroundY ("Ground UV Y (trees anchor)", Float) = 0.48
        _TopY ("Full sway UV Y", Float) = 0.85
        _HorizonY ("Horizon UV Y (water)", Float) = 0.765
        _ShoreDir ("Shore Dir (uv, toward land)", Vector) = (0.94, -0.34, 0, 0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            sampler2D _MaskTex, _SkyTex;
            float _HasMask;
            float4 _ShoreDir;
            float _Mode, _Amp, _Phase, _Shimmer, _GroundY, _TopY, _HorizonY;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float shine = 0;
                half4 overrideCol = 0;
                float useOverride = 0;

                if (_Mode < 0.5)
                {
                    // ---- 🌳 ต้นไม้ไหว: ยิ่งสูงยิ่งไหว โคนต้นนิ่ง ----
                    float h = smoothstep(_GroundY, _TopY, uv.y);
                    float w = sin(_Phase + uv.y * 5.0 + uv.x * 2.5) * 0.65
                            + sin(_Phase * 1.7 + uv.y * 9.0 - uv.x * 3.0) * 0.35;
                    float2 duv = uv;
                    duv.x += _Amp * h * w;
                    duv.y += _Amp * 0.25 * h * sin(_Phase * 1.3 + uv.x * 6.0);
                    if (_HasMask > 0.5)
                    {
                        // ต้นไม้ที่ขยับมาทับจุดนี้ (mask ที่ตำแหน่งเลื่อน) ทับบนฟ้าที่ลบต้นไม้แล้ว
                        float m = tex2D(_MaskTex, duv).r;
                        half4 treeCol = tex2D(_MainTex, duv);
                        half4 skyCol = tex2D(_SkyTex, uv);
                        overrideCol = lerp(skyCol, treeCol, m);
                        overrideCol.a = tex2D(_MainTex, uv).a;
                        useOverride = 1;
                    }
                    else uv = duv;
                }
                else if (_Mode > 1.5)
                {
                    // ---- 🌊 คลื่นซัดฝั่ง: น้ำทั้งผืนเลื่อนไปทางฝั่ง (_Amp = ระยะตอนนี้) ใกล้ขอบฟ้าไม่ขยับ ----
                    float fade = 1.0 - smoothstep(_HorizonY - 0.22, _HorizonY - 0.02, uv.y);
                    float along = 0.8 + 0.2 * sin(uv.x * 7.0 + uv.y * 5.0 + _Phase * 0.6);   // ขอบน้ำไม่แข็งทื่อ
                    float2 duv = uv - _ShoreDir.xy * _Amp * fade * along;
                    float m = tex2D(_MaskTex, duv).r;
                    half4 c = lerp(tex2D(_SkyTex, uv), tex2D(_MainTex, duv), m);
                    half4 orig = tex2D(_MainTex, uv);
                    c = lerp(c, orig, tex2D(_MaskTex, uv).g);   // หินทับน้ำเสมอ
                    c.a = orig.a;
                    overrideCol = c;
                    useOverride = 1;
                }
                else
                {
                    // ---- 🌊 น้ำทะเล: หาว่าตรงนี้เป็นน้ำมั้ย (ฟ้ากว่าแดงชัดๆ + อยู่ใต้ขอบฟ้า) ----
                    fixed4 c0 = tex2D(_MainTex, uv);
                    float water = saturate((c0.b - c0.r - 0.12) * 8.0) * (1.0 - smoothstep(_HorizonY - 0.015, _HorizonY, uv.y));
                    // ไกลขอบฟ้า คลื่นเล็กลง (perspective)
                    float depth = lerp(1.0, 0.35, saturate(uv.y / _HorizonY));
                    float2 d = float2(sin(_Phase + uv.y * 42.0 + uv.x * 12.0),
                                      cos(_Phase * 0.8 + uv.x * 31.0 - uv.y * 14.0));
                    uv += d * _Amp * water * depth;
                    // ประกายแดดบนผิวน้ำ
                    float s = sin(_Phase * 1.4 + uv.x * 38.0 + uv.y * 61.0) * sin(_Phase * 0.9 - uv.x * 23.0 + uv.y * 47.0);
                    shine = water * _Shimmer * smoothstep(0.55, 1.0, s);
                }

                half4 baseCol = useOverride > 0.5 ? overrideCol : tex2D(_MainTex, uv);
                half4 color = (baseCol + _TextureSampleAdd) * IN.color;
                color.rgb += shine * color.a;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                color.rgb *= color.a;
                return color;
            }
        ENDCG
        }
    }
}
