using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Physics
{
    public class Physics
    {

        public PhysicsWorld PhysicsWorld;
        public CollisionSystem CollisionSystem;

        public Physics() 
        {
            PhysicsWorld = new PhysicsWorld();
            CollisionSystem = new CollisionSystem();
        }


        public void Initialize()
        {

        }

        public void OnLoad()
        {
            PhysicsWorld.Register(Engine.Nodes.NodeManager.GetNode("plane"));
            PhysicsWorld.Register(Engine.Nodes.NodeManager.GetNode("houseBall"));
            //PhysicsWorld.Register(Engine.Nodes.NodeManager.GetNode("house"));
        }


        public void OnUpdate(double delta)
        {
            PhysicsWorld.OnUpdate(delta);
            CollisionSystem.OnUpdate(delta);
        }
    }
}
