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

                Rotation = QuaternionToEulerDegrees(rotation);
            }

            private static Vector3 QuaternionToEulerDegrees(Quaternion q)
            {
                Vector3 angles;

                float sinrCosp = 2f * (q.W * q.X + q.Y * q.Z);
                float cosrCosp = 1f - 2f * (q.X * q.X + q.Y * q.Y);
                angles.X = MathF.Atan2(sinrCosp, cosrCosp);

                float sinp = 2f * (q.W * q.Y - q.Z * q.X);
                angles.Y = MathF.Abs(sinp) >= 1f
                    ? MathF.CopySign(MathF.PI / 2f, sinp)
                    : MathF.Asin(sinp);

                float sinyCosp = 2f * (q.W * q.Z + q.X * q.Y);
                float cosyCosp = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
                angles.Z = MathF.Atan2(sinyCosp, cosyCosp);

                return new Vector3(
                    float.RadiansToDegrees(angles.X),
                    float.RadiansToDegrees(angles.Y),
                    float.RadiansToDegrees(angles.Z)
                );
            }

            /// <summary>
            /// Converts this transform from screen-space pixel coordinates to NDC.
            ///
            /// Coordinate convention (pixel space, input):
            ///   - Origin (0, 0) is the CENTER of the screen.
            ///   - X grows right, Y grows up (matches NDC / math convention).
            ///   - Position is the CENTER of the element (half-extent is implicit in Scale).
            ///   - Scale.X / Scale.Y are the element's width / height in pixels.
            ///
            /// After the call (NDC space, output):
            ///   - Position maps [-screenW/2 .. screenW/2] → [-1 .. 1]  (same for Y)
            ///   - Scale    maps pixel size              → NDC size  (size / screen * 2)
            /// </summary>
            public void ToScreenSpace(Vector2 screen)
            {
                // NDC size: element width/height expressed as a fraction of the half-screen.
                float ndcScaleX = Scale.X / screen.X * 2f;
                float ndcScaleY = Scale.Y / screen.Y * 2f;

                // Center-origin pixel → NDC.
                // Input range: [-screen/2 .. screen/2]  → output: [-1 .. 1]
                float ndcX = Position.X / (screen.X * 0.5f);
                float ndcY = Position.Y / (screen.Y * 0.5f);

                Position = new Vector3(ndcX, ndcY, Position.Z);
                Scale = new Vector3(ndcScaleX, ndcScaleY, Scale.Z);
            }

            /// <summary>
            /// Inverse of ToScreenSpace. Converts NDC back to center-origin pixel space.
            /// </summary>
            public void FromScreenSpace(Vector2 screen)
            {
                float pixelScaleX = Scale.X * screen.X * 0.5f;
                float pixelScaleY = Scale.Y * screen.Y * 0.5f;

                float pixelX = Position.X * screen.X * 0.5f;
                float pixelY = Position.Y * screen.Y * 0.5f;

                Position = new Vector3(pixelX, pixelY, Position.Z);
                Scale = new Vector3(pixelScaleX, pixelScaleY, Scale.Z);
            }
        }
    }
}