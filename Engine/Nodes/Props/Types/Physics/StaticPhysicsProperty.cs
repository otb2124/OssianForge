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

        public override void OnDestroy(Node node)
        {
            var nullBody = Engine.Physics.GetWorld(0).JitterWorld.NullBody;
            foreach (var shape in _body.OwnedShapes)
                nullBody.RemoveShape(shape, setMassInertia: false);
            _body.OwnedShapes.Clear();
        }
    }
}