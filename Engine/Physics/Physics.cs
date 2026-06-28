using Jitter2;
using Jitter2.LinearMath;
using System.Numerics;


namespace OssianForge.Engine.Physics
{
    public class Physics
    {
        public List<PhysicsWorld> PhysicsWorlds = new();

        // Convenience accessors
        public PhysicsWorld World3D => PhysicsWorlds[0];
        public PhysicsWorld WorldScreen => PhysicsWorlds[1];

        public Physics()
        {
            PhysicsWorlds.Add(new PhysicsWorld(0, new Vector3(0, -9.81f, 0)));
            PhysicsWorlds.Add(new PhysicsWorld(1, new Vector3(0, -9.81f / 3f, 0)));
            //PhysicsWorlds[1].LockPosition = AxisLock.Z;
            //PhysicsWorlds[1].LockRotation = AxisLock.X | AxisLock.Y;
        }

        public void OnLoad()
        {
            //foreach (var world in PhysicsWorlds)
                //world.RegisterAll();
        }

        public void OnUpdate(double delta)
        {
            foreach (var world in PhysicsWorlds)
                world.OnUpdate(delta);
        }

        public PhysicsWorld GetWorld(int index) => PhysicsWorlds[index];
    }
}