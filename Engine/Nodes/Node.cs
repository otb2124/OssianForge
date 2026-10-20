using OssianForge.Engine.Nodes.Props;

namespace OssianForge.Engine.Nodes
{
    public class Node
    {
        private string _id;
        public string Id
        {
            get => _id;
            set
            {
                Engine.Nodes.NodeManager?.RekeyNode(_id, value, this);
                _id = value;
            }
        }

        /// <summary>Id as authored in the scene file. Unique only inside its scene instance. Id is the global runtime key.</summary>
        public string LocalId;

        /// <summary>True on the root of an instantiated scene (scene ref or spawned prefab). Local lookups stop here.</summary>
        public bool IsInstanceRoot;

        /// <summary>Authored id of the scene-ref node that created this instance, as the parent scene sees it.</summary>
        public string InstanceName;

        public string Name;
        public Node Parent;
        public List<Node> Children = new();
        public List<NodeProperty> Properties = new();

        // ── lifecycle toggles ────────────────────────────────────────────────
        public bool StartEnabled = true;
        public bool UpdateEnabled = true;
        public bool RenderEnabled = true;

        public bool Enabled
        {
            set => StartEnabled = UpdateEnabled = RenderEnabled = value;
            get => StartEnabled && UpdateEnabled && RenderEnabled;
        }

        public bool Writable = true;

        public bool HasLocalName(string name) => LocalId == name || InstanceName == name;
        public void AddChild(Node child)
        {
            Engine.Nodes.NodeManager.AddChild(this, child);
        }

        public void RemoveChild(Node child)
        {
            Engine.Nodes.NodeManager.RemoveChild(this, child);
        }

        public void AddProperty(NodeProperty prop)
        {
            Properties.Add(prop);
        }

        public T GetProperty<T>() where T : NodeProperty
            => Properties.OfType<T>().FirstOrDefault();

        public void SetProperty<T>(T prop) where T : NodeProperty
        {
            var existing = Properties.FindIndex(p => p is T);
            if (existing >= 0)
                Properties[existing] = prop;
            else
                Properties.Add(prop);
        }

        public void SetProperty(NodeProperty prop)
        {
            int i = Properties.FindIndex(p => p.GetType() == prop.GetType());
            if (i >= 0) Properties[i] = prop; else Properties.Add(prop);
        }

        public NodeProperty GetProperty(Type type)
        {
            if (type == null) return null;

            return Properties.FirstOrDefault(p =>
                p.GetType() == type || type.IsAssignableFrom(p.GetType()));
        }

        public List<T> GetProperties<T>() where T : NodeProperty
            => Properties.OfType<T>().ToList();

        public T GetChild<T>() where T : Node
            => Children.OfType<T>().FirstOrDefault();

        public List<T> GetChildren<T>() where T : Node
            => Children.OfType<T>().ToList();


        public Node GetInstanceRoot()
        {
            var n = this;
            while (n.Parent != null && !n.IsInstanceRoot) n = n.Parent;
            return n;
        }

        /// <summary>Finds a node by authored id inside this node's own scene instance.
        /// Nested instances are opaque: their root is findable, their insides are not.</summary>
        public Node FindLocal(string localId)
            => string.IsNullOrEmpty(localId) ? null : SearchLocal(GetInstanceRoot(), localId);

        private static Node SearchLocal(Node n, string localId)
        {
            foreach (var c in n.Children)
            {
                if (c.HasLocalName(localId)) return c;
                var found = SearchLocal(c, localId);
                if (found != null) return found;
            }
            return null;
        }




        public virtual void OnStart() 
        {
            if (!StartEnabled) return;
            foreach (var property in Properties)
            {
                if (!property.Enabled) continue;
                property.OnStart(this);
            }
        }




        public virtual void OnUpdate(double delta)
        {
            if (!UpdateEnabled) return;
            foreach (var property in Properties)
            {
                if (!property.Enabled) continue;
                property.OnUpdate(this, delta);
            }
        }





        public virtual void OnRender(double delta)
        {
            if (!RenderEnabled) return;
            foreach (var property in Properties)
            {
                if (!property.Enabled) continue;
                property.OnRender(this, delta);
            }
        }
    }
}