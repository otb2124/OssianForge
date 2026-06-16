using OssianForge.Engine.Resources.Sounds;



namespace OssianForge.Engine.Nodes.Props
{
    public class SoundProperty : NodeProperty, IDisposable
    {
        public SoundResource SoundResource { get; private set; }

        public bool AutoPlay = false;

        private bool _started = false;

        public SoundProperty(string soundResourceId)
        {
            SoundResource = Engine.Resources.GetResource<SoundResource>(soundResourceId)
                ?? throw new Exception($"[SOUND] SoundResource not found: '{soundResourceId}'");
        }

        public override void OnStart(Node node)
        {
            //SoundResource.Spatial = true;
            //SoundResource.Loop = true;
            if(AutoPlay) SoundResource.Play();
            _started = true;
        }

        public override void OnUpdate(Node node, double delta)
        {
            if (!_started) return;

            // Keep the OpenAL source position in sync with the node's world transform
            if (SoundResource.Spatial)
            {
                var transform = node.GetProperty<TransformProperty>();
                if (transform != null)
                    SoundResource.SetPosition(transform.Transform.Position);
            }
        }

        // Convenience passthrough so callers don't need to reach into SoundResource
        public void Play() => SoundResource.Play();
        public void Pause() => SoundResource.Pause();
        public void Stop() => SoundResource.Stop();

        public void Dispose() => SoundResource?.Dispose();
    }
}