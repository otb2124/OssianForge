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
                // Yaw and pitch both come from THIS node's own transform.
                // Mouse X writes to selfTransform.Transform.Rotation.Y (camera yaw).
                // Mouse Y writes to selfTransform.Transform.Rotation.X (camera pitch).
                // The player root stays at zero rotation — it's a pure position anchor.
                float yawRad = float.DegreesToRadians(selfTransform.Transform.Rotation.Y);
                float pitchRad = float.DegreesToRadians(
                    Math.Clamp(selfTransform.Transform.Rotation.X, MinPitch, MaxPitch));

                // Spherical offset: camera sits OrbitDistance behind and above the target.
                // Yaw 0 → camera is at +Z (behind when facing -Z), adjust the phase if needed.
                Vector3 orbitOffset = new Vector3(
                    OrbitDistance * MathF.Cos(pitchRad) * MathF.Sin(yawRad),
                    OrbitDistance * MathF.Sin(pitchRad),
                    OrbitDistance * MathF.Cos(pitchRad) * MathF.Cos(yawRad));

                Vector3 lookAt = target.WorldTransform.Position;
                Camera.Position = lookAt + orbitOffset;

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
