using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Resources
{
    public class Resource
    {
        public static readonly HashSet<string> Prefixes = new()
        {
            "mesh.", "shader.", "texture.", "cubemap.",
            "collider.", "font.", "sound.", "animation."
        };

        public string Id;
        public bool IsLoaded { get; private set; }

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
    }
}
