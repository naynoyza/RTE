// SeedFx — shader เอฟเฟกต์ของภารกิจที่ 2 (ใช้กับ SpriteRenderer / ParticleSystem / LineRenderer)
//   สี = texture x สีจุดยอด (vertex color) x _Color
//   _SrcBlend/_DstBlend: (SrcAlpha, One) = แสงบวก (Additive)  |  (SrcAlpha, OneMinusSrcAlpha) = โปร่งใสปกติ
//   _Scroll: เลื่อน texture ตามแกน U ทุกเฟรม (ใช้ทำสายน้ำ/ลำแสงที่ไหล)
//   อยู่ใน Resources เพื่อให้ Shader.Find หาเจอใน Build ด้วย
Shader "RTE/SeedFx"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
        _Scroll ("Scroll U per second", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color; float _Scroll;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.x -= _Time.y * _Scroll;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target { return tex2D(_MainTex, i.uv) * i.color; }
            ENDCG
        }
    }
}
