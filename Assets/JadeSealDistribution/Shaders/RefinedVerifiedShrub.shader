Shader "JadeSealDistribution/RefinedVerifiedShrub" {
Properties {_MainTex("Verified colour and opacity",2D)="white"{} _BumpMap("Verified normal",2D)="bump"{} _Tint("Foliage colour multiplier",Color)=(1.65,1.65,1.15,1) _Cutoff("Cutout",Range(0,1))=0.3 _AOStrength("Vertex AO strength",Range(0,1))=.25}
SubShader {Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"} Cull Off LOD 200
CGPROGRAM
#pragma surface surf Standard alphatest:_Cutoff addshadow fullforwardshadows
#pragma target 3.0
sampler2D _MainTex;sampler2D _BumpMap;fixed4 _Tint;half _AOStrength;
struct Input {float2 uv_MainTex;float4 color:COLOR;};
void surf(Input IN,inout SurfaceOutputStandard o){fixed4 c=tex2D(_MainTex,IN.uv_MainTex);o.Albedo=saturate(c.rgb*_Tint.rgb);o.Alpha=c.a;o.Normal=UnpackNormal(tex2D(_BumpMap,IN.uv_MainTex));o.Occlusion=lerp(1,clamp(IN.color.r,.45,1),_AOStrength);o.Smoothness=.1;}
ENDCG
} Fallback "Transparent/Cutout/Diffuse"
}
