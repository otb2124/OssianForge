using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Utils
{
    public static class MathUtils
    {
        public static uint NextPowerOfTwo(uint v)
        {
            v--;
            v |= v >> 1; v |= v >> 2; v |= v >> 4;
            v |= v >> 8; v |= v >> 16;
            return v + 1;
        }

        public class Signal
        {
            private readonly List<Action> _listeners = new();

            public void Connect(Action listener) => _listeners.Add(listener);
            public void Disconnect(Action listener) => _listeners.Remove(listener);
            public void Emit()
            {
                foreach (var listener in _listeners)
                    listener?.Invoke();
            }
        }

        public class Signal<T>
        {
            private readonly List<Action<T>> _listeners = new();

            public void Connect(Action<T> listener) => _listeners.Add(listener);
            public void Disconnect(Action<T> listener) => _listeners.Remove(listener);
            public void Emit(T value)
            {
                foreach (var listener in _listeners)
                    listener?.Invoke(value);
            }
        }

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


            public void SetMatrix(Matrix4x4 matrix)
            {
                Matrix4x4.Decompose(matrix, out Vector3 scale, out Quaternion rotation, out Vector3 translation);

                Position = translation;
                Scale = scale;

                // Convert quaternion back to euler degrees
                Rotation = QuaternionToEulerDegrees(rotation);
            }

            private static Vector3 QuaternionToEulerDegrees(Quaternion q)
            {
                Vector3 angles;

                // X (pitch)
                float sinrCosp = 2f * (q.W * q.X + q.Y * q.Z);
                float cosrCosp = 1f - 2f * (q.X * q.X + q.Y * q.Y);
                angles.X = MathF.Atan2(sinrCosp, cosrCosp);

                // Y (yaw)
                float sinp = 2f * (q.W * q.Y - q.Z * q.X);
                angles.Y = MathF.Abs(sinp) >= 1f
                    ? MathF.CopySign(MathF.PI / 2f, sinp)
                    : MathF.Asin(sinp);

                // Z (roll)
                float sinyCosp = 2f * (q.W * q.Z + q.X * q.Y);
                float cosyCosp = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
                angles.Z = MathF.Atan2(sinyCosp, cosyCosp);

                // Radians to degrees
                return new Vector3(
                    float.RadiansToDegrees(angles.X),
                    float.RadiansToDegrees(angles.Y),
                    float.RadiansToDegrees(angles.Z)
                );
            }
        }
    }
}
