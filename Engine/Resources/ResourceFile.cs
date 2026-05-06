using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Resources
{

    public class ResourceFile
    {

        public static readonly string CONTENT_FOLDER_PATH = "Content";

        public string Id;
        public string Path;
        public string Raw;

        public ResourceFile()
        {
        }

        public virtual void Load()
        {
            Raw = File.ReadAllText(CONTENT_FOLDER_PATH + "/" + Path);
        }

        public string GetExtension()
        {
            return System.IO.Path.GetExtension(Path);
        }
    }
}
