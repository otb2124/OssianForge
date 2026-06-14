using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Content.ScriptFiles.Packs.StateMachine
{
    public class State
    {

        public string Name;
        public static string StaticString = "static string";

        public State() 
        {
            Name = "state name";
        }


        public static string GetDefaultName()
        {
            return "new default name";
        }
    }
}
