using System.Numerics;

namespace OssianForge.Engine.Nodes.Props
{
    public class PhysicalProperty : NodeProperty
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

        public PhysicalProperty(bool isStatic, bool useGravity,
                                float mass = 1f, float bounciness = 0f,
                                float friction = 0.6f,
                                float linearDamping = 0.02f,
                                float angularDamping = 0.05f)
        {
            IsStatic = isStatic;
            UseGravity = useGravity;
            Mass = mass;
            Bounciness = bounciness;
            Friction = friction;
            LinearDamping = linearDamping;
            AngularDamping = angularDamping;
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