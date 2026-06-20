using Jitter2;
using Jitter2.Collision.Shapes;
using Jitter2.Dynamics;
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
        public PhysicsProperty PhysicsProperty;
        public ColliderProperty ColliderProperty;
        public TransformProperty TransformProperty;

        public RigidBody? JitterBody;
        public List<RigidBodyShape> OwnedShapes = new();

        private JVector _initialPosition;
        private JQuaternion _initialOrientation;

        public readonly List<Constraint> Constraints = new();

        public PhysicsBody(Node node, World jitterWorld)
        {
            NodeId = node.Id;
            PhysicsProperty = node.GetProperty<PhysicsProperty>();
            ColliderProperty = node.GetProperty<ColliderProperty>();
            TransformProperty = node.GetProperty<TransformProperty>();

            // Use WorldTransform — spawn position/orientation/scale must account
            // for any parent composition (e.g. a collider on a child of "player").
            var pos = TransformProperty.WorldTransform.Position;
            var jPos = new JVector(pos.X, pos.Y, pos.Z);

            if (PhysicsProperty.IsStatic)
            {
                var t = TransformProperty.WorldTransform;
                pos = t.Position;
                var scale = t.Scale;

                var sourceMesh = ColliderProperty.ColliderResource.TriangleMesh!;
                var transformed = new List<JTriangle>();

                for (int i = 0; i < sourceMesh.Indices.Length; i++)
                {
                    var idx = sourceMesh.Indices[i];
                    var a = sourceMesh.Vertices[idx.IndexA];
                    var b = sourceMesh.Vertices[idx.IndexB];
                    var c = sourceMesh.Vertices[idx.IndexC];

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
                var t = TransformProperty.WorldTransform;
                var scale = t.Scale;

                var scaledPoints = ColliderProperty.ColliderResource.Points
                    .Select(p => new JVector(p.X * scale.X, p.Y * scale.Y, p.Z * scale.Z))
                    .ToList();

                var shape = new PointCloudShape(scaledPoints);

                JitterBody = jitterWorld.CreateRigidBody();
                JitterBody.AddShape(shape);
                JitterBody.Position = new JVector(t.Position.X, t.Position.Y, t.Position.Z);
                JitterBody.AffectedByGravity = PhysicsProperty.UseGravity;
                JitterBody.Friction = PhysicsProperty.Friction;
                JitterBody.Restitution = PhysicsProperty.Bounciness;
                JitterBody.Damping = (PhysicsProperty.LinearDamping, PhysicsProperty.AngularDamping);
                JitterBody.Tag = NodeId;

                var rot = t.Rotation;
                var initRot = System.Numerics.Quaternion.CreateFromYawPitchRoll(
                    float.DegreesToRadians(rot.Y),
                    float.DegreesToRadians(rot.X),
                    float.DegreesToRadians(rot.Z));
                JitterBody.Orientation = new JQuaternion(initRot.X, initRot.Y, initRot.Z, initRot.W);

                _initialPosition = JitterBody.Position;
                _initialOrientation = JitterBody?.Orientation ?? JQuaternion.Identity;

                OwnedShapes.Add(shape);
            }
        }

        public void SyncFromJitter(AxisLock lockPosition = AxisLock.None, AxisLock lockRotation = AxisLock.None)
        {
            if (JitterBody == null) return;

            EnforceAxisLocks(lockPosition, lockRotation);

            var p = JitterBody.Position;
            var o = JitterBody.Orientation;
            var v = JitterBody.Velocity;

            // Physics is the authority on world position/rotation for this node.
            // Write to WorldTransform (read by rendering) — NOT local Transform,
            // since Transform is meant to stay the authored/script-editable value
            // and TransformProperty.OnUpdate will overwrite WorldTransform from
            // Transform + parent every frame anyway, undoing this otherwise.
            //
            // If this body has no parent (the common physics case — top-level
            // dynamic objects), Transform and WorldTransform should be kept in
            // sync so script reads of either field stay consistent.
            TransformProperty.WorldTransform.Position = new Vector3(p.X, p.Y, p.Z);
            TransformProperty.WorldTransform.Rotation = ToEuler(o);

            var parent = Engine.Nodes.NodeManager.GetNode(NodeId).Parent;

            if (parent?.GetProperty<TransformProperty>() == null)
            {
                TransformProperty.Transform.Position = TransformProperty.WorldTransform.Position;
                TransformProperty.Transform.Rotation = TransformProperty.WorldTransform.Rotation;
            }

            PhysicsProperty.Velocity = new Vector3(v.X, v.Y, v.Z);
        }

        public void SyncToJitter()
        {
            if (JitterBody == null) return;
            if (PhysicsProperty.ManualVelocity == Vector3.Zero) return;

            JitterBody.Velocity = new JVector(
                PhysicsProperty.ManualVelocity.X,
                PhysicsProperty.ManualVelocity.Y,
                PhysicsProperty.ManualVelocity.Z);

            PhysicsProperty.ManualVelocity = Vector3.Zero;
        }

        public void AddForce(Vector3 force)
        {
            JitterBody?.AddForce(new JVector(force.X, force.Y, force.Z));
        }

        public void AddImpulse(Vector3 impulse)
        {
            if (JitterBody == null) return;
            JitterBody.Velocity += new JVector(impulse.X, impulse.Y, impulse.Z);
        }

        private static JVector TransformJVertex(JVector local, Transform t)
        {
            var matrix = t.ToMatrix();
            var v = System.Numerics.Vector3.Transform(
                new System.Numerics.Vector3(local.X, local.Y, local.Z),
                matrix);
            return new JVector(v.X, v.Y, v.Z);
        }

        private static Vector3 ToEuler(JQuaternion q)
        {
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

            var pos = JitterBody.Position;
            var vel = JitterBody.Velocity;

            if (lockPosition.HasFlag(AxisLock.X)) { pos.X = _initialPosition.X; vel.X = 0f; }
            if (lockPosition.HasFlag(AxisLock.Y)) { pos.Y = _initialPosition.Y; vel.Y = 0f; }
            if (lockPosition.HasFlag(AxisLock.Z)) { pos.Z = _initialPosition.Z; vel.Z = 0f; }

            JitterBody.Position = pos;
            JitterBody.Velocity = vel;

            var angVel = JitterBody.AngularVelocity;

            if (lockRotation.HasFlag(AxisLock.X)) angVel.X = 0f;
            if (lockRotation.HasFlag(AxisLock.Y)) angVel.Y = 0f;
            if (lockRotation.HasFlag(AxisLock.Z)) angVel.Z = 0f;

            JitterBody.AngularVelocity = angVel;

            if (lockRotation != AxisLock.None)
            {
                var q = JitterBody.Orientation;
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