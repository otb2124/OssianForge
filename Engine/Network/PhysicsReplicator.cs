using System.Numerics;
using Jitter2.LinearMath;
using OssianForge.Content.Cooked;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Nodes.Props.Types.Physics;

namespace OssianForge.Engine.Network.Replication
{
    /// <summary>
    /// Physics role handling + the physics facts other peers can use (velocity, grounded).
    /// Owner simulates normally. On a proxy the body is turned into a follower: no gravity, no velocity,
    /// it is teleported to the replicated transform by the existing TransformDirty path in SyncTo.
    /// </summary>
    public sealed class PhysicsReplicator : NetReplicator
    {
        public override byte WireId => 3;

        public static NetReplicator? Create(Node n)
            => n.GetProperty<RigidPhysicsProperty>() == null ? null : new PhysicsReplicator();

        /// <summary>Last velocity / grounded flag received from the owner (proxy side).</summary>
        public Vector3 RemoteVelocity { get; private set; }
        public bool RemoteGrounded { get; private set; }

        private RigidPhysicsProperty _phys = null!;
        private bool _sent, _lastGrounded;
        private Vector3 _lastVel;

        public override void OnAttach() => _phys = Node.GetProperty<RigidPhysicsProperty>()!;

        public override void OnRoleChanged(bool isProxy)
        {
            var body = _phys.RigidBody.JitterBody;
            if (body == null) return;

            if (isProxy)
            {
                body.AffectedByGravity = false;
                body.Velocity = JVector.Zero;
                body.AngularVelocity = JVector.Zero;
            }
            else
            {
                body.AffectedByGravity = !_phys.Lock.HasFlag(PhysicsLock.Gravity);
            }
        }

        public override bool WriteState(BlobWriter w, bool full)
        {
            var body = _phys.RigidBody.JitterBody;
            if (body == null) return false;

            var v = new Vector3(body.Velocity.X, body.Velocity.Y, body.Velocity.Z);
            bool grounded = _phys.RigidBody.IsGrounded;

            bool changed = full || !_sent || grounded != _lastGrounded || (v - _lastVel).LengthSquared() > 0.01f;
            if (!changed) return false;

            w.Struct(v);
            w.U8((byte)(grounded ? 1 : 0));

            if (!full) { _sent = true; _lastVel = v; _lastGrounded = grounded; }
            return true;
        }

        public override void ReadState(BlobReader r)
        {
            RemoteVelocity = r.Struct<Vector3>();
            RemoteGrounded = r.U8() != 0;
        }
    }
}