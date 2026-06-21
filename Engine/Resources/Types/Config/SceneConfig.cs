using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Nodes.Props.Types.Scene;
using System.Numerics;
using System.Text.Json;
using static OssianForge.Engine.Utils.MathUtils;

namespace OssianForge.Engine.Resources.Config
{

    public class SceneConfig : ConfigFile
    {

        public JsonDocument Document;

        public SceneConfig(string id, string path) : base(id, path) { }


        //TODO: fix to use ConfigFile methods
        public override void Load()
        {
            base.Load();
            string globalPath = CONTENT_FOLDER_PATH + "/" + Path;
            string raw = File.ReadAllText(globalPath);
            Document = JsonDocument.Parse(raw);
        }

        public Node GetScene()
        {
            return ParseNode(Document.RootElement);
        }

        // ── node ─────────────────────────────────────────────────────────────────

        public static Node ParseNode(JsonElement el)
        {
            var node = new Node();

            if (el.TryGetProperty("id", out var id))
                node.Id = id.GetString();

            if (el.TryGetProperty("name", out var name))
                node.Name = name.GetString();

            if (el.TryGetProperty("properties", out var props))
                foreach (var prop in props.EnumerateArray())
                    node.AddProperty(ParseProperty(prop));

            if (el.TryGetProperty("children", out var children))
                foreach (var child in children.EnumerateArray())
                    node.AddChild(ParseNode(child));

            return node;
        }

        // ── property dispatch ─────────────────────────────────────────────────────

        private static NodeProperty ParseProperty(JsonElement el)
        {
            string type = el.GetProperty("type").GetString()
                ?? throw new Exception("[SCENE CONFIG] Property missing 'type'");

            var data = el.TryGetProperty("data", out var d) ? d : (JsonElement?)null;

            return type switch
            {
                "CameraProperty" => new CameraProperty(),
                "TransformProperty" => ParseTransformProperty(data),
                "MeshProperty" => new MeshProperty(Str(data, 0)),
                "CubemapMaterialProperty" => ParseCubemapMaterialProperty(data),
                "TextureMaterialProperty" => ParseTextureMaterialProperty(data),
                "TextMaterialProperty" => ParseTextMaterialProperty(data),
                "PointEmissionProperty" => ParsePointEmissionProperty(data),
                "SunEmissionProperty" => ParseSunEmissionProperty(data),
                "SpotEmissionProperty" => ParseSpotEmissionProperty(data),
                "EmissionProperty" => ParsePointEmissionProperty(data),
                "ColliderProperty" => new ColliderProperty(Str(data, 0)),
                "PhysicsProperty" => ParsePhysicsProperty(el, data),
                "AnimationProperty" => ParseAnimationProperty(el, data),
                "ControlProperty" => ParseControlProperty(data),
                "ActionProperty" => ParseActionProperty(data),
                "SoundProperty" => ParseSoundProperty(data),
                "ScriptProperty" => new ScriptProperty(Str(data, 0)),
                "GroupProperty" => new GroupProperty(Str(data, 0)),
                "StateMachineProperty" => ParseStateMachineProperty(data),
                "SceneReferenceProperty" => new SceneReferenceProperty(Str(data, 0)),
                _ => throw new Exception($"[SCENE CONFIG] Unknown property type '{type}'")
            };
        }

        // ── property parsers ──────────────────────────────────────────────────────

        private static TransformProperty ParseTransformProperty(JsonElement? data)
        {
            if (data == null) return new TransformProperty();

            var arr = data.Value;

            var vec3arr = arr[0];
            var position = ParseVector3(vec3arr[0].GetString());
            var rotation = ParseVector3(vec3arr[1].GetString());
            var scale = ParseVector3(vec3arr[2].GetString());
            var transform = new Transform(position, rotation, scale);

            int len = arr.GetArrayLength();

            RenderSpace space = RenderSpace.World;
            Anchor anchor = Anchor.None;

            if (len > 1) space = Enum.Parse<RenderSpace>(arr[1].GetString()!, true);
            if (len > 2) anchor = Enum.Parse<Anchor>(arr[2].GetString()!, true);

            return new TransformProperty(transform, space, anchor);
        }

        // "data": [ "texture.id", "shader.id" ]
        // "data": [ "texture.id", "shader.id", ["DisableDepthTest", "AdditiveBlend"] ]
        private static TextureMaterialProperty ParseTextureMaterialProperty(JsonElement? data)
        {
            var arr = data!.Value;
            string tex = arr[0].GetString();
            string shad = arr[1].GetString();
            var actions = ParseRenderActions(arr, 2);
            return new TextureMaterialProperty(tex, shad, actions);
        }

        // "data": [ "cubemap.id", "shader.id" ]
        // "data": [ "cubemap.id", "shader.id", ["DisableDepthTest"] ]
        private static CubemapMaterialProperty ParseCubemapMaterialProperty(JsonElement? data)
        {
            var arr = data!.Value;
            string cube = arr[0].GetString();
            string shad = arr[1].GetString();
            var actions = ParseRenderActions(arr, 2);
            return new CubemapMaterialProperty(cube, shad, actions);
        }

        // "data": [ "text content", 32, "1,1,1,1", "font.id", "shader.id" ]
        // "data": [ "text content", 32, "1,1,1,1", "font.id", "shader.id", ["DisableDepthTest"] ]
        private static TextMaterialProperty ParseTextMaterialProperty(JsonElement? data)
        {
            var arr = data!.Value;
            string text = arr[0].GetString();
            int size = arr[1].GetInt32();
            var color = ParseVector4(arr[2].GetString());
            string font = arr[3].GetString();
            string shad = arr[4].GetString();
            var actions = ParseRenderActions(arr, 5);
            return new TextMaterialProperty(text, size, color, font, shad, actions);
        }

        private static PointEmissionProperty ParsePointEmissionProperty(JsonElement? data)
        {
            var arr = data!.Value;
            var color = ParseVector3(arr[0].GetString());
            float intensity = arr.GetArrayLength() > 1 ? arr[1].GetSingle() : 1f;
            float radius = arr.GetArrayLength() > 2 ? arr[2].GetSingle() : 10f;
            return new PointEmissionProperty(color, intensity, radius);
        }

        private static SunEmissionProperty ParseSunEmissionProperty(JsonElement? data)
        {
            var arr = data!.Value;
            var direction = ParseVector3(arr[0].GetString());
            var color = ParseVector3(arr[1].GetString());
            float intensity = arr.GetArrayLength() > 2 ? arr[2].GetSingle() : 1f;
            return new SunEmissionProperty(direction, color, intensity);
        }

        private static SpotEmissionProperty ParseSpotEmissionProperty(JsonElement? data)
        {
            var arr = data!.Value;
            var direction = ParseVector3(arr[0].GetString());
            var color = ParseVector3(arr[1].GetString());
            float intensity = arr.GetArrayLength() > 2 ? arr[2].GetSingle() : 1f;
            float radius = arr.GetArrayLength() > 3 ? arr[3].GetSingle() : 15f;
            float inner = arr.GetArrayLength() > 4 ? arr[4].GetSingle() : 12.5f;
            float outer = arr.GetArrayLength() > 5 ? arr[5].GetSingle() : 17.5f;
            return new SpotEmissionProperty(direction, color, intensity, radius, inner, outer);
        }

        private static PhysicsProperty ParsePhysicsProperty(JsonElement el, JsonElement? data)
        {
            var arr = data!.Value;
            bool isStatic = arr[0].GetBoolean();
            bool isDynamic = arr[1].GetBoolean();

            PhysicsProperty prop = arr.GetArrayLength() > 2
                ? new PhysicsProperty(isStatic, isDynamic, arr[2].GetSingle(), arr[3].GetSingle())
                : new PhysicsProperty(isStatic, isDynamic);

            if (el.TryGetProperty("world", out var world))
                prop.SetWorld(world.GetInt32());

            return prop;
        }

        private static AnimationProperty ParseAnimationProperty(JsonElement el, JsonElement? data)
        {
            var prop = new AnimationProperty(Str(data, 0));

            if (el.TryGetProperty("play", out var play))
            {
                string clip = play.GetProperty("clip").GetString();
                bool loop = play.GetProperty("loop").GetBoolean();
                float speed = play.GetProperty("speed").GetSingle();
                prop.Play(clip, loop, speed);
            }

            return prop;
        }

        private static ControlProperty ParseControlProperty(JsonElement? data)
        {
            bool isInteractable = true;
            bool isDraggable = false;
            bool isDropTarget = false;
            string dragGroupId = null;
            Dictionary<string, List<string>> actionMap = null;

            if (data != null)
            {
                var arr = data.Value;
                int len = arr.GetArrayLength();

                if (len > 0) isInteractable = arr[0].GetBoolean();
                if (len > 1) isDraggable = arr[1].GetBoolean();
                if (len > 2) isDropTarget = arr[2].GetBoolean();
                if (len > 3 && arr[3].ValueKind == JsonValueKind.String)
                    dragGroupId = arr[3].GetString();

                if (len > 4 && arr[4].ValueKind == JsonValueKind.Object)
                {
                    actionMap = new();
                    foreach (var entry in arr[4].EnumerateObject())
                    {
                        var ids = new List<string>();
                        foreach (var id in entry.Value.EnumerateArray())
                            ids.Add(id.GetString()!);
                        actionMap[entry.Name] = ids;
                    }
                }
            }

            return new ControlProperty(isInteractable, isDraggable, isDropTarget, dragGroupId, actionMap);
        }


        private static ActionProperty ParseActionProperty(JsonElement? data)
        {
            Dictionary<string, List<string>> actionMap = null;

            if (data != null)
            {
                var arr = data.Value;
                if (arr.GetArrayLength() > 0 && arr[0].ValueKind == JsonValueKind.Object)
                    actionMap = ParseActionMap(arr[0]);
            }

            return new ActionProperty(actionMap);
        }

        private static Dictionary<string, List<string>> ParseActionMap(JsonElement obj)
        {
            var actionMap = new Dictionary<string, List<string>>();
            foreach (var entry in obj.EnumerateObject())
            {
                var ids = new List<string>();
                foreach (var id in entry.Value.EnumerateArray())
                    ids.Add(id.GetString()!);
                actionMap[entry.Name] = ids;
            }
            return actionMap;
        }

        private static SoundProperty ParseSoundProperty(JsonElement? data)
        {
            var arr = data!.Value;
            string resId = arr[0].GetString();
            bool autoPlay = arr.GetArrayLength() > 1 && arr[1].GetBoolean();
            return new SoundProperty(resId) { AutoPlay = autoPlay };
        }


        private static StateMachineProperty ParseStateMachineProperty(JsonElement? data)
        {
            string smConfigId = Str(data, 0); // e.g. "configfile.statemachine.player"
            var config = Engine.Resources.GetResourceFile<StateMachineConfig>(smConfigId);
            return config.BuildProperty();
        }

        // ── RenderAction helper ───────────────────────────────────────────────────

        /// <summary>
        /// Reads an optional trailing JSON array of RenderAction name strings at
        /// position <paramref name="index"/> in <paramref name="arr"/>.
        ///
        /// JSON examples:
        ///   (absent)                    → RenderAction[0]  (no actions)
        ///   ["DisableDepthTest"]        → RenderAction[1]
        ///   ["DisableDepthTest","AdditiveBlend"] → RenderAction[2]
        /// </summary>
        private static RenderAction[] ParseRenderActions(JsonElement arr, int index)
        {
            if (arr.GetArrayLength() <= index)
                return Array.Empty<RenderAction>();

            var el = arr[index];
            if (el.ValueKind != JsonValueKind.Array)
                return Array.Empty<RenderAction>();

            var actions = new List<RenderAction>();
            foreach (var item in el.EnumerateArray())
            {
                string name = item.GetString()
                    ?? throw new Exception("[SCENE CONFIG] RenderAction entry must be a string");

                if (!Enum.TryParse<RenderAction>(name, ignoreCase: true, out var action))
                    throw new Exception($"[SCENE CONFIG] Unknown RenderAction '{name}'");

                actions.Add(action);
            }
            return actions.ToArray();
        }

        // ── vector helpers ────────────────────────────────────────────────────────

        private static string Str(JsonElement? data, int index)
            => data!.Value[index].GetString()
               ?? throw new Exception($"[SCENE CONFIG] Expected string at data[{index}]");

        private static Vector3 ParseVector3(string s)
        {
            var parts = s.Split(',');
            return new Vector3(
                float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
        }

        private static Vector4 ParseVector4(string s)
        {
            var parts = s.Split(',');
            return new Vector4(
                float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}