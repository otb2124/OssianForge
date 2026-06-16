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


    public class TransformProperty : NodeProperty
    {
        public Transform Transform;

        // --- clipping ---
        public bool ClipsChildren = false;

        // --- render space ---
        public RenderSpace RenderSpace = RenderSpace.World;


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
                    Transform.ToScreenSpace(new Vector2(Engine.Graphics.WindowSize.X, Engine.Graphics.WindowSize.Y));
                    break;
                case RenderSpace.Billboard:
                case RenderSpace.BillboardFree:
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
    }
}