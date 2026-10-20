using OssianForge.Content.Cooked;
using OssianForge.Engine.Nodes.Props;

namespace OssianForge.Engine.Network.Replication
{
    /// <summary>
    /// Replicates animation intent (clip, loop, speed, playing, time), never bone palettes: every peer
    /// samples the skeleton itself. The owner polls AnimationProperty, so nothing in it needs events.
    /// </summary>
    public sealed class AnimationReplicator : NetReplicator
    {
        public override byte WireId => 2;

        public double ResyncSeconds = 1.0;
        public double DriftSeconds = 0.15;

        private AnimationProperty _anim = null!;
        private bool _sent;
        private string _lastClip = "";
        private bool _lastPlaying, _lastLoop;
        private float _lastSpeed;
        private double _lastSyncAt;

        public override void OnAttach() => _anim = Node.GetProperty<AnimationProperty>()!;

        public override bool WriteState(BlobWriter w, bool full)
        {
            var res = _anim.AnimationResource;
            string clip = _anim.CurrentClip?.Name ?? "";
            bool playing = _anim.IsPlaying, loop = res.IsLooping;
            float speed = res.PlaybackSpeed;
            double now = NetClock.Now;

            bool changed = clip != _lastClip || playing != _lastPlaying || loop != _lastLoop || speed != _lastSpeed;
            if (!full && _sent && !changed && now - _lastSyncAt < ResyncSeconds) return false;

            w.Str(clip);
            w.U8((byte)((playing ? 1 : 0) | (loop ? 2 : 0)));
            w.F32(speed);
            w.F64(_anim.CurrentTime);

            if (!full)
            {
                _sent = true;
                _lastClip = clip; _lastPlaying = playing; _lastLoop = loop; _lastSpeed = speed;
                _lastSyncAt = now;
            }
            return true;
        }

        public override void ReadState(BlobReader r)
        {
            string clip = r.Str();
            byte flags = r.U8();
            float speed = r.F32();
            double ticks = r.F64();
            bool playing = (flags & 1) != 0, loop = (flags & 2) != 0;

            if (clip.Length == 0)
            {
                if (_anim.IsPlaying) _anim.Stop();
                return;
            }

            bool clipChanged = _anim.CurrentClip?.Name != clip;
            if (clipChanged || (playing && !_anim.IsPlaying)) _anim.Play(clip, loop, speed);
            else _anim.SetSpeed(speed);

            if (!playing) { _anim.Pause(); return; }

            double tps = _anim.CurrentClip?.TicksPerSecond > 0 ? _anim.CurrentClip!.TicksPerSecond : 30.0;
            if (Math.Abs(_anim.CurrentTime - ticks) > DriftSeconds * tps)
                _anim.Seek(ticks);   // only correct real drift, don't fight the local clock
        }
    }
}