using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OssianForge.Engine.Utils.Math;

namespace OssianForge.Engine.Nodes.Props
{
    public class TransformProperty : NodeProperty
    {

        public Transform Transform;

        public TransformProperty(Transform transform)
        {
            Transform = transform;
        }

        public TransformProperty()
        {
            Transform = Transform.Default;
        }
    }
}
