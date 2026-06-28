using System;
using System.Collections.Generic;
using OssianForge.Engine.Core;
using OssianForge.Engine.Resources.Config;
using OssianForge.Engine.Utils.ConditionNode;

namespace OssianForge.Engine.Nodes.Props
{
    public abstract class State
    {
        public string Name { get; }
        protected State(string name) => Name = name;

        public virtual void OnEnter(Node node) { }
        public virtual void OnExit(Node node) { }
        public virtual void OnUpdate(Node node, double delta) { }
        public virtual void OnRender(Node node, double delta) { }
    }

    // Thin state backed entirely by action ids — no subclass needed
    public class ActionState : State
    {
        private readonly List<string> _onEnter;
        private readonly List<string> _onExit;
        private readonly List<string> _onUpdate;

        public ActionState(
            string name,
            List<string>? onEnter = null,
            List<string>? onExit = null,
            List<string>? onUpdate = null)
            : base(name)
        {
            _onEnter = onEnter ?? new List<string>();
            _onExit = onExit ?? new List<string>();
            _onUpdate = onUpdate ?? new List<string>();
        }

        public override void OnEnter(Node node)
            => _onEnter.ForEach(id => Engine.Resources.InvokeAction(id, node));

        public override void OnExit(Node node)
            => _onExit.ForEach(id => Engine.Resources.InvokeAction(id, node));

        public override void OnUpdate(Node node, double delta)
            => _onUpdate.ForEach(id => Engine.Resources.InvokeAction(id, node, delta));
    }

    public class StateMachineProperty : NodeProperty
    {
        private readonly Dictionary<string, State> _states = new();
        private readonly List<(string From, string To, ConditionNode Condition)> _transitions = new();

        public State? Current { get; private set; }

        // ── registration ─────────────────────────────────────────────────────────

        public StateMachineProperty AddState(State state)
        {
            _states[state.Name] = state;
            return this;
        }

        public StateMachineProperty AddTransition(string from, string to, ConditionNode condition)
        {
            _transitions.Add((from, to, condition));
            return this;
        }

        // ── initial state ─────────────────────────────────────────────────────────

        public void SetInitial(string name)
        {
            if (!_states.TryGetValue(name, out var state))
                throw new InvalidOperationException($"[SM] Unknown state '{name}'");
            Current = state;
        }

        // ── lifecycle ─────────────────────────────────────────────────────────────

        public override void OnStart(Node node)
        {
            base.OnStart(node);
            Current?.OnEnter(node);
        }

        public override void OnUpdate(Node node, double delta)
        {
            if (Current == null) return;

            foreach (var (from, to, condition) in _transitions)
            {
                if (from != Current.Name) continue;
                if (!condition.Evaluate(node)) continue;
                TransitionTo(to, node);
                break;
            }

            Current?.OnUpdate(node, delta);

            Console.WriteLine(Current.Name);
        }

        public override void OnRender(Node node, double delta)
            => Current?.OnRender(node, delta);

        // ── manual control ────────────────────────────────────────────────────────

        public void TransitionTo(string name, Node node)
        {
            if (!_states.TryGetValue(name, out var next))
                throw new InvalidOperationException($"[SM] Unknown state '{name}'");
            if (next == Current) return;

            //Console.WriteLine($"[STATEMACHINE PROPERTY] '{node.Id}' transitioning: {Current?.Name ?? "(none)"} → {next.Name}");

            Current?.OnExit(node);
            Current = next;
            Current.OnEnter(node);
        }
    }
}