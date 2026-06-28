using Jitter2;
using Jitter2.Collision.Shapes;
using Jitter2.Dynamics;
using Jitter2.LinearMath;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Numerics;
using static OssianForge.Engine.Utils.MathUtils;
using Constraint = Jitter2.Dynamics.Constraints.Constraint;

namespace OssianForge.Engine.Physics
{
    public abstract class PhysicsBody
    {
        public List<RigidBodyShape> OwnedShapes = new();
        public readonly List<Constraint> Constraints = new();

        protected PhysicsBody()
        {
        }

        public virtual void Init(Node node)
        {

        }

        // ── Shared static helpers ────────────────────────────────────────────

        protected static JVector TransformJVertex(JVector local, Transform t)
        {
            var matrix = t.ToMatrix();
            var v = Vector3.Transform(new Vector3(local.X, local.Y, local.Z), matrix);
            return new JVector(v.X, v.Y, v.Z);
        }

        protected static Vector3 ToEuler(JQuaternion q)
        {
            var sq = new Quaternion(q.X, q.Y, q.Z, q.W);

            float sinr_cosp = 2f * (sq.W * sq.X + sq.Y * sq.Z);
            float cosr_cosp = 1f - 2f * (sq.X * sq.X + sq.Y * sq.Y);
            float roll = MathF.Atan2(sinr_cosp, cosr_cosp);

            float sinp = 2f * (sq.W * sq.Y - sq.Z * sq.X);
            float pitch = MathF.Abs(sinp) >= 1f
                ? MathF.CopySign(MathF.PI / 2f, sinp)
                : MathF.Asin(sinp);

            float siny_cosp = 2f * (sq.W * sq.Z + sq.X * sq.Y);
            float cosy_cosp = 1f - 2f * (sq.Y * sq.Y + sq.Z * sq.Z);
            float yaw = MathF.Atan2(siny_cosp, cosy_cosp);

            return new Vector3(
                roll * (180f / MathF.PI),
                pitch * (180f / MathF.PI),
                yaw * (180f / MathF.PI));
        }

        protected static Vector3 ToEulerRadians(Quaternion sq)
        {
            float sinr_cosp = 2f * (sq.W * sq.X + sq.Y * sq.Z);
            float cosr_cosp = 1f - 2f * (sq.X * sq.X + sq.Y * sq.Y);
            float roll = MathF.Atan2(sinr_cosp, cosr_cosp);

            float sinp = 2f * (sq.W * sq.Y - sq.Z * sq.X);
            float pitch = MathF.Abs(sinp) >= 1f
                ? MathF.CopySign(MathF.PI / 2f, sinp)
                : MathF.Asin(sinp);

            float siny_cosp = 2f * (sq.W * sq.Z + sq.X * sq.Y);
            float cosy_cosp = 1f - 2f * (sq.Y * sq.Y + sq.Z * sq.Z);
            float yaw = MathF.Atan2(siny_cosp, cosy_cosp);

            return new Vector3(roll, pitch, yaw);
        }
    }
}