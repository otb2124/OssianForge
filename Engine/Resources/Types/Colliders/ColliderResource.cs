using Jitter2.Collision.Shapes;
using Jitter2.LinearMath;
using OssianForge.Engine.Resources.Meshes;
using System.Numerics;
using static OssianForge.Engine.Utils.MathUtils;

namespace OssianForge.Engine.Resources.Colliders
{
    public abstract class ColliderResource : Resource, IDisposable
    {

        public JVector AabbMin { get; protected set; }
        public JVector AabbMax { get; protected set; }

        protected ColliderResource(string id)
        {
            Id = id;
        }

        // Shape for dynamic rigid bodies (convex)
        public abstract RigidBodyShape CreateDynamicShape(Vector3 nodeScale);

        // Shapes for static world geometry (triangle mesh)
        public abstract IEnumerable<RigidBodyShape> CreateStaticShapes(Transform worldTransform);

        // Optional: debug mesh for visualization
        public virtual SubMeshResource GetDebugMesh() => null;

        public void Dispose() { }
    }
}