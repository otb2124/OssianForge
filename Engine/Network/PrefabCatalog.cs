using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Resources.Config;

namespace OssianForge.Engine.Network.Replication
{
    /// <summary>
    /// Scenes whose ROOT node carries a NetworkIdentityProperty with spawn flags. Filled once, when the scene
    /// resources are loaded (before networking starts).
    /// </summary>
    public sealed class PrefabCatalog
    {
        /// <summary>Group name of the nodes that mark where a peer's entity appears.</summary>
        public const string SpawnPointGroup = "spawnPoint";

        private readonly List<(string SceneId, NetSpawn Flags)> _entries = new();

        public IReadOnlyList<(string SceneId, NetSpawn Flags)> Entries => _entries;
        public IEnumerable<string> SceneIds => _entries.Select(e => e.SceneId);

        /// <summary>Scene spawned once per peer. Null when no scene declares SpawnPerPeer.</summary>
        public string? PeerPrefabId { get; private set; }

        public void Discover(IEnumerable<SceneConfig> scenes)
        {
            _entries.Clear();
            PeerPrefabId = null;

            var perPeer = new List<string>();
            foreach (var scene in scenes)
            {
                var flags = scene.RootSpawnFlags();
                if (flags == NetSpawn.None) continue;

                _entries.Add((scene.Id, flags));
                if (flags.HasFlag(NetSpawn.SpawnPerPeer)) perPeer.Add(scene.Id);
            }

            if (perPeer.Count > 1)
                Console.WriteLine($"[REPL] {perPeer.Count} scenes declare SpawnPerPeer ({string.Join(", ", perPeer)}); using '{perPeer[0]}'.");
            if (perPeer.Count > 0) PeerPrefabId = perPeer[0];

            Console.WriteLine($"[REPL] prefab catalog: {_entries.Count} scene(s), PeerPrefabId={PeerPrefabId ?? "(null)"}");
        }
    }
}