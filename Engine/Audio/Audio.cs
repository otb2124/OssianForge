using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenAL;

namespace OssianForge.Engine.Audio
{

    public class Audio
    {


        public AudioSystem AudioSystem;


        public Audio()
        {
            AudioSystem = new AudioSystem();
        }

        public void Initialize()
        {
            AudioSystem.Initialize();
        }

        public void OnUpdate()
        {
            
        }
    }


    
}
