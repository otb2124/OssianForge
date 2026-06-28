using Jitter2.Collision.Shapes;
using Jitter2.Dynamics;
using Jitter2.LinearMath;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Nodes.Props.Types.Physics;
using System.Numerics;

namespace OssianForge.Engine.Physics
{
    public class PhysicsRigidBody : PhysicsBody
    {
        public RigidBody JitterBody;

        private JVector _initialPosition;
        private JQuaternion _initialOrientation;
        private JVector _colliderCentroidOffset;

        private bool _isGrounded;

        public PhysicsRigidBody() : base()
        {
            
        }

        public override void Init(Node node)
        {
            base.Init(node);

            var transformProperty = node.GetProperty<TransformProperty>();
            var colliderProperty = node.GetProperty<ColliderProperty>();
            var physicsProperty = node.GetProperty<RigidPhysicsProperty>();

            var t = transformProperty.WorldTransform;
            var nodeScale = t.Scale;
            var collider = colliderProperty.ColliderResource;

            var shape = collider.CreateDynamicShape(nodeScale);

            // CapsuleShape (and any future native shape) is centered at origin — offset is zero.
            // MeshColliderResource centers its PointCloudShape at the AABB midpoint and
            // returns that shape already centered, so we read the offset from the AABB.
            _colliderCentroidOffset = collider is CapsuleColliderResource
                ? JVector.Zero
                : new JVector(
                    (collider.AabbMin.X + collider.AabbMax.X) * 0.5f * nodeScale.X,
                    (collider.AabbMin.Y + collider.AabbMax.Y) * 0.5f * nodeScale.Y,
                    (collider.AabbMin.Z + collider.AabbMax.Z) * 0.5f * nodeScale.Z);

            JitterBody = Engine.Physics.GetWorld(physicsProperty.WorldIndex).JitterWorld.CreateRigidBody();
            JitterBody.AddShape(shape);

            var pLock = physicsProperty.Lock;

            JitterBody.Position = new JVector(
                t.Position.X + _colliderCentroidOffset.X,
                t.Position.Y + _colliderCentroidOffset.Y,
                t.Position.Z + _colliderCentroidOffset.Z);

            JitterBody.AffectedByGravity = !pLock.HasFlag(PhysicsLock.Gravity);
            JitterBody.Friction = pLock.HasFlag(PhysicsLock.Friction) ? 0f : physicsProperty.Friction;
            JitterBody.Restitution = physicsProperty.Restitution;
            JitterBody.Damping = (
                pLock.HasFlag(PhysicsLock.LinearDamping) ? 0f : physicsProperty.LinearDamping,
                pLock.HasFlag(PhysicsLock.AngularDamping) ? 0f : physicsProperty.AngularDamping);
            JitterBody.Tag = node.Id;

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

        private JVector GetCurrentWorldPosition(Node node)
        {
            if (node == null) return JitterBody.Position;

            var transformProperty = node.GetProperty<TransformProperty>();

            var matrix = transformProperty._transform.ToMatrix();
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

        private Quaternion GetCurrentWorldRotation(Node node)
        {
            if (node == null) return Quaternion.Identity;

            var transformProperty = node.GetProperty<TransformProperty>();

            var matrix = transformProperty._transform.ToMatrix();
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
        public void SyncTo(Node node)
        {
            var transformProperty = node.GetProperty<TransformProperty>();
            var physicsProperty = node.GetProperty<RigidPhysicsProperty>();
            var colliderProperty = node.GetProperty<ColliderProperty>();

            if (transformProperty.TransformDirty)
            {
                var worldPos = GetCurrentWorldPosition(node);
                var worldRot = GetCurrentWorldRotation(node);

                JitterBody.Position = new JVector(
                    worldPos.X + _colliderCentroidOffset.X,
                    worldPos.Y + _colliderCentroidOffset.Y,
                    worldPos.Z + _colliderCentroidOffset.Z);

                JitterBody.Orientation = new JQuaternion(worldRot.X, worldRot.Y, worldRot.Z, worldRot.W);
                JitterBody.Velocity = JVector.Zero;
                JitterBody.AngularVelocity = JVector.Zero;

                transformProperty.TransformDirty = false;
                physicsProperty.ManualVelocity = Vector3.Zero;
                physicsProperty.ManualImpulse = Vector3.Zero;
                return;
            }

            
            var mv = physicsProperty.ManualVelocity;

            //add check if is grounded to pass
            if (mv != Vector3.Zero)
            {
                float currentY = JitterBody.Velocity.Y;
                float preservedY = currentY > 0.5f ? currentY : 0f;
                JitterBody.Velocity = new JVector(mv.X, preservedY, mv.Z);
            }

            if (colliderProperty.ColliderResource is CapsuleColliderResource)
            {
                JitterBody.SetActivationState(true);
            }

            if (physicsProperty.ManualImpulse != Vector3.Zero)
            {
                var v = JitterBody.Velocity;
                JitterBody.Velocity = new JVector(
                    v.X + physicsProperty.ManualImpulse.X,
                    v.Y + physicsProperty.ManualImpulse.Y,
                    v.Z + physicsProperty.ManualImpulse.Z);

                physicsProperty.ManualImpulse = Vector3.Zero;
            }

            physicsProperty.ManualVelocity = Vector3.Zero;
        }
        public void SyncFrom(Node node, AxisLock lockPosition = AxisLock.None, AxisLock lockRotation = AxisLock.None)
        {
            var transformProperty = node.GetProperty<TransformProperty>();
            var physicsProperty = node.GetProperty<RigidPhysicsProperty>();

            EnforcePhysicsLocks(node);
            EnforceAxisLocks(lockPosition, lockRotation);

            _isGrounded = JitterBody.Velocity.Y is > -0.5f and < 0.5f;

            var p = JitterBody.Position;
            var o = JitterBody.Orientation;
            var v = JitterBody.Velocity;

            var worldPos = new Vector3(
                p.X - _colliderCentroidOffset.X,
                p.Y - _colliderCentroidOffset.Y,
                p.Z - _colliderCentroidOffset.Z);

            var worldRot = Vector3.Zero;
            if (!physicsProperty.Lock.HasFlag(PhysicsLock.Rotation))
                worldRot = ToEuler(o);


            var parentTp = node?.Parent?.GetProperty<TransformProperty>();

            if (parentTp != null)
            {
                if (Matrix4x4.Invert(parentTp.WorldTransform.ToMatrix(), out var invParent))
                    transformProperty._transform.Position = Vector3.Transform(worldPos, invParent);
                else
                    transformProperty._transform.Position = worldPos;

                if (!physicsProperty.Lock.HasFlag(PhysicsLock.Rotation))
                    transformProperty._transform.Rotation = worldRot - parentTp.WorldTransform.Rotation;
            }
            else
            {
                transformProperty._transform.Position = worldPos;

                if (!physicsProperty.Lock.HasFlag(PhysicsLock.Rotation))
                    transformProperty._transform.Rotation = worldRot;
            }

            transformProperty.WorldTransform.Position = worldPos;

            if (!physicsProperty.Lock.HasFlag(PhysicsLock.Rotation))
                transformProperty.WorldTransform.Rotation = worldRot;
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

        private void EnforcePhysicsLocks(Node node)
        {
            var physicsProperty = node.GetProperty<RigidPhysicsProperty>();

            var pLock = physicsProperty.Lock;
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

            JitterBody.AffectedByGravity = !pLock.HasFlag(PhysicsLock.Gravity);
            JitterBody.Friction = pLock.HasFlag(PhysicsLock.Friction) ? 0f : physicsProperty.Friction;
            JitterBody.Damping = (
                pLock.HasFlag(PhysicsLock.LinearDamping) ? 0f : physicsProperty.LinearDamping,
                pLock.HasFlag(PhysicsLock.AngularDamping) ? 0f : physicsProperty.AngularDamping);
        }

        
    }
}