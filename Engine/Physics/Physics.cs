using Jitter2;
using Jitter2.LinearMath;


namespace OssianForge.Engine.Physics
{
    public class Physics
    {
        public List<PhysicsWorld> PhysicsWorlds = new();
        public CollisionSystem CollisionSystem;

        // Convenience accessors
        public PhysicsWorld World3D => PhysicsWorlds[0];
        public PhysicsWorld WorldScreen => PhysicsWorlds[1];

        public Physics()
        {
            // World 0: 3D world — normal gravity
            var world3D = new PhysicsWorld(0);
            world3D.JitterWorld.Gravity = new JVector(0, -9.81f, 0);
            PhysicsWorlds.Add(world3D);

            // World 1: screen space — gravity pulls down in 2D (Y only, no Z)
            var worldScreen = new PhysicsWorld(1);
            worldScreen.JitterWorld.Gravity = new JVector(0, 500, 0);
            PhysicsWorlds.Add(worldScreen);

            CollisionSystem = new CollisionSystem();
        }

        public void OnLoad()
        {
            foreach (var world in PhysicsWorlds)
                world.RegisterAll();
        }

        public void OnUpdate(double delta)
        {
            foreach (var world in PhysicsWorlds)
                world.OnUpdate(delta);

            CollisionSystem.OnUpdate(delta);
        }

        public PhysicsWorld GetWorld(int index) => PhysicsWorlds[index];
    }
}