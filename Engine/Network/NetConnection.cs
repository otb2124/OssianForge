using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading.Channels;

namespace OssianForge.Engine.Network
{
    /// <summary>
    /// One TCP link. Frames are [int32 length][payload]. Reading and writing run on thread-pool tasks;
    /// the owner is notified through the callback (frame == null means the link closed, delivered once).
    /// </summary>
    public sealed class NetConnection
    {
        private const int MaxFrameBytes = 1 << 20;

        private readonly TcpClient _tcp;
        private readonly NetworkStream _stream;
        private readonly Channel<byte[]> _outbox =
            Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
        private readonly Action<NetConnection, byte[]?> _onFrame;
        private int _closed;

        /// <summary>-1 until the handshake assigns one.</summary>
        public int PeerId = -1;
        public string Name = "";
        public string Remote { get; }

        public NetConnection(TcpClient tcp, Action<NetConnection, byte[]?> onFrame)
        {
            _tcp = tcp;
            _stream = tcp.GetStream();
            _onFrame = onFrame;
            Remote = tcp.Client.RemoteEndPoint?.ToString() ?? "?";
        }

        public void Start()
        {
            _ = Task.Run(ReadLoop);
            _ = Task.Run(WriteLoop);
        }

        public void Send(byte[] payload)
        {
            var frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
            payload.CopyTo(frame.AsSpan(4));
            _outbox.Writer.TryWrite(frame);
        }

        /// <summary>Closes after everything already queued has been written (used to deliver a Reject).</summary>
        public void Shutdown() => _outbox.Writer.TryComplete();

        /// <summary>Closes immediately.</summary>
        public void Close()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 1) return;
            _outbox.Writer.TryComplete();
            try { _tcp.Close(); } catch { /* already gone */ }
            _onFrame(this, null);
        }

        private async Task ReadLoop()
        {
            var header = new byte[4];
            try
            {
                while (true)
                {
                    if (!await ReadExactly(header)) break;
                    int length = BinaryPrimitives.ReadInt32LittleEndian(header);
                    if (length <= 0 || length > MaxFrameBytes) break;

                    var payload = new byte[length];
                    if (!await ReadExactly(payload)) break;
                    _onFrame(this, payload);
                }
            }
            catch { /* socket closed or reset */ }
            finally { Close(); }
        }

        private async Task<bool> ReadExactly(byte[] buffer)
        {
            int got = 0;
            while (got < buffer.Length)
            {
                int n = await _stream.ReadAsync(buffer.AsMemory(got));
                if (n <= 0) return false;
                got += n;
            }
            return true;
        }

        private async Task WriteLoop()
        {
            try
            {
                await foreach (var frame in _outbox.Reader.ReadAllAsync())
                    await _stream.WriteAsync(frame);
            }
            catch { /* socket closed or reset */ }
            finally { Close(); }
        }
    }
}