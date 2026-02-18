using System;
using System.Collections.Generic;

public enum BTStatus
{
    Success,
    Failure,
    Running
}

public abstract class BTNode
{
    public abstract BTStatus Tick(EnemyContext ctx);
    public virtual void Reset() { }
}

public class BTSelector : BTNode
{
    private List<BTNode> children;
    private int runningIndex = -1;

    public BTSelector(params BTNode[] children)
    {
        this.children = new List<BTNode>(children);
    }

    public override BTStatus Tick(EnemyContext ctx)
    {
        int startIndex = runningIndex >= 0 ? runningIndex : 0;

        for (int i = startIndex; i < children.Count; i++)
        {
            BTStatus status = children[i].Tick(ctx);

            if (status == BTStatus.Running)
            {
                runningIndex = i;
                return BTStatus.Running;
            }

            if (status == BTStatus.Success)
            {
                runningIndex = -1;
                return BTStatus.Success;
            }
        }

        runningIndex = -1;
        return BTStatus.Failure;
    }

    public override void Reset()
    {
        runningIndex = -1;
        foreach (var child in children) child.Reset();
    }
}

public class BTSequence : BTNode
{
    private List<BTNode> children;
    private int runningIndex = -1;

    public BTSequence(params BTNode[] children)
    {
        this.children = new List<BTNode>(children);
    }

    public override BTStatus Tick(EnemyContext ctx)
    {
        int startIndex = runningIndex >= 0 ? runningIndex : 0;

        for (int i = startIndex; i < children.Count; i++)
        {
            BTStatus status = children[i].Tick(ctx);

            if (status == BTStatus.Running)
            {
                runningIndex = i;
                return BTStatus.Running;
            }

            if (status == BTStatus.Failure)
            {
                runningIndex = -1;
                return BTStatus.Failure;
            }
        }

        runningIndex = -1;
        return BTStatus.Success;
    }

    public override void Reset()
    {
        runningIndex = -1;
        foreach (var child in children) child.Reset();
    }
}

public class BTCondition : BTNode
{
    private Func<EnemyContext, bool> condition;

    public BTCondition(Func<EnemyContext, bool> condition)
    {
        this.condition = condition;
    }

    public override BTStatus Tick(EnemyContext ctx)
    {
        return condition(ctx) ? BTStatus.Success : BTStatus.Failure;
    }
}

public class BTAction : BTNode
{
    private Func<EnemyContext, BTStatus> action;

    public BTAction(Func<EnemyContext, BTStatus> action)
    {
        this.action = action;
    }

    public override BTStatus Tick(EnemyContext ctx)
    {
        return action(ctx);
    }
}
