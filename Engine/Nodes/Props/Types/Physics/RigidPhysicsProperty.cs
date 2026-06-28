using OssianForge.Engine.Physics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Nodes.Props.Types.Physics
{

    public class RigidPhysicsProperty : PhysicsProperty
    {
        public Vector3 ManualVelocity;
        public Vector3 ManualImpulse;
        public float Mass = 1f;
        public float Restitution = 0f;
        public float LinearDamping = 0.02f;
        public float AngularDamping = 0.05f;
        public float Friction = 0.6f;
        public PhysicsLock Lock = PhysicsLock.None;

        private readonly PhysicsRigidBody _body = new();
        public override PhysicsBody Body => _body;
        public PhysicsRigidBody RigidBody => _body;

        private Action? _preStep;
        private Action? _postStep;

        public RigidPhysicsProperty(int worldIndex,
                                  float mass = 1f,
                                  float bounciness = 0f,
                                  float friction = 0.6f,
                                  float linearDamping = 0.02f,
                                  float angularDamping = 0.05f,
                                  PhysicsLock physLock = PhysicsLock.None)
            : base(worldIndex)
        {
            Mass = mass;
            Restitution = bounciness;
            LinearDamping = linearDamping;
            AngularDamping = angularDamping;
            Friction = friction;
            Lock = physLock;
        }

        public override void OnStart(Node node)
        {
            base.OnStart(node);
            var world = Engine.Physics.GetWorld(WorldIndex);
            _preStep = () => _body.SyncTo(node);
            _postStep = () => _body.SyncFrom(node);
            world.OnPreStep += _preStep;
            world.OnPostStep += _postStep;
        }

        public void AddForce(Vector3 force) { } //=> Velocity += force / Mass;
        public void AddImpulse(Vector3 impulse) { } //=> Velocity += impulse;
    }
}
