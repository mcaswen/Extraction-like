using System;
using System.Linq;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.View;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>生成并保存正式地图主题、字体和 UGUI 资产；运行时只实例化 Prefab。</summary>
public static class MapGraphUguiPrefabFactory
{
    public const string Folder="Assets/Prefabs/MapGraph/UI";
    public const string ThemePath="Assets/SO/MapGraph/SO_MapGraphTheme_Raid.asset";
    public const string FontPath="Assets/Font/MapGraphChinese SDF.asset";
    public const string HudPath="Assets/Resources/HUD/Pfb_RaidCommandMap.prefab";
    public const string OverlayPath=Folder+"/Pfb_MapGraphOverlay.prefab";
    private const string DefinitionPath="Assets/SO/MapGraph/SO_MapGraphDefinition_Scenezl_Final1.asset";
    [MenuItem("Tools/Map Graph/Create UGUI Prefabs")]
    public static void CreateUguiPrefabs()
    {
        FolderExists(Folder); FolderExists("Assets/SO/MapGraph"); FolderExists("Assets/Resources/HUD");
        var theme=AssetDatabase.LoadAssetAtPath<SO_MapGraphTheme>(ThemePath);
        if(theme==null) {theme=ScriptableObject.CreateInstance<SO_MapGraphTheme>();AssetDatabase.CreateAsset(theme,ThemePath);}
        theme.Font=EnsureFont(); EditorUtility.SetDirty(theme);
        var zone=Zone(theme); var edge=Edge(); var node=Node(theme); var agent=Agent(theme);
        var overlay=Overlay(theme,zone,edge,node,agent); Hud(theme,overlay);
        AssetDatabase.SaveAssets();
    }
    public static void EnsureCurrentAssets()
    {
        var hud=AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        if(hud==null||hud.GetComponentsInChildren<MapGraphSymbolGraphic>(true).Any(x=>x.GetComponent<CanvasRenderer>()==null))CreateUguiPrefabs();
    }
    private static TMP_FontAsset EnsureFont()
    {
        var asset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if(asset==null)
        {
            var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/text-c.ttf");
            if(font==null)throw new InvalidOperationException("缺少正式中文字体 text-c.ttf");
            asset=TMP_FontAsset.CreateFontAsset(font,48,5,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
            asset.name="MapGraphChinese SDF"; AssetDatabase.CreateAsset(asset,FontPath);
            AssetDatabase.AddObjectToAsset(asset.material,asset);
        }
        string characters=string.Concat(Enumerable.Range(32,95).Select(c=>((char)c).ToString()))+
            "区域指挥资源敌人撤离收起展开隐藏显示滚轮缩放拖动平移角色玩家自主规划中进入路线行进等待导航处理反击随后恢复已到达中断战死待命选择后点击群下达暂不可用完成前往此处，·";
        var definition=AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>(DefinitionPath);
        if(definition!=null)characters+=string.Concat(definition.Zones.Select(x=>x.DisplayName));
        if(!asset.TryAddCharacters(characters,out string missing))throw new InvalidOperationException("地图字体缺少字形："+missing);
        foreach(var texture in asset.atlasTextures)if(!AssetDatabase.Contains(texture))AssetDatabase.AddObjectToAsset(texture,asset);
        EditorUtility.SetDirty(asset);return asset;
    }
    private static MapGraphZoneView Zone(SO_MapGraphTheme theme)
    {
        var root=Ui("Pfb_MapGraphZoneView",null,typeof(Image),typeof(MapGraphZoneView));
        var border=Symbol("Border",root.transform,MapGraphSymbol.Border,theme.Border);
        var label=Label("ZoneName",root.transform,theme,14); label.text="区域";
        Bind(root.GetComponent<MapGraphZoneView>(),("_fill",root.GetComponent<Image>()),("_border",border),("_name",label));
        return Save<MapGraphZoneView>(root,Folder+"/Pfb_MapGraphZoneView.prefab");
    }
    private static MapGraphEdgeView Edge()
    {
        var root=Ui("Pfb_MapGraphEdgeView",null,typeof(Image),typeof(MapGraphEdgeView));
        Bind(root.GetComponent<MapGraphEdgeView>(),("_lineImage",root.GetComponent<Image>()));
        return Save<MapGraphEdgeView>(root,Folder+"/Pfb_MapGraphEdgeView.prefab");
    }
    private static MapGraphNodeView Node(SO_MapGraphTheme theme)
    {
        var root=Ui("Pfb_MapGraphNodeView",null,typeof(Image),typeof(MapGraphNodeView));
        var symbol=Symbol("Symbol",root.transform,MapGraphSymbol.Box,theme.Resource);
        var ring=Symbol("Targets",root.transform,MapGraphSymbol.Ring,theme.Route,false);
        var label=Label("TargetAgents",root.transform,theme,11);
        Bind(root.GetComponent<MapGraphNodeView>(),("_bodyImage",root.GetComponent<Image>()),("_symbol",symbol),("_targetRing",ring),("_identity",label));
        return Save<MapGraphNodeView>(root,Folder+"/Pfb_MapGraphNodeView.prefab");
    }
    private static MapGraphAgentView Agent(SO_MapGraphTheme theme)
    {
        var root=Ui("Pfb_MapGraphAgentView",null,typeof(MapGraphAgentView));
        var ring=Symbol("Focus",root.transform,MapGraphSymbol.Ring,theme.Text,false);
        var body=Symbol("Core",root.transform,MapGraphSymbol.Agent,theme.Text);
        var identity=Label("Identity",root.transform,theme,11);Stretch(identity.rectTransform);
        var status=Label("Status",root.transform,theme,10);
        Bind(root.GetComponent<MapGraphAgentView>(),("_body",body),("_ring",ring),("_identity",identity),("_status",status));
        return Save<MapGraphAgentView>(root,Folder+"/Pfb_MapGraphAgentView.prefab");
    }
    private static GameObject Overlay(SO_MapGraphTheme theme,MapGraphZoneView zone,MapGraphEdgeView edge,MapGraphNodeView node,MapGraphAgentView agent)
    {
        var root=Ui("Pfb_MapGraphOverlay",null,typeof(Image),typeof(MapGraphOverlayController),typeof(MapGraphViewport));
        var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(380,350);
        root.GetComponent<Image>().color=theme.Background;
        Symbol("PanelBorder",root.transform,MapGraphSymbol.Border,theme.Border);
        var content=Ui("MapContent",root.transform,typeof(Image),typeof(RectMask2D)); Stretch((RectTransform)content.transform);
        ((RectTransform)content.transform).offsetMin=new Vector2(10,44);((RectTransform)content.transform).offsetMax=new Vector2(-10,-50);
        content.GetComponent<Image>().color=Color.clear;content.GetComponent<Image>().raycastTarget=true;
        var zones=Ui("Zones",content.transform);var edges=Ui("Connections",content.transform);var nodes=Ui("Clusters",content.transform);var agents=Ui("Agents",content.transform);
        var title=Label("Title",root.transform,theme,14);Header(title,new Vector2(18,-17),new Vector2(160,28));title.alignment=TextAlignmentOptions.Left;
        var hint=Label("Hint",root.transform,theme,10);Header(hint,new Vector2(-18,-17),new Vector2(300,24),true);hint.alignment=TextAlignmentOptions.Right;
        var legend=Label("Legend",root.transform,theme,10);Footer(legend,new Vector2(18,30),new Vector2(320,18));legend.alignment=TextAlignmentOptions.Left;
        var detail=Label("Detail",root.transform,theme,10);Footer(detail,new Vector2(18,13),new Vector2(900,20));detail.alignment=TextAlignmentOptions.Left;
        Bind(root.GetComponent<MapGraphOverlayController>(),("_content",content.transform),("_backgroundImage",root.GetComponent<Image>()),
            ("_zonesRoot",zones.transform),("_edgesRoot",edges.transform),("_nodesRoot",nodes.transform),("_agentsRoot",agents.transform),
            ("_zoneViewPrefab",zone),("_edgeViewPrefab",edge),("_nodeViewPrefab",node),("_agentViewPrefab",agent),
            ("_title",title),("_hint",hint),("_legend",legend),("_detail",detail));
        Bind(root.GetComponent<MapGraphViewport>(),("_panel",root.transform),("_content",content.transform));
        return Save(root,OverlayPath);
    }
    private static void Hud(SO_MapGraphTheme theme,GameObject overlay)
    {
        var root=Ui("Pfb_RaidCommandMap",null,typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(AgentGraphProjectionController),typeof(MapGraphPresenter));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=200;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var backdrop=Ui("ExpandedBackdrop",root.transform,typeof(Image));Stretch((RectTransform)backdrop.transform);backdrop.GetComponent<Image>().color=new Color(.02f,.03f,.035f,.78f);
        var panel=(GameObject)PrefabUtility.InstantiatePrefab(overlay,root.transform);
        Bind(panel.GetComponent<MapGraphViewport>(),("_canvas",canvas),("_backdrop",backdrop));
        Bind(root.GetComponent<MapGraphPresenter>(),("_theme",theme),("_overlay",panel.GetComponent<MapGraphOverlayController>()),
            ("_viewport",panel.GetComponent<MapGraphViewport>()),("_projection",root.GetComponent<AgentGraphProjectionController>()));
        Save(root,HudPath);
    }
    private static GameObject Ui(string name,Transform parent,params Type[] components)
    {
        var all=new Type[components.Length+1];all[0]=typeof(RectTransform);Array.Copy(components,0,all,1,components.Length);
        var root=new GameObject(name,all);root.layer=LayerMask.NameToLayer("UI");
        if(parent!=null)root.transform.SetParent(parent,false);
        var rect=(RectTransform)root.transform;rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.sizeDelta=Vector2.zero;
        return root;
    }
    private static MapGraphSymbolGraphic Symbol(string name,Transform parent,MapGraphSymbol shape,Color color,bool stretch=true)
    {var root=Ui(name,parent,typeof(MapGraphSymbolGraphic));if(stretch)Stretch((RectTransform)root.transform);var graphic=root.GetComponent<MapGraphSymbolGraphic>();graphic.Configure(shape,color);return graphic;}
    private static TMP_Text Label(string name,Transform parent,SO_MapGraphTheme theme,float size)
    {
        var root=Ui(name,parent,typeof(TextMeshProUGUI));var label=root.GetComponent<TextMeshProUGUI>();
        label.font=theme.Font;label.fontSize=size;label.text=string.Empty;label.color=theme.Text;label.alignment=TextAlignmentOptions.Center;
        label.enableWordWrapping=false;label.overflowMode=TextOverflowModes.Ellipsis;label.raycastTarget=false;return label;
    }
    private static void Header(TMP_Text label,Vector2 offset,Vector2 size,bool right=false)
    {var r=label.rectTransform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(right?1:0,1);r.anchoredPosition=offset;r.sizeDelta=size;}
    private static void Footer(TMP_Text label,Vector2 offset,Vector2 size)
    {var r=label.rectTransform;r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;r.anchoredPosition=offset;r.sizeDelta=size;}
    private static void Stretch(RectTransform rect) {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    private static void Bind(UnityEngine.Object obj,params (string,UnityEngine.Object)[] fields)
    {var serialized=new SerializedObject(obj);foreach(var field in fields)serialized.FindProperty(field.Item1).objectReferenceValue=field.Item2;serialized.ApplyModifiedPropertiesWithoutUndo();}
    private static T Save<T>(GameObject root,string path)where T:Component=>Save(root,path).GetComponent<T>();
    private static GameObject Save(GameObject root,string path)
    {var saved=PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);return saved;}
    private static void FolderExists(string path)
    {var parts=path.Split('/');string current=parts[0];for(int i=1;i<parts.Length;i++){string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}}
}
