namespace OssianForge.Engine.Nodes.Types
{
    public class Node
    {
        public string Id;
        public string Name;
        public Node Parent;
        public List<Node> Children = new();
        public List<Props.NodeProperty> Properties = new();

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

        public void AddProperty(Props.NodeProperty prop)
        {
            Properties.Add(prop);
        }

        public T GetProperty<T>() where T : Props.NodeProperty
            => Properties.OfType<T>().FirstOrDefault();

        public List<T> GetProperties<T>() where T : Props.NodeProperty
            => Properties.OfType<T>().ToList();

        public T GetChild<T>() where T : Node
            => Children.OfType<T>().FirstOrDefault();

        public List<T> GetChildren<T>() where T : Node
            => Children.OfType<T>().ToList();

        public virtual void OnUpdate()
        {
            foreach (var child in Children)
                child.OnUpdate();
        }

        public virtual void OnRender()
        {
            foreach (var child in Children)
                child.OnRender();
        }
    }
}