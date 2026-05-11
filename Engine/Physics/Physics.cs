using Jitter2;

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

        public void OnLoad()
        {
            PhysicsWorld.RegisterAll();
        }

        public void OnUpdate(double delta)
        {
            // Jitter steps first — it integrates, detects, and resolves all at once
            PhysicsWorld.OnUpdate(delta);
            // Trigger-only check on top
            CollisionSystem.OnUpdate(delta);
        }
    }
}