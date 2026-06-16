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
        ScreenSpace,        // flat quad, NDC direct, identity view/proj
    }

    public enum Anchor
    {
        None,
        TopLeft, TopCenter, TopRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        BottomLeft, BottomCenter, BottomRight,
    }

    public enum Pivot
    {
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
        public Pivot Pivot = Pivot.MiddleCenter;


        public TransformProperty(
            Transform transform,
            RenderSpace renderSpace = RenderSpace.World,
            Anchor anchor = Anchor.None,
            Pivot pivot = Pivot.MiddleCenter)
        {
            Transform = transform;
            RenderSpace = renderSpace;
            Anchor = anchor;
            Pivot = pivot;
            ApplyRenderSpaceDefaults();
        }

        public TransformProperty()
        {
            Transform = Transform.Default;
        }

        public void SetRenderSpace(RenderSpace space)
        {
            RenderSpace = space;
            ApplyRenderSpaceDefaults();
        }


        private void ApplyRenderSpaceDefaults()
        {
            switch (RenderSpace)
            {
                case RenderSpace.ScreenSpace:
                    Vector2 screen = new Vector2(
                        Engine.Graphics.WindowSize.X,
                        Engine.Graphics.WindowSize.Y);

                    if (Anchor != Anchor.None)
                    {
                        // ResolveAnchor gives us element center in pixel space
                        Transform.Position = ResolveAnchor(Transform.Position, Transform.Scale, screen);
                    }
                    else
                    {
                        // No anchor: position is in pixel space, treat MiddleCenter pivot as default.
                        // pixelPos is the pivot point; MiddleCenter pivot = element center, so no offset needed.
                        // If you want Pivot to still work without anchor, apply pivot-to-center offset:
                        Vector2 pivotOffset = PivotOffset(Pivot, new Vector2(Transform.Scale.X, Transform.Scale.Y));
                        Transform.Position = new Vector3(
                            Transform.Position.X - pivotOffset.X,
                            Transform.Position.Y - pivotOffset.Y,
                            Transform.Position.Z);
                    }

                    Transform.ToScreenSpace(screen);
                    break;

                default:
                    break;
            }
        }

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





        private Vector3 ResolveAnchor(Vector3 pixelPos, Vector3 pixelSize, Vector2 screen)
        {
            Vector2 size = new Vector2(pixelSize.X, pixelSize.Y);
            Vector2 anchorOrigin = AnchorOrigin(Anchor, screen);

            // pivotOffset: vector from element center to pivot point (Y-up)
            // We subtract it so that the pivot lands on the anchor, then we get center back.
            Vector2 pivotToCenterOffset = PivotOffset(Pivot, size);

            // anchorOrigin + pixelPos = where the pivot should land in pixel space
            // subtract pivot-from-center offset to get the element's center
            float cx = anchorOrigin.X + pixelPos.X - pivotToCenterOffset.X;
            float cy = anchorOrigin.Y + pixelPos.Y - pivotToCenterOffset.Y;

            return new Vector3(cx, cy, pixelPos.Z);
        }

        private static Vector2 AnchorOrigin(Anchor anchor, Vector2 screen) => anchor switch
        {
            Anchor.TopLeft => new Vector2(0, screen.Y),
            Anchor.TopCenter => new Vector2(screen.X / 2, screen.Y),
            Anchor.TopRight => new Vector2(screen.X, screen.Y),
            Anchor.MiddleLeft => new Vector2(0, screen.Y / 2),
            Anchor.MiddleCenter => new Vector2(screen.X / 2, screen.Y / 2),
            Anchor.MiddleRight => new Vector2(screen.X, screen.Y / 2),
            Anchor.BottomLeft => new Vector2(0, 0),
            Anchor.BottomCenter => new Vector2(screen.X / 2, 0),
            Anchor.BottomRight => new Vector2(screen.X, 0),
            _ => Vector2.Zero,
        };

        private static Vector2 PivotOffset(Pivot pivot, Vector2 size)
        {
            // X: how far is the pivot from the element center, horizontally
            float x = pivot switch
            {
                Pivot.TopLeft or Pivot.MiddleLeft or Pivot.BottomLeft => -size.X / 2f,  // pivot is left edge
                Pivot.TopCenter or Pivot.MiddleCenter or Pivot.BottomCenter => 0f,            // pivot is center
                _ => size.X / 2f,  // pivot is right edge
            };
            // Y: how far is the pivot from center, vertically (Y-up)
            float y = pivot switch
            {
                Pivot.BottomLeft or Pivot.BottomCenter or Pivot.BottomRight => -size.Y / 2f,  // pivot is bottom edge
                Pivot.MiddleLeft or Pivot.MiddleCenter or Pivot.MiddleRight => 0f,           // pivot is center
                _ => size.Y / 2f, // pivot is top edge
            };
            return new Vector2(x, y);
        }
    }
}