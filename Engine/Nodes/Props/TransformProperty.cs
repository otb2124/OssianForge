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

        public (float x, float y, float width, float height) ResolveRect(float parentW, float parentH)
        {
            float anchorX = parentW * AnchorLeft;
            float anchorY = parentH * AnchorTop;

            float x = anchorX + Transform.Position.X;
            float y = anchorY + Transform.Position.Y;

            return (x, y, Transform.Scale.X, Transform.Scale.Y);
        }

        // ResolvePosition stays but simplifies:
        public Vector2 ResolvePosition(float parentW, float parentH)
        {
            var (x, y, _, _) = ResolveRect(parentW, parentH);
            return new Vector2(x, y);
        }

        public void SetMatrix(Matrix4x4 matrix) => Transform.SetMatrix(matrix);

        public Matrix4x4 GetMatrix()
        {
            var cam = Engine.Graphics.GetCurrentCamera();

            if (RenderSpace == RenderSpace.ScreenSpace)
            {
                var screen = Engine.Graphics.WindowSize;
                var (x, y, w, h) = ResolveRect(screen.X, screen.Y);

                // Convert pixel position (top-left of rect) to NDC center
                float pixelCenterX = x + w * 0.5f;
                float pixelCenterY = y + h * 0.5f;

                float centerX = (pixelCenterX / screen.X) * 2f - 1f;
                float centerY = (pixelCenterY / screen.Y) * 2f - 1f;

                // Convert pixel size to NDC scale
                float scaleX = w / screen.X * 2f;
                float scaleY = h / screen.Y * 2f;

                var screenTransform = new Transform(
                    new Vector3(centerX, centerY, 0f),
                    Transform.Rotation,               // Z roll passes through
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