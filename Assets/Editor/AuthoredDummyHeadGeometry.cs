using System.Collections.Generic;
using UnityEngine;
// Original project-authored latitude/longitude geometry, 2026-10-19.
// Inputs are approved centers and radius only. No Unity primitive mesh, source
// vertex array, template sphere, or extracted engine asset is read or sampled.
public static class AuthoredDummyHeadGeometry {
 public const int LongitudeSegments=12, LatitudeSegments=8;
 public const string Method="Original analytic latitude/longitude sphere, 12x8 cells, radial normals, equirectangular UV0; poles omit degenerate triangles";
 public static void Append(Vector3 center,float radius,List<Vector3> vertices,List<Vector3> normals,List<Vector4> tangents,List<Vector2> uv,List<int> indices){
  int first=vertices.Count;
  for(int j=0;j<=LatitudeSegments;j++){
   float phi=Mathf.PI*j/LatitudeSegments,sy=Mathf.Cos(phi),ring=j==0||j==LatitudeSegments?0:Mathf.Sin(phi);
   for(int i=0;i<=LongitudeSegments;i++){
    float theta=2*Mathf.PI*i/LongitudeSegments;
    var n=new Vector3(ring*Mathf.Cos(theta),sy,ring*Mathf.Sin(theta));
    vertices.Add(center+n*radius);normals.Add(n);tangents.Add(new Vector4(-Mathf.Sin(theta),0,Mathf.Cos(theta),-1));uv.Add(new Vector2(i/(float)LongitudeSegments,1-j/(float)LatitudeSegments));
   }
  }
  for(int j=0;j<LatitudeSegments;j++)for(int i=0;i<LongitudeSegments;i++){
   int a=first+j*(LongitudeSegments+1)+i,b=a+1,c=a+LongitudeSegments+1,d=c+1;
   if(j>0){indices.Add(a);indices.Add(b);indices.Add(c);}
   if(j<LatitudeSegments-1){indices.Add(b);indices.Add(d);indices.Add(c);}
  }
 }
}
