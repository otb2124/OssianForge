using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static OssianForge.Engine.Utils.Math;

namespace OssianForge.Engine.Nodes.Props
{
    public class PhysicalProperty : NodeProperty
    {

        public Vector3 Velocity;
        public float Mass = 1f;
        public float Bounciness = 0f;
        public bool IsStatic = false;
        public bool UseGravity = true;

        public PhysicalProperty(bool isStatic, bool useGravity, float mass = 1f, float bounciness = 0f)
        {
            IsStatic = isStatic;
            UseGravity = useGravity;
            Mass = mass;
            Bounciness = bounciness;
        }

        public void AddForce(Vector3 force)
        {
            if (!IsStatic)
                Velocity += force / Mass;
        }

        public void AddImpulse(Vector3 impulse)
        {
            if (!IsStatic)
                Velocity += impulse;
        }
    }
}
