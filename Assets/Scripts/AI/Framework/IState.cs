using UnityEngine;

public interface IState
{
    void Enter();
    void Tick();
    void FixedTick();
    void Exit();
}

public abstract class EnemyState : IState
{
    protected EnemyBase owner;

    public EnemyState(EnemyBase owner)
    {
        this.owner = owner;
    }

    public virtual void Enter() { }
    public virtual void Tick() { }
    public virtual void FixedTick() { }
    public virtual void Exit() { }
}
