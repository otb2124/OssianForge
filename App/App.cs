using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using OssianForge.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OssianForge.Engine.Ipc;

namespace OssianForge.App
{
    public class App
    {

        public App(string[] args)
        {
            ManagerLink.Configure(args);

            Engine.Engine.Create();
            Engine.Engine.Initialize();

            //logic loops bind
            Engine.Engine.Graphics.Window.Load += Engine.Engine.OnLoad;
            Engine.Engine.Graphics.Window.Update += Engine.Engine.OnUpdate;
            Engine.Engine.Graphics.Window.Render += Engine.Engine.OnRender;
            Engine.Engine.Graphics.Window.FocusChanged += Engine.Engine.OnFocusChanged;
            Engine.Engine.Graphics.Window.Resize += Engine.Engine.OnResize;
        }

        public void Run() => Engine.Engine.OnRun();
    }
}
