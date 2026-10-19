using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Gameplay.MapGraph.View;
using Gameplay.Agent.Runtime;
using Gameplay.Agent.Progression;
using ExtractionLike.Aerospace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Runs only when explicitly invoked in an isolated graphical Editor.
[InitializeOnLoad]
public static class AstronUiVisualReview
{
    const string Key = "AstronUiReview";
    const string RaidScene = "Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
    static double next;
    static int stage;
    static int mutationFrame;
    static string output;
    static readonly List<string> checks = new List<string>();
    static readonly List<string> errors = new List<string>();
    static bool stopping;
    static AstronUiVisualReview() { EditorApplication.update += Tick; Application.logMessageReceived += Log; }
    static void Log(string message, string stack, LogType type)
    { if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception)) errors.Add(message); }
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs();
        int outputIndex=Array.IndexOf(args,"-astronReviewOutput");
        if(outputIndex<0||outputIndex+1>=args.Length) throw new ArgumentException("-astronReviewOutput is required.");
        if(Directory.Exists(".git")||File.Exists(".git")) throw new InvalidOperationException("Run visual review in an isolated project copy, not the working repository.");
        output = args[outputIndex + 1];
        Directory.CreateDirectory(output);
        File.Delete(Path.Combine(output,"failure.txt"));
        File.Delete(Path.Combine(output,"result.json"));
        SessionState.SetBool(Key + "Done", false);
        if (args.Contains("-astronInstall")) AstronUiAssetBuilder.Rebuild();
        SessionState.SetString(Key + "Output", output);
        // Isolate save data and prefs from the user's normal play session.
        PlayerSettings.companyName = "AnomalySearch.Automation";
        PlayerSettings.productName = "AgentRepro_AstronUiVisualReview";
        UnityEngine.Random.InitState(731);
        EditorSettings.enterPlayModeOptionsEnabled = false;
        EditorSceneManager.OpenScene("Assets/Scenes/Scene_MainMenu.unity");
        SetSize(1920, 1080);
        QualitySettings.SetQualityLevel(2, true);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        if (SessionState.GetBool(Key + "Done", false) && !EditorApplication.isPlayingOrWillChangePlaymode)
        { SessionState.SetBool(Key + "Done", false); EditorApplication.Exit(0); return; }
        if (!SessionState.GetBool(Key, false)) return;
        output = SessionState.GetString(Key + "Output", "");
        if (stopping)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); }
            return;
        }
        if (!EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            var map = Object.FindObjectOfType<MapGraphPresenter>();
            switch(stage)
            {
                case 0: Application.runInBackground=true; Wait(3); break;
                case 1: Capture("01-menu-1920"); break;
                case 2: SetSize(1280,720); Wait(1); break;
                case 3: Capture("02-menu-1280"); break;
                case 4:
                    Click(GameObject.Find("Start Game Button"));
                    Wait(2); break;
                case 5:
                    Require(SceneManager.GetActiveScene().name == "Scene_ElementSelectionMenu", "Start button enters attribute selection");
                    SetSize(1920,1080);
                    SceneManager.LoadScene(RaidScene); Wait(8); break;
                case 6:
                    Require(map != null, "Formal raid command map instantiated");
                    Require(Object.FindObjectOfType<PlayerStatusHudController>() != null, "Player status HUD instantiated");
                    Require(map.Overlay.NodeViews.Count > 0 && map.Overlay.EdgeViews.Count > 0, "Live nodes and edges present");
                    CheckNoArchiveHud();
                    Time.timeScale = 0;
                    map.Viewport.SetExpanded(false); CheckMapArtwork(map); Wait(1); break;
                case 7: Capture("03-raid-1920"); break;
                case 8: map.Viewport.SetExpanded(true); Wait(1); break;
                case 9: Capture("04-map-expanded-1920"); break;
                case 10:
                    var pointer = new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0,2), button = PointerEventData.InputButton.Left, delta = new Vector2(60,30) };
                    map.Viewport.OnScroll(pointer); map.Viewport.OnDrag(pointer);
                    Require(map.Viewport.Zoom > 1 && map.Viewport.Pan != Vector2.zero, "Map zoom and pan work"); Wait(1); break;
                case 11: Capture("05-map-zoomed-1920"); break;
                case 12:
                    map.Viewport.SetVisible(false);
                    Require(!map.Viewport.IsVisible, "Map can hide");
                    map.Viewport.SetVisible(true); map.Viewport.SetExpanded(false);
                    SetSize(1280,720); Wait(1); break;
                case 13: Capture("06-raid-1280"); break;
                case 14:
                    SetSize(1920,1080);
                    var archive = Object.FindObjectOfType<AerospaceScienceUI>();
                    Require(archive != null, "Archive remains available through the existing runtime");
                    // Exercise the same Open path used by J without recreating a persistent button.
                    archive.Open(null, false); Wait(2); break;
                case 15: Capture("07-archive-open-1920"); break;
                case 16:
                    Require(Object.FindObjectOfType<AerospaceScienceUI>().IsOpen, "Archive opens without a persistent HUD entrance");
                    Object.FindObjectOfType<AerospaceScienceUI>().Close();
                    InventoryScreenController.Instance.OpenInventory(); Wait(1); break;
                case 17:
                    Require(InventoryScreenController.Instance.IsInventoryOpen, "Inventory still opens");
                    Require(!Object.FindObjectOfType<PlayerStatusHudController>().transform.Find("PlayerStatusHudRoot").gameObject.activeSelf,"Status HUD does not cover inventory");
                    CheckNoArchiveHud();
                    Capture("08-inventory-1920"); break;
                case 18:
                    InventoryScreenController.Instance.CloseInventory();
                    Require(!InventoryScreenController.Instance.IsInventoryOpen, "Inventory still closes");
                    AgentRuntimeRegistry.ActiveInstance.TryGetFocusedHandle(out var handle);
                    handle.PawnRoot.TakeCombatDamage(100, handle.PawnRoot.transform.position, Vector3.forward, null);
                    handle.PawnRoot.GetComponent<AgentLevelProgressionController>().AddExperience(AgentExperienceSourceKind.LowTierLootBox);
                    var item=Resources.Load<InventoryItemDatabase>("Inventory/InventoryItemDatabase").Items
                        .First(x=>x!=null&&x.Type==ItemType.Medical);
                    Require(InventoryScreenController.Instance.TryPickupItem(item,1),"Inventory accepts an item");
                    mutationFrame=Time.frameCount; Wait(2); break;
                case 19:
                    Require(Time.frameCount>mutationFrame,"Runtime advanced after value changes");
                    Capture("09-hud-values-1920"); CheckBars(); break;
                case 20:
                    map.Viewport.SetExpanded(true); CheckMapArtwork(map); Wait(1); break;
                case 21:
                    int submitted = map.SubmittedCommandCount;
                    Click(map.Overlay.NodeViews.Values.First().gameObject);
                    Require(map.SubmittedCommandCount==submitted+1,"Map artwork does not block target selection");
                    Wait(1); break;
                case 22: Capture("10-map-target-1920"); break;
                case 23:
                    SetSize(1440,900); map.Viewport.SetExpanded(false); Wait(1); break;
                case 24: Capture("11-raid-1440x900"); break;
                case 25:
                    SceneManager.LoadScene("Scene_MainMenu"); Wait(2); break;
                case 26: Capture("12-menu-1440x900"); break;
                case 27:
                    foreach(string name in new[]{"Start Game Button","Settings Button","Exit Game Button"})
                    {
                        var button=GameObject.Find(name).GetComponent<Button>();
                        var corners=new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
                        Require(corners.All(c=>c.x>=0&&c.x<=Screen.width&&c.y>=0&&c.y<=Screen.height),name+" is inside the 16:10 viewport");
                        Require(button.onClick.GetPersistentEventCount()==1,name+" retains its action");
                    }
                    Require(Directory.GetFiles(output,"*.png").Length==12,"All 12 screenshots saved");
                    File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(new Result { checks=checks.ToArray(), errors=errors.ToArray(), graphics=SystemInfo.graphicsDeviceName },true));
                    SessionState.SetBool(Key + "Done",true); SessionState.SetBool(Key,false); Time.timeScale=1; EditorApplication.isPlaying=false;
                    stopping=true; break;
            }
            stage++;
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(output,"failure.txt"),ex.ToString());
            SessionState.SetBool(Key,false); EditorApplication.isPlaying=false; EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }
    static void CheckNoArchiveHud()
    {
        var runtime=Object.FindObjectOfType<AerospaceCollectionRuntime>();
        Require(runtime!=null,"Aerospace runtime remains available");
        var children=runtime.GetComponentsInChildren<Transform>(true);
        foreach(string name in new[]{"Open_Aerospace_Archive","Grant_Five_Ship_Parts"})
            Require(!children.Any(t=>t.name==name),"Removed persistent button is absent: "+name);
    }
    static void CheckMapArtwork(MapGraphPresenter map)
    {
        Canvas.ForceUpdateCanvases();
        foreach(string name in new[]{"Title","Hint","Legend","Detail"})
            Require(!map.Overlay.transform.Find(name).gameObject.activeSelf,"Map peripheral label hidden: "+name);
        var content=map.Overlay.Content;
        var image=(RectTransform)content.Find("IllustratedMap");
        Require(image.gameObject.activeSelf,"Formal map uses the matching illustration");
        Require(image.rect.width>=content.rect.width-.1f&&image.rect.height>=content.rect.height-.1f,"Map background covers viewport");
        Vector2 artworkSize=image.GetComponent<Image>().sprite.rect.size;
        Require(Mathf.Abs(image.rect.width/image.rect.height-artworkSize.x/artworkSize.y)<.001f,"Map artwork keeps its original aspect ratio");
        Require(Mathf.Abs(content.rect.width/content.rect.height-artworkSize.x/artworkSize.y)<.001f,"Map viewport shows the full artwork without cropping");
        // Landmarks measured from the user's reference, normalized from its bottom-left corner.
        CheckReferenceAnchor(map,new Vector2(-480,420),new Vector2(.137f,.802f),"Laboratory upper-left resource");
        CheckReferenceAnchor(map,new Vector2(-30,-240),new Vector2(.438f,.201f),"Tower lower resource");
        CheckReferenceAnchor(map,new Vector2(390,-330),new Vector2(.719f,.119f),"Dormitory lower resource");
        foreach(var node in map.Overlay.NodeViews.Values)
            Require(content.rect.Contains(content.InverseTransformPoint(node.RectTransform.position)),"Visible node: "+node.NodeId);
    }
    static void CheckReferenceAnchor(MapGraphPresenter map,Vector2 graphPoint,Vector2 expected,string name)
    {
        Rect rect=map.Overlay.Content.rect;
        Vector2 actual=map.Overlay.MapPosition(graphPoint)-rect.min;
        actual=new Vector2(actual.x/rect.width,actual.y/rect.height);
        Require(Vector2.Distance(actual,expected)<.005f,"Reference alignment: "+name);
    }
    static void CheckBars()
    {
        var hud=Object.FindObjectOfType<PlayerStatusHudController>();
        var all=hud.GetComponentsInChildren<RectTransform>(true);
        AgentRuntimeRegistry.ActiveInstance.TryGetFocusedHandle(out var handle);
        var progression=handle.PawnRoot.GetComponent<AgentLevelProgressionController>();
        string[] fills={"HealthFill","CarryFill","ExperienceFill"};
        string[] bars={"HealthBarProgress","CarryLoadBarProgress","ExperienceBarProgress"};
        float[] ratios={(float)handle.ReadOnly.CurrentHealth/handle.ReadOnly.MaxHealth,InventoryScreenController.Instance.GetBackpackCapacityRatio(),progression.ExperienceRatio};
        File.WriteAllText(Path.Combine(output,"bar-data.txt"),"HUD enabled="+hud.enabled+" active="+hud.gameObject.activeInHierarchy+" frame="+Time.frameCount+" mutation="+mutationFrame+"\n"+
            string.Join("\n",all.Where(r=>fills.Contains(r.name)||bars.Contains(r.name)).Select(r=>r.name+"="+r.rect+" sizeDelta="+r.sizeDelta))+"\nratios="+string.Join(",",ratios));
        for(int i=0;i<3;i++)
        {
            float actual=all.First(r=>r.name==fills[i]).rect.width/all.First(r=>r.name==bars[i]).rect.width;
            Require(Mathf.Abs(actual-Mathf.Clamp01(ratios[i]))<.01f,fills[i]+" follows live data (actual="+actual+", expected="+ratios[i]+")");
        }
        Require(ratios[0]>0&&ratios[0]<1,"Partial health fill exercised");
        Require(ratios[1]>0,"Occupied backpack fill exercised");
        Require(ratios[2]>0&&ratios[2]<1,"Partial experience fill exercised");
    }
    static void Wait(double seconds) { next=EditorApplication.timeSinceStartup+seconds; }
    static void Require(bool condition,string name) { if(!condition)throw new InvalidOperationException(name);checks.Add(name); }
    static void Click(GameObject go)
    {
        Require(go != null,"Click target exists");
        Canvas.ForceUpdateCanvases();
        var rect=go.GetComponent<RectTransform>();
        var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        var hit=hits.FirstOrDefault().gameObject;
        Require(hit!=null&&(hit==go||hit.transform.IsChildOf(go.transform)),"Raycast reaches "+go.name);
        ExecuteEvents.ExecuteHierarchy(hit,data,ExecuteEvents.pointerClickHandler);
    }
    static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));
        Wait(1);
    }
    static void SetSize(int width,int height)
    {
        const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        var asm=typeof(UnityEditor.Editor).Assembly;
        var sizesType=asm.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
        var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{0});
        var size=Activator.CreateInstance(asm.GetType("UnityEditor.GameViewSize"),flags,null,new object[]{Enum.ToObject(asm.GetType("UnityEditor.GameViewSizeType"),1),width,height,"Astron review"},null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
        int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
        var type=asm.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);
        type.GetProperty("selectedSizeIndex",flags).SetValue(view,count-1);
        view.maximized=true;view.Focus();view.Repaint();
    }
    [Serializable] class Result { public string graphics; public string[] checks,errors; }
}
