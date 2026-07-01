using System;
using System.Collections.Generic;
using System.Numerics;
using OssianForge.Engine.Nodes.Props.Types.Physics;
using OssianForge.Engine.Physics;
using OssianForge.Engine.Resources.Colliders;
using OssianForge.Engine.Resources.Meshes;
using static OssianForge.Engine.Utils.MathUtils;

namespace OssianForge.Engine.Nodes.Props
{
    public enum Anchor3D
    {
        Center,
        Bottom,
        Top,
    }

    public class ColliderProperty : NodeProperty
    {
        public Action<Node>? OnCollision;
        public ColliderResource ColliderResource;
        public WireframeMaterialProperty Material;

        private SubMeshResource _debugMesh;

        public Anchor3D Anchor { get; set; } = Anchor3D.Center;
        public Transform LocalTransform = new Transform(new Vector3(0,0,0), new Vector3(0,0,0), new Vector3(1,1,1));

        public ColliderProperty(string colliderId, Anchor3D achor = Anchor3D.Center, Transform? localTransform = null)
        {
            ColliderResource = Engine.Resources.GetResource<ColliderResource>(colliderId);
            Material = new WireframeMaterialProperty(new Vector4(0f, 1f, 0f, 1f));
            Anchor = achor;
            LocalTransform = localTransform ?? Transform.Default;
        }

        public override void OnStart(Node node)
        {
            base.OnStart(node);
            var mesh = node.GetProperty<MeshProperty>()?.MeshResource;
            float yAnchorOffset = 0f;
            if (mesh != null)
            {
                // Mesh is drawn shifted by -HipsOffset, so AABB effective range is:
                float meshMin = mesh.LocalAabbMin.Y - mesh.HipsOffset.Y;
                float meshMax = mesh.LocalAabbMax.Y - mesh.HipsOffset.Y;
                float meshHeight = meshMax - meshMin;

                float colliderMin = ColliderResource.AabbMin.Y;
                float colliderMax = ColliderResource.AabbMax.Y;
                float colliderHeight = colliderMax - colliderMin;

                float scaleY = LocalTransform.Scale.Y;
                float scaledColliderMin = colliderMin * scaleY;
                float scaledColliderMax = colliderMax * scaleY;
                float scaledColliderHeight = scaledColliderMax - scaledColliderMin;

                yAnchorOffset = Anchor switch
                {
                    Anchor3D.Bottom => meshMin - scaledColliderMin,
                    Anchor3D.Top => meshMax - scaledColliderMax,
                    _ => (meshMin + meshHeight * 0.5f)
                                     - (scaledColliderMin + scaledColliderHeight * 0.5f),
                };
            }

            LocalTransform.Position.Y += yAnchorOffset;
        }


        /*
        public override void OnRender(Node node, double delta)
        {
            var transform = node.GetProperty<TransformProperty>();
            if (transform == null) return;

            var physicsProperty = node.GetProperty<PhysicsProperty>();

            _debugMesh ??= ColliderResource.GetDebugMesh();
            if (_debugMesh == null) return;

            if (physicsProperty?.Body is PhysicsRigidBody rigidBody && rigidBody.JitterBody != null)
            {
                var p = rigidBody.JitterBody.Position;
                var o = rigidBody.JitterBody.Orientation;

                var rotation = new Quaternion(o.X, o.Y, o.Z, o.W);
                var position = new Vector3(p.X, p.Y, p.Z);

                var model = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);

                Engine.Graphics.Batch.DrawSubMesh(
                    _debugMesh,
                    Material,
                    model,
                    transform.GetCameraView(),
                    transform.GetCameraProjection(),
                    null);
            }
            else if (physicsProperty?.Body is PhysicsStaticBody)
            {
                // Static shapes are already baked into world space via CreateStaticShapes(worldTransform),
                // but GetDebugMesh() returns LOCAL-space geometry, not the transformed static shapes.
                // Use the node's own transform here instead.
                Engine.Graphics.Batch.DrawSubMesh(
                    _debugMesh,
                    Material,
                    transform.GetCameraModel(),
                    transform.GetCameraView(),
                    transform.GetCameraProjection(),
                    null);
            }
        }
        */

    }
}