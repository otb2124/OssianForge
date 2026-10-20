using System.Numerics;
using OssianForge.Content.Cooked;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Resources.Config;
using Net = OssianForge.Engine.Network.Network;

namespace OssianForge.Engine.Network.Replication
{
    /// <summary>
    /// Entity layer on top of the session layer. Owns the NetId table, spawn/despawn, state batching,
    /// late-join snapshots and relaying. Knows nothing about what a replicator writes.
    ///
    /// Main thread only. Hooks: OnUpdate runs at the end of Network.OnUpdate (before Nodes.OnUpdate),
    /// OnLateUpdate runs after Physics.OnUpdate.
    /// </summary>
    public sealed class NetworkReplication
    {
        private const int StaticBit = 0x40000000;
        private static readonly NetChannel[] Channels = { NetChannel.Unreliable, NetChannel.Reliable };

        private readonly Net _net;
        private readonly Dictionary<int, NetworkIdentityProperty> _entities = new();
        private int _nextDynamicId = 1;

        /// <summary>Host (or offline): optional spawn position per peer id. Default: the "spawnPoint" group.</summary>
        public Func<int, Vector3?>? SpawnPointProvider;

        /// <summary>Spawnable scenes found at resource-load time.</summary>
        public PrefabCatalog Prefabs { get; } = new();

        public IReadOnlyDictionary<int, NetworkIdentityProperty> Entities => _entities;

        private string? _peerPrefabId;
        /// <summary>Scene spawned once per peer. Explicit assignment wins, else the catalog's SpawnPerPeer scene (may be null).</summary>
        public string? PeerPrefabId
        {
            get => _peerPrefabId ?? Prefabs.PeerPrefabId;
            set => _peerPrefabId = value;
        }

        public NetworkReplication(Net net)
        {
            _net = net;
            SpawnPointProvider = DefaultSpawnPoint;
            net.MessageReceived += OnMessage;
            net.PeerJoined += OnPeerJoined;
            net.PeerLeft += OnPeerLeft;
            net.Connected += MarkAllRolesPending;
            net.Disconnected += OnDisconnected;
        }

        // ── registry ─────────────────────────────────────────────────────────

        internal void Register(NetworkIdentityProperty id)
        {
            if (id.NetId == 0)
            {
                var n = id.Entity!;
                id.NetId = StaticNetId(!string.IsNullOrEmpty(n.Id) ? n.Id : n.Name ?? "");
            }

            if (_entities.TryGetValue(id.NetId, out var other) && !ReferenceEquals(other, id))
            {
                Console.WriteLine($"[REPL] NetId {id.NetId} collision ('{id.Entity?.Id}' vs '{other.Entity?.Id}'). Not replicated.");
                return;
            }

            Console.WriteLine($"[REPL] register '{id.Entity?.Id}' netId=0x{id.NetId:X} dynamic={id.IsDynamic}");

            _entities[id.NetId] = id;
            id.RolePending = true;
        }

        internal void Unregister(NetworkIdentityProperty id)
        {
            if (_entities.TryGetValue(id.NetId, out var cur) && ReferenceEquals(cur, id))
                _entities.Remove(id.NetId);
        }

        private static int StaticNetId(string key)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in key) { h ^= c; h *= 16777619; }
                return StaticBit | (int)(h & 0x3FFFFFFF);
            }
        }

        private void MarkAllRolesPending()
        {
            foreach (var id in _entities.Values) id.RolePending = true;
        }

        // ── spawning ─────────────────────────────────────────────────────────

        /// <summary>Host (or offline): create a prefab entity and announce it. Safe to call from scripts mid-frame.</summary>
        public void Spawn(string prefabId, int ownerPeerId = Net.HostPeerId, Action<Node>? onSpawned = null)
        {
            if (_net.IsClient) { Console.WriteLine("[REPL] Only the host can spawn."); return; }
            NodeManager.Enqueue(() =>
            {
                var node = SpawnNow(prefabId, ownerPeerId);
                if (node != null) onSpawned?.Invoke(node);
            });
        }

        public void Despawn(Node node)
        {
            if (_net.IsClient) { Console.WriteLine("[REPL] Only the host can despawn."); return; }
            var id = node.GetProperty<NetworkIdentityProperty>();
            if (id == null) return;
            NodeManager.Enqueue(() => DespawnNow(id, broadcast: true));
        }

        private Node? SpawnNow(string prefabId, int owner)
        {
            int netId = _nextDynamicId++;
            var node = Instantiate(prefabId, netId, owner, SpawnPointProvider?.Invoke(owner));
            if (node == null) return null;

            if (_net.IsHost && _net.IsReady)
                _net.Broadcast(BuildSpawn(node.GetProperty<NetworkIdentityProperty>()!));
            return node;
        }

        private void DespawnNow(NetworkIdentityProperty id, bool broadcast)
        {
            if (broadcast && _net.IsHost)
                _net.Broadcast(Net.Build(PacketType.Despawn, w => w.I32(id.NetId)));
            if (id.Entity != null)
                Engine.Nodes.NodeManager.DestroySubtree(id.Entity);
        }

        /// <summary>Position of the spawnPoint node chosen by peer id; null when the scene has none (prefab keeps its own transform).</summary>
        private static Vector3? DefaultSpawnPoint(int peerId)
        {
            var pts = Engine.Nodes.NodeManager.GetNodesInGroup(PrefabCatalog.SpawnPointGroup);
            if (pts.Count == 0) return null;

            int index = ((peerId % pts.Count) + pts.Count) % pts.Count;
            return pts[index].GetProperty<TransformProperty>()?.WorldTransform.Position;
        }

        private Node? Instantiate(string prefabId, int netId, int owner, Vector3? at)
        {
            var scene = Engine.Resources.GetResource<SceneConfig>(prefabId);
            if (scene == null) { Console.WriteLine($"[REPL] Prefab scene '{prefabId}' not found."); return null; }

            // "<rootLocalId>@<netId>": the root's id, and the namespace of everything below it.
            var scope = new InstanceScope($"{scene.RootLocalId ?? "prefab"}@{netId}");
            var node = scene.Instantiate(scope);
            node.Id = scope.Path;
            node.IsInstanceRoot = true;

            var id = node.GetProperty<NetworkIdentityProperty>();
            if (id == null) { id = new NetworkIdentityProperty(); node.AddProperty(id); }
            id.NetId = netId;
            id.OwnerPeerId = owner;
            id.PrefabId = prefabId;

            var tp = node.GetProperty<TransformProperty>();
            if (tp == null) { tp = new TransformProperty(); node.AddProperty(tp); }
            if (at.HasValue) tp.Position = at.Value;

            var nm = Engine.Nodes.NodeManager;
            var parent = nm.Roots.FirstOrDefault();
            if (parent != null) parent.AddChild(node); else nm.AddNode(node);
            nm.StartSubtree(node);
            return node;
        }

        // ── per frame ────────────────────────────────────────────────────────

        /// <summary>Roles and proxy interpolation. Runs before Nodes.OnUpdate.</summary>
        public void OnUpdate(double delta)
        {
            foreach (var id in _entities.Values)
            {
                if (id.RolePending) id.ApplyRole();
                if (!id.IsProxy) continue;

                foreach (var r in id.Replicators)
                {
                    try { r.ApplyProxy(delta); }
                    catch (Exception ex) { Console.WriteLine($"[REPL] {r.GetType().Name}.ApplyProxy: {ex.Message}"); }
                }
            }
        }

        /// <summary>Collects and sends state for every entity this peer owns. Runs after Physics.OnUpdate.</summary>
        public void OnLateUpdate(double delta)
        {
            if (!_net.IsOnline || !_net.IsReady || _entities.Count == 0) return;

            List<byte[]>? unreliable = null, reliable = null;

            foreach (var id in _entities.Values)
            {
                if (!id.IsAuthority) continue;

                id.SendTimer += (float)delta;
                if (id.SendTimer < 1f / Math.Max(id.SendRate, 0.1f)) continue;
                id.SendTimer = 0f;

                foreach (var ch in Channels)
                {
                    var blob = BuildEntity(id, full: false, ch, out int blocks);
                    if (blocks == 0) continue;

                    if (ch == NetChannel.Reliable) (reliable ??= new()).Add(blob);
                    else (unreliable ??= new()).Add(blob);
                }
            }

            if (unreliable != null) Send(NetChannel.Unreliable, BuildStatePacket(unreliable));
            if (reliable != null) Send(NetChannel.Reliable, BuildStatePacket(reliable));
        }

        internal void SendEvent(NetworkIdentityProperty id, NetReplicator rep, Action<BlobWriter> write)
        {
            if (!id.IsAuthority || !_net.IsOnline || !_net.IsReady) return;

            var bw = new BlobWriter(16);
            write(bw);
            byte[] payload = bw.ToArray();

            Send(NetChannel.Reliable, Net.Build(PacketType.EntityEvent, w =>
            {
                w.I32(id.NetId);
                w.U16((ushort)rep.NodeIndex);
                w.U8(rep.WireId);
                w.Arr<byte>(payload);
            }));
        }

        // ── sending ──────────────────────────────────────────────────────────

        private void Send(NetChannel channel, byte[] payload)
        {
            _ = channel;   // seam: both channels use the TCP link for now
            if (_net.IsHost) _net.Broadcast(payload);
            else _net.SendToHost(payload);
        }

        /// <summary>[netId][blockCount]{[nodeIndex][wireId][len + bytes]}. Blocks carry their own length so peers can skip ids they don't know.</summary>
        private byte[] BuildEntity(NetworkIdentityProperty id, bool full, NetChannel? only, out int blocks)
        {
            var parts = new List<(ushort Node, byte Wire, byte[] Data)>();
            foreach (var r in id.Replicators)
            {
                if (only.HasValue && r.Channel != only.Value) continue;

                var bw = new BlobWriter(32);
                bool wrote;
                try { wrote = r.WriteState(bw, full); }
                catch (Exception ex)
                {
                    Console.WriteLine($"[REPL] {r.GetType().Name}.WriteState: {ex.Message}");
                    continue;
                }
                if (wrote) parts.Add(((ushort)r.NodeIndex, r.WireId, bw.ToArray()));
            }

            blocks = parts.Count;
            var w = new BlobWriter(64);
            w.I32(id.NetId);
            w.U8((byte)parts.Count);
            foreach (var p in parts) { w.U16(p.Node); w.U8(p.Wire); w.Arr<byte>(p.Data); }
            return w.ToArray();
        }

        private static byte[] BuildStatePacket(List<byte[]> entities)
            => Net.Build(PacketType.EntityState, w =>
            {
                w.I32(entities.Count);
                foreach (var e in entities) w.Arr<byte>(e);
            });

        private byte[] BuildSpawn(NetworkIdentityProperty id)
        {
            byte[] blob = BuildEntity(id, full: true, only: null, out _);
            return Net.Build(PacketType.Spawn, w =>
            {
                w.Str(id.PrefabId);
                w.I32(id.OwnerPeerId);
                w.Arr<byte>(blob);
            });
        }

        // ── receiving ────────────────────────────────────────────────────────

        private void OnMessage(int sender, PacketType type, BlobReader r)
        {
            switch (type)
            {
                case PacketType.Spawn: if (_net.IsClient) HandleSpawn(r); break;
                case PacketType.Despawn: if (_net.IsClient) HandleDespawn(r); break;
                case PacketType.EntityState: HandleState(sender, r); break;
                case PacketType.EntityEvent: HandleEvent(sender, r); break;
            }
        }

        /// <summary>
        /// Host: only the owner may speak for an entity. Guest: only the host speaks (it relays), and never
        /// about entities this peer owns.
        /// </summary>
        private bool Accept(NetworkIdentityProperty id, int sender)
            => _net.IsHost
                ? id.OwnerPeerId == sender
                : sender == Net.HostPeerId && id.OwnerPeerId != _net.LocalPeerId;

        private void HandleSpawn(BlobReader r)
        {
            string prefab = r.Str();
            int owner = r.I32();
            var er = new BlobReader(r.Arr<byte>());
            int netId = er.I32();

            if (_entities.ContainsKey(netId)) return;

            var node = Instantiate(prefab, netId, owner, null);
            var id = node?.GetProperty<NetworkIdentityProperty>();
            if (id != null) ApplyBlocks(id, er);
        }

        private void HandleDespawn(BlobReader r)
        {
            int netId = r.I32();
            if (_entities.TryGetValue(netId, out var id)) DespawnNow(id, broadcast: false);
        }

        private void HandleState(int sender, BlobReader r)
        {
            int count = r.I32();
            List<byte[]>? relay = _net.IsHost ? new() : null;

            for (int i = 0; i < count; i++)
            {
                byte[] blob = r.Arr<byte>();
                var er = new BlobReader(blob);
                int netId = er.I32();

                if (!_entities.TryGetValue(netId, out var id) || !Accept(id, sender)) continue;

                ApplyBlocks(id, er);
                relay?.Add(blob);
            }

            if (relay is { Count: > 0 })
                _net.BroadcastExceptPeer(BuildStatePacket(relay), sender);
        }

        private void HandleEvent(int sender, BlobReader r)
        {
            int netId = r.I32();
            ushort node = r.U16();
            byte wire = r.U8();
            byte[] payload = r.Arr<byte>();

            if (!_entities.TryGetValue(netId, out var id) || !Accept(id, sender)) return;

            try { id.FindReplicator(node, wire)?.ReadEvent(new BlobReader(payload)); }
            catch (Exception ex) { Console.WriteLine($"[REPL] event on netId {netId}: {ex.Message}"); }

            if (_net.IsHost)
            {
                _net.BroadcastExceptPeer(Net.Build(PacketType.EntityEvent, w =>
                {
                    w.I32(netId);
                    w.U16(node);
                    w.U8(wire);
                    w.Arr<byte>(payload);
                }), sender);
            }
        }

        private static void ApplyBlocks(NetworkIdentityProperty id, BlobReader er)
        {
            int n = er.U8();
            for (int i = 0; i < n; i++)
            {
                ushort node = er.U16();
                byte wire = er.U8();
                byte[] data = er.Arr<byte>();

                var rep = id.FindReplicator(node, wire);
                if (rep == null) continue;   // unknown here: skipped thanks to the length prefix

                // A bad block must never cost the peer its connection (Network drops peers on exceptions).
                try { rep.ReadState(new BlobReader(data)); }
                catch (Exception ex) { Console.WriteLine($"[REPL] {rep.GetType().Name}.ReadState on netId {id.NetId}: {ex.Message}"); }
            }
        }

        // ── session events ───────────────────────────────────────────────────

        private void OnPeerJoined(PeerInfo peer)
        {
            Console.WriteLine($"[REPL] peer {peer.Id} joined; PeerPrefabId={PeerPrefabId ?? "(null)"}, entities={_entities.Count}");

            if (!_net.IsHost) return;

            // 1. everything that already exists, to the newcomer only
            var statics = new List<byte[]>();
            foreach (var id in _entities.Values.ToList())
            {
                if (id.IsDynamic)
                {
                    _net.SendTo(peer.Id, BuildSpawn(id));
                }
                else
                {
                    var blob = BuildEntity(id, full: true, only: null, out int blocks);
                    if (blocks > 0) statics.Add(blob);
                }
            }
            if (statics.Count > 0) _net.SendTo(peer.Id, BuildStatePacket(statics));

            if (PeerPrefabId != null)
            {
                try { SpawnNow(PeerPrefabId, peer.Id); }
                catch (Exception ex)
                {
                    Console.WriteLine($"[REPL] Spawning '{PeerPrefabId}' for peer {peer.Id} failed: {ex}");
                }
            }
        }

        private void OnPeerLeft(PeerInfo peer)
        {
            if (!_net.IsHost) return;
            foreach (var id in _entities.Values.Where(e => e.IsDynamic && e.OwnerPeerId == peer.Id).ToList())
                DespawnNow(id, broadcast: true);
        }

        private void OnDisconnected(string reason)
        {
            foreach (var id in _entities.Values.Where(e => e.IsDynamic).ToList())
                DespawnNow(id, broadcast: false);
            MarkAllRolesPending();   // offline: everything is authoritative again
        }
    }
}