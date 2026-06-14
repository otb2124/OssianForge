using OssianForge.Engine.Inputs;
using System.Numerics;
using static OssianForge.Engine.Utils.MathUtils;

namespace OssianForge.Engine.Graphics.Camera
{
    public class Camera
    {
        public Vector3 Position = new Vector3(0, 1.5f, 3f);
        public float Fov = 45f;
        public float AspectRatio;

        private float _yaw = -90f; // facing forward by default
        private float _pitch = 0f;

        private Vector2 _lastMousePos;
        private bool _firstMouse = true;

        private const float MoveSpeed = 3f;
        private const float MouseSensitivity = 0.3f;
        private const float ZoomSpeed = 2f;
        private const float MinFov = 10f;
        private const float MaxFov = 120f;

        public Camera()
        {
            AspectRatio = (float)Engine.Graphics.WindowSize.X / Engine.Graphics.WindowSize.Y;
        }

        public void OnUpdate(double delta)
        {
            ControlCamera((float)delta);
        }

        private void ControlCamera(float delta)
        {
            ControlMouseRotation();
            ControlMovement(delta);
            ControlZoom();
        }

        private void ControlMouseRotation()
        {
            Vector2 mousePos = Engine.Inputs.mouse.Position;

            if (_firstMouse)
            {
                _lastMousePos = mousePos;
                _firstMouse = false;
                return;
            }

            Vector2 diff = mousePos - _lastMousePos;
            _lastMousePos = mousePos;

            _yaw += diff.X * MouseSensitivity;
            _pitch = Math.Clamp(_pitch - diff.Y * MouseSensitivity, -89f, 89f);
        }

        private void ControlMovement(float delta)
        {
            var keys = Engine.Inputs.KeyHandler;
            Vector3 forward = GetForward();
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));

            if (keys.IsStateActive(KeyHandler.KeyStates.MOVEUPPRESSED))
                Position += forward * MoveSpeed * delta;
            if (keys.IsStateActive(KeyHandler.KeyStates.MOVEDOWNPRESSED))
                Position -= forward * MoveSpeed * delta;
            if (keys.IsStateActive(KeyHandler.KeyStates.MOVELEFTPRESSED))
                Position -= right * MoveSpeed * delta;
            if (keys.IsStateActive(KeyHandler.KeyStates.MOVERIGHTPRESSED))
                Position += right * MoveSpeed * delta;
        }

        private void ControlZoom()
        {
            var keys = Engine.Inputs.KeyHandler;
            if (keys.IsStateActive(KeyHandler.KeyStates.CAMERAZOOMUPPRESSED))
                Fov = Math.Clamp(Fov - ZoomSpeed, MinFov, MaxFov);
            if (keys.IsStateActive(KeyHandler.KeyStates.CAMERAZOOMDOWNPRESSED))
                Fov = Math.Clamp(Fov + ZoomSpeed, MinFov, MaxFov);
        }

        public Matrix4x4 GetView()
        {
            return Matrix4x4.CreateLookAt(Position, Position + GetForward(), Vector3.UnitY);
        }

        public Matrix4x4 GetProjection()
        {
            return Matrix4x4.CreatePerspectiveFieldOfView(
                float.DegreesToRadians(Fov),
                AspectRatio,
                0.1f, 100f);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Billboard — Y-axis locked (trees, sprites, light icons)
        // The quad always faces the camera but cannot tilt; "up" is always world Y.
        // ──────────────────────────────────────────────────────────────────────────
        public Matrix4x4 GetBillboardModel(Transform transform)
        {
            Matrix4x4.Invert(GetView(), out var invView);
            var right = new Vector3(invView.M11, invView.M12, invView.M13);
            var up = new Vector3(invView.M21, invView.M22, invView.M23);
            var forward = new Vector3(invView.M31, invView.M32, invView.M33);

            var billboard = new Matrix4x4(
                right.X, right.Y, right.Z, 0,
                up.X, up.Y, up.Z, 0,
                forward.X, forward.Y, forward.Z, 0,
                0, 0, 0, 1
            );

            return Matrix4x4.CreateScale(transform.Scale.X, transform.Scale.Y, 1.0f)
                 * billboard
                 * Matrix4x4.CreateTranslation(transform.Position);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // BillboardFree — all axes rotate (particles, sparks, debris)
        // Uses the camera's actual up vector so the quad can freely spin in world space.
        // Identical math to Billboard; the difference is intent — use this when
        // you want the sprite to roll with the camera (e.g. a spark viewed from below).
        // ──────────────────────────────────────────────────────────────────────────
        public Matrix4x4 GetBillboardFreeModel(Transform transform)
        {
            // Face the camera from the object's position using the camera's full orientation.
            // Unlike the Y-locked version we do not force the up axis to Vector3.UnitY, so
            // the quad inherits whatever tilt the camera has.
            Vector3 toCamera = Vector3.Normalize(Position - transform.Position);
            Vector3 camUp = new Vector3(0, 1, 0); // world up as fallback

            Matrix4x4.Invert(GetView(), out var invView);
            Vector3 cameraUp = new Vector3(invView.M21, invView.M22, invView.M23);

            Vector3 right = Vector3.Normalize(Vector3.Cross(cameraUp, toCamera));
            Vector3 up = Vector3.Normalize(Vector3.Cross(toCamera, right));

            var billboard = new Matrix4x4(
                right.X, right.Y, right.Z, 0,
                up.X, up.Y, up.Z, 0,
                toCamera.X, toCamera.Y, toCamera.Z, 0,
                0, 0, 0, 1
            );

            return Matrix4x4.CreateScale(transform.Scale.X, transform.Scale.Y, 1.0f)
                 * billboard
                 * Matrix4x4.CreateTranslation(transform.Position);
        }

        public Matrix4x4 GetScreenSpaceModel(Transform transform)
        {
            float rollRad = float.DegreesToRadians(transform.Rotation.Z);
            float pitchRad = float.DegreesToRadians(transform.Rotation.X);
            float yawRad = float.DegreesToRadians(transform.Rotation.Y);

            float scaleZ = (transform.Scale.X + transform.Scale.Y) * 0.5f;

            return Matrix4x4.CreateRotationX(pitchRad)
                 * Matrix4x4.CreateRotationY(yawRad)
                 * Matrix4x4.CreateRotationZ(rollRad)
                 * Matrix4x4.CreateScale(transform.Scale.X, transform.Scale.Y, scaleZ)
                 * Matrix4x4.CreateTranslation(transform.Position.X, transform.Position.Y, 0f);
        }


        public Matrix4x4 GetScreenSpaceView()
        {
            return Matrix4x4.CreateLookAt(
                new Vector3(0f, 0f, 1000f),
                new Vector3(0f, 0f, 0f),
                Vector3.UnitY
            );
        }

        public Matrix4x4 GetScreenSpaceProjection()
        {
            var screen = Engine.Graphics.WindowSize;
            return Matrix4x4.CreateOrthographicOffCenter(
                left: 0f,
                right: screen.X,
                bottom: 0f,
                top: screen.Y,
                zNearPlane: 0.1f,
                zFarPlane: 2000f
            );
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Shared helpers
        // ──────────────────────────────────────────────────────────────────────────

        public Matrix4x4 GetViewNoTranslation()
        {
            var view = GetView();
            return new Matrix4x4(
                view.M11, view.M12, view.M13, 0,
                view.M21, view.M22, view.M23, 0,
                view.M31, view.M32, view.M33, 0,
                0, 0, 0, 1);
        }

        // Forward direction derived from yaw/pitch
        private Vector3 GetForward()
        {
            float yawRad = float.DegreesToRadians(_yaw);
            float pitchRad = float.DegreesToRadians(_pitch);
            return Vector3.Normalize(new Vector3(
                MathF.Cos(pitchRad) * MathF.Cos(yawRad),
                MathF.Sin(pitchRad),
                MathF.Cos(pitchRad) * MathF.Sin(yawRad)
            ));
        }
    }
}