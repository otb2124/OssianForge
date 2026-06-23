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
        // Authored/local transform — what scenes set, what actions/scripts mutate.
        // Never overwritten by parent composition.
        public Transform Transform;

        // Computed world transform — Transform composed with every ancestor's
        // Transform, recomputed every frame in OnUpdate. Rendering, physics,
        // and anything needing "where is this actually in the world" should
        // read THIS, not Transform.
        public Transform WorldTransform;

        // --- clipping ---
        public bool ClipsChildren = false;

        // --- render space ---
        public RenderSpace RenderSpace = RenderSpace.World;
        public Anchor Anchor = Anchor.None;

        public TransformProperty(
            Transform transform,
            RenderSpace renderSpace = RenderSpace.World,
            Anchor anchor = Anchor.None)
        {
            Transform = transform;
            WorldTransform = transform;
            RenderSpace = renderSpace;
            Anchor = anchor;
        }

        public TransformProperty()
        {
            Transform = Transform.Default;
            WorldTransform = Transform.Default;
        }

        // Called after the full scene tree is built, so Parent is guaranteed to be set.
        public override void OnStart(Node node)
        {
            base.OnStart(node);
            ApplyRenderSpaceDefaults(node);
            RecomputeWorldTransform(node);
        }

        // Recompute every frame so live parent edits (e.g. player walking) propagate
        // down to children automatically — no compounding, since we always rebuild
        // WorldTransform fresh from the authored Transform + parent's WorldTransform.
        public override void OnUpdate(Node node, double delta)
        {
            // Apply root motion from animation into world position
            var anim = node.GetProperty<AnimationProperty>();
            if (anim != null && anim.ApplyRootMotion && anim.RootMotionDelta != Vector3.Zero)
            {
                // Root motion is in model-local space — rotate it by current world yaw
                float yawRad = float.DegreesToRadians(WorldTransform.Rotation.Y);
                float cos = MathF.Cos(yawRad);
                float sin = MathF.Sin(yawRad);
                Vector3 d = anim.RootMotionDelta;
                Vector3 worldDelta = new Vector3(
                    d.X * cos + d.Z * sin,
                    d.Y,
                    -d.X * sin + d.Z * cos);

                Transform.Position += worldDelta;
            }

            Transform.Rotation = new Vector3(
                NormalizeAngle(Transform.Rotation.X),
                NormalizeAngle(Transform.Rotation.Y),
                NormalizeAngle(Transform.Rotation.Z));

            RecomputeWorldTransform(node);
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
                WorldTransform = Transform;
                return;
            }

            TransformProperty parentTransform = node?.Parent?.GetProperty<TransformProperty>();
            if (parentTransform == null)
            {
                WorldTransform = Transform;
                return;
            }

            Matrix4x4 world = Transform.ToMatrix() * parentTransform.WorldTransform.ToMatrix();
            WorldTransform.SetMatrix(world);

            // DEBUG
            if (node.Id == "player" || node.Id == "playerBody" || node.Id == "playerBodyMesh")
            {
                Console.WriteLine($"[TRANSFORM] {node.Id} | " +
                    $"local pos={Transform.Position:F2} rot={Transform.Rotation:F2} | " +
                    $"world pos={WorldTransform.Position:F2} rot={WorldTransform.Rotation:F2} | " +
                    $"parent={node.Parent?.Id} parent world pos={parentTransform.WorldTransform.Position:F2}");
            }
        }

        // -----------------------------------------------------------------------
        // Core layout resolution
        // -----------------------------------------------------------------------

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
                    parentTransform.Transform.Scale.X * screen.X * 0.5f,
                    parentTransform.Transform.Scale.Y * screen.Y * 0.5f);

                containerCenterPx = new Vector2(
                    parentTransform.Transform.Position.X * screen.X * 0.5f,
                    parentTransform.Transform.Position.Y * screen.Y * 0.5f);
            }
            else
            {
                containerSizePx = screen;
                containerCenterPx = Vector2.Zero;
            }

            Vector2 halfContainer = containerSizePx * 0.5f;
            Vector2 halfSelf = new Vector2(Transform.Scale.X * 0.5f, Transform.Scale.Y * 0.5f);

            Vector2 localOffset = new Vector2(Transform.Position.X, Transform.Position.Y);

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

            Transform.Position = new Vector3(centerInScreen.X, centerInScreen.Y, Transform.Position.Z);
            Transform.ToScreenSpace(screen);
        }

        // -----------------------------------------------------------------------
        // Matrix helpers
        // -----------------------------------------------------------------------

        public void SetMatrix(Matrix4x4 matrix) => Transform.SetMatrix(matrix);

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

        // -----------------------------------------------------------------------
        // Anchor helper
        // -----------------------------------------------------------------------

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