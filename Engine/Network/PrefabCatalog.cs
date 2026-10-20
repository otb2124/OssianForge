using System.Numerics;
using OssianForge.Engine.Nodes.Props;

namespace OssianForge.Engine.Network.Replication
{
    /// <summary>
    /// Scenes the main scene declares as spawnable: a scene-reference node whose NetworkIdentityProperty has spawn
    /// flags. SceneBuilder registers them while the scene is built (before networking starts); the ref node
    /// itself stays inert. No such ref in the scene means nothing is spawned.
    /// </summary>
    public sealed class PrefabCatalog
    {
        public sealed record Entry(string SceneId, NetSpawn Flags, float SendRate, Vector3? Position);

        private readonly Dictionary<string, Entry> _entries = new();

        public IReadOnlyCollection<Entry> Entries => _entries.Values;

        /// <summary>Scene spawned once per peer. Null when no ref declares SpawnPerPeer.</summary>
        public string? PeerPrefabId { get; private set; }

        public bool TryGet(string sceneId, out Entry entry) => _entries.TryGetValue(sceneId, out entry!);

        public void Register(string sceneId, NetworkIdentityProperty identity, Vector3? position)
        {
            if (_entries.ContainsKey(sceneId)) return;

            _entries[sceneId] = new Entry(sceneId, identity.Spawn, identity.SendRate, position);

            if (identity.Spawn.HasFlag(NetSpawn.SpawnPerPeer))
            {
                if (PeerPrefabId == null) PeerPrefabId = sceneId;
                else Console.WriteLine($"[REPL] Several SpawnPerPeer scenes declared; using '{PeerPrefabId}', ignoring '{sceneId}'.");
            }

            Console.WriteLine($"[REPL] prefab declared: '{sceneId}' flags={identity.Spawn} at={position?.ToString() ?? "(own transform)"}");
        }
    }
}