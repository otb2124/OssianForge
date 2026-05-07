using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Physics
{
    public class Physics
    {

        public PhysicsWorld PhysicsWord;

        public Physics() 
        {
            PhysicsWord = new PhysicsWorld();
        }


        public void Initialize()
        {

        }

        public void OnLoad()
        {
            
        }


        public void OnUpdate(double delta)
        {
            PhysicsWord.Step((float)delta);
        }

        public void OnRender()
        {

        }
    }
}
