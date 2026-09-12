using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Navigation
{
    /// <summary>消除导航角色的原生避让优先级平局，只在成员变化时同步。释放时恢复仍由本模块拥有的写入。</summary>
    public sealed class AgentNavigationAvoidanceAssignment
    {
        public readonly struct AgentEntry
        {
            public string Id { get; }
            public NavMeshAgent Navigation { get; }
            public AgentEntry(string id,NavMeshAgent navigation) {Id=id;Navigation=navigation;}
        }
        private sealed class Record
        {public string Id;public NavMeshAgent Navigation;public int Original,Applied;}
        private static readonly Dictionary<NavMeshAgent,Record> Owners=new Dictionary<NavMeshAgent,Record>();
        private readonly Dictionary<string,Record> _records=new Dictionary<string,Record>(StringComparer.Ordinal);
        private readonly List<Record> _ordered=new List<Record>();
        private readonly HashSet<string> _present=new HashSet<string>();
        private readonly List<string> _removed=new List<string>();
        private readonly bool[] _reserved=new bool[100],_used=new bool[100];
        public int Count=>_records.Count;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetOwnership()=>Owners.Clear();
        public void Synchronize(IReadOnlyList<AgentEntry> agents)
        {
            _present.Clear();
            for(int i=0;i<agents.Count;i++)
            {
                var entry=agents[i];if(entry.Navigation==null||string.IsNullOrEmpty(entry.Id)||!_present.Add(entry.Id))continue;
                if(_records.TryGetValue(entry.Id,out var record)&&record.Navigation!=entry.Navigation)
                {Restore(record);_records.Remove(entry.Id);record=null;}
                if(record==null)
                {
                    int original=entry.Navigation.avoidancePriority;
                    if(Owners.TryGetValue(entry.Navigation,out var previous)&&original==previous.Applied)original=previous.Original;
                    record=new Record{Id=entry.Id,Navigation=entry.Navigation,Original=original,Applied=entry.Navigation.avoidancePriority};
                    Owners[entry.Navigation]=record;
                    _records.Add(entry.Id,record);
                }
                // 尊重另一个拥有者或调试者在同步之间进行的实际配置变更。
                if(record.Navigation.avoidancePriority!=record.Applied)record.Original=record.Navigation.avoidancePriority;
            }
            _removed.Clear();foreach(var pair in _records)if(!_present.Contains(pair.Key))_removed.Add(pair.Key);
            foreach(string id in _removed){Restore(_records[id]);_records.Remove(id);}
            _ordered.Clear();Array.Clear(_reserved,0,100);Array.Clear(_used,0,100);
            foreach(var record in _records.Values)
                if(Owners.TryGetValue(record.Navigation,out var owner)&&ReferenceEquals(owner,record))
                {_ordered.Add(record);_reserved[Mathf.Clamp(record.Original,0,99)]=true;}
            _ordered.Sort((a,b)=>string.CompareOrdinal(a.Id,b.Id));
            foreach(var record in _ordered)
            {
                int desired=Mathf.Clamp(record.Original,0,99),assigned=desired;
                if(_used[desired])
                    for(int delta=1;delta<100;delta++)
                    {
                        int higher=desired+delta,lower=desired-delta;
                        if(higher<100&&!_used[higher]&&!_reserved[higher]){assigned=higher;break;}
                        if(lower>=0&&!_used[lower]&&!_reserved[lower]){assigned=lower;break;}
                    }
                _used[assigned]=true;record.Navigation.avoidancePriority=assigned;record.Applied=assigned;
            }
        }
        public void Clear(){foreach(var record in _records.Values)Restore(record);_records.Clear();_ordered.Clear();}
        private static void Restore(Record record)
        {
            if(!Owners.TryGetValue(record.Navigation,out var owner)||!ReferenceEquals(owner,record))return;
            Owners.Remove(record.Navigation);
            if(record.Navigation!=null&&record.Navigation.avoidancePriority==record.Applied)record.Navigation.avoidancePriority=record.Original;
        }
    }
}
