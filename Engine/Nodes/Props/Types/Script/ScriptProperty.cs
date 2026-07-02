using OssianForge.Engine.Resources.Scripts;
using System.Reflection;


namespace OssianForge.Engine.Nodes.Props
{
    public class ScriptProperty : NodeProperty
    {
        private readonly string _scriptFileId;
        private object _instance;
        private MethodInfo _onStart;
        private MethodInfo _onUpdate;
        private MethodInfo _onRender;

        public ScriptProperty(string scriptFileId)
        {
            _scriptFileId = scriptFileId;
        }

        public override void OnStart(Node node)
        {
            base.OnStart(node);
            // Lazy-init here so the resource system is guaranteed to be loaded
            if (_instance == null)
                Initialize();

            _onStart?.Invoke(_instance, new object[] { node });
        }

        public override void OnUpdate(Node node, double delta)
            => _onUpdate?.Invoke(_instance, new object[] { node, delta });

        public override void OnRender(Node node, double delta)
            => _onRender?.Invoke(_instance, new object[] { node, delta });

        private void Initialize()
        {
            var scriptFile = Engine.Resources.GetResource<ScriptFile>(_scriptFileId)
                ?? throw new Exception($"[SCRIPT] ScriptFile not found: '{_scriptFileId}'");

            _instance = Activator.CreateInstance(scriptFile.ScriptType)
                ?? throw new Exception($"[SCRIPT] Failed to instantiate '{scriptFile.ScriptType.Name}'");

            var type = scriptFile.ScriptType;
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var nodeType = new[] { typeof(Node) };
            var updateType = new[] { typeof(Node), typeof(double) };

            _onStart = type.GetMethod("OnStart", flags, null, nodeType, null);
            _onUpdate = type.GetMethod("OnUpdate", flags, null, updateType, null);
            _onRender = type.GetMethod("OnRender", flags, null, updateType, null);
        }

        public T GetInstance<T>() where T : class => _instance as T;
    }
}