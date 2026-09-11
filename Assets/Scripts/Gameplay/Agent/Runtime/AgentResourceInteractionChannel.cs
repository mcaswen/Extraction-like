using System;
using Gameplay.Agent.Data;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    public static class AgentResourceInteractionChannel
    {
        public static event Action<AgentResourceInteractionEvent> Published;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Published = null;
        public static void Publish(AgentResourceInteractionEvent value) => Published?.Invoke(value);
    }
}
