Shader "JadeSealDistribution/VerifiedRockTriplanar" {
Properties {_MainTex("Verified CC0 rock colour",2D)="white"{} _WorldScale("Metre texture scale",Float)=0.4}
SubShader {Tags {"RenderType"="Opaque"} LOD 200
CGPROGRAM
#pragma surface surf Standard fullforwardshadows
#pragma target 3.0
sampler2D _MainTex; float _WorldScale;
struct Input {float3 worldPos;float3 worldNormal;};
float2 patch(float2 p){return float2(0.375,0.84)+abs(frac(p*0.5)*2-1)*float2(0.10,0.09);}
void surf(Input IN,inout SurfaceOutputStandard o){float3 w=pow(abs(normalize(IN.worldNormal)),4);w/=max(dot(w,float3(1,1,1)),0.0001);float3 p=IN.worldPos*_WorldScale;o.Albedo=tex2D(_MainTex,patch(p.yz)).rgb*w.x+tex2D(_MainTex,patch(p.xz)).rgb*w.y+tex2D(_MainTex,patch(p.xy)).rgb*w.z;o.Smoothness=0.16;}
ENDCG
} Fallback "Diffuse"
}

