using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Resources.Config
{
    public class ScriptPacksConfig : ResourceFile
    {


        public ScriptPacksConfig(string id, string path)
        {
            Id = id;
            Path = path;
        }


        public override void Load()
        {
            base.Load();
        }
    }
}
