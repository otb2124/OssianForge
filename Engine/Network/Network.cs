using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using OssianForge.Content.Cooked;

namespace OssianForge.Engine.Network
{
    public enum NetRole { Offline, Host, Client }

    /// <summary>
    /// Packet ids. 0-31 are reserved for this module's session traffic; gameplay packets start at 32
    /// and are delivered through Network.MessageReceived.
    /// </summary>
    public enum PacketType : byte
    {
        Hello = 1,
        Welcome = 2,
        Reject = 3,
        PeerJoined = 4,
        PeerLeft = 5,
        Ping = 6,
        Pong = 7,
        Chat = 8,
    }

    public sealed class PeerInfo
    {
        public int Id;
        public string Name = "";
        /// <summary>Round trip to the host, measured by guests (0 for everyone else).</summary>
        public double RttMs;
    }

    /// <summary>
    /// Host-authoritative session layer. The host (peer 0) listens; guests connect and talk only to the host,
    /// which relays. Sockets run on thread-pool tasks, but every received packet is queued and handled in
    /// OnUpdate on the engine thread, so handlers can touch the node tree freely.
    ///
    /// Lifecycle: Create/Initialize like other modules, OnUpdate every frame (after Inputs, before Nodes),
    /// StartIfRequested at the end of Engine.OnLoad, Shutdown when the window closes. Main thread only.
    /// </summary>
    public sealed class Network
    {
        public const ushort ProtocolVersion = 1;
        public const int DefaultPort = 7777;
        public const int HostPeerId = 0;

        private const float PingIntervalSeconds = 1f;

        public NetRole Role { get; private set; } = NetRole.Offline;
        public bool IsHost => Role == NetRole.Host;
        public bool IsClient => Role == NetRole.Client;
        public bool IsOnline => Role != NetRole.Offline;

        /// <summary>Host: listening. Guest: handshake finished and a peer id assigned.</summary>
        public bool IsReady { get; private set; }

        public int LocalPeerId { get; private set; } = -1;
        public string LocalName { get; private set; } = "";
        public double RttMs { get; private set; }
        public IReadOnlyDictionary<int, PeerInfo> Peers => _peers;

        /// <summary>Host started listening, or a guest finished the handshake.</summary>
        public event Action? Connected;
        /// <summary>Left the session: connect failed, rejected, host lost, or Disconnect() called.</summary>
        public event Action<string>? Disconnected;
        /// <summary>Another peer is in the session. A guest gets one of these per existing peer right after the handshake.</summary>
        public event Action<PeerInfo>? PeerJoined;
        public event Action<PeerInfo>? PeerLeft;
        public event Action<int, string>? ChatReceived;
        /// <summary>Gameplay packets (type >= 32): sender peer id, type, reader positioned after the type byte.</summary>
        public event Action<int, PacketType, BlobReader>? MessageReceived;

        private readonly Dictionary<int, PeerInfo> _peers = new();
        private readonly List<NetConnection> _connections = new();   // host: every guest link
        private readonly ConcurrentQueue<InboxItem> _inbox = new();
        private TcpListener? _listener;
        private NetConnection? _server;                               // guest: the link to the host
        private int _nextPeerId = 1;
        private int _session;      // bumped by every Host/Join/Reset so late async callbacks are ignored
        private float _pingTimer;

        private enum InboxKind { Connected, ConnectFailed, Frame, Closed }

        private readonly record struct InboxItem(
            int Session, InboxKind Kind, NetConnection? Conn, byte[]? Frame = null, string? Error = null);

        // ── Starting and stopping ────────────────────────────────────────────

        public void StartIfRequested()
        {
            if (NetLaunchArgs.Host)
            {
                Host(NetLaunchArgs.Port, NetLaunchArgs.Name);
            }
            else if (NetLaunchArgs.JoinAddress != null)
            {
                if (TryParseEndpoint(NetLaunchArgs.JoinAddress, out string host, out int port))
                    Join(host, port, NetLaunchArgs.Name);
                else
                    Console.WriteLine($"[NET] Could not parse --join '{NetLaunchArgs.JoinAddress}'.");
            }
        }

        public bool Host(int port = DefaultPort, string? name = null)
        {
            if (IsOnline)
            {
                Console.WriteLine("[NET] Already in a session. Use 'net stop' first.");
                return false;
            }

            TcpListener listener;
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[NET] Could not listen on port {port}: {ex.Message}");
                return false;
            }

            int session = ++_session;
            _listener = listener;
            Role = NetRole.Host;
            IsReady = true;
            LocalPeerId = HostPeerId;
            LocalName = Sanitize(name ?? Environment.UserName, 24, "host");
            _nextPeerId = 1;
            _peers.Clear();
            _peers[HostPeerId] = new PeerInfo { Id = HostPeerId, Name = LocalName };

            _ = Task.Run(() => AcceptLoop(listener, session));

            LogLocalAddresses(port);
            Connected?.Invoke();
            return true;
        }

        public bool Join(string host, int port = DefaultPort, string? name = null)
        {
            if (IsOnline)
            {
                Console.WriteLine("[NET] Already in a session. Use 'net stop' first.");
                return false;
            }

            int session = ++_session;
            Role = NetRole.Client;
            IsReady = false;
            LocalPeerId = -1;
            LocalName = Sanitize(name ?? Environment.UserName, 24, "guest");
            _peers.Clear();

            Console.WriteLine($"[NET] Connecting to {host}:{port}...");

            _ = Task.Run(async () =>
            {
                try
                {
                    var tcp = new TcpClient { NoDelay = true };
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await tcp.ConnectAsync(host, port, timeout.Token);

                    var conn = NewConnection(tcp, session);
                    _inbox.Enqueue(new InboxItem(session, InboxKind.Connected, conn));
                    conn.Start();
                }
                catch (Exception ex)
                {
                    string why = ex is OperationCanceledException ? "timed out" : ex.Message;
                    _inbox.Enqueue(new InboxItem(session, InboxKind.ConnectFailed, null, Error: why));
                }
            });

            return true;
        }

        public void Disconnect(string reason = "disconnected")
        {
            if (!IsOnline) return;
            Reset();
            Console.WriteLine($"[NET] {reason}");
            Disconnected?.Invoke(reason);
        }

        /// <summary>Quietly drops everything (window closing). No events.</summary>
        public void Shutdown() => Reset();

        private void Reset()
        {
            _session++;   // everything still in flight now belongs to a dead session and is ignored

            try { _listener?.Stop(); } catch { /* already stopped */ }
            _listener = null;

            foreach (var conn in _connections) conn.Close();
            _connections.Clear();
            _server?.Close();
            _server = null;

            _peers.Clear();
            Role = NetRole.Offline;
            IsReady = false;
            LocalPeerId = -1;
            RttMs = 0;
            _pingTimer = 0;
        }

        // ── Sending ──────────────────────────────────────────────────────────

        public static byte[] Build(PacketType type, Action<BlobWriter>? write = null)
        {
            var w = new BlobWriter(64);
            w.U8((byte)type);
            write?.Invoke(w);
            return w.ToArray();
        }

        /// <summary>Guest to host.</summary>
        public void SendToHost(byte[] payload) => _server?.Send(payload);

        /// <summary>Host to one guest.</summary>
        public void SendTo(int peerId, byte[] payload)
        {
            foreach (var conn in _connections)
                if (conn.PeerId == peerId) { conn.Send(payload); return; }
        }

        /// <summary>Host to every guest (except one, e.g. the sender of a relayed packet).</summary>
        public void Broadcast(byte[] payload, NetConnection? except = null)
        {
            foreach (var conn in _connections)
                if (conn.PeerId >= 0 && conn != except) conn.Send(payload);
        }

        public void SendChat(string text)
        {
            text = Sanitize(text, 200);
            if (text.Length == 0) return;

            if (IsHost) BroadcastChat(HostPeerId, text);
            else if (IsClient && IsReady) SendToHost(Build(PacketType.Chat, w => w.Str(text)));
            else Console.WriteLine("[NET] Not in a session.");
        }

        private void BroadcastChat(int fromPeer, string text)
        {
            Broadcast(Build(PacketType.Chat, w => { w.I32(fromPeer); w.Str(text); }));
            RaiseChat(fromPeer, text);
        }

        private void RaiseChat(int fromPeer, string text)
        {
            string who = _peers.TryGetValue(fromPeer, out var p) ? p.Name : $"peer {fromPeer}";
            Console.WriteLine($"[CHAT] {who}: {text}");
            ChatReceived?.Invoke(fromPeer, text);
        }

        // ── Per-frame ────────────────────────────────────────────────────────

        public void OnUpdate(double delta)
        {
            while (_inbox.TryDequeue(out var item))
            {
                if (item.Session != _session)
                {
                    if (item.Kind == InboxKind.Connected) item.Conn?.Close();
                    continue;
                }

                switch (item.Kind)
                {
                    case InboxKind.Connected:
                        OnConnected(item.Conn!);
                        break;

                    case InboxKind.ConnectFailed:
                        Disconnect($"connect failed: {item.Error}");
                        break;

                    case InboxKind.Closed:
                        OnClosed(item.Conn!);
                        break;

                    case InboxKind.Frame:
                        try
                        {
                            OnFrame(item.Conn!, item.Frame!);
                        }
                        catch (Exception ex)
                        {
                            // A malformed packet must never take the engine down: drop that peer.
                            Console.WriteLine($"[NET] Bad packet from {item.Conn!.Remote}: {ex.Message}");
                            item.Conn.Close();
                        }
                        break;
                }
            }

            if (IsClient && IsReady)
            {
                _pingTimer += (float)delta;
                if (_pingTimer >= PingIntervalSeconds)
                {
                    _pingTimer = 0;
                    SendToHost(Build(PacketType.Ping, w => w.F64(NowSeconds())));
                }
            }
        }

        private void OnConnected(NetConnection conn)
        {
            if (IsHost)
            {
                _connections.Add(conn);
                Console.WriteLine($"[NET] Incoming connection from {conn.Remote}");
            }
            else
            {
                _server = conn;
                conn.Send(Build(PacketType.Hello, w => { w.U16(ProtocolVersion); w.Str(LocalName); }));
            }
        }

        private void OnClosed(NetConnection conn)
        {
            if (IsHost)
            {
                _connections.Remove(conn);
                if (conn.PeerId >= 0 && _peers.Remove(conn.PeerId, out var peer))
                {
                    Broadcast(Build(PacketType.PeerLeft, w => w.I32(peer.Id)));
                    Console.WriteLine($"[NET] {peer.Name} (peer {peer.Id}) left.");
                    PeerLeft?.Invoke(peer);
                }
            }
            else if (conn == _server)
            {
                Disconnect("connection to the host was lost");
            }
        }

        private void OnFrame(NetConnection conn, byte[] frame)
        {
            var r = new BlobReader(frame);
            var type = (PacketType)r.U8();

            if (IsHost)
            {
                if (_connections.Contains(conn)) HandleAsHost(conn, type, r);
            }
            else
            {
                HandleAsClient(type, r);
            }
        }

        // ── Host side ────────────────────────────────────────────────────────

        private void HandleAsHost(NetConnection conn, PacketType type, BlobReader r)
        {
            switch (type)
            {
                case PacketType.Hello:
                    {
                        if (conn.PeerId >= 0) return;

                        ushort version = r.U16();
                        string name = Sanitize(r.Str(), 24, "guest");

                        if (version != ProtocolVersion)
                        {
                            conn.Send(Build(PacketType.Reject, w =>
                                w.Str($"protocol mismatch (host {ProtocolVersion}, you {version})")));
                            conn.Shutdown();
                            return;
                        }

                        int id = _nextPeerId++;
                        if (_peers.Values.Any(p => p.Name == name)) name = $"{name}#{id}";

                        var peer = new PeerInfo { Id = id, Name = name };
                        conn.PeerId = id;
                        conn.Name = name;
                        _peers[id] = peer;

                        conn.Send(Build(PacketType.Welcome, w =>
                        {
                            w.I32(id);
                            w.I32(_peers.Count);
                            foreach (var p in _peers.Values) { w.I32(p.Id); w.Str(p.Name); }
                        }));

                        Broadcast(Build(PacketType.PeerJoined, w => { w.I32(id); w.Str(name); }), except: conn);

                        Console.WriteLine($"[NET] {name} joined as peer {id} ({conn.Remote}).");
                        PeerJoined?.Invoke(peer);
                        break;
                    }

                case PacketType.Ping:
                    {
                        if (conn.PeerId < 0) return;
                        double sent = r.F64();
                        conn.Send(Build(PacketType.Pong, w => w.F64(sent)));
                        break;
                    }

                case PacketType.Chat:
                    {
                        if (conn.PeerId < 0) return;
                        BroadcastChat(conn.PeerId, Sanitize(r.Str(), 200));
                        break;
                    }

                default:
                    if (conn.PeerId >= 0) MessageReceived?.Invoke(conn.PeerId, type, r);
                    break;
            }
        }

        // ── Guest side ───────────────────────────────────────────────────────

        private void HandleAsClient(PacketType type, BlobReader r)
        {
            switch (type)
            {
                case PacketType.Welcome:
                    {
                        LocalPeerId = r.I32();
                        int count = r.I32();
                        _peers.Clear();
                        for (int i = 0; i < count; i++)
                        {
                            var p = new PeerInfo { Id = r.I32(), Name = r.Str() };
                            _peers[p.Id] = p;
                            if (p.Id == LocalPeerId) LocalName = p.Name;   // the host may have renamed us
                        }

                        IsReady = true;
                        Console.WriteLine($"[NET] Joined as '{LocalName}' (peer {LocalPeerId}), {_peers.Count} peer(s) in session.");
                        Connected?.Invoke();

                        foreach (var p in _peers.Values.ToList())
                            if (p.Id != LocalPeerId) PeerJoined?.Invoke(p);
                        break;
                    }

                case PacketType.Reject:
                    Disconnect($"rejected by host: {r.Str()}");
                    break;

                case PacketType.PeerJoined:
                    {
                        var p = new PeerInfo { Id = r.I32(), Name = r.Str() };
                        _peers[p.Id] = p;
                        Console.WriteLine($"[NET] {p.Name} joined (peer {p.Id}).");
                        PeerJoined?.Invoke(p);
                        break;
                    }

                case PacketType.PeerLeft:
                    {
                        int id = r.I32();
                        if (_peers.Remove(id, out var p))
                        {
                            Console.WriteLine($"[NET] {p.Name} left (peer {p.Id}).");
                            PeerLeft?.Invoke(p);
                        }
                        break;
                    }

                case PacketType.Pong:
                    {
                        double sent = r.F64();
                        RttMs = (NowSeconds() - sent) * 1000.0;
                        if (_peers.TryGetValue(HostPeerId, out var host)) host.RttMs = RttMs;
                        break;
                    }

                case PacketType.Chat:
                    {
                        int from = r.I32();
                        RaiseChat(from, r.Str());
                        break;
                    }

                default:
                    MessageReceived?.Invoke(HostPeerId, type, r);
                    break;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task AcceptLoop(TcpListener listener, int session)
        {
            try
            {
                while (true)
                {
                    var tcp = await listener.AcceptTcpClientAsync();
                    tcp.NoDelay = true;

                    var conn = NewConnection(tcp, session);
                    _inbox.Enqueue(new InboxItem(session, InboxKind.Connected, conn));
                    conn.Start();
                }
            }
            catch { /* listener stopped */ }
        }

        private NetConnection NewConnection(TcpClient tcp, int session)
            => new(tcp, (conn, frame) => _inbox.Enqueue(frame == null
                ? new InboxItem(session, InboxKind.Closed, conn)
                : new InboxItem(session, InboxKind.Frame, conn, frame)));

        /// <summary>"25.1.2.3" or "25.1.2.3:7777" or "hostname:7777". IPv4/hostnames only.</summary>
        public static bool TryParseEndpoint(string text, out string host, out int port)
        {
            host = text.Trim();
            port = DefaultPort;

            int colon = host.LastIndexOf(':');
            if (colon < 0) return host.Length > 0;

            if (!int.TryParse(host[(colon + 1)..], out port) || port is < 1 or > 65535) return false;
            host = host[..colon];
            return host.Length > 0;
        }

        private static string Sanitize(string text, int maxLength, string fallback = "")
        {
            string clean = new string(text.Where(c => !char.IsControl(c)).Take(maxLength).ToArray()).Trim();
            return clean.Length == 0 ? fallback : clean;
        }

        private static double NowSeconds() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

        private static void LogLocalAddresses(int port)
        {
            Console.WriteLine($"[NET] Hosting on port {port}. Guests can join with one of:");
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var ua in nic.GetIPProperties().UnicastAddresses)
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                        Console.WriteLine($"[NET]   {ua.Address}:{port}   ({nic.Name})");
            }
        }
    }
}