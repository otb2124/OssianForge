using OssianForge.Engine.Physics;
using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    [Flags]
    public enum PhysicsLock
    {
        None = 0,
        Friction = 1 << 0,
        Gravity = 1 << 1,
        LinearDamping = 1 << 2,
        AngularDamping = 1 << 3,
        Rotation = 1 << 4,
        LinearX = 1 << 5,
        LinearY = 1 << 6,
        LinearZ = 1 << 7,
        AngularX = 1 << 8,
        AngularY = 1 << 9,
        AngularZ = 1 << 10,
        AllLinear = LinearX | LinearY | LinearZ,
        AllAngular = AngularX | AngularY | AngularZ | Rotation,
        AllDamping = LinearDamping | AngularDamping,
    }

    public abstract class PhysicsProperty : NodeProperty
    {
        public int WorldIndex;

        public abstract PhysicsBody Body { get; }

        protected PhysicsProperty(int worldIndex)
        {
            WorldIndex = worldIndex;
        }

        public override void OnStart(Node node)
        {
            base.OnStart(node);
            Body.Init(node);
        }
    }
}