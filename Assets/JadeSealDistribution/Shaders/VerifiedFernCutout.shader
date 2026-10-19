Shader "JadeSealDistribution/VerifiedFernCutout" {
Properties {_MainTex("Verified CC0 colour",2D)="white"{} _AlphaTex("Verified CC0 opacity",2D)="white"{} _Cutoff("Cutout",Range(0,1))=0.45}
SubShader {Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"} Cull Off LOD 200
CGPROGRAM
#pragma surface surf Standard alphatest:_Cutoff addshadow fullforwardshadows
#pragma target 3.0
sampler2D _MainTex;sampler2D _AlphaTex;
struct Input {float2 uv_MainTex;};
void surf(Input IN,inout SurfaceOutputStandard o){o.Albedo=tex2D(_MainTex,IN.uv_MainTex).rgb;o.Alpha=tex2D(_AlphaTex,IN.uv_MainTex).r;o.Smoothness=0.12;}
ENDCG
} Fallback "Transparent/Cutout/Diffuse"
}
