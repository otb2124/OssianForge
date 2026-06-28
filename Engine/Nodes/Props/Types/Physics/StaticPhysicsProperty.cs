using OssianForge.Engine.Physics;

namespace OssianForge.Engine.Nodes.Props
{
    public class StaticPhysicsProperty : PhysicsProperty
    {
        private readonly PhysicsStaticBody _body = new();
        public override PhysicsBody Body => _body;
        public PhysicsStaticBody StaticBody => _body;

        public StaticPhysicsProperty(int worldIndex) : base(worldIndex)
        {
        }
    }
}