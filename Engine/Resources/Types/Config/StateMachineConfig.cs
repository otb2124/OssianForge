using System;
using System.Linq;
using System.Text.Json;
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Resources;
using OssianForge.Engine.Utils.ConditionNode;

namespace OssianForge.Engine.Resources.Config
{
    public class StateMachineConfig : ConfigFile
    {
        public JsonDocument Document;

        public StateMachineConfig(string id, string path) : base(id, path) { }

        public override void Load()
        {
            //base.Load();
            string globalPath = CONTENT_FOLDER_PATH + "/" + Path;
            string raw = File.ReadAllText(globalPath);
            Document = JsonDocument.Parse(raw);
        }

        /// <summary>Builds a fresh StateMachineProperty instance from this config's JSON.</summary>
        public Nodes.Props.StateMachineProperty BuildProperty()
        {
            var root = Document.RootElement;
            var sm = new Nodes.Props.StateMachineProperty();

            foreach (var stateEl in root.GetProperty("states").EnumerateArray())
            {
                string id = stateEl.GetProperty("id").GetString()!;

                var onEnter = ParseActionList(stateEl, "onEnter");
                var onExit = ParseActionList(stateEl, "onExit");
                var onUpdate = ParseActionList(stateEl, "onUpdate");

                sm.AddState(new ActionState(id, onEnter, onExit, onUpdate));
            }

            foreach (var transEl in root.GetProperty("transitions").EnumerateArray())
            {
                string from = transEl.GetProperty("from").GetString()!;
                string to = transEl.GetProperty("to").GetString()!;
                var condition = ConditionNodeParser.Parse(transEl.GetProperty("condition"));

                sm.AddTransition(from, to, condition);
            }

            string initial = root.GetProperty("initial").GetString()!;
            sm.SetInitial(initial);

            return sm;
        }

        private static List<string> ParseActionList(JsonElement stateEl, string field)
        {
            if (!stateEl.TryGetProperty(field, out var arr))
                return new List<string>();

            return arr.EnumerateArray()
                .Select(el => el.GetString()!)
                .ToList();
        }
    }
}