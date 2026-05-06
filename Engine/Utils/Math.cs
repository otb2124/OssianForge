using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Utils
{
    public static class Math
    {

        public struct Transform
        {
            public Vector3 Position;
            public Vector3 Rotation; // Euler angles in degrees (X, Y, Z)
            public Vector3 Scale;

            public static Transform Default => new Transform
            {
                Position = Vector3.Zero,
                Rotation = Vector3.Zero,
                Scale = Vector3.One
            };

            public Transform(Vector3 position, Vector3 rotation, Vector3 scale)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }

            public Matrix4x4 ToMatrix()
            {
                Matrix4x4 translation = Matrix4x4.CreateTranslation(Position);
                Matrix4x4 rotX = Matrix4x4.CreateRotationX(float.DegreesToRadians(Rotation.X));
                Matrix4x4 rotY = Matrix4x4.CreateRotationY(float.DegreesToRadians(Rotation.Y));
                Matrix4x4 rotZ = Matrix4x4.CreateRotationZ(float.DegreesToRadians(Rotation.Z));
                Matrix4x4 scale = Matrix4x4.CreateScale(Scale);

                return scale * rotX * rotY * rotZ * translation;
            }
        }
    }
}
