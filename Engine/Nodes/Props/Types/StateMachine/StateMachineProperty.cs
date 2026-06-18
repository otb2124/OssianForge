// ── state machine property ─────────────────────────────────────────────────────

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
        private readonly string? _onEnter;
        private readonly string? _onExit;
        private readonly string? _onUpdate;

        public ActionState(string name, string? onEnter = null, string? onExit = null, string? onUpdate = null)
            : base(name)
        {
            _onEnter = onEnter;
            _onExit = onExit;
            _onUpdate = onUpdate;
        }

        public override void OnEnter(Node node) { if (_onEnter != null) ReflectionDispatcher.Invoke(_onEnter); }
        public override void OnExit(Node node) { if (_onExit != null) ReflectionDispatcher.Invoke(_onExit); }
        public override void OnUpdate(Node node, double delta) { if (_onUpdate != null) ReflectionDispatcher.Invoke(_onUpdate); }
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
            => Current?.OnEnter(node);

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
        }

        public override void OnRender(Node node, double delta)
            => Current?.OnRender(node, delta);

        // ── manual control ────────────────────────────────────────────────────────

        public void TransitionTo(string name, Node node)
        {
            if (!_states.TryGetValue(name, out var next))
                throw new InvalidOperationException($"[SM] Unknown state '{name}'");
            if (next == Current) return;

            Current?.OnExit(node);
            Current = next;
            Current.OnEnter(node);
        }
    }
}