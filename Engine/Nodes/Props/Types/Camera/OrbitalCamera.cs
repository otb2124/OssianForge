using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Nodes.Props.Types.Camera
{
    public class OrbitalCameraProperty : CameraProperty
    {
        public string TargetNodeId = "";
        public float OrbitDistance = 5f;
        public float MinPitch = -89f;
        public float MaxPitch = 89f;

        public OrbitalCameraProperty(string targetNodeId, float orbitDistance = 5f, float minPitch = -89f, float maxPitch = 89) : base() 
        {
            TargetNodeId = targetNodeId;
            OrbitDistance = orbitDistance;
            MinPitch = minPitch;
            MaxPitch = maxPitch;
        }

        public override void OnUpdate(Node node, double delta)
        {
            var selfTransform = node.GetProperty<TransformProperty>();
            var target = FindTargetTransform();
            if (selfTransform != null && target != null)
            {
                // Clamp pitch on the stored rotation so it never accumulates past limits.
                // Yaw is left free — full 360 horizontal rotation is correct for 3rd person.
                selfTransform.Rotation = new Vector3(
                    Math.Clamp(selfTransform.Transform.Rotation.X, MinPitch, MaxPitch),
                    selfTransform.Transform.Rotation.Y,
                    selfTransform.Transform.Rotation.Z);

                float yawRad = float.DegreesToRadians(selfTransform.Transform.Rotation.Y);
                float pitchRad = float.DegreesToRadians(selfTransform.Transform.Rotation.X);

                Vector3 orbitOffset = new Vector3(
                    OrbitDistance * MathF.Cos(pitchRad) * MathF.Sin(yawRad),
                    OrbitDistance * MathF.Sin(pitchRad),
                    OrbitDistance * MathF.Cos(pitchRad) * MathF.Cos(yawRad));

                Vector3 lookAt = target.WorldTransform.Position;
                Camera.Position = lookAt + orbitOffset;

                Console.WriteLine($"transform:{target.Transform.Position}, world:{target.WorldTransform.Position}");

                Vector3 toTarget = Vector3.Normalize(lookAt - Camera.Position);
                Camera.SetLookDirection(toTarget);
            }
            Camera.OnUpdate(delta);
        }

        private TransformProperty FindTargetTransform()
        {
            if (string.IsNullOrEmpty(TargetNodeId)) return null;
            return Engine.Nodes.NodeManager.GetNode(TargetNodeId)?.GetProperty<TransformProperty>();
        }
    }
}
