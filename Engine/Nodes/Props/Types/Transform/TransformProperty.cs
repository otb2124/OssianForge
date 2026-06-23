using Jitter2.LinearMath;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static OssianForge.Engine.Utils.MathUtils;

namespace OssianForge.Engine.Nodes.Props
{
    public enum RenderSpace
    {
        World,
        Billboard,
        BillboardFree,
        ScreenSpace,
    }

    public enum Anchor
    {
        None,
        TopLeft, TopCenter, TopRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        BottomLeft, BottomCenter, BottomRight,
    }

    public class TransformProperty : NodeProperty
    {
        public bool TransformDirty = false;

        public Transform _transform;

        public Transform Transform
        {
            get => _transform;
            set
            {
                _transform = value;
                TransformDirty = true;
            }
        }

        public Vector3 Position
        {
            get => _transform.Position;
            set { _transform.Position = value; TransformDirty = true; }
        }

        public Vector3 Rotation
        {
            get => _transform.Rotation;
            set { _transform.Rotation = value; TransformDirty = true; }
        }

        public Vector3 Scale
        {
            get => _transform.Scale;
            set { _transform.Scale = value; TransformDirty = true; }
        }

        public Transform WorldTransform;

        public bool ClipsChildren = false;

        public RenderSpace RenderSpace = RenderSpace.World;
        public Anchor Anchor = Anchor.None;

        public TransformProperty(
            Transform transform,
            RenderSpace renderSpace = RenderSpace.World,
            Anchor anchor = Anchor.None)
        {
            _transform = transform;
            WorldTransform = transform;
            RenderSpace = renderSpace;
            Anchor = anchor;
        }

        public TransformProperty()
        {
            _transform = Transform.Default;
            WorldTransform = Transform.Default;
        }

        public override void OnStart(Node node)
        {
            base.OnStart(node);
            ApplyRenderSpaceDefaults(node);
            RecomputeWorldTransform(node);
        }

        public override void OnUpdate(Node node, double delta)
        {
            var anim = node.GetProperty<AnimationProperty>();
            if (anim != null && anim.ApplyRootMotion && anim.RootMotionDelta != Vector3.Zero)
            {
                float yawRad = float.DegreesToRadians(WorldTransform.Rotation.Y);
                float cos = MathF.Cos(yawRad);
                float sin = MathF.Sin(yawRad);
                Vector3 d = anim.RootMotionDelta;
                Vector3 worldDelta = new Vector3(
                    d.X * cos + d.Z * sin,
                    d.Y,
                    -d.X * sin + d.Z * cos);

                _transform.Position += worldDelta;
            }

            _transform.Rotation = new Vector3(
                _transform.Rotation.X,
                NormalizeAngle(_transform.Rotation.Y),
                _transform.Rotation.Z);

            RecomputeWorldTransform(node);
        }

        internal void SetTransformFromPhysics(Vector3 position, Vector3 rotation)
        {
            _transform.Position = position;
            _transform.Rotation = rotation;
            // deliberately does NOT set TransformDirty
        }

        private static float NormalizeAngle(float degrees)
        {
            degrees %= 360f;
            if (degrees < 0f) degrees += 360f;
            return degrees;
        }

        private void RecomputeWorldTransform(Node node)
        {
            if (RenderSpace == RenderSpace.ScreenSpace)
            {
                WorldTransform = _transform;
                return;
            }

            TransformProperty parentTransform = node?.Parent?.GetProperty<TransformProperty>();
            if (parentTransform == null)
            {
                WorldTransform = _transform;
                return;
            }

            Matrix4x4 world = _transform.ToMatrix() * parentTransform.WorldTransform.ToMatrix();
            WorldTransform.SetMatrix(world);
        }

        private void ApplyRenderSpaceDefaults(Node node)
        {
            if (RenderSpace != RenderSpace.ScreenSpace)
                return;

            Vector2 screen = new Vector2(
                Engine.Graphics.WindowSize.X,
                Engine.Graphics.WindowSize.Y);

            TransformProperty parentTransform = node?.Parent?.GetProperty<TransformProperty>();
            bool hasScreenSpaceParent = parentTransform != null
                                     && parentTransform.RenderSpace == RenderSpace.ScreenSpace;

            Vector2 containerSizePx;
            Vector2 containerCenterPx;

            if (hasScreenSpaceParent)
            {
                containerSizePx = new Vector2(
                    parentTransform._transform.Scale.X * screen.X * 0.5f,
                    parentTransform._transform.Scale.Y * screen.Y * 0.5f);

                containerCenterPx = new Vector2(
                    parentTransform._transform.Position.X * screen.X * 0.5f,
                    parentTransform._transform.Position.Y * screen.Y * 0.5f);
            }
            else
            {
                containerSizePx = screen;
                containerCenterPx = Vector2.Zero;
            }

            Vector2 halfContainer = containerSizePx * 0.5f;
            Vector2 halfSelf = new Vector2(_transform.Scale.X * 0.5f, _transform.Scale.Y * 0.5f);

            Vector2 localOffset = new Vector2(_transform.Position.X, _transform.Position.Y);

            Vector2 centerInContainer;
            if (Anchor != Anchor.None)
            {
                Vector2 anchorFlush = AnchorOrigin(Anchor, halfContainer, halfSelf);
                centerInContainer = anchorFlush + localOffset;
            }
            else
            {
                centerInContainer = localOffset;
            }

            Vector2 centerInScreen = containerCenterPx + centerInContainer;

            _transform.Position = new Vector3(centerInScreen.X, centerInScreen.Y, _transform.Position.Z);
            _transform.ToScreenSpace(screen);  // mutates _transform directly — no copy problem
        }

        public void SetMatrix(Matrix4x4 matrix) => _transform.SetMatrix(matrix);  // mutates _transform directly

        public Matrix4x4 GetCameraModel()
        {
            var cam = Engine.Graphics.GetCurrentCamera();
            return RenderSpace switch
            {
                RenderSpace.Billboard => cam.GetBillboardModel(WorldTransform),
                RenderSpace.BillboardFree => cam.GetBillboardFreeModel(WorldTransform),
                RenderSpace.ScreenSpace => cam.GetScreenSpaceModel(WorldTransform),
                _ => WorldTransform.ToMatrix()
            };
        }

        public Matrix4x4 GetCameraView()
        {
            if (RenderSpace == RenderSpace.ScreenSpace)
                return Matrix4x4.Identity;
            return Engine.Graphics.GetCurrentCamera().GetView();
        }

        public Matrix4x4 GetCameraProjection()
        {
            if (RenderSpace == RenderSpace.ScreenSpace)
                return Matrix4x4.Identity;
            return Engine.Graphics.GetCurrentCamera().GetProjection();
        }

        private static Vector2 AnchorOrigin(Anchor anchor, Vector2 halfContainer, Vector2 halfSelf)
        {
            float hw = halfContainer.X;
            float hh = halfContainer.Y;
            float ex = halfSelf.X;
            float ey = halfSelf.Y;

            return anchor switch
            {
                Anchor.TopLeft => new Vector2(-hw + ex, hh - ey),
                Anchor.TopCenter => new Vector2(0, hh - ey),
                Anchor.TopRight => new Vector2(hw - ex, hh - ey),
                Anchor.MiddleLeft => new Vector2(-hw + ex, 0),
                Anchor.MiddleCenter => new Vector2(0, 0),
                Anchor.MiddleRight => new Vector2(hw - ex, 0),
                Anchor.BottomLeft => new Vector2(-hw + ex, -hh + ey),
                Anchor.BottomCenter => new Vector2(0, -hh + ey),
                Anchor.BottomRight => new Vector2(hw - ex, -hh + ey),
                _ => Vector2.Zero,
            };
        }
    }
}