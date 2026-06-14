using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OssianForge.Engine.Resources;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace OssianForge.Engine.Resources.Scripts
{
    public class ScriptFile : ResourceFile
    {

        public Type ScriptType { get; private set; }
        public Assembly CompiledAssembly { get; private set; }

        public ScriptFile(string id, string path)
        {
            Id = id;
            Path = path;
        }

        public ScriptFile() { }

        public override void Load()
        {
            string globalPath = CONTENT_FOLDER_PATH + "/" + Path;
            string source = File.ReadAllText(globalPath);

            // reference all assemblies the host app already has loaded
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .ToList();

            var compilation = CSharpCompilation.Create(
                assemblyName: System.IO.Path.GetFileNameWithoutExtension(Path),
                syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                var errors = string.Join("\n", result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));
                throw new Exception($"Script compile error in '{Path}':\n{errors}");
            }

            ms.Seek(0, SeekOrigin.Begin);
            CompiledAssembly = AssemblyLoadContext.Default.LoadFromStream(ms);

            var types = CompiledAssembly.GetExportedTypes();

            ScriptType = types.Length == 1
                ? types[0]
                : types.FirstOrDefault(t => t.Name == DeriveName(Id))
                  ?? throw new Exception($"Could not resolve type in script '{Id}'. " +
                                         $"Found: {string.Join(", ", types.Select(t => t.Name))}");
        }


        private static string DeriveName(string scriptFileId)
        {
            var filename = System.IO.Path.GetFileNameWithoutExtension(scriptFileId);
            return char.ToUpper(filename[0]) + filename.Substring(1);
        }

        public T CreateInstance<T>(string typeName, params object[] args) where T : class
        {
            var type = CompiledAssembly.GetExportedTypes()
                .FirstOrDefault(t => t.Name == typeName)
                ?? throw new Exception($"Type '{typeName}' not found in script '{Id}'");

            return Activator.CreateInstance(type, args) as T
                ?? throw new Exception($"Failed to create instance of '{typeName}'");
        }

        public object CreateInstance(string typeName, params object[] args)
        {
            var type = CompiledAssembly.GetExportedTypes()
                .FirstOrDefault(t => t.Name == typeName)
                ?? throw new Exception($"Type '{typeName}' not found in script '{Id}'");

            return Activator.CreateInstance(type, args)
                ?? throw new Exception($"Activator returned null for '{typeName}'");
        }
    }
}