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

            Camera.Position = transform.WorldTransform.Position;
            Camera.SetViewMatrix(transform.WorldTransform.ToMatrix());
        }
    }
}