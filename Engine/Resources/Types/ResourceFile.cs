using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Resources
{

    public class ResourceFile
    {
        public static readonly HashSet<string> Prefixes = new()
        {
            "shaderfile.", "meshfile.", "animationfile.", "texturefile.",
            "configfile.", "filepack.", "soundfile.", "script."
        };

        public static readonly string CONTENT_FOLDER_PATH = "Content";

        public string Id;
        public string Path;
        public bool IsLoaded { get; private set; }

        public ResourceFile()
        {
        }

        public virtual void Load()
        {
            if (IsLoaded) return;
            IsLoaded = true;
        }

        public virtual void Unload()
        {
            IsLoaded = false;
        }

        public virtual void Reload()
        {
            Unload();
            Load();
        }

        public string GetExtension()
        {
            return System.IO.Path.GetExtension(Path);
        }
    }
}
