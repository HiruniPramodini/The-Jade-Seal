Shader "JadeSealDistribution/BambooAtlas" {
Properties {_MainTex("Verified culms and original leaf atlas",2D)="white"{} _Cutoff("Cutout",Range(0,1))=.3}
SubShader {Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"} Cull Off LOD 200
CGPROGRAM
#pragma surface surf Standard alphatest:_Cutoff addshadow fullforwardshadows
#pragma target 3.0
sampler2D _MainTex;
struct Input {float2 uv_MainTex;float4 color:COLOR;};
void surf(Input IN,inout SurfaceOutputStandard o){fixed4 c=tex2D(_MainTex,IN.uv_MainTex);o.Albedo=c.rgb*IN.color.rgb;o.Alpha=c.a;o.Smoothness=.1;}
ENDCG
} Fallback "Transparent/Cutout/Diffuse"
}
