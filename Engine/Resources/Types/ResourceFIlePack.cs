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
            string fullFolder = CONTENT_FOLDER_PATH + "/" + Path;

            if (!Directory.Exists(fullFolder))
                throw new DirectoryNotFoundException($"ResourceFilePack: folder not found: {fullFolder}");

            var filePaths = Directory.GetFiles(fullFolder, "*", SearchOption.AllDirectories);

            Files.Clear();

            foreach (var absolutePath in filePaths)
            {
                string relativePath = System.IO.Path.GetRelativePath(CONTENT_FOLDER_PATH, absolutePath)
                                                    .Replace('\\', '/');

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