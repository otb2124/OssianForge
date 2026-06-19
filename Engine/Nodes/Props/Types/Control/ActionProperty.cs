using System.Collections.Generic;

namespace OssianForge.Engine.Nodes.Props
{
    public class ActionProperty : NodeProperty
    {
        public List<string> OnStartActions { get; } = new();
        public List<string> OnUpdateActions { get; } = new();
        public List<string> OnRenderActions { get; } = new();

        public ActionProperty(Dictionary<string, List<string>> actionMap = null)
        {
            if (actionMap == null) return;

            if (actionMap.TryGetValue("OnStart", out var v)) OnStartActions = v;
            if (actionMap.TryGetValue("OnUpdate", out v)) OnUpdateActions = v;
            if (actionMap.TryGetValue("OnRender", out v)) OnRenderActions = v;
        }

        public override void OnStart(Node node)
            => OnStartActions.ForEach(Engine.Resources.InvokeAction);

        public override void OnUpdate(Node node, double delta)
            => OnUpdateActions.ForEach(Engine.Resources.InvokeAction);

        public override void OnRender(Node node, double delta)
            => OnRenderActions.ForEach(Engine.Resources.InvokeAction);
    }
}