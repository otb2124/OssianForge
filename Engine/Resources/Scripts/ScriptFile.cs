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
        public Assembly CompiledAssembly { get; private set; }

        public ScriptFile(string id, string path)
        {
            Id = id;
            Path = path;
        }

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
                assemblyName: Id,
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
        }
    }
}