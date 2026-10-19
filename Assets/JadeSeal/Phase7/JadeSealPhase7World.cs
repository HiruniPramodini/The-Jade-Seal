using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Unity.Profiling;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace JadeSeal.World
{
    [Serializable] public sealed class JadeSealPhase7Route { public string name;public Vector3[] points; }
    public sealed class JadeSealPhase7World : MonoBehaviour
    { public JadeSealPhase7Route[] routes;public string geometryFingerprint;public int agentType;public string approvedScene; }
    /// <summary>Temporary Play Mode-only capsule and profiler. No combat/AI/progression.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class JadeSealPhase7Tester : MonoBehaviour
    {
        [Serializable] public sealed class CaseResult{public string name,result;public int reached,total;public float maximumHeightError;public double seconds;public Vector3 finalPosition;}
        [Serializable] public sealed class Result
        { public string utc,scene,unity,cpu,gpu,folder,limitations;public bool editor=true,complete,allPassed;public int width,height,frames,unfocusedFrames,vSync,targetFps;public double averageFps,minimumFps,meanFrameMs,p95FrameMs,maximumFrameMs,averageDrawCalls=-1,averageTriangles=-1,averageMainThreadMs=-1;public long peakDrawCalls=-1,peakTriangles=-1,peakAllocatedBytes,peakReservedBytes,peakMonoUsedBytes;public CaseResult[] traversal;public string[] stalls; }
        public bool complete,manual;public string status;public string exportFolder;
        CharacterController capsule;Camera view;RenderTexture target;JadeSealPhase7World plan;int caseIndex,waypoint;double caseStart,waypointStart,start,last;float vertical,pitch,maxHeight;readonly List<CaseResult> cases=new List<CaseResult>();readonly List<double> frames=new List<double>();readonly List<string> stalls=new List<string>();readonly List<Behaviour> suspended=new List<Behaviour>();ProfilerRecorder draw,tri,main;double dcSum,trSum,mainSum,total;int dcN,trN,mainN;Result report;StreamWriter csv;bool previousBackground;
        static readonly ProfilerMarker MoveMarker=new ProfilerMarker("JadeSeal.Phase7.CapsuleTraversal");
        public static JadeSealPhase7Tester Create(JadeSealPhase7World p,bool manualMode)
        {
            var go=new GameObject("PHASE7_TEMPORARY_CAPSULE_TESTER_REMOVE_BY_EXITING_PLAY_MODE");var c=go.AddComponent<CharacterController>();c.height=1.8f;c.radius=.32f;c.center=Vector3.up*.9f;c.skinWidth=.025f;c.stepOffset=.3f;c.slopeLimit=45;c.minMoveDistance=0;
            var camera=new GameObject("Phase7_PlayerHeight_1600x900").AddComponent<Camera>();camera.transform.SetParent(go.transform,false);camera.transform.localPosition=Vector3.up*1.62f;camera.fieldOfView=68;camera.nearClipPlane=.08f;camera.farClipPlane=200;
            var r=go.AddComponent<JadeSealPhase7Tester>();r.plan=p;r.manual=manualMode;r.capsule=c;r.view=camera;r.Begin();return r;
        }
        void Begin()
        {
            previousBackground=Application.runInBackground;Application.runInBackground=true;
            foreach(var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))if(c!=view&&c.enabled){suspended.Add(c);c.enabled=false;}
            foreach(var a in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))if(a.enabled){suspended.Add(a);a.enabled=false;}
            target=new RenderTexture(1600,900,24){name="Phase7_Profiler_Render_Target"};target.Create();if(!manual)view.targetTexture=target;
            exportFolder=Path.GetFullPath(Path.Combine(Application.dataPath,"../JadeSealEvidence/Phase7/Runs",DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")));Directory.CreateDirectory(exportFolder);File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../JadeSealEvidence/Phase7/LatestRun.txt")),exportFolder);
            report=new Result{utc=DateTime.UtcNow.ToString("O"),scene=gameObject.scene.path,unity=Application.unityVersion,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,folder=exportFolder,width=manual?Screen.width:1600,height=manual?Screen.height:900,vSync=QualitySettings.vSyncCount,targetFps=Application.targetFrameRate,limitations="Fresh Phase 7 Editor Play Mode measurements at 1600x900 offscreen camera resolution. Includes Editor overhead and tool interaction; global profiler counters may include other views/shadow passes and lag. No standalone/GPU timing benchmark. Average FPS=frames/summed wall-frame time; minimum=1000/slowest frame ms. Five-second warmup excluded; no other outliers removed. Capsule automation uses physical CharacterController.Move with gravity, no jump, no NavMeshAgent and only resets at independent case starts. Human control feel/visual approval still requires manual review."};
            draw=Try(ProfilerCategory.Render,"Draw Calls Count");tri=Try(ProfilerCategory.Render,"Triangles Count");main=Try(ProfilerCategory.Internal,"Main Thread");csv=new StreamWriter(Path.Combine(exportFolder,"frames.csv"));csv.WriteLine("frame,frameMs,drawCalls,triangles,mainThreadMs,allocatedBytes,reservedBytes,monoUsedBytes,case,waypoint,x,y,z,grounded,focused");start=last=Time.realtimeSinceStartupAsDouble;StartCase();
        }
        static ProfilerRecorder Try(ProfilerCategory c,string n){try{return ProfilerRecorder.StartNew(c,n,1);}catch{return default;}}
        static long Value(ProfilerRecorder r)=>r.Valid&&r.Count>0?r.LastValue:-1;
        void StartCase(){var route=plan.routes[caseIndex];capsule.enabled=false;transform.position=route.points[0]+Vector3.up*.04f;capsule.enabled=true;waypoint=1;vertical=-2;maxHeight=0;caseStart=waypointStart=Time.realtimeSinceStartupAsDouble;status=route.name;}
        void FinishCase(string result)
        {var route=plan.routes[caseIndex];cases.Add(new CaseResult{name=route.name,result=result,reached=Mathf.Min(waypoint,route.points.Length),total=route.points.Length,seconds=Time.realtimeSinceStartupAsDouble-caseStart,maximumHeightError=maxHeight,finalPosition=transform.position});caseIndex++;if(caseIndex>=plan.routes.Length)Finish();else StartCase();}
        void Update()
        {
            if(complete)return;float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);Vector3 direction=Vector3.zero;
            if(manual){
#if ENABLE_INPUT_SYSTEM
                var k=Keyboard.current;var m=Mouse.current;if(k!=null){var v=new Vector3((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),0,(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));direction=transform.TransformDirection(v.normalized);if(k.escapeKey.wasPressedThisFrame){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}if(k.f10Key.wasPressedThisFrame){Finish();return;}}
                if(m!=null){if(m.leftButton.wasPressedThisFrame){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}if(Cursor.lockState==CursorLockMode.Locked){var d=m.delta.ReadValue();transform.Rotate(0,d.x*.12f,0);pitch=Mathf.Clamp(pitch-d.y*.12f,-80,80);view.transform.localRotation=Quaternion.Euler(pitch,0,0);}}
#endif
            }else if(Time.realtimeSinceStartupAsDouble-start>5){var route=plan.routes[caseIndex];var goal=route.points[waypoint];var delta=goal-transform.position;delta.y=0;float error=Mathf.Abs(transform.position.y-goal.y);if(delta.magnitude<.2f&&error<.55f){waypoint++;waypointStart=Time.realtimeSinceStartupAsDouble;if(waypoint>=route.points.Length){FinishCase("PASS");return;}}else if(Time.realtimeSinceStartupAsDouble-waypointStart>12){FinishCase("FAIL: stalled at waypoint "+waypoint+" expected="+goal+" actual="+transform.position);return;}else{direction=delta.normalized;if(direction.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(direction);maxHeight=Mathf.Max(maxHeight,error);}}
            using(MoveMarker.Auto()){if(capsule.isGrounded&&vertical<0)vertical=-2;vertical=Mathf.Max(-25,vertical-20*dt);capsule.Move(direction*(manual?4:6)*dt+Vector3.up*vertical*dt);}if(transform.position.y< -5){if(manual)StartCase();else FinishCase("FAIL: fell below world");}
        }
        void LateUpdate()
        {
            if(complete)return;double now=Time.realtimeSinceStartupAsDouble,ms=(now-last)*1000;last=now;if(now-start<5||ms<=0)return;frames.Add(ms);total+=ms/1000;long dc=Value(draw),tr=Value(tri),mt=Value(main);long allocated=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),reserved=UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong(),mono=UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();report.peakAllocatedBytes=Math.Max(report.peakAllocatedBytes,allocated);report.peakReservedBytes=Math.Max(report.peakReservedBytes,reserved);report.peakMonoUsedBytes=Math.Max(report.peakMonoUsedBytes,mono);if(dc>=0){dcSum+=dc;dcN++;report.peakDrawCalls=Math.Max(report.peakDrawCalls,dc);}if(tr>=0){trSum+=tr;trN++;report.peakTriangles=Math.Max(report.peakTriangles,tr);}if(mt>=0){mainSum+=mt/1000000d;mainN++;}if(!Application.isFocused)report.unfocusedFrames++;if(ms>100&&stalls.Count<200)stalls.Add("frame="+frames.Count+" ms="+ms.ToString("F2",CultureInfo.InvariantCulture)+" case="+status+" waypoint="+waypoint+" mainThreadMs="+(mt/1000000d).ToString("F2",CultureInfo.InvariantCulture));var p=transform.position;csv.WriteLine(string.Join(",",new[]{frames.Count.ToString(),ms.ToString("F5",CultureInfo.InvariantCulture),dc.ToString(),tr.ToString(),(mt/1000000d).ToString("F5",CultureInfo.InvariantCulture),allocated.ToString(),reserved.ToString(),mono.ToString(),caseIndex.ToString(),waypoint.ToString(),p.x.ToString("F4",CultureInfo.InvariantCulture),p.y.ToString("F4",CultureInfo.InvariantCulture),p.z.ToString("F4",CultureInfo.InvariantCulture),capsule.isGrounded?"1":"0",Application.isFocused?"1":"0"}));
        }
        public void Finish(){if(complete)return;complete=true;csv?.Dispose();csv=null;report.complete=true;report.allPassed=!manual&&cases.Count==plan.routes.Length&&cases.All(c=>c.result=="PASS");report.traversal=cases.ToArray();report.frames=frames.Count;report.stalls=stalls.ToArray();if(frames.Count>0){var sorted=frames.OrderBy(v=>v).ToArray();report.averageFps=frames.Count/total;report.meanFrameMs=total*1000/frames.Count;report.maximumFrameMs=sorted.Last();report.minimumFps=1000/sorted.Last();report.p95FrameMs=sorted[Mathf.Clamp(Mathf.CeilToInt(sorted.Length*.95f)-1,0,sorted.Length-1)];}if(dcN>0)report.averageDrawCalls=dcSum/dcN;if(trN>0)report.averageTriangles=trSum/trN;if(mainN>0)report.averageMainThreadMs=mainSum/mainN;File.WriteAllText(Path.Combine(exportFolder,"summary.json"),JsonUtility.ToJson(report,true));draw.Dispose();tri.Dispose();main.Dispose();status="Complete: "+report.allPassed;}
        void OnDestroy(){if(report!=null&&!complete)Finish();foreach(var b in suspended)if(b!=null)b.enabled=true;if(view!=null)view.targetTexture=null;if(target!=null){target.Release();Destroy(target);}Application.runInBackground=previousBackground;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
}

