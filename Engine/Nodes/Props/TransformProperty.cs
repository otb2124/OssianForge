using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static OssianForge.Engine.Utils.Math;

namespace OssianForge.Engine.Nodes.Props
{
    public enum SizeMode
    {
        Fixed,
        FillParent,
        FitContent
    }

    public enum AnchorPreset
    {
        None,           // 3D, anchor ignored
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        Center,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

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

        // optional — null means 3D node, anchor system ignored entirely
        public AnchorPreset Anchor = AnchorPreset.None;
        public Vector2? Pivot = null;       // 0,0 = topleft corner, 0.5,0.5 = center, 1,1 = bottomright
        public Vector2? Size = null;        // screen space size in pixels
        public Vector2? Offset = null;      // offset from anchor point in pixels
        public SizeMode SizeMode = SizeMode.Fixed;

        // --- clipping ---
        public bool ClipsChildren = false;
        public Vector2? ClipSize = null;    // null means use node's own Size

        // --- render space ---
        // Replaces the old bool Billboard + bool ScreenSpace pair.
        // ScreenSpace and ScreenSpaceFixed imply billboard behaviour; the camera handles it internally.
        public RenderSpace RenderSpace = RenderSpace.World;

        // --- render flags ---
        // Previously set via material BeginAction/EndAction lambdas; moved here so the renderer
        // can apply them automatically based on RenderSpace or per-node overrides.
        public bool DepthWrite = true;
        public bool DepthTest = true;

        // When true the node's matrix is not multiplied by its parent's transform.
        // Essential for HUD nodes that must not inherit world-space translations or rotations.
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

        // Convenience: set RenderSpace and automatically apply the correct depth flag defaults.
        // Call this instead of setting RenderSpace directly when you want the "sensible defaults"
        // behaviour (e.g. HUD nodes almost always want DepthTest = false).
        public void SetRenderSpace(RenderSpace space)
        {
            RenderSpace = space;
            ApplyRenderSpaceDefaults();
        }

        // Applies opinionated defaults for each render space.
        // You can still override DepthWrite/DepthTest individually afterwards.
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
                    // Lights and particles typically need additive blending over depth,
                    // but the caller controls BlendFunc — just disable depth write here
                    // since billboards are usually transparent quads.
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

        public void SetMatrix(Matrix4x4 matrix)
        {
            Transform.SetMatrix(matrix);
        }

        public Matrix4x4 GetMatrix()
        {
            var cam = Engine.Graphics.GetCurrentCamera();
            return RenderSpace switch
            {
                RenderSpace.Billboard => cam.GetBillboardMatrix(Transform),
                RenderSpace.BillboardFree => cam.GetBillboardFreeMatrix(Transform),
                RenderSpace.ScreenSpace => cam.GetScreenSpaceMatrix(Transform),
                _ => Transform.ToMatrix()
            };
        }

        // Resolves final screen position from anchor + pivot + offset + screen size.
        public Vector2 ResolveScreenPosition(Vector2 screenSize, Vector2 parentSize)
        {
            Vector2 anchorPoint = Anchor switch
            {
                AnchorPreset.TopLeft => new Vector2(0, 0),
                AnchorPreset.TopCenter => new Vector2(parentSize.X / 2, 0),
                AnchorPreset.TopRight => new Vector2(parentSize.X, 0),
                AnchorPreset.MiddleLeft => new Vector2(0, parentSize.Y / 2),
                AnchorPreset.Center => new Vector2(parentSize.X / 2, parentSize.Y / 2),
                AnchorPreset.MiddleRight => new Vector2(parentSize.X, parentSize.Y / 2),
                AnchorPreset.BottomLeft => new Vector2(0, parentSize.Y),
                AnchorPreset.BottomCenter => new Vector2(parentSize.X / 2, parentSize.Y),
                AnchorPreset.BottomRight => new Vector2(parentSize.X, parentSize.Y),
                _ => Vector2.Zero
            };

            Vector2 pivotOffset = Vector2.Zero;
            if (Pivot.HasValue && Size.HasValue)
                pivotOffset = Pivot.Value * Size.Value;

            Vector2 off = Offset ?? Vector2.Zero;
            return anchorPoint + off - pivotOffset;
        }
    }
}