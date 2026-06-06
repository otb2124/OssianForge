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
        public static Physics.Physics Physics;
        public static UI.UI UI;

        public static Utils.Console.DebugConsole DebugConsole;

        public static void Create()
        {
            Graphics = new Graphics.Graphics();
            Resources = new Resources.Resources();
            Nodes = new Nodes.Nodes();
            Inputs = new Inputs.Inputs();
            Physics = new Physics.Physics();
            UI = new UI.UI();

            DebugConsole = new Utils.Console.DebugConsole();
        }

        public static void Initialize()
        {
            Graphics.Initialize();
            Resources.Initialize();
            Nodes.Initialize();
            Inputs.Initialize();
            UI.Initialize();
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
            Graphics.InitializeBatch();
            Resources.OnLoad();
            Graphics.OnLoad();
            Nodes.OnLoad();
            Inputs.OnLoad();
            Physics.OnLoad();
            UI.OnLoad();
            DebugConsole.Start();
        }

        public static void OnUpdate(double delta)
        {
            Nodes.OnUpdate(delta);
            Inputs.OnUpdate(delta);
            Physics.OnUpdate(delta);
            UI.OnUpdate(delta);
        }

        public static void OnRender(double delta)
        {
            Graphics.OnRender(delta);
            UI.OnRender(delta);
        }

        public static void OnResize(Vector2D<int> size)
        {
            Graphics.OnResize(size);
        }
    }
}
