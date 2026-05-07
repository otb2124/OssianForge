using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

        public void SetMatrix(Matrix4x4 matrix)
        {
            Transform.SetMatrix(matrix);
        }
    }
}
