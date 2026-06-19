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
            var transform = node.GetProperty<TransformProperty>();
            Camera.Position = transform.Transform.Position;
        }

        public override void OnUpdate(Node node, double delta)
        {
            Camera.OnUpdate(delta);
        }
    }
}
