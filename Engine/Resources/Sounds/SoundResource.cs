using Silk.NET.OpenAL;
using System.Numerics;

namespace OssianForge.Engine.Resources.Sounds
{
    public class SoundResource : Resource, IDisposable
    {
        public string SoundFileId { get; private set; }
        public SoundFile SoundFile { get; private set; }

        // Source properties — set before Play() or at any time.
        public float Volume { get => _volume; set { _volume = value; Apply(); } }
        public float Pitch { get => _pitch; set { _pitch = value; Apply(); } }
        public bool Loop { get => _loop; set { _loop = value; Apply(); } }
        public bool Spatial { get => _spatial; set { _spatial = value; Apply(); } }

        private float _volume = 1f;
        private float _pitch = 1f;
        private bool _loop = false;
        private bool _spatial = false;   // false = 2D (music/UI), true = 3D positional

        private uint _source;
        private AL _al => Engine.Audio.AudioSystem.AL;

        public SoundResource(string id, string soundFileId)
        {
            Id = id;
            SoundFileId = soundFileId;
        }

        public override void Load()
        {
            SoundFile = Engine.Resources.GetResourceFile<SoundFile>(SoundFileId)
                ?? throw new Exception($"[SOUND] SoundFile not found: '{SoundFileId}'");

            _source = _al.GenSource();

            // Attach the shared buffer
            _al.SetSourceProperty(_source, SourceInteger.Buffer, (int)SoundFile.BufferId);

            // 2D sounds: disable distance attenuation by placing them at the listener
            if (!_spatial)
                _al.SetSourceProperty(_source, SourceBoolean.SourceRelative, true);

            Apply();
        }

        // ── Playback controls ────────────────────────────────────────────────

        public void Play()
        {
            Console.WriteLine($"[SOUNDRESOURCE] Plays {Id}");
            _al.SourcePlay(_source);
        }

        public void Pause()
        {
            _al.SourcePause(_source);
        }

        public void Stop()
        {
            _al.SourceStop(_source);
            // Rewind so next Play() starts from the beginning
            _al.SourceRewind(_source);
        }

        public bool IsPlaying
        {
            get
            {
                _al.GetSourceProperty(_source, GetSourceInteger.SourceState, out int state);
                return state == (int)SourceState.Playing;
            }
        }

        public bool IsPaused
        {
            get
            {
                _al.GetSourceProperty(_source, GetSourceInteger.SourceState, out int state);
                return state == (int)SourceState.Paused;
            }
        }

        /// <summary>Playback position in seconds.</summary>
        public float PlaybackPosition
        {
            get
            {
                _al.GetSourceProperty(_source, SourceFloat.SecOffset, out float v);
                return v;
            }
            set => _al.SetSourceProperty(_source, SourceFloat.SecOffset, value);
        }

        /// <summary>
        /// Update world position for 3D spatial sounds.
        /// Call this every frame from SoundProperty.OnUpdate.
        /// </summary>
        public void SetPosition(Vector3 position)
        {
            if (!_spatial) return;
            _al.SetSourceProperty(_source, SourceVector3.Position, position);
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void Apply()
        {
            if (_source == 0) return;
            _al.SetSourceProperty(_source, SourceFloat.Gain, _volume);
            _al.SetSourceProperty(_source, SourceFloat.Pitch, _pitch);
            _al.SetSourceProperty(_source, SourceBoolean.Looping, _loop);
            _al.SetSourceProperty(_source, SourceBoolean.SourceRelative, !_spatial);
        }

        public void Dispose()
        {
            if (_source != 0)
            {
                _al.SourceStop(_source);
                _al.DeleteSource(_source);
                _source = 0;
            }
        }
    }
}