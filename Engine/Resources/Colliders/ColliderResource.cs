using OssianForge.Engine.Resources.Meshes;
using System.Numerics;

namespace OssianForge.Engine.Resources.Colliders
{
    /// <summary>
    /// Created with a MeshResource reference.
    /// Call Load() once — bakes all SubMeshColliders from the mesh data.
    /// </summary>
    public class ColliderResource : Resource, IDisposable
    {
        public List<SubCollider> SubColliders = new();

        public string MeshResourceId;
        private MeshResource _source;

        public ColliderResource(string id, string meshResourceId)
        {
            Id = id;
            MeshResourceId = meshResourceId;
        }

        public override void Load()
        {
            SubColliders.Clear();

            _source = Engine.Resources.GetResource(MeshResourceId) as MeshResource;

            // Mirror the submesh layout exactly
            foreach (var sub in _source.SubMeshes)
            {
                var col = new SubCollider();
                col.Bake(sub.RawVertices, sub.HasNormals, sub.HasUV);
                SubColliders.Add(col);
            }
        }

        public void Dispose() { /* no GPU resources */ }
    }
}