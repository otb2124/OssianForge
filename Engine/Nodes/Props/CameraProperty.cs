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
    }
}
