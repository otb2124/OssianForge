using System;
using System.Linq;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Resources.Config;

namespace OssianForge.Engine.Utils.Console
{
    public class SceneCommand : ICommand
    {
        public string Name => "scene";
        public string Description => "scene | scene list | scene load id:<sceneId>  (or: scene <sceneId>)";

        public void Execute(CommandContext ctx)
        {
            var scenes = Engine.Resources.GetResources<SceneConfig>();

            // The parser lower-cases bare words, so "scene load id:Some.Id" keeps its case while
            // "scene some.id" does not. Both are matched case-insensitively below.
            var bare = ctx.Args.Where(kv => kv.Value == "").Select(kv => kv.Key).ToList();
            var sub = bare.FirstOrDefault(k => k is "list" or "load");

            if (sub == "list")
            {
                foreach (var s in scenes)
                {
                    bool current = s.Id == Engine.Nodes.CurrentSceneId;
                    DebugConsole.Write($"{(current ? "*" : " ")} {s.Id}", current ? ConsoleColor.Green : ConsoleColor.White);
                }
                return;
            }

            var wanted = ctx.Get("id") ?? bare.FirstOrDefault(k => k != "load");
            if (string.IsNullOrEmpty(wanted))
            {
                DebugConsole.Write($"Current scene: {Engine.Nodes.CurrentSceneId}", ConsoleColor.Cyan);
                return;
            }

            var target = scenes.FirstOrDefault(s => s.Id.Equals(wanted, StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                DebugConsole.Write($"Unknown scene '{wanted}'. Try 'scene list'.", ConsoleColor.Red);
                return;
            }

            // Console input runs on its own thread; the swap has to happen on the update thread
            // (GL resource loading, tree mutation). An exception inside the queued action would
            // escape DebugConsole.Execute's try/catch, so it is handled here.
            NodeManager.Enqueue(() =>
            {
                try
                {
                    Engine.Nodes.ChangeScene(target.Id);
                    DebugConsole.Write($"Scene is now '{target.Id}'", ConsoleColor.Green);
                }
                catch (Exception ex)
                {
                    DebugConsole.Write($"Scene change failed: {ex.Message}", ConsoleColor.Red);
                }
            });
        }
    }
}