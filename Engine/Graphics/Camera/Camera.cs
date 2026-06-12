using OssianForge.Engine.Inputs;
using System.Numerics;
using static OssianForge.Engine.Utils.Math;

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
        public Matrix4x4 GetBillboardMatrix(Transform transform)
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
        public Matrix4x4 GetBillboardFreeMatrix(Transform transform)
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

        // ──────────────────────────────────────────────────────────────────────────
        // ScreenSpace — NDC-mapped, scales with FOV (diegetic UI, world-attached HUD)
        //
        // Transform.Position.XY are NDC coords:  (-1,-1) = bottom-left of screen
        //                                          ( 0, 0) = center
        //                                          ( 1, 1) = top-right
        // Transform.Position.Z  is ignored        (depth is fixed internally)
        // Transform.Scale.XY    are fractions of the half-screen size at depth
        //
        // This is placed in world space at a fixed depth in front of the camera, so
        // it still interacts with the depth buffer (useful for in-world overlays).
        // If you want a true HUD that ignores depth and FOV, use ScreenSpaceFixed.
        // ──────────────────────────────────────────────────────────────────────────
        public Matrix4x4 GetScreenSpaceMatrix(Transform transform)
        {
            Vector3 forward = GetForward();
            Matrix4x4.Invert(GetView(), out var invView);
            var right = new Vector3(invView.M11, invView.M12, invView.M13);
            var up = new Vector3(invView.M21, invView.M22, invView.M23);

            const float depth = 2.0f;

            // Half-extents of the frustum at this depth — this is the correct geometric formula.
            // At depth d: half-height = tan(fov/2) * d, half-width = half-height * aspect
            float halfH = MathF.Tan(float.DegreesToRadians(Fov) * 0.5f) * depth;
            float halfW = halfH * AspectRatio;

            // transform.Position.XY in NDC [-1,1] maps to actual world offsets
            Vector3 worldPos = Position
                + forward * depth
                + right * (transform.Position.X * halfW)
                + up * (transform.Position.Y * halfH);

            // Scale so that Scale(1,1) = one full half-screen unit
            float scaleX = transform.Scale.X * halfW;
            float scaleY = transform.Scale.Y * halfH;

            var billboard = new Matrix4x4(
                right.X, right.Y, right.Z, 0,
                up.X, up.Y, up.Z, 0,
                forward.X, forward.Y, forward.Z, 0,
                0, 0, 0, 1
            );

            return Matrix4x4.CreateScale(scaleX, scaleY, 1f)
                 * billboard
                 * Matrix4x4.CreateTranslation(worldPos);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Shared helpers
        // ──────────────────────────────────────────────────────────────────────────

        public (Matrix4x4 view, Matrix4x4 viewNoTranslation) GetViewMatrices()
        {
            var view = GetView();
            var viewNoTranslation = new Matrix4x4(
                view.M11, view.M12, view.M13, 0,
                view.M21, view.M22, view.M23, 0,
                view.M31, view.M32, view.M33, 0,
                0, 0, 0, 1);
            return (view, viewNoTranslation);
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