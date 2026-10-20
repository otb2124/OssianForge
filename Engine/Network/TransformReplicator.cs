using System.Numerics;
using OssianForge.Content.Cooked;
using OssianForge.Engine.Nodes.Props;

namespace OssianForge.Engine.Network.Replication
{
    /// <summary>
    /// Replicates the node's local transform. Owner sends on change; proxies buffer snapshots and render
    /// slightly in the past (InterpDelay) so motion stays smooth despite jittery arrival times.
    /// </summary>
    public sealed class TransformReplicator : NetReplicator
    {
        public override byte WireId => 1;

        public float InterpDelay = 0.1f;
        public float PositionEpsilon = 0.001f;
        public float RotationEpsilonDeg = 0.05f;

        private const int MaxSnaps = 16;
        private struct Snap { public double T; public Vector3 Pos, Rot; }

        private TransformProperty _tp = null!;

        // owner side
        private bool _sent;
        private Vector3 _lastPos, _lastRot, _lastScale;

        // proxy side
        private readonly List<Snap> _snaps = new();
        private Vector3 _scale;
        private bool _hasScale;
        private bool _rootMotionWas, _wasProxy;

        public override void OnAttach() => _tp = Node.GetProperty<TransformProperty>()!;

        public override void OnRoleChanged(bool isProxy)
        {
            // Root motion would be added on top of the replicated position.
            var anim = Node.GetProperty<AnimationProperty>();
            if (anim != null)
            {
                if (isProxy && !_wasProxy) { _rootMotionWas = anim.ApplyRootMotion; anim.ApplyRootMotion = false; }
                else if (!isProxy && _wasProxy) anim.ApplyRootMotion = _rootMotionWas;
            }
            _wasProxy = isProxy;
            _snaps.Clear();
        }

        public override bool WriteState(BlobWriter w, bool full)
        {
            var t = _tp.Transform;

            bool moved = full || !_sent
                || Vector3.DistanceSquared(t.Position, _lastPos) > PositionEpsilon * PositionEpsilon
                || Vector3.DistanceSquared(t.Rotation, _lastRot) > RotationEpsilonDeg * RotationEpsilonDeg;
            bool scaled = full || !_sent || t.Scale != _lastScale;

            if (!moved && !scaled) return false;

            w.U8((byte)((moved ? 1 : 0) | (scaled ? 2 : 0)));
            if (moved) { w.Struct(t.Position); w.Struct(t.Rotation); }
            if (scaled) w.Struct(t.Scale);

            if (!full)   // snapshots must not consume dirty state
            {
                _sent = true;
                if (moved) { _lastPos = t.Position; _lastRot = t.Rotation; }
                if (scaled) _lastScale = t.Scale;
            }
            return true;
        }

        public override void ReadState(BlobReader r)
        {
            byte flags = r.U8();
            if ((flags & 1) != 0)
            {
                var snap = new Snap { T = NetClock.Now, Pos = r.Struct<Vector3>(), Rot = r.Struct<Vector3>() };
                _snaps.Add(snap);
                if (_snaps.Count > MaxSnaps) _snaps.RemoveAt(0);
            }
            if ((flags & 2) != 0) { _scale = r.Struct<Vector3>(); _hasScale = true; }
        }

        public override void ApplyProxy(double delta)
        {
            if (_snaps.Count == 0) return;

            double renderT = NetClock.Now - InterpDelay;
            Vector3 pos, rot;

            if (_snaps.Count == 1 || renderT <= _snaps[0].T)
            {
                pos = _snaps[0].Pos; rot = _snaps[0].Rot;
            }
            else if (renderT >= _snaps[^1].T)
            {
                pos = _snaps[^1].Pos; rot = _snaps[^1].Rot;   // no extrapolation (yet)
            }
            else
            {
                int i = 0;
                while (i < _snaps.Count - 2 && _snaps[i + 1].T < renderT) i++;
                var a = _snaps[i]; var b = _snaps[i + 1];
                float k = (float)Math.Clamp((renderT - a.T) / Math.Max(b.T - a.T, 1e-6), 0.0, 1.0);

                pos = Vector3.Lerp(a.Pos, b.Pos, k);
                rot = new Vector3(LerpAngle(a.Rot.X, b.Rot.X, k), LerpAngle(a.Rot.Y, b.Rot.Y, k), LerpAngle(a.Rot.Z, b.Rot.Z, k));
            }

            var cur = _tp.Transform;
            bool changed = cur.Position != pos || cur.Rotation != rot || (_hasScale && cur.Scale != _scale);
            if (!changed) return;

            cur.Position = pos;
            cur.Rotation = rot;
            if (_hasScale) cur.Scale = _scale;
            _tp.Transform = cur;   // setter marks dirty, so a physics body follows
        }

        private static float LerpAngle(float a, float b, float t)
        {
            float d = ((b - a + 540f) % 360f) - 180f;
            return a + d * t;
        }
    }
}