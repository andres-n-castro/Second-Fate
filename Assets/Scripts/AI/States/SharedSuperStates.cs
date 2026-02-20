// ---------------------------------------------------------------
//  NonCombatSuperState
//  Generic hierarchical wrapper for non-combat behavior (patrol/roam/idle).
//  Configure the initial substate via SetInitialSubState before use.
//  ForceSubState transitions the internal sub-machine directly (within-super).
//  Cross-super transitions (Combat → NonCombat) go through the outer FSM.
// ---------------------------------------------------------------
public class NonCombatSuperState : HierarchicalState
{
    private IState initialSubState;

    public NonCombatSuperState(EnemyBase owner) : base(owner) { }

    public void SetInitialSubState(IState state)
    {
        initialSubState = state;
    }

    public override void Enter()
    {
        if (initialSubState != null)
            subMachine.ChangeState(initialSubState);
    }

    public void ForceSubState(IState state)
    {
        subMachine.ChangeState(state);
    }
}

// ---------------------------------------------------------------
//  CombatSuperState
//  Generic hierarchical wrapper for combat behavior (chase/attack/reposition).
//  Configure the initial substate via SetInitialSubState before use.
//  ForceSubState transitions the internal sub-machine directly (within-super).
//  Cross-super transitions (NonCombat → Combat) go through the outer FSM.
// ---------------------------------------------------------------
public class CombatSuperState : HierarchicalState
{
    private IState initialSubState;

    public CombatSuperState(EnemyBase owner) : base(owner) { }

    public void SetInitialSubState(IState state)
    {
        initialSubState = state;
    }

    public override void Enter()
    {
        if (initialSubState != null)
            subMachine.ChangeState(initialSubState);
    }

    public void ForceSubState(IState state)
    {
        subMachine.ChangeState(state);
    }
}
