public class StateMachine
{
    public IState CurrentState { get; private set; }
    public IState PreviousState { get; private set; }

    public void ChangeState(IState newState)
    {
        if (newState == CurrentState) return;

        CurrentState?.Exit();
        PreviousState = CurrentState;
        CurrentState = newState;
        CurrentState.Enter();
    }

    public void Tick()
    {
        CurrentState?.Tick();
    }

    public void FixedTick()
    {
        CurrentState?.FixedTick();
    }
}

public abstract class HierarchicalState : EnemyState
{
    protected StateMachine subMachine = new StateMachine();

    public HierarchicalState(EnemyBase owner) : base(owner) { }

    public override void Tick()
    {
        subMachine.Tick();
    }

    public override void FixedTick()
    {
        subMachine.FixedTick();
    }

    public override void Exit()
    {
        subMachine.CurrentState?.Exit();
    }
}
