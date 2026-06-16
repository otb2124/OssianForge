using Silk.NET.OpenAL;
using System.Numerics;

namespace OssianForge.Engine.Audio
{
    public class AudioSystem : IDisposable
    {
        public AL AL { get; private set; }
        public ALContext ALC { get; private set; }

        private unsafe Device* _device;
        private unsafe Context* _context;

        public unsafe void Initialize()
        {
            ALC = ALContext.GetApi(soft: true);
            AL = AL.GetApi(soft: true);

            _device = ALC.OpenDevice(string.Empty);
            if (_device == null)
                throw new Exception("[AUDIO] Failed to open OpenAL device.");

            _context = ALC.CreateContext(_device, null);
            ALC.MakeContextCurrent(_context);

            AL.GetError(); // clear any startup error

            Console.WriteLine("[AUDIO] OpenAL initialized.");
        }

        /// <summary>
        /// Call every frame with the camera's world position and forward/up vectors
        /// so 3D positional audio is correct.
        /// </summary>
        public unsafe void UpdateListener(Vector3 position, Vector3 forward, Vector3 up)
        {
            AL.SetListenerProperty(ListenerVector3.Position, position);

            float* orientation = stackalloc float[6]
            {
                forward.X, forward.Y, forward.Z,
                up.X,      up.Y,      up.Z
            };
            AL.SetListenerProperty(ListenerFloatArray.Orientation, orientation);
        }

        public unsafe void Dispose()
        {
            ALC.MakeContextCurrent(null);
            ALC.DestroyContext(_context);
            ALC.CloseDevice(_device);
            AL.Dispose();
            ALC.Dispose();
        }
    }
}