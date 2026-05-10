
public sealed class AnimContext
{
    public AnimationDriver Anim { get; set; }
    public CharacterInputReader Input { get; set; }
    public CharacterMotor Motor { get; set; }
    public CharacterCombat Combat { get; set; }
    public InputBuffer InputBuffer { get; set; }

    public float StateTime { get; set; }
    public float NormalizedTime { get; set; }

    public AnimStateId CurrentStateId { get; set; }
}
