using UnityEngine;
namespace JadeSeal.Distribution {
// Candidate-only mesh LOD: preserves original transform hierarchy and collision proxies.
// Uses the actual rendering camera, including offscreen test cameras. No allocations per render.
[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
public sealed class VerifiedShrubLod : MonoBehaviour {
 public Mesh[] levels; public float mediumScreenHeight=.055f,lowScreenHeight=.022f;
 MeshFilter filter;MeshRenderer view;public int currentLevel;
 void Awake(){filter=GetComponent<MeshFilter>();view=GetComponent<MeshRenderer>();}
 void OnWillRenderObject(){if(!Application.isPlaying||levels==null||levels.Length!=3)return;
 var c=Camera.current;if(!c||c.cameraType==CameraType.Reflection||c.cameraType==CameraType.SceneView)return;
 var b=view.bounds;float size=Mathf.Max(b.size.x,Mathf.Max(b.size.y,b.size.z));
 float height=c.orthographic?size/(2*c.orthographicSize):size/(2*Mathf.Max(.1f,Vector3.Distance(c.transform.position,b.center))*Mathf.Tan(c.fieldOfView*Mathf.Deg2Rad*.5f));
 int lod=height>mediumScreenHeight?0:height>lowScreenHeight?1:2;
 if(filter.sharedMesh!=levels[lod])filter.sharedMesh=levels[lod];currentLevel=lod;
 }
 void OnDisable(){if(filter&&levels!=null&&levels.Length>0&&levels[0])filter.sharedMesh=levels[0];}
}
}
