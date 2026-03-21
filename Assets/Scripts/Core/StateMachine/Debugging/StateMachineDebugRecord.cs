
namespace Core.StateMachine.Debugging
{
    public readonly struct StateMachineDebugRecord
    {
        public readonly double TimeSeconds;
        public readonly string StateName;
        public readonly string Message;

        public StateMachineDebugRecord(double timeSeconds, string stateName, string message)
        {
            TimeSeconds = timeSeconds;
            StateName = stateName;
            Message = message;
        }

        public override string ToString()
        {
            return $"[{TimeSeconds:F3}] {StateName} :: {Message}";
        }
    }
}