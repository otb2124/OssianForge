using Jitter2;
using Jitter2.Collision.Shapes;
using Jitter2.Dynamics;
using Jitter2.LinearMath;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Numerics;

namespace OssianForge.Engine.Physics
{
    public class PhysicsRigidBody : PhysicsBody
    {
        public RigidBody JitterBody;

        private readonly JVector _initialPosition;
        private readonly JQuaternion _initialOrientation;
        private readonly JVector _colliderCentroidOffset;

        public PhysicsRigidBody(Node node, World jitterWorld) : base(node)
        {
            var t = TransformProperty.WorldTransform;
            var nodeScale = t.Scale;
            var localMatrix = ColliderProperty.LocalTransform.ToMatrix();

            var transformedPoints = ColliderProperty.ColliderResource.Points
                .Select(p =>
                {
                    var scaled = new Vector3(p.X * nodeScale.X, p.Y * nodeScale.Y, p.Z * nodeScale.Z);
                    var local = Vector3.Transform(scaled, localMatrix);
                    return new JVector(local.X, local.Y, local.Z);
                })
                .ToList();

            var centroid = new JVector(
                transformedPoints.Average(p => p.X),
                transformedPoints.Average(p => p.Y),
                transformedPoints.Average(p => p.Z));

            _colliderCentroidOffset = centroid;

            var centeredPoints = transformedPoints
                .Select(p => new JVector(
                    p.X - centroid.X,
                    p.Y - centroid.Y,
                    p.Z - centroid.Z))
                .ToList();

            var shape = new PointCloudShape(centeredPoints);

            JitterBody = jitterWorld.CreateRigidBody();
            JitterBody.AddShape(shape);

            var pLock = PhysicsProperty.Lock;

            JitterBody.Position = new JVector(
                t.Position.X + centroid.X,
                t.Position.Y + centroid.Y,
                t.Position.Z + centroid.Z);

            JitterBody.AffectedByGravity = PhysicsProperty.UseGravity && !pLock.HasFlag(PhysicsLock.Gravity);
            JitterBody.Friction = pLock.HasFlag(PhysicsLock.Friction) ? 0f : PhysicsProperty.Friction;
            JitterBody.Restitution = PhysicsProperty.Bounciness;
            JitterBody.Damping = (
                pLock.HasFlag(PhysicsLock.LinearDamping) ? 0f : PhysicsProperty.LinearDamping,
                pLock.HasFlag(PhysicsLock.AngularDamping) ? 0f : PhysicsProperty.AngularDamping);
            JitterBody.Tag = NodeId;

            var rot = t.Rotation;
            var initRot = Quaternion.CreateFromYawPitchRoll(
                float.DegreesToRadians(rot.Y),
                float.DegreesToRadians(rot.X),
                float.DegreesToRadians(rot.Z));
            JitterBody.Orientation = new JQuaternion(initRot.X, initRot.Y, initRot.Z, initRot.W);

            _initialPosition = JitterBody.Position;
            _initialOrientation = JitterBody.Orientation;

            OwnedShapes.Add(shape);
        }

        // ── World transform helpers ──────────────────────────────────────────

        private JVector GetCurrentWorldPosition()
        {
            var node = Engine.Nodes.NodeManager.GetNode(NodeId);
            if (node == null) return JitterBody.Position;

            var matrix = TransformProperty._transform.ToMatrix();
            var parent = node.Parent;
            while (parent != null)
            {
                var parentTp = parent.GetProperty<TransformProperty>();
                if (parentTp == null) break;
                matrix = matrix * parentTp._transform.ToMatrix();
                parent = parent.Parent;
            }

            return new JVector(matrix.M41, matrix.M42, matrix.M43);
        }

        private Quaternion GetCurrentWorldRotation()
        {
            var node = Engine.Nodes.NodeManager.GetNode(NodeId);
            if (node == null) return Quaternion.Identity;

            var matrix = TransformProperty._transform.ToMatrix();
            var parent = node.Parent;
            while (parent != null)
            {
                var parentTp = parent.GetProperty<TransformProperty>();
                if (parentTp == null) break;
                matrix = matrix * parentTp._transform.ToMatrix();
                parent = parent.Parent;
            }

            Matrix4x4.Decompose(matrix, out _, out var rot, out _);
            return rot;
        }

        // ── Sync ─────────────────────────────────────────────────────────────

        public void SyncFromJitter(AxisLock lockPosition = AxisLock.None, AxisLock lockRotation = AxisLock.None)
        {
            EnforcePhysicsLocks();
            EnforceAxisLocks(lockPosition, lockRotation);

            var p = JitterBody.Position;
            var o = JitterBody.Orientation;
            var v = JitterBody.Velocity;

            var worldPos = new Vector3(
                p.X - _colliderCentroidOffset.X,
                p.Y - _colliderCentroidOffset.Y,
                p.Z - _colliderCentroidOffset.Z);

            var worldRot = Vector3.Zero;
            if (!PhysicsProperty.Lock.HasFlag(PhysicsLock.Rotation))
                worldRot = ToEuler(o);

            var node = Engine.Nodes.NodeManager.GetNode(NodeId);
            var parentTp = node?.Parent?.GetProperty<TransformProperty>();

            if (parentTp != null)
            {
                if (Matrix4x4.Invert(parentTp.WorldTransform.ToMatrix(), out var invParent))
                    TransformProperty._transform.Position = Vector3.Transform(worldPos, invParent);
                else
                    TransformProperty._transform.Position = worldPos;

                if (!PhysicsProperty.Lock.HasFlag(PhysicsLock.Rotation))
                    TransformProperty._transform.Rotation = worldRot - parentTp.WorldTransform.Rotation;
            }
            else
            {
                TransformProperty._transform.Position = worldPos;

                if (!PhysicsProperty.Lock.HasFlag(PhysicsLock.Rotation))
                    TransformProperty._transform.Rotation = worldRot;
            }

            TransformProperty.WorldTransform.Position = worldPos;

            if (!PhysicsProperty.Lock.HasFlag(PhysicsLock.Rotation))
                TransformProperty.WorldTransform.Rotation = worldRot;

            PhysicsProperty.Velocity = new Vector3(v.X, v.Y, v.Z);
        }

        public void SyncToJitter()
        {
            if (TransformProperty.TransformDirty)
            {
                var worldPos = GetCurrentWorldPosition();
                var worldRot = GetCurrentWorldRotation();

                JitterBody.Position = new JVector(
                    worldPos.X + _colliderCentroidOffset.X,
                    worldPos.Y + _colliderCentroidOffset.Y,
                    worldPos.Z + _colliderCentroidOffset.Z);

                JitterBody.Orientation = new JQuaternion(worldRot.X, worldRot.Y, worldRot.Z, worldRot.W);
                JitterBody.Velocity = JVector.Zero;
                JitterBody.AngularVelocity = JVector.Zero;

                TransformProperty.TransformDirty = false;
                PhysicsProperty.ManualVelocity = Vector3.Zero;
                PhysicsProperty.ManualImpulse = Vector3.Zero;
                return;
            }

            float currentY = JitterBody.Velocity.Y;
            var mv = PhysicsProperty.ManualVelocity;
            JitterBody.Velocity = new JVector(mv.X, currentY, mv.Z);

            if (PhysicsProperty.ManualImpulse != Vector3.Zero)
            {
                var v = JitterBody.Velocity;
                JitterBody.Velocity = new JVector(
                    v.X + PhysicsProperty.ManualImpulse.X,
                    v.Y + PhysicsProperty.ManualImpulse.Y,
                    v.Z + PhysicsProperty.ManualImpulse.Z);

                PhysicsProperty.ManualImpulse = Vector3.Zero;
            }

            PhysicsProperty.ManualVelocity = Vector3.Zero;
        }

        // ── Forces ───────────────────────────────────────────────────────────

        public void AddForce(Vector3 force)
            => JitterBody.AddForce(new JVector(force.X, force.Y, force.Z));

        public void AddImpulse(Vector3 impulse)
            => JitterBody.Velocity += new JVector(impulse.X, impulse.Y, impulse.Z);

        // ── Lock enforcement ─────────────────────────────────────────────────

        private void EnforceAxisLocks(AxisLock lockPosition, AxisLock lockRotation)
        {
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
                var sq = new Quaternion(q.X, q.Y, q.Z, q.W);
                var euler = ToEulerRadians(sq);

                if (lockRotation.HasFlag(AxisLock.X)) euler.X = 0f;
                if (lockRotation.HasFlag(AxisLock.Y)) euler.Y = 0f;
                if (lockRotation.HasFlag(AxisLock.Z)) euler.Z = 0f;

                var corrected = Quaternion.CreateFromYawPitchRoll(euler.Y, euler.X, euler.Z);
                JitterBody.Orientation = new JQuaternion(corrected.X, corrected.Y, corrected.Z, corrected.W);
            }
        }

        private void EnforcePhysicsLocks()
        {
            var pLock = PhysicsProperty.Lock;
            if (pLock == PhysicsLock.None) return;

            var vel = JitterBody.Velocity;
            if (pLock.HasFlag(PhysicsLock.LinearX)) vel.X = 0f;
            if (pLock.HasFlag(PhysicsLock.LinearY)) vel.Y = 0f;
            if (pLock.HasFlag(PhysicsLock.LinearZ)) vel.Z = 0f;
            JitterBody.Velocity = vel;

            var angVel = JitterBody.AngularVelocity;
            if (pLock.HasFlag(PhysicsLock.Rotation) || pLock.HasFlag(PhysicsLock.AllAngular))
            {
                JitterBody.AngularVelocity = JVector.Zero;
            }
            else
            {
                if (pLock.HasFlag(PhysicsLock.AngularX)) angVel.X = 0f;
                if (pLock.HasFlag(PhysicsLock.AngularY)) angVel.Y = 0f;
                if (pLock.HasFlag(PhysicsLock.AngularZ)) angVel.Z = 0f;
                JitterBody.AngularVelocity = angVel;
            }

            JitterBody.AffectedByGravity = PhysicsProperty.UseGravity && !pLock.HasFlag(PhysicsLock.Gravity);
            JitterBody.Friction = pLock.HasFlag(PhysicsLock.Friction) ? 0f : PhysicsProperty.Friction;
            JitterBody.Damping = (
                pLock.HasFlag(PhysicsLock.LinearDamping) ? 0f : PhysicsProperty.LinearDamping,
                pLock.HasFlag(PhysicsLock.AngularDamping) ? 0f : PhysicsProperty.AngularDamping);
        }
    }
}