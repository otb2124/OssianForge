using Jitter2;
using Jitter2.Collision.Shapes;
using Jitter2.Dynamics;
using Jitter2.Dynamics.Constraints;
using Jitter2.LinearMath;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Data;
using System.Numerics;
using static OssianForge.Engine.Utils.MathUtils;
using Constraint = Jitter2.Dynamics.Constraints.Constraint;

namespace OssianForge.Engine.Physics
{
    public class PhysicsBody
    {
        public string NodeId;
        public PhysicalProperty PhysicalProperty;
        public ColliderProperty ColliderProperty;
        public TransformProperty TransformProperty;

        // Null for static mesh bodies — those use world.NullBody directly
        public RigidBody? JitterBody;

        // The shapes we added, kept so we can remove them on Unregister
        public List<RigidBodyShape> OwnedShapes = new();


        private JVector _initialPosition;
        private JQuaternion _initialOrientation;

        public readonly List<Constraint> Constraints = new();

        public PhysicsBody(Node node, World jitterWorld)
        {
            NodeId = node.Id;
            PhysicalProperty = node.GetProperty<PhysicalProperty>();
            ColliderProperty = node.GetProperty<ColliderProperty>();
            TransformProperty = node.GetProperty<TransformProperty>();

            var pos = TransformProperty.Transform.Position;
            var jPos = new JVector(pos.X, pos.Y, pos.Z);

            if (PhysicalProperty.IsStatic)
            {
                var t = TransformProperty.Transform;
                pos = t.Position;
                var scale = t.Scale;
                var rot = t.Rotation; // assuming this is Euler degrees or a quaternion

                var sourceMesh = ColliderProperty.ColliderResource.TriangleMesh!;
                var transformed = new List<JTriangle>();

                for (int i = 0; i < sourceMesh.Indices.Length; i++)
                {
                    var idx = sourceMesh.Indices[i];
                    var a = sourceMesh.Vertices[idx.IndexA];
                    var b = sourceMesh.Vertices[idx.IndexB];
                    var c = sourceMesh.Vertices[idx.IndexC];

                    // Use scale, then rotation, then position
                    a = TransformJVertex(a, t);
                    b = TransformJVertex(b, t);
                    c = TransformJVertex(c, t);

                    transformed.Add(new JTriangle(a, b, c));
                }

                var positionedMesh = new TriangleMesh(transformed, ignoreDegenerated: true);

                foreach (var shape in TriangleShape.CreateAllShapes(positionedMesh))
                {
                    jitterWorld.NullBody.AddShape(shape, setMassInertia: false);
                    OwnedShapes.Add(shape);
                }
            }
            else
            {
                var t = TransformProperty.Transform;
                var scale = t.Scale;

                // Apply scale to the point cloud so the convex hull matches the rendered size
                var scaledPoints = ColliderProperty.ColliderResource.Points
                    .Select(p => new JVector(p.X * scale.X, p.Y * scale.Y, p.Z * scale.Z))
                    .ToList();

                var shape = new PointCloudShape(scaledPoints);

                JitterBody = jitterWorld.CreateRigidBody();
                JitterBody.AddShape(shape);
                JitterBody.Position = new JVector(t.Position.X, t.Position.Y, t.Position.Z);
                JitterBody.AffectedByGravity = PhysicalProperty.UseGravity;
                JitterBody.Friction = PhysicalProperty.Friction;
                JitterBody.Restitution = PhysicalProperty.Bounciness;
                JitterBody.Damping = (PhysicalProperty.LinearDamping, PhysicalProperty.AngularDamping);
                JitterBody.Tag = NodeId;

                _initialPosition = JitterBody.Position;
                _initialOrientation = JitterBody?.Orientation ?? JQuaternion.Identity;

                OwnedShapes.Add(shape);
            }
        }

        public void SyncFromJitter(AxisLock lockPosition = AxisLock.None, AxisLock lockRotation = AxisLock.None)
        {
            if (JitterBody == null) return;

            // Enforce locks directly on the Jitter body BEFORE reading back to Transform
            EnforceAxisLocks(lockPosition, lockRotation);

            var p = JitterBody.Position;
            TransformProperty.Transform.Position = new Vector3(p.X, p.Y, p.Z);

            var o = JitterBody.Orientation;
            TransformProperty.Transform.Rotation = ToEuler(o);

            var v = JitterBody.Velocity;
            PhysicalProperty.Velocity = new Vector3(v.X, v.Y, v.Z);
        }

        public void AddForce(Vector3 force)
        {
            JitterBody?.AddForce(new JVector(force.X, force.Y, force.Z));
        }

        public void AddImpulse(Vector3 impulse)
        {
            // Impulse = force applied for one frame's worth of time
            if (JitterBody == null) return;
            JitterBody.Velocity += new JVector(impulse.X, impulse.Y, impulse.Z);
        }


        private static JVector TransformJVertex(JVector local, Transform t)
        {
            // Reuse the same matrix your renderer uses
            var matrix = t.ToMatrix();

            var v = System.Numerics.Vector3.Transform(
                new System.Numerics.Vector3(local.X, local.Y, local.Z),
                matrix);

            return new JVector(v.X, v.Y, v.Z);
        }

        private static Vector3 ToEuler(JQuaternion q)
        {
            // Convert JQuaternion to System.Numerics.Quaternion then to Euler
            var sq = new System.Numerics.Quaternion(q.X, q.Y, q.Z, q.W);

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

            // Convert radians to degrees if your Transform expects degrees
            return new Vector3(
                roll * (180f / MathF.PI),
                pitch * (180f / MathF.PI),
                yaw * (180f / MathF.PI));
        }

        private static Vector3 ToEulerRadians(System.Numerics.Quaternion sq)
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


        private void EnforceAxisLocks(AxisLock lockPosition, AxisLock lockRotation)
        {
            if (JitterBody == null) return;

            // ── Position locks ──────────────────────────────────────────────────────
            // Snap the body back to its initial coordinate on locked axes
            // and kill velocity on that axis so it can't drift.
            var pos = JitterBody.Position;
            var vel = JitterBody.Velocity;

            if (lockPosition.HasFlag(AxisLock.X))
            {
                pos.X = _initialPosition.X;
                vel.X = 0f;
            }
            if (lockPosition.HasFlag(AxisLock.Y))
            {
                pos.Y = _initialPosition.Y;
                vel.Y = 0f;
            }
            if (lockPosition.HasFlag(AxisLock.Z))
            {
                pos.Z = _initialPosition.Z;
                vel.Z = 0f;
            }

            JitterBody.Position = pos;
            JitterBody.Velocity = vel;

            // ── Rotation locks ──────────────────────────────────────────────────────
            // Kill angular velocity on locked axes, then rebuild the orientation
            // quaternion from only the free axis so the body can't tumble.
            var angVel = JitterBody.AngularVelocity;

            if (lockRotation.HasFlag(AxisLock.X)) angVel.X = 0f;
            if (lockRotation.HasFlag(AxisLock.Y)) angVel.Y = 0f;
            if (lockRotation.HasFlag(AxisLock.Z)) angVel.Z = 0f;

            JitterBody.AngularVelocity = angVel;

            // If any rotation axis is locked, also correct the orientation itself
            // so accumulated drift doesn't sneak in over many frames.
            if (lockRotation != AxisLock.None)
            {
                var q = JitterBody.Orientation;
                // Convert to System.Numerics.Quaternion for easier manipulation
                var sq = new System.Numerics.Quaternion(q.X, q.Y, q.Z, q.W);
                var euler = ToEulerRadians(sq);

                if (lockRotation.HasFlag(AxisLock.X)) euler.X = 0f;
                if (lockRotation.HasFlag(AxisLock.Y)) euler.Y = 0f;
                if (lockRotation.HasFlag(AxisLock.Z)) euler.Z = 0f;

                var corrected = System.Numerics.Quaternion.CreateFromYawPitchRoll(euler.Y, euler.X, euler.Z);
                JitterBody.Orientation = new JQuaternion(corrected.X, corrected.Y, corrected.Z, corrected.W);
            }
        }
    }
}