using System.Collections.Generic;

namespace JadeSeal.EnemyAI.BehaviorTree
{
    public enum NodeState
    {
        RUNNING,
        SUCCESS,
        FAILURE
    }

    /// <summary>
    /// Base class for all nodes in the Mountain Archer Behavior Tree.
    /// Implements standard modular decision-making architecture for SE3062 Intelligent Systems.
    /// </summary>
    public abstract class BTNode
    {
        protected NodeState state;
        public NodeState State => state;

        public abstract NodeState Evaluate();
    }

    /// <summary>
    /// Evaluates child nodes in sequence. If any child fails, the sequence fails.
    /// Succeeds only if all children succeed.
    /// </summary>
    public class BTSequence : BTNode
    {
        protected List<BTNode> children = new List<BTNode>();

        public BTSequence(List<BTNode> children)
        {
            this.children = children;
        }

        public override NodeState Evaluate()
        {
            bool anyChildRunning = false;

            foreach (var node in children)
            {
                switch (node.Evaluate())
                {
                    case NodeState.FAILURE:
                        state = NodeState.FAILURE;
                        return state;
                    case NodeState.SUCCESS:
                        continue;
                    case NodeState.RUNNING:
                        anyChildRunning = true;
                        continue;
                    default:
                        state = NodeState.SUCCESS;
                        return state;
                }
            }

            state = anyChildRunning ? NodeState.RUNNING : NodeState.SUCCESS;
            return state;
        }
    }

    /// <summary>
    /// Evaluates child nodes as fallbacks. If any child succeeds, the selector succeeds.
    /// Fails only if all children fail.
    /// </summary>
    public class BTSelector : BTNode
    {
        protected List<BTNode> children = new List<BTNode>();

        public BTSelector(List<BTNode> children)
        {
            this.children = children;
        }

        public override NodeState Evaluate()
        {
            foreach (var node in children)
            {
                switch (node.Evaluate())
                {
                    case NodeState.FAILURE:
                        continue;
                    case NodeState.SUCCESS:
                        state = NodeState.SUCCESS;
                        return state;
                    case NodeState.RUNNING:
                        state = NodeState.RUNNING;
                        return state;
                    default:
                        continue;
                }
            }

            state = NodeState.FAILURE;
            return state;
        }
    }

    /// <summary>
    /// Leaf action node that executes a delegate callback.
    /// </summary>
    public class BTActionNode : BTNode
    {
        private readonly System.Func<NodeState> action;

        public BTActionNode(System.Func<NodeState> action)
        {
            this.action = action;
        }

        public override NodeState Evaluate()
        {
            if (action == null)
            {
                state = NodeState.FAILURE;
                return state;
            }

            state = action.Invoke();
            return state;
        }
    }
}
