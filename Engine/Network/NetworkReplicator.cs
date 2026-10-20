using System.Diagnostics;
using OssianForge.Content.Cooked;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Nodes.Props.Types.Camera;

namespace OssianForge.Engine.Network.Replication
{
    /// <summary>
    /// Delivery class a replicator asks for. Today both ride the single TCP link; when an unreliable
    /// transport (UDP) shows up, only NetworkReplication.Send changes - replicators stay as they are.
    /// </summary>
    public enum NetChannel { Reliable, Unreliable }

    public static class NetClock
    {
        public static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    }

    /// <summary>
    /// One replicated "aspect" of a node in the entity's subtree (transform, animation, physics, ...). NetworkIdentityProperty owns a
    /// list of these; the registry decides which ones a node gets from the properties it carries.
    ///
    /// Wire ids: 1-127 engine, 128-255 game / script packs. Never reuse a retired id.
    ///
    /// Contract:
    ///  - WriteState(full: false) runs on whoever owns the entity, at its send rate. Return false when there
    ///    is nothing new. It may update dirty tracking.
    ///  - WriteState(full: true) is a stateless snapshot (late join, spawn). It must NOT touch dirty tracking,
    ///    otherwise other peers would miss the change that the snapshot "consumed".
    ///  - ReadState / ApplyProxy run on everybody else. Unknown or malformed blocks are skipped by length prefix.
    /// </summary>
    public abstract class NetReplicator
    {
        public abstract byte WireId { get; }
        public virtual NetChannel Channel => NetChannel.Unreliable;

        public NetworkIdentityProperty Identity { get; internal set; } = null!;
        public Node Node { get; internal set; } = null!;

        /// <summary>Position of Node in the entity's subtree (depth-first). Same on every peer that loaded the same scene.</summary>
        public int NodeIndex { get; internal set; }

        /// <summary>Property references only. Other properties may not have started yet.</summary>
        public virtual void OnAttach() { }

        /// <summary>Called once all properties have started, and again whenever authority changes.</summary>
        public virtual void OnRoleChanged(bool isProxy) { }

        public abstract bool WriteState(BlobWriter w, bool full);
        public abstract void ReadState(BlobReader r);

        /// <summary>Proxy only, every frame before Nodes.OnUpdate: interpolate / apply what was received.</summary>
        public virtual void ApplyProxy(double delta) { }

        /// <summary>Reliable one-shot message from the owner's matching replicator (see SendEvent).</summary>
        public virtual void ReadEvent(BlobReader r) { }

        protected void SendEvent(Action<BlobWriter> write) => Identity.Replication?.SendEvent(Identity, this, write);
    }

    /// <summary>
    /// property type -> replicator factory. A node gets a replicator for every registered property it carries.
    /// The factory may return null to opt out for a particular node. Script packs can register their own.
    /// </summary>
    public static class NetReplicatorRegistry
    {
        private static readonly List<(Type Prop, Func<Node, NetReplicator?> Make)> Entries = new();

        static NetReplicatorRegistry()
        {
            // Screen-space (UI) and camera nodes are local-only views, not shared state.
            Register<TransformProperty>(n =>
                n.GetProperty<TransformProperty>()!.RenderSpace == RenderSpace.ScreenSpace
                || n.GetProperty<CameraProperty>() != null ? null : new TransformReplicator());
            Register<AnimationProperty>(n => new AnimationReplicator());
            Register<PhysicsProperty>(PhysicsReplicator.Create);
        }

        public static void Register<TProp>(Func<Node, NetReplicator?> make) where TProp : NodeProperty
            => Register(typeof(TProp), make);

        public static void Register(Type propertyType, Func<Node, NetReplicator?> make)
        {
            lock (Entries) Entries.Add((propertyType, make));
        }

        internal static List<NetReplicator> CreateFor(Node node)
        {
            var list = new List<NetReplicator>();
            lock (Entries)
            {
                foreach (var (prop, make) in Entries)
                {
                    if (node.GetProperty(prop) == null) continue;
                    var rep = make(node);
                    if (rep == null) continue;
                    if (list.Any(r => r.WireId == rep.WireId))
                        throw new InvalidOperationException($"Duplicate replicator wire id {rep.WireId}.");
                    list.Add(rep);
                }
            }
            return list;
        }
    }
}