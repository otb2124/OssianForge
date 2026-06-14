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
                    ToScreenSpace();
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

        public void SetMatrix(Matrix4x4 matrix) => Transform.SetMatrix(matrix);

        public void ToScreenSpace()
        {
            var screen = Engine.Graphics.WindowSize;

            float scaleX = Transform.Scale.X / screen.X;
            float scaleY = Transform.Scale.Y / screen.Y;

            float ndcX = (Transform.Position.X / screen.X) + scaleX * 0.5f - 1f;
            float ndcY = (Transform.Position.Y / screen.Y) + scaleY * 0.5f - 1f;

            Transform.Position = new Vector3(ndcX, ndcY, Transform.Position.Z);
            Transform.Scale = new Vector3(scaleX, scaleY, Transform.Scale.Z);
        }

        public Matrix4x4 GetCameraModel()
        {
            var cam = Engine.Graphics.GetCurrentCamera();

            return RenderSpace switch
            {
                RenderSpace.Billboard => cam.GetBillboardMatrix(Transform),
                RenderSpace.BillboardFree => cam.GetBillboardFreeMatrix(Transform),
                RenderSpace.ScreenSpace => cam.GetScreenSpaceMatrixFixed(Transform),
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
    }
}