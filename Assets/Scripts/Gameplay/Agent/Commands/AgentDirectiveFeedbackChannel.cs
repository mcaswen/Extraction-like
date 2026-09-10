using System;
using UnityEngine;

namespace Gameplay.Agent.Commands
{
    public static class AgentDirectiveFeedbackChannel
    {
        public static event Action<AgentDirectiveResult> Published;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Published = null;
        public static void Publish(AgentDirectiveResult result) => Published?.Invoke(result);
    }
}
