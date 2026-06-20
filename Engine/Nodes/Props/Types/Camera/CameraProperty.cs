using OssianForge.Engine.Graphics.Camera;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace OssianForge.Engine.Nodes.Props
{
    public class CameraProperty : NodeProperty
    {
        public Camera Camera;

        public CameraProperty()
        {
            Camera = new Camera();
        }

        public override void OnStart(Node node)
        {
            base.OnStart(node);
            SyncCameraFromTransform(node);
        }

        public override void OnUpdate(Node node, double delta)
        {
            SyncCameraFromTransform(node);
            Camera.OnUpdate(delta);
        }

        private void SyncCameraFromTransform(Node node)
        {
            var transform = node.GetProperty<TransformProperty>();
            if (transform == null) return;

            // World-space, not local — matters as soon as the camera is parented
            // (e.g. under "player"). TransformProperty.WorldTransform is recomputed
            // every frame from local Transform + parent, so reading it here keeps
            // the camera correctly following its parent's live position/rotation.
            Camera.Position = transform.WorldTransform.Position;
            Camera.SetRotation(transform.WorldTransform.Rotation);
        }
    }
}