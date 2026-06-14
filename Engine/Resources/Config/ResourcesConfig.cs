using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Resources.Config
{
    public class ResourcesConfig : ConfigFile
    {


        public ResourcesConfig(string id, string path) : base(id, path, ConfigFormat.Json)
        {

        }
    }
}
