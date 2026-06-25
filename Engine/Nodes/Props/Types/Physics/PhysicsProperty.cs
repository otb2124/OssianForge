using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    [Flags]
    public enum PhysicsLock
    {
        None = 0,
        Friction = 1 << 0,
        Gravity = 1 << 1,
        LinearDamping = 1 << 2,
        AngularDamping = 1 << 3,
        Rotation = 1 << 4,
        LinearX = 1 << 5,
        LinearY = 1 << 6,
        LinearZ = 1 << 7,
        AngularX = 1 << 8,
        AngularY = 1 << 9,
        AngularZ = 1 << 10,

        AllLinear = LinearX | LinearY | LinearZ,
        AllAngular = AngularX | AngularY | AngularZ | Rotation,
        AllDamping = LinearDamping | AngularDamping,
    }

    public class PhysicsProperty : NodeProperty
    {
        public Vector3 Velocity;
        public float Mass = 1f;
        public float Bounciness = 0f;
        public float Friction = 0.6f;
        public float LinearDamping = 0.02f;
        public float AngularDamping = 0.05f;
        public bool IsStatic = false;
        public bool UseGravity = true;

        public int WorldIndex = 0;

        public Vector3 ManualVelocity;
        public Vector3 ManualImpulse;

        public PhysicsLock Lock = PhysicsLock.None;

        public PhysicsProperty(int worldIndex, bool isStatic, bool useGravity,
                                float mass = 1f, float bounciness = 0f,
                                float friction = 0.6f,
                                float linearDamping = 0.02f,
                                float angularDamping = 0.05f, PhysicsLock physLock = PhysicsLock.None)
        {
            WorldIndex = worldIndex;
            IsStatic = isStatic;
            UseGravity = useGravity;
            Mass = mass;
            Bounciness = bounciness;
            Friction = friction;
            LinearDamping = linearDamping;
            AngularDamping = angularDamping;
            Lock = physLock;
        }

        public void SetWorld(int worldIndex)
        {
            WorldIndex = worldIndex;
        }

        public void AddForce(Vector3 force)
        {
            if (!IsStatic) Velocity += force / Mass;
        }

        public void AddImpulse(Vector3 impulse)
        {
            if (!IsStatic) Velocity += impulse;
        }
    }
}