using OssianForge.Engine.Resources.Meshes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Resources.Animations
{

    public class Animation
    {
        public string Id;
        //etc
    }

    public class AnimationResource : Resource
    {
        List<Animation> Animations;

        public string MeshResourceId;

        public AnimationResource(string id, string meshResourceId)
        {
            Id = id;
            MeshResourceId = meshResourceId;
        }

        public override void Load()
        {
            MeshResource source = Engine.Resources.GetResource(MeshResourceId) as MeshResource;
        }
    }
}
