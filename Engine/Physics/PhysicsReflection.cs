using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Physics
{
    public static class PhysicsReflection
    {
        public static bool IsGrounded(Node node)
        {
            var physProp = node.GetProperty<PhysicsProperty>();
            if (physProp == null) return false;

            var world = Engine.Physics.GetWorld(physProp.WorldIndex);
            bool result = world?.IsGrounded(node) ?? false;

            return result;
        }
    }
}
