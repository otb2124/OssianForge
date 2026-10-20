using OssianForge.Engine.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Nodes.Props
{
    /// <summary>Where a property written on a scene-reference node lands in the referenced scene.</summary>
    public enum OverrideTarget { Content, Root }

    public class NodeProperty
    {
        /// <summary>Content: the scene's content node (root, or its single child when the root is an empty wrapper). Root: always the instance root.</summary>
        public virtual OverrideTarget SceneRefTarget => OverrideTarget.Content;

        public string NodeId;
        public bool Started = false;
        public bool Writable = true;

        public bool Enabled = true;
        public virtual void OnStart(Node node) { NodeId = node.Id; Started = true; }
        public virtual void OnUpdate(Node node, double delta) { }
        public virtual void OnRender(Node node, double delta) { }
        public virtual void OnDestroy(Node node) { }
    }
}