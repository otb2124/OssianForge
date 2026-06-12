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
        ScreenSpace,     // pinned to screen in NDC coords, scales with FOV
    }


    public enum AnchorPreset
    {
        None,           // 3D node, entire anchor system ignored
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        Center,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight,
        FullRect,       // stretches to fill parent entirely
        TopWide,        // full width, pinned to top
        BottomWide,     // full width, pinned to bottom
        LeftWide,       // full height, pinned to left
        RightWide,      // full height, pinned to right
    }

    public class TransformProperty : NodeProperty
    {
        public Transform Transform;

        // --- Godot-style anchor system ---
        // Each value is normalized 0-1 relative to the parent rect.
        // AnchorPreset just sets these four values for you.
        public float AnchorLeft = 0f;
        public float AnchorTop = 0f;
        public float AnchorRight = 0f;
        public float AnchorBottom = 0f;

        // Pixel offsets from each anchor edge to the corresponding rect edge.
        // Positive = inward (shrink), negative = outward (grow past anchor).
        // e.g. FullRect with OffsetLeft=16 gives a 16px left margin.
        public float OffsetLeft = 0f;
        public float OffsetTop = 0f;
        public float OffsetRight = 0f;
        public float OffsetBottom = 0f;

        // --- clipping ---
        public bool ClipsChildren = false;

        // --- render space ---
        public RenderSpace RenderSpace = RenderSpace.World;

        // --- render flags ---
        public bool DepthWrite = true;
        public bool DepthTest = true;
        public bool IgnoreParentTransform = false;

        public TransformProperty(Transform transform, RenderSpace renderSpace = RenderSpace.World)
        {
            Transform = transform;
            RenderSpace = renderSpace;
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

        // Sets the four anchor floats from a preset — same values Godot uses internally.
        public void SetAnchorPreset(AnchorPreset preset)
        {
            (AnchorLeft, AnchorTop, AnchorRight, AnchorBottom) = preset switch
            {
                AnchorPreset.TopLeft => (0f, 0f, 0f, 0f),
                AnchorPreset.TopCenter => (0.5f, 0f, 0.5f, 0f),
                AnchorPreset.TopRight => (1f, 0f, 1f, 0f),
                AnchorPreset.MiddleLeft => (0f, 0.5f, 0f, 0.5f),
                AnchorPreset.Center => (0.5f, 0.5f, 0.5f, 0.5f),
                AnchorPreset.MiddleRight => (1f, 0.5f, 1f, 0.5f),
                AnchorPreset.BottomLeft => (0f, 1f, 0f, 1f),
                AnchorPreset.BottomCenter => (0.5f, 1f, 0.5f, 1f),
                AnchorPreset.BottomRight => (1f, 1f, 1f, 1f),
                AnchorPreset.FullRect => (0f, 0f, 1f, 1f),
                AnchorPreset.TopWide => (0f, 0f, 1f, 0f),
                AnchorPreset.BottomWide => (0f, 1f, 1f, 1f),
                AnchorPreset.LeftWide => (0f, 0f, 0f, 1f),
                AnchorPreset.RightWide => (1f, 0f, 1f, 1f),
                _ => (0f, 0f, 0f, 0f),
            };
        }

        // The core layout formula — matches Godot exactly.
        // Returns the rect in pixels relative to the parent's top-left corner.
        public (float x, float y, float width, float height) ResolveRect(float parentW, float parentH)
        {
            float left = parentW * AnchorLeft + OffsetLeft;
            float top = parentH * AnchorTop + OffsetTop;
            float right = parentW * AnchorRight + OffsetRight;
            float bottom = parentH * AnchorBottom + OffsetBottom;

            return (left, top, right - left, bottom - top);
        }

        // Convenience: get just the top-left position (for non-stretching nodes).
        public Vector2 ResolvePosition(float parentW, float parentH)
        {
            var (x, y, _, _) = ResolveRect(parentW, parentH);
            return new Vector2(x, y);
        }

        private void ApplyRenderSpaceDefaults()
        {
            switch (RenderSpace)
            {
                case RenderSpace.ScreenSpace:
                    DepthWrite = false;
                    DepthTest = false;
                    IgnoreParentTransform = true;
                    break;
                case RenderSpace.Billboard:
                case RenderSpace.BillboardFree:
                    DepthWrite = false;
                    DepthTest = true;
                    break;
                default:
                    DepthWrite = true;
                    DepthTest = true;
                    IgnoreParentTransform = false;
                    break;
            }
        }

        public void SetOffset(float x, float y, float width, float height)
        {
            OffsetLeft = x;
            OffsetTop = y;
            OffsetRight = x + width;
            OffsetBottom = y + height;
        }

        public void SetMatrix(Matrix4x4 matrix) => Transform.SetMatrix(matrix);

        public Matrix4x4 GetMatrix()
        {
            var cam = Engine.Graphics.GetCurrentCamera();

            if (RenderSpace == RenderSpace.ScreenSpace)
            {
                var screen = Engine.Graphics.WindowSize;
                var (x, y, w, h) = ResolveRect(screen.X, screen.Y);

                // center of the rect in pixel space
                float pixelCenterX = x + w * 0.5f;
                float pixelCenterY = y + h * 0.5f;

                // convert pixel center to NDC
                float centerX = (pixelCenterX / screen.X) * 2f - 1f;
                float centerY = -((pixelCenterY / screen.Y) * 2f - 1f); // flip Y

                // scale is fraction of HALF-screen because GetScreenSpaceMatrix multiplies by halfW/halfH
                float scaleX = w / screen.X * 2f;
                float scaleY = h / screen.Y * 2f;

                var screenTransform = new Transform(
                    new Vector3(centerX, centerY, 0f),
                    Vector3.Zero,
                    new Vector3(scaleX, scaleY, 1f)
                );
                return cam.GetScreenSpaceMatrix(screenTransform);
            }

            return RenderSpace switch
            {
                RenderSpace.Billboard => cam.GetBillboardMatrix(Transform),
                RenderSpace.BillboardFree => cam.GetBillboardFreeMatrix(Transform),
                _ => Transform.ToMatrix()
            };
        }
    }
}