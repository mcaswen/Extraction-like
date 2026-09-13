#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Linq;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.View;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid
{
    /// <summary>渲染验收专用：有限次切换真实地图视口，不操作角色任务或背包。</summary>
    public sealed class SceneRaidMapViewportDriver
    {
        private readonly SceneRaidEvidenceWriter _writer;
        private MapGraphPresenter _map;
        private int _step;
        private double _nextLookup;
        public SceneRaidMapViewportDriver(SceneRaidEvidenceWriter writer){_writer=writer;}
        public void Tick()
        {
            if(_step>=2)return;
            double now=_writer.WallSeconds;
            if(now<(_step==0?10:40))return;
            if(_map==null)
            {
                if(now<_nextLookup)return;_nextLookup=now+.5;
                _map=UnityEngine.Object.FindObjectOfType<MapGraphPresenter>();if(_map==null)return;
            }
            var before=Roots();bool expanded=_step==0;
            _map.Viewport.SetExpanded(expanded);_step++;
            _writer.Add("map.viewport",JsonUtility.ToJson(new Change{ordinal=_step,expanded=_map.Viewport.IsExpanded,
                before=before,after=Roots(),submittedCommands=_map.SubmittedCommandCount}));
        }
        private static string[] Roots()
        {
            var registry=AgentRuntimeRegistry.ActiveInstance;
            return registry==null?Array.Empty<string>():registry.RegisteredAgents.Where(x=>x.PawnRoot!=null).Select(x=>
            {var r=x.PawnRoot.RouteSnapshot;return x.AgentId.Value+":"+r.Request.RequestId+":"+r.RouteVersion+":"+r.StepIndex+":"+r.Stage;}).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        [Serializable]private sealed class Change{public int ordinal,submittedCommands;public bool expanded;public string[] before,after;}
    }
}
#endif
