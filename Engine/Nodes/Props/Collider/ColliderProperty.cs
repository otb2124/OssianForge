using System;
using OssianForge.Engine.Resources.Colliders;

namespace OssianForge.Engine.Nodes.Props
{
    public class ColliderProperty : NodeProperty
    {
        public bool IsTrigger;
        public Action<Node>? OnCollision;
        public ColliderResource ColliderResource;

        public ColliderProperty(string colliderId, bool isTrigger = false)
        {
            ColliderResource = Engine.Resources.GetResource<ColliderResource>(colliderId);
            IsTrigger = isTrigger;
        }
    }
}