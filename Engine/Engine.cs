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
using OssianForge.Engine.Ipc;

namespace OssianForge.Engine
{
    public static class Engine
    {

        public static Graphics.Graphics Graphics;
        public static Resources.Resources Resources;
        public static Nodes.Nodes Nodes;
        public static Devices.Devices Devices;
        public static Inputs.Inputs Inputs;
        public static Physics.Physics Physics;
        public static Audio.Audio Audio;

        public static Utils.Console.DebugConsole DebugConsole;

        public static void Create()
        {
            Graphics = new Graphics.Graphics();
            Resources = new Resources.Resources();
            Nodes = new Nodes.Nodes();
            Devices = new Devices.Devices();
            Inputs = new Inputs.Inputs();
            Physics = new Physics.Physics();
            Audio = new Audio.Audio();

            DebugConsole = new Utils.Console.DebugConsole();
        }

        public static void Initialize()
        {
            Devices.Initialize();
            Graphics.Initialize();
            Resources.Initialize();
            Nodes.Initialize();
            Inputs.Initialize();
            Audio.Initialize();
        }

        public static void OnRun()
        {
            try
            {
                Graphics.OnRun();
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
            // First: needs the window, and must run before Graphics.OnLoad starts the head tracker's camera.
            Devices.OnLoad();
            Graphics.InitializeBatch();
            Resources.OnLoad();
            Devices.ApplyConfig(); // needs the loaded config; must precede Graphics.OnLoad (head tracker camera)
            Graphics.OnLoad();
            Nodes.OnLoad();
            Inputs.OnLoad();
            Physics.OnLoad();
            Resources.PostLoad();
            DebugConsole.Start();

            ManagerLink.StartIfRequested();
        }

        public static void OnUpdate(double delta)
        {
            Devices.OnUpdate(delta);
            Inputs.OnUpdate(delta);
            Nodes.OnUpdate(delta);
            Physics.OnUpdate(delta);
        }

        public static void OnRender(double delta)
        {
            Graphics.OnRender(delta);
        }

        public static void OnResize(Vector2D<int> size)
        {
            Graphics.OnResize(size);
        }

        public static void OnFocusChanged(bool focused)
        {
            Inputs.OnFocusChanged(focused);
        }
    }
}