using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Automation.SceneRaid;
using AnomalySearch.Automation.SceneRaid.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.View;
using Gameplay.Raid;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace AnomalySearch.Editor.SceneRaid
{
    /// <summary>只在有限指挥诊断中采样实际 GameView，独立 Player 性能不安装。</summary>
    internal sealed class SceneRaidMapVisualCapture
    {
        private readonly string _output;
        private readonly HashSet<string> _captured=new HashSet<string>();
        private MapGraphPresenter _map;
        private double _next;
        private bool _originalExpanded;
        internal SceneRaidMapVisualCapture(SceneRaidScenarioConfig config)
        { _output=Path.Combine(config.outputPath,"map-visual"); }
        internal void Tick()
        {
            double now=EditorApplication.timeSinceStartup;if(now<_next||_captured.Count>=9)return;_next=now+.1;
            if(_map==null)
            {
                _map=Object.FindObjectOfType<MapGraphPresenter>();
                if(_map==null)return;
                _originalExpanded=_map.Viewport.IsExpanded;_next=now+.5;return;
            }
            if(!_captured.Contains("01-compact"))
            { _map.Viewport.SetExpanded(false);Capture("01-compact");CaptureCache();_map.Viewport.SetExpanded(true);_next=now+.5;return; }
            if(!_captured.Contains("02-expanded")){Capture("02-expanded");return;}
            var rows=SceneRaidRouteEvidence.CaptureDisplay(_map);
            string name=null;
            if(rows.Any(x=>x.retaliating))name="03-retaliation";
            if(name==null||_captured.Contains(name))
                name=rows.Any(x=>x.mode=="Waiting")?"04-waiting":null;
            if(name==null||_captured.Contains(name))
                name=rows.Any(x=>x.mode=="Extracting")?"05-extracting":null;
            if(name==null||_captured.Contains(name))
                name=rows.Any(x=>x.onEdge&&x.validDistance&&x.progress>.25f&&x.progress<.75f)?"06-mid-edge":null;
            if(name==null||_captured.Contains(name))
                name=rows.Any(x=>x.onEdge&&x.validDistance&&x.progress>.85f)?"07-near-anchor":null;
            if(name==null||_captured.Contains(name))
                name=rows.Count(x=>x.hasMarker&&!string.IsNullOrEmpty(x.requestId))>=2?"08-two-agents":null;
            if(name==null||_captured.Contains(name))
                name=rows.Any(x=>x.mode=="Processing")?"09-processing":null;
            if(name!=null&&!_captured.Contains(name)){Capture(name);_next=now+.35;}
        }
        private void Capture(string name)
        {
            var view=Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(x=>x.GetType().FullName=="UnityEditor.GameView");
            if(view==null)return;
            Directory.CreateDirectory(_output);
            double before=EditorApplication.timeSinceStartup;
            var pixels=UnityEditorViewCapture.Capture(view,Path.Combine(_output,name+".png"));
            var record=new Visual{utc=DateTime.UtcNow.ToString("o"),name=name,width=pixels.x,height=pixels.y,
                expanded=_map.Viewport.IsExpanded,timeScale=Time.timeScale,captureSeconds=EditorApplication.timeSinceStartup-before,
                roots=Object.FindObjectsOfType<AgentPawnRoot>().Select(SceneRaidRouteEvidence.Capture).ToArray(),
                displays=SceneRaidRouteEvidence.CaptureDisplay(_map)};
            File.WriteAllText(Path.Combine(_output,name+".json"),JsonUtility.ToJson(record,true));_captured.Add(name);
        }
        private void CaptureCache()
        {
            var installer=Object.FindObjectOfType<RaidMapCommandInstaller>();if(installer==null)return;
            string signature=RuntimeFixtureAccess.Read<string>(installer,"_navigationFingerprint");
            var rows=Object.FindObjectsOfType<AgentPawnRoot>().Select(p=>
            {
                var profile=AgentNavigationProfile.FromAgent(p.NavMeshAgent);
                var service=new MapGraphNavigationCostService(installer.Binding.MapDefinition,installer.Binding,profile,"","",signature);
                return new CacheAgent{agent=p.AgentIdValue,reason=service.LastInvalidationReason,pending=service.PendingEdgeCount,
                    profile=JsonUtility.ToJson(MapGraphNavigationCostService.CaptureProfile(profile))};
            }).ToArray();
            File.WriteAllText(Path.Combine(_output,"startup-cache.json"),JsonUtility.ToJson(new Cache{
                saved=installer.Binding.MapDefinition.NavigationBake.RuntimeNavigationFingerprint,installed=signature,
                current=MapGraphNavigationFingerprint.Capture(SceneManager.GetActiveScene()),queries=installer.Environments.CalculationCount,agents=rows},true));
        }
        internal void Dispose(){if(_map!=null)_map.Viewport.SetExpanded(_originalExpanded);_map=null;}
        [Serializable]private sealed class Visual{public string utc,name;public int width,height;public bool expanded;public float timeScale;public double captureSeconds;public SceneRaidRouteEvidence.RootRecord[] roots;public SceneRaidRouteEvidence.DisplayRecord[] displays;}
        [Serializable]private sealed class Cache{public string saved,installed,current;public long queries;public CacheAgent[] agents;}
        [Serializable]private sealed class CacheAgent{public string agent,reason,profile;public int pending;}
    }
}
