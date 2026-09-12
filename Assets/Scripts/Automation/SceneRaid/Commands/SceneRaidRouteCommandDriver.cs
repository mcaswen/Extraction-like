#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Agent.Core;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.View;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid.Commands
{
    /// <summary>有限的正式地图操作脚本。只选群、切焦点及调用 Handler，背包和执行仍由原所有者管理。</summary>
    public sealed class SceneRaidRouteCommandDriver
    {
        [Serializable] public sealed class Candidate
        {public string node,zone,kind,failure;public Vector3 origin,anchor;public float distance;public bool reachable;}
        [Serializable] public sealed class Submission
        {
            public string scenario,operation,agent,requestId,node,zone,stage,reason;
            public int ordinal,uiCommandsBefore,uiCommandsAfter;
            public Candidate selected;
            public SceneRaidRouteEvidence.RootRecord before,after;
        }
        [Serializable] private sealed class Scan{public string scenario,operation,agent;public long queries;public Candidate[] candidates;}
        [Serializable] private sealed class Progress{public string scenario,requestId;public Vector3 submittedPosition,currentPosition;public float distance;public SceneRaidRouteEvidence.RootRecord root;}
        [Serializable] private sealed class Completion{public string scenario,status,reason;public int submitted;}
        private readonly SceneRaidRouteScenario _scenario;
        private readonly SceneRaidEvidenceWriter _writer;
        private readonly AgentNavigationSegmentQuery.Buffer _buffer=new AgentNavigationSegmentQuery.Buffer();
        private readonly List<Candidate> _candidates=new List<Candidate>();
        private MapGraphPresenter _map;
        private MapGraphTargetBinding[] _targets;
        private int _scanIndex,_step;
        private string _nearZone,_lastRequest;
        private Vector3 _submittedPosition;
        private bool _waitingAcceptance,_progressRecorded,_done;
        public int SubmittedCount{get;private set;}
        public bool IsDone=>_done;
        public SceneRaidRouteCommandDriver(SceneRaidRouteScenario scenario,SceneRaidEvidenceWriter writer)
        {scenario.Validate();_scenario=scenario;_writer=writer;_writer.Add("routeScript.started",JsonUtility.ToJson(scenario));}
        public void Tick()
        {
            if(_done)return;
            if(_writer.WallSeconds>_scenario.wallDeadline){Stop("PARTIAL","State prerequisite deadline");return;}
            var registry=AgentRuntimeRegistry.ActiveInstance;
            if(registry==null||!registry.TryGetHandle(_scenario.agent,out var handle))return;
            if(!handle.IsAlive){Stop("PARTIAL","Commanded agent died normally");return;}
            var pawn=handle.PawnRoot;
            if(_map==null)_map=UnityEngine.Object.FindObjectOfType<MapGraphPresenter>();
            if(_map==null||_map.Binding==null||!pawn.RouteSnapshot.IsInstalled||!AgentNavigationQuery.IsReady(pawn.NavMeshAgent))return;
            var root=pawn.RouteSnapshot;
            if(_waitingAcceptance)
            {
                if(root.Request.RequestId!=_lastRequest||!root.HasRoute)return;
                if(root.Stage==AgentRouteStage.Rejected||root.Stage==AgentRouteStage.Failed||root.Stage==AgentRouteStage.Dead)
                {Stop("PARTIAL","Requested root terminated before prerequisite: "+root.Failure);return;}
                if(!root.IsActive&&root.Stage!=AgentRouteStage.Completed)return;
                _waitingAcceptance=false;
            }
            if(_step==1&&!_progressRecorded)
            {
                float distance=Vector3.Distance(_submittedPosition,pawn.Position);
                if(root.Request.RequestId!=_lastRequest||distance<_scenario.movementBeforeReplacement)return;
                _writer.Add("routeScript.progress",JsonUtility.ToJson(new Progress{scenario=_scenario.id,requestId=_lastRequest,
                    submittedPosition=_submittedPosition,currentPosition=pawn.Position,distance=distance,root=SceneRaidRouteEvidence.Capture(pawn)}));
                _progressRecorded=true;
            }
            // 等现有背包暂停结束、反击自然结束再有限改令，反击身份由独立根探针持续核对。
            if(Time.timeScale==0||root.HasPendingRequest||root.CurrentStep.IsRetaliating)return;
            if(_step==2){Submit(pawn,null,"__invalid_map_command_node__");Stop("SUBMITTED","Finite script exhausted; actual results and settlement require independent contracts");return;}
            if(_targets==null)
            {
                _targets=_map.Binding.TargetBindings.GroupBy(x=>x.NodeId).Select(x=>x.First()).OrderBy(x=>x.NodeId,StringComparer.Ordinal).ToArray();
                _scanIndex=0;_candidates.Clear();
            }
            for(int count=0;count<2&&_scanIndex<_targets.Length;count++,_scanIndex++)
            {
                var target=_targets[_scanIndex];var node=_map.Binding.MapDefinition.Nodes.First(x=>x.NodeId==target.NodeId);
                if(node.NodeKind==MapGraphNodeKind.Extraction||!target.TryGetNavigationAnchor(out var anchor))continue;
                var result=AgentNavigationSegmentQuery.Calculate(pawn.NavMeshAgent,anchor,_buffer);
                _candidates.Add(new Candidate{node=node.NodeId,zone=node.ZoneId,kind=node.NodeKind.ToString(),origin=pawn.Position,
                    anchor=anchor,reachable=result.IsComplete,distance=result.IsComplete?result.Length:0,failure=result.Failure??""});
            }
            if(_scanIndex<_targets.Length)return;
            _writer.Add("routeScript.candidates",JsonUtility.ToJson(new Scan{scenario=_scenario.id,operation=_scenario.operations[_step],agent=_scenario.agent,
                queries=_buffer.CalculationCount,candidates=_candidates.ToArray()}));
            var available=_candidates.Where(x=>x.reachable&&x.distance>=(_step==0?_scenario.nearMinimum:_scenario.farMinimum)&&(_step==0||x.zone!=_nearZone));
            var selected=(_step==0?available.OrderBy(x=>x.distance):available.OrderByDescending(x=>x.distance)).ThenBy(x=>x.node,StringComparer.Ordinal).FirstOrDefault();
            if(selected==null){Stop("PARTIAL","No reachable candidate satisfies scenario");return;}
            if(_step==0)_nearZone=selected.zone;
            Submit(pawn,selected,selected.node);_targets=null;_waitingAcceptance=true;
        }
        private void Submit(AgentPawnRoot pawn,Candidate selected,string node)
        {
            if(!AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent(_scenario.agent))throw new InvalidOperationException("Route script focus failed");
            var record=new Submission{scenario=_scenario.id,operation=_scenario.operations[_step],ordinal=_step,agent=_scenario.agent,node=node,
                selected=selected,zone=selected?.zone??"",before=SceneRaidRouteEvidence.Capture(pawn),uiCommandsBefore=_map.SubmittedCommandCount};
            _map.SubmitNode(node);var result=_map.LastSubmittedResult;
            _lastRequest=result.Request.RequestId;_submittedPosition=pawn.Position;SubmittedCount++;
            record.requestId=_lastRequest;record.stage=result.Stage.ToString();record.reason=result.Reason.ToString();
            record.after=SceneRaidRouteEvidence.Capture(pawn);record.uiCommandsAfter=_map.SubmittedCommandCount;
            _writer.Add("routeScript.submitted",JsonUtility.ToJson(record));_step++;
        }
        public void Stop(string status="PARTIAL",string reason="Raid ended before finite script completed")
        {
            if(_done)return;_done=true;
            _writer.Add("routeScript.finished",JsonUtility.ToJson(new Completion{scenario=_scenario.id,status=status,reason=reason,submitted=SubmittedCount}));
        }
    }
}
#endif
