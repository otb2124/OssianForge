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

    public class TransformProperty : NodeProperty
    {
        public Transform Transform;

        // optional — null means 3D node, anchor system ignored entirely
        public AnchorPreset Anchor = AnchorPreset.None;
        public Vector2? Pivot = null;       // 0,0 = topleft corner, 0.5,0.5 = center, 1,1 = bottomright
        public Vector2? Size = null;        // screen space size in pixels
        public Vector2? Offset = null;      // offset from anchor point in pixels
        public SizeMode SizeMode = SizeMode.Fixed;

        public bool IsScreenSpace => Anchor != AnchorPreset.None;

        // --- clipping --- 
        public bool ClipsChildren = false;
        public Vector2? ClipSize = null;   // null means use node's own Size from TransformProperty

        public TransformProperty(Transform transform)
        {
            Transform = transform;
        }

        public TransformProperty()
        {
            Transform = Transform.Default;
        }

        public void SetMatrix(Matrix4x4 matrix)
        {
            Transform.SetMatrix(matrix);
        }

        // resolves final screen position from anchor + pivot + offset + screen size
        public Vector2 ResolveScreenPosition(Vector2 screenSize, Vector2 parentSize)
        {
            if (!IsScreenSpace) return Vector2.Zero;

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