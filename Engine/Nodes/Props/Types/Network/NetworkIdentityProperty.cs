using OssianForge.Engine.Network.Replication;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using Net = OssianForge.Engine.Network.Network;

namespace OssianForge.Engine.Nodes.Props
{
    /// <summary>
    /// Marks a node as a network entity. It carries identity (NetId, owner, prefab) and owns the replicators
    /// built from the properties of this node AND every descendant (a character is usually a root with a body
    /// child that carries the mesh, animation and physics). A descendant that has its own
    /// NetworkIdentityProperty is a separate entity and is skipped. Offline it does nothing: IsAuthority is always true.
    ///
    /// Ownership model: the owner peer simulates the entity, everybody else mirrors it (proxy). Default owner
    /// is the host. Guests only send state for entities they own; the host relays it to the other guests.
    /// </summary>
    public class NetworkIdentityProperty : NodeProperty
    {
        private const int StaticBit = 0x40000000;

        /// <summary>0 until registered. Scene-placed: hash of the node id (both peers load the same scene). Dynamic: host counter.</summary>
        public int NetId { get; internal set; }
        public int OwnerPeerId { get; internal set; } = Net.HostPeerId;
        /// <summary>SceneConfig id used to recreate the entity on other peers. Null for scene-placed entities.</summary>
        public string? PrefabId { get; internal set; }

        public float SendRate = 20f;

        /// <summary>Property type names that must not run on proxies (they would fight the replicated state).</summary>
        public string[] ProxyDisabled = { "StateMachineProperty", "ScriptProperty", "ActionProperty" };

        public Node? Entity { get; private set; }

        public bool IsDynamic => NetId != 0 && (NetId & StaticBit) == 0;
        public bool IsAuthority
        {
            get
            {
                var n = Engine.Network;
                return !n.IsOnline || OwnerPeerId == n.LocalPeerId;
            }
        }
        public bool IsProxy => !IsAuthority;

        internal NetworkReplication? Replication;
        internal List<NetReplicator> Replicators = new();
        internal float SendTimer;
        internal bool RolePending = true;

        public bool PlayerTemplate;

        public override void OnStart(Node node)
        {
            base.OnStart(node);
            Entity = node;

            Replicators = new List<NetReplicator>();
            int index = 0;
            CollectReplicators(node, ref index, isRoot: true);

            Console.WriteLine($"[REPL] '{node.Id}' replicators: {Replicators.Count}");

            Replication = Engine.Network.Replication;
            Replication.Register(this);
        }

        public override void OnDestroy(Node node) => Replication?.Unregister(this);

        private void CollectReplicators(Node n, ref int index, bool isRoot)
        {
            if (!isRoot && n.GetProperty<NetworkIdentityProperty>() != null) return;   // nested entity

            int nodeIndex = index++;
            foreach (var rep in NetReplicatorRegistry.CreateFor(n))
            {
                rep.Identity = this;
                rep.Node = n;
                rep.NodeIndex = nodeIndex;
                Replicators.Add(rep);
                rep.OnAttach();
            }
            foreach (var child in n.Children) CollectReplicators(child, ref index, false);
        }

        public NetReplicator? FindReplicator(int nodeIndex, byte wireId)
            => Replicators.FirstOrDefault(r => r.NodeIndex == nodeIndex && r.WireId == wireId);
        public T? GetReplicator<T>() where T : NetReplicator => Replicators.OfType<T>().FirstOrDefault();

        /// <summary>Runs after every property has started (never from OnStart: physics bodies don't exist yet then).</summary>
        internal void ApplyRole()
        {
            RolePending = false;
            if (Entity == null) return;

            bool proxy = IsProxy;
            SetProxyState(Entity, proxy, isRoot: true);
            foreach (var r in Replicators) r.OnRoleChanged(proxy);
        }

        private void SetProxyState(Node n, bool proxy, bool isRoot)
        {
            if (!isRoot && n.GetProperty<NetworkIdentityProperty>() != null) return;   // nested entity

            foreach (var p in n.Properties)
                if (!ReferenceEquals(p, this) && ProxyDisabled.Contains(p.GetType().Name))
                    p.Enabled = !proxy;
            foreach (var child in n.Children) SetProxyState(child, proxy, false);
        }
    }
}