using OssianForge.Engine.Resources.Scripts;
using System;

namespace OssianForge.Engine.Resources.Scripts
{
    public class ScriptResource : Resource
    {
        public Type ScriptType { get; private set; }
        private readonly string _scriptFileId;

        public ScriptResource(string id, string scriptFileId)
        {
            Id = id;
            _scriptFileId = scriptFileId;
        }

        public override void Load()
        {
            var scriptFile = Engine.Resources.GetResourceFile<ScriptFile>(_scriptFileId)
                ?? throw new Exception($"ScriptFile not found: '{_scriptFileId}'");

            var types = scriptFile.CompiledAssembly.GetExportedTypes();

            ScriptType = types.Length == 1
                ? types[0]
                : types.FirstOrDefault(t => t.Name == DeriveName(_scriptFileId))
                  ?? throw new Exception($"Could not resolve type in script '{_scriptFileId}'. " +
                                         $"Found: {string.Join(", ", types.Select(t => t.Name))}");
        }

        private static string DeriveName(string scriptFileId)
        {
            var part = scriptFileId.Split('.')[^1];
            return char.ToUpper(part[0]) + part.Substring(1);
        }

        public T CreateInstance<T>(string typeName, params object[] args) where T : class
        {
            var scriptFile = Engine.Resources.GetResourceFile<ScriptFile>(_scriptFileId)
                ?? throw new Exception($"ScriptFile not found: '{_scriptFileId}'");

            var type = scriptFile.CompiledAssembly.GetExportedTypes()
                .FirstOrDefault(t => t.Name == typeName)
                ?? throw new Exception($"Type '{typeName}' not found in script '{_scriptFileId}'");

            return Activator.CreateInstance(type, args) as T
                ?? throw new Exception($"Failed to create instance of '{typeName}'");
        }
    }
}