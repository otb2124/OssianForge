using OssianForge.Engine.Nodes.Props;

namespace OssianForge.Engine.Nodes
{
    public class Node
    {
        public string Id;
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
        }

        public void AddChild(Node child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        public void RemoveChild(Node child)
        {
            child.Parent = null;
            Children.Remove(child);
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







        public virtual void OnStart() 
        {
            if (!StartEnabled) return;
            foreach (var property in Properties)
            {
                property.OnStart(this);
            }
        }




        public virtual void OnUpdate(double delta)
        {
            if (!UpdateEnabled) return;
            foreach (var property in Properties)
            {
                property.OnUpdate(this, delta);
            }
        }





        public virtual void OnRender(double delta)
        {
            if (!RenderEnabled) return;
            foreach (var property in Properties)
            {
                property.OnRender(this, delta);
            }
        }
    }
}