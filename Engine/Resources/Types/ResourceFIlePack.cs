using OssianForge.Engine.Ipc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OssianForge.Engine.Resources
{
    public class ResourceFilePack<T> : Resource where T : Resource, new()
    {
        public List<T> Files { get; private set; } = new();
        public string Path;

        public ResourceFilePack(string id, string path)
        {
            Id = id;
            Path = path;
        }

        public override void Load()
        {
            var filePaths = CookerLink.Source.List(Path).ToList();

            Files.Clear();

            foreach (var relativePath in filePaths)
            {

                var file = new T
                {
                    Id = this.Id,
                    //Path = relativePath
                };

                file.Load();
                Files.Add(file);
                //Console.WriteLine($"[ResourceFilePack] loaded file with id {file.Id}, path {file.Path}");
            }
        }

        public T GetById(string id) => Files.FirstOrDefault(f => f.Id == id);

        public Resource GetByIdBase(string id) => GetById(id);
        public IEnumerable<Resource> GetAllFiles() => Files;
    }
}