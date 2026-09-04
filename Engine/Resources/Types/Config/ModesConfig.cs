using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OssianForge.Engine.Resources.Config
{
    public class ModeActions
    {
        public List<string> OnEnter { get; set; } = new();
        public List<string> OnUpdate { get; set; } = new();
        public List<string> OnExit { get; set; } = new();
    }

    public class ModeDefinition
    {
        public string Id { get; set; } = string.Empty;
        public ModeActions Actions { get; set; } = new();
    }

    public class ModesConfig : ConfigFile
    {
        public JsonDocument Document { get; private set; }

        public string CurrentMode => GetString("currentMode", "");

        public ModesConfig(string id, string path) : base(id, path) { }

        public override void Load()
        {
            base.Load();
            string globalPath = CONTENT_FOLDER_PATH + "/" + Path;
            string raw = File.ReadAllText(globalPath);
            Document = JsonDocument.Parse(raw);

            ApplyMode(CurrentMode);
        }

        public List<ModeDefinition> GetModes()
        {
            var modes = new List<ModeDefinition>();

            if (!Document.RootElement.TryGetProperty("modes", out var modesEl) || modesEl.ValueKind != JsonValueKind.Array)
                return modes;

            foreach (var element in modesEl.EnumerateArray())
            {
                var mode = new ModeDefinition
                {
                    Id = element.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : ""
                };

                if (element.TryGetProperty("actions", out var actionsEl) && actionsEl.ValueKind == JsonValueKind.Object)
                {
                    mode.Actions.OnEnter = ParseActionList(actionsEl, "OnEnter");
                    mode.Actions.OnUpdate = ParseActionList(actionsEl, "OnUpdate");
                    mode.Actions.OnExit = ParseActionList(actionsEl, "OnExit");
                }

                modes.Add(mode);
            }

            return modes;
        }

        private static List<string> ParseActionList(JsonElement parent, string propertyName)
        {
            var result = new List<string>();
            if (parent.TryGetProperty(propertyName, out var arrayEl) && arrayEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arrayEl.EnumerateArray())
                {
                    if (item.GetString() is string actionStr)
                        result.Add(actionStr);
                }
            }
            return result;
        }

        public void SetCurrentMode(string modeId)
        {
            Set("currentMode", modeId);
            //Save();
        }

        /// <summary>
        /// Transitions from the current active mode (executing its OnExit actions) 
        /// to targetModeId (executing its OnEnter actions).
        /// </summary>
        public void ApplyMode(string targetModeId)
        {
            string previousModeId = CurrentMode;
            var modes = GetModes();

            // 1. Run OnExit actions of current mode
            if (!string.IsNullOrEmpty(previousModeId))
            {
                var currentMode = modes.Find(m => string.Equals(m.Id, previousModeId, StringComparison.OrdinalIgnoreCase));
                if (currentMode != null)
                {
                    foreach (var actionId in currentMode.Actions.OnExit)
                    {
                        Engine.Resources.InvokeAction(actionId);
                    }
                }
            }

            // 2. Locate target mode
            var targetMode = modes.Find(m => string.Equals(m.Id, targetModeId, StringComparison.OrdinalIgnoreCase));
            if (targetMode == null)
            {
                throw new Exception($"[MODES CONFIG] Mode '{targetModeId}' was not found in '{Id}'.");
            }

            // 3. Run OnEnter actions of target mode
            foreach (var actionId in targetMode.Actions.OnEnter)
            {
                Engine.Resources.InvokeAction(actionId);
            }

            // 4. Save state
            SetCurrentMode(targetMode.Id);
        }

        /// <summary>
        /// Call this once per frame/tick to execute the active mode's OnUpdate actions.
        /// </summary>
        /// 
        /*
        public void UpdateCurrentMode(double? delta = null)
        {
            string currentId = CurrentMode;
            if (string.IsNullOrEmpty(currentId)) return;

            var modes = GetModes();
            var currentMode = modes.Find(m => string.Equals(m.Id, currentId, StringComparison.OrdinalIgnoreCase));

            if (currentMode != null)
            {
                foreach (var actionId in currentMode.Actions.OnUpdate)
                {
                    Engine.Resources.InvokeAction(actionId, delta: delta);
                }
            }
        }
        */
    }
}