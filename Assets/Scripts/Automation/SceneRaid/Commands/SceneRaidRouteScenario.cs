#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;

namespace AnomalySearch.Automation.SceneRaid.Commands
{
    [Serializable] public sealed class SceneRaidRouteScenario
    {
        public int schemaVersion=1;
        public string id,agent="2";
        public float nearMinimum=10,farMinimum=150,movementBeforeReplacement=3,wallDeadline=60;
        public string[] operations={"Near","FarCrossZone","InvalidPreserves"};
        public void Validate()
        {
            if(schemaVersion!=1||string.IsNullOrWhiteSpace(id)||(agent!="1"&&agent!="2")||
                operations==null||operations.Length!=3||operations[0]!="Near"||operations[1]!="FarCrossZone"||operations[2]!="InvalidPreserves"||
                !Positive(nearMinimum)||!Positive(farMinimum)||farMinimum<=nearMinimum||
                !Positive(movementBeforeReplacement)||!Positive(wallDeadline)||wallDeadline>120)
                throw new InvalidOperationException("Invalid route command scenario.");
        }
        private static bool Positive(float value)=>value>0&&!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
#endif
