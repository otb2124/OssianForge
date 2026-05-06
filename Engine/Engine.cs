using Silk.NET.Maths;
using Silk.NET.Windowing;
using OssianForge.Engine.Graphics;
using OssianForge.Engine.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine
{
    public static class Engine
    {

        public static Graphics.Graphics Graphics;
        public static Resources.Resources Resources;
        public static Nodes.Nodes Nodes;
        public static Inputs.Inputs Inputs;

        public static void Create()
        {
            Graphics = new Graphics.Graphics();
            Resources = new Resources.Resources();
            Nodes = new Nodes.Nodes();
            Inputs = new Inputs.Inputs();
        }

        public static void Initialize()
        {
            Graphics.Initialize();
            Resources.Initialize();
            Nodes.Initialize();
            Inputs.Initialize();
        }

        public static void OnRun()
        {
            try
            {
                Graphics.Window.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine("=== CRASH ===");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.ReadLine();
            }
        }

        public static void OnLoad()
        {
            Graphics.InitializeOpenGL();
            Resources.OnLoad();
            Graphics.OnLoad();
            Nodes.OnLoad();
            Inputs.OnLoad();
        }

        public static void OnUpdate(double delta)
        {
            Nodes.OnUpdate(delta);
            Inputs.OnUpdate(delta);
            Graphics.OnUpdate(delta);
        }

        public static void OnRender(double delta)
        {
            Graphics.OnRender(delta);
        }

        public static void OnResize(Vector2D<int> size)
        {
            Graphics.OnResize(size);
        }
    }
}
