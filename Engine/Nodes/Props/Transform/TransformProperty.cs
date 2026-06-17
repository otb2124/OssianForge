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
        World,           // normal 3D transform, no special handling
        Billboard,       // faces camera, Y-axis locked (trees, sprites, lights)
        BillboardFree,   // faces camera on all axes (particles, sparks)
        ScreenSpace,     // flat quad, NDC direct, identity view/proj
    }

    /// <summary>
    /// Screen-space anchor. Defines which edge/corner the node's Position offset
    /// is measured from — either the screen (if no ScreenSpace parent) or the
    /// parent element's bounds (if the parent also has a ScreenSpace TransformProperty).
    ///
    /// Coordinate convention (pixel space, BEFORE ToScreenSpace is called):
    ///   - Origin (0, 0) = center of the container (screen or parent element).
    ///   - X+ = right, Y+ = up.
    ///   - Position is always the element's own CENTER.
    ///
    /// With Anchor.None the Position is a raw center-origin pixel offset from the container.
    ///
    /// With any other anchor the element is inset by its own half-extent so that
    /// Position=(0,0,0) places it flush with that edge/corner, fully visible.
    /// Position is then an additional pixel OFFSET from that flush position.
    ///
    /// Examples (parent or screen 400×200, element 50×50):
    ///   Anchor.TopLeft,    Position=(5, -5, 0)  → 5 px right / 5 px below top-left corner
    ///   Anchor.BottomRight, Position=(-5, 5, 0) → 5 px left  / 5 px above bottom-right corner
    /// </summary>
    public enum Anchor
    {
        None,
        TopLeft, TopCenter, TopRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        BottomLeft, BottomCenter, BottomRight,
    }

    public class TransformProperty : NodeProperty
    {
        public Transform Transform;

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
            RenderSpace = renderSpace;
            Anchor = anchor;
        }

        public TransformProperty()
        {
            Transform = Transform.Default;
        }

        // Called after the full scene tree is built, so Parent is guaranteed to be set.
        public override void OnStart(Node node)
        {
            ApplyRenderSpaceDefaults(node);
        }

        public void SetRenderSpace(RenderSpace space, Node node = null)
        {
            RenderSpace = space;
            ApplyRenderSpaceDefaults(node);
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

            // --- Determine container ---
            // If the parent node also has a ScreenSpace TransformProperty its
            // Transform is already in NDC (OnStart ran top-down, parent first).
            // We convert that NDC rect back to pixels so everything stays in one
            // unit during layout, then ToScreenSpace at the end.
            TransformProperty parentTransform = node?.Parent?.GetProperty<TransformProperty>();
            bool hasScreenSpaceParent = parentTransform != null
                                     && parentTransform.RenderSpace == RenderSpace.ScreenSpace;

            Vector2 containerSizePx;   // full width/height of the container in pixels
            Vector2 containerCenterPx; // container center in SCREEN center-origin pixels

            if (hasScreenSpaceParent)
            {
                // Parent NDC → pixels.
                // NDC size  [-1..1] range covers the full screen dimension.
                // parentNdcScale.X == 1.0 means "full screen width", so pixel size = ndcScale * screen/2 * ... 
                // ToScreenSpace stores: ndcScale = pixelSize / screen * 2  →  pixelSize = ndcScale * screen / 2
                containerSizePx = new Vector2(
                    parentTransform.Transform.Scale.X * screen.X * 0.5f,
                    parentTransform.Transform.Scale.Y * screen.Y * 0.5f);

                // Parent NDC position → center-origin pixels.
                // ndcPos = pixelCenter / (screen/2)  →  pixelCenter = ndcPos * screen/2
                containerCenterPx = new Vector2(
                    parentTransform.Transform.Position.X * screen.X * 0.5f,
                    parentTransform.Transform.Position.Y * screen.Y * 0.5f);
            }
            else
            {
                // Root screen-space element: container IS the screen.
                containerSizePx = screen;
                containerCenterPx = Vector2.Zero; // screen center-origin = (0,0)
            }

            // --- Resolve anchor within the container ---
            Vector2 halfContainer = containerSizePx * 0.5f;
            Vector2 halfSelf = new Vector2(Transform.Scale.X * 0.5f, Transform.Scale.Y * 0.5f);

            Vector2 localOffset = new Vector2(Transform.Position.X, Transform.Position.Y);

            Vector2 centerInContainer; // element center relative to container center, in pixels
            if (Anchor != Anchor.None)
            {
                // AnchorOrigin gives the flush position relative to the container center.
                Vector2 anchorFlush = AnchorOrigin(Anchor, halfContainer, halfSelf);
                centerInContainer = anchorFlush + localOffset;
            }
            else
            {
                // No anchor: Position is a direct offset from the container center.
                centerInContainer = localOffset;
            }

            // --- Convert to screen center-origin pixel space, then to NDC ---
            Vector2 centerInScreen = containerCenterPx + centerInContainer;

            Transform.Position = new Vector3(centerInScreen.X, centerInScreen.Y, Transform.Position.Z);
            Transform.ToScreenSpace(screen);
        }

        // -----------------------------------------------------------------------
        // Matrix helpers (unchanged)
        // -----------------------------------------------------------------------

        public void SetMatrix(Matrix4x4 matrix) => Transform.SetMatrix(matrix);

        public Matrix4x4 GetCameraModel()
        {
            var cam = Engine.Graphics.GetCurrentCamera();
            return RenderSpace switch
            {
                RenderSpace.Billboard => cam.GetBillboardModel(Transform),
                RenderSpace.BillboardFree => cam.GetBillboardFreeModel(Transform),
                RenderSpace.ScreenSpace => cam.GetScreenSpaceModel(Transform),
                _ => Transform.ToMatrix()
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

        /// <summary>
        /// Returns the element CENTER relative to the CONTAINER CENTER, in pixels,
        /// already inset by halfSelf so that Position=(0,0) places the element
        /// flush with the container edge — fully visible, nothing clipped.
        ///
        ///   halfContainer = containerSize / 2   (screen or parent element)
        ///   halfSelf      = thisElement.Scale / 2
        ///
        /// Examples — container 400×200, element 50×50 (halfContainer=200×100, halfSelf=25×25):
        ///   TopLeft      = (-175,  75)   → element top-left corner = container top-left corner
        ///   TopRight     = ( 175,  75)
        ///   BottomLeft   = (-175, -75)
        ///   BottomRight  = ( 175, -75)
        ///   MiddleCenter = (   0,   0)   → element centered in container
        /// </summary>
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