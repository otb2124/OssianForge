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

    public enum Anchor2D
    {
        None,
        TopLeft, TopCenter, TopRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        BottomLeft, BottomCenter, BottomRight,
    }

    [Flags]
    public enum PropagationLock
    {
        None = 0,
        PosX = 1 << 0,
        PosY = 1 << 1,
        PosZ = 1 << 2,
        RotX = 1 << 3,
        RotY = 1 << 4,
        RotZ = 1 << 5,
        ScaleX = 1 << 6,
        ScaleY = 1 << 7,
        ScaleZ = 1 << 8,

        Position = PosX | PosY | PosZ,
        Rotation = RotX | RotY | RotZ,
        Scale = ScaleX | ScaleY | ScaleZ,
        All = Position | Rotation | Scale,
    }

    public class TransformProperty : NodeProperty
    {
        public bool TransformDirty = false;

        public Transform InitialTransform;
        public Transform _transform;

        public Transform Transform
        {
            get => _transform;
            set { _transform = value; SetDirty(); }
        }

        public Vector3 Position
        {
            get => _transform.Position;
            set { _transform.Position = value; SetDirty(); }
        }

        public Vector3 Rotation
        {
            get => _transform.Rotation;
            set { _transform.Rotation = value; SetDirty(); }
        }

        public Vector3 Scale
        {
            get => _transform.Scale;
            set { _transform.Scale = value; SetDirty(); }
        }

        public Transform WorldTransform;

        public bool ClipsChildren = false;

        public RenderSpace RenderSpace = RenderSpace.World;
        public Anchor2D Anchor = Anchor2D.None;

        public PropagationLock StopPropagation = PropagationLock.None;

        public TransformProperty(
            Transform transform,
            RenderSpace renderSpace = RenderSpace.World,
            Anchor2D anchor = Anchor2D.None, PropagationLock stopProp = PropagationLock.None)
        {
            InitialTransform = transform;
            _transform = transform;
            WorldTransform = transform;
            RenderSpace = renderSpace;
            Anchor = anchor;
            StopPropagation = stopProp;
        }

        public TransformProperty()
        {
            _transform = Transform.Default;
            WorldTransform = Transform.Default;
        }

        public override void OnStart(Node node)
        {
            base.OnStart(node);

            var parentTp = node.Parent?.GetProperty<TransformProperty>();

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

        public void RecomputeWorldTransform(Node node)
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

            if (StopPropagation != PropagationLock.None)
            {
                var w = WorldTransform;
                var l = _transform;

                if (StopPropagation.HasFlag(PropagationLock.PosX)) w.Position.X = l.Position.X;
                if (StopPropagation.HasFlag(PropagationLock.PosY)) w.Position.Y = l.Position.Y;
                if (StopPropagation.HasFlag(PropagationLock.PosZ)) w.Position.Z = l.Position.Z;
                if (StopPropagation.HasFlag(PropagationLock.RotX)) w.Rotation.X = l.Rotation.X;
                if (StopPropagation.HasFlag(PropagationLock.RotY)) w.Rotation.Y = l.Rotation.Y;
                if (StopPropagation.HasFlag(PropagationLock.RotZ)) w.Rotation.Z = l.Rotation.Z;
                if (StopPropagation.HasFlag(PropagationLock.ScaleX)) w.Scale.X = l.Scale.X;
                if (StopPropagation.HasFlag(PropagationLock.ScaleY)) w.Scale.Y = l.Scale.Y;
                if (StopPropagation.HasFlag(PropagationLock.ScaleZ)) w.Scale.Z = l.Scale.Z;

                WorldTransform = w;
            }
        }

        public void ApplyRenderSpaceDefaults(Node node)
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
                // Parent scale is already NDC — convert back to pixels
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
            if (Anchor != Anchor2D.None)
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


        private void SetDirty()
        {
            TransformDirty = true;
            var node = Engine.Nodes.NodeManager.GetNode(NodeId);
            if (node == null) return;
            foreach (var child in node.Children)
                child.GetProperty<TransformProperty>()?.SetDirtyRecursive();
        }

        private void SetDirtyRecursive()
        {
            TransformDirty = true;
            var node = Engine.Nodes.NodeManager.GetNode(NodeId);
            if (node == null) return;
            foreach (var child in node.Children)
                child.GetProperty<TransformProperty>()?.SetDirtyRecursive();
        }

        private static Vector2 AnchorOrigin(Anchor2D anchor, Vector2 halfContainer, Vector2 halfSelf)
        {
            float hw = halfContainer.X;
            float hh = halfContainer.Y;
            float ex = halfSelf.X;
            float ey = halfSelf.Y;

            return anchor switch
            {
                Anchor2D.TopLeft => new Vector2(-hw + ex, hh - ey),
                Anchor2D.TopCenter => new Vector2(0, hh - ey),
                Anchor2D.TopRight => new Vector2(hw - ex, hh - ey),
                Anchor2D.MiddleLeft => new Vector2(-hw + ex, 0),
                Anchor2D.MiddleCenter => new Vector2(0, 0),
                Anchor2D.MiddleRight => new Vector2(hw - ex, 0),
                Anchor2D.BottomLeft => new Vector2(-hw + ex, -hh + ey),
                Anchor2D.BottomCenter => new Vector2(0, -hh + ey),
                Anchor2D.BottomRight => new Vector2(hw - ex, -hh + ey),
                _ => Vector2.Zero,
            };
        }
    }
}