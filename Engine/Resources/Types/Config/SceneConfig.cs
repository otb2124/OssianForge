using OssianForge.Content;
using OssianForge.Engine.Ipc;
using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Nodes.Props.Types.Camera;
using OssianForge.Engine.Nodes.Props.Types.Physics;
using OssianForge.Engine.Nodes.Props.Types.Scene;
using System.Globalization;
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
            string raw = CookerLink.Source.ReadAllText(Path);
            Document = JsonDocument.Parse(raw);
        }

        /// <summary>Authored id of the scene's root node, or null.</summary>
        public string? RootLocalId
            => Document != null && Document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;

        public Node GetScene() => Instantiate(default);

        /// <summary>Builds a fresh node tree from this scene. Ids are namespaced under <paramref name="scope"/>.</summary>
        public Node Instantiate(InstanceScope scope = default)
            => SceneBuilder.Build(Document.RootElement, scope);

        // ── property dispatch ─────────────────────────────────────────────────────

        internal static NodeProperty ParseProperty(JsonElement el)
        {
            string type = el.GetProperty("type").GetString()
                ?? throw new Exception("[SCENE CONFIG] Property missing 'type'");

            var data = el.TryGetProperty("data", out var d) ? d : (JsonElement?)null;

            // Registered factories first, then the legacy switch.
            if (PropertyFactory.TryCreate(type, data, out var made))
                return made!;

            return type switch
            {
                "CameraProperty" => new CameraProperty(),
                "MeshProperty" => new MeshProperty(Str(data, 0)),
                "CubemapMaterialProperty" => ParseCubemapMaterialProperty(data),
                "TextureMaterialProperty" => ParseTextureMaterialProperty(data),
                "TextMaterialProperty" => ParseTextMaterialProperty(data),
                "ColorMaterialProperty" => ParseColorMaterialProperty(data),
                "PointEmissionProperty" => ParsePointEmissionProperty(data),
                "SunEmissionProperty" => ParseSunEmissionProperty(data),
                "SpotEmissionProperty" => ParseSpotEmissionProperty(data),
                "EmissionProperty" => ParsePointEmissionProperty(data),
                "ColliderProperty" => ParseColliderProperty(data),
                "RigidPhysicsProperty" => ParseRigidPhysicsProperty(el, data),
                "StaticPhysicsProperty" => ParseStaticPhysicsProperty(el, data),
                "AnimationProperty" => ParseAnimationProperty(el, data),
                "ControlProperty" => ParseControlProperty(data),
                "ActionProperty" => ParseActionProperty(data),
                "SoundProperty" => ParseSoundProperty(data),
                "ScriptProperty" => new ScriptProperty(Str(data, 0)),
                "GroupProperty" => new GroupProperty(Str(data, 0)),
                "StateMachineProperty" => ParseStateMachineProperty(data),
                "OrbitalCameraProperty" => ParseOrbitalCameraProperty(data),
                _ => throw new Exception($"[SCENE CONFIG] Unknown property type '{type}'")
            };
        }

        // ── property parsers ──────────────────────────────────────────────────────

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

        private static ColorMaterialProperty ParseColorMaterialProperty(JsonElement? data)
        {
            var arr = data!.Value;
            string rawColor = arr[0].GetString()!;
            string shaderId = arr[1].GetString()!;
            var actions = ParseRenderActions(arr, 2);

            Vector4 colorVector = ParseColorVector(rawColor);

            return new ColorMaterialProperty(colorVector, shaderId, actions);
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

        private static RigidPhysicsProperty ParseRigidPhysicsProperty(JsonElement el, JsonElement? data)
        {
            var arr = data!.Value;
            int len = arr.GetArrayLength();

            int world = arr[0].GetInt32();
            float mass = len > 1 ? arr[1].GetSingle() : 1f;
            float bounciness = len > 2 ? arr[2].GetSingle() : 0f;
            float friction = len > 3 ? arr[3].GetSingle() : 0.6f;
            float linearDamping = len > 4 ? arr[4].GetSingle() : 0.02f;
            float angularDamping = len > 5 ? arr[5].GetSingle() : 0.05f;
            PhysicsLock pLock = PhysicsLock.None;
            if (len > 6 && arr[6].ValueKind == JsonValueKind.Array)
            {
                foreach (var flag in arr[6].EnumerateArray())
                    pLock |= Enum.Parse<PhysicsLock>(flag.GetString()!, true);
            }

            return new RigidPhysicsProperty(world, mass, bounciness, friction, linearDamping, angularDamping, pLock);
        }

        private static StaticPhysicsProperty ParseStaticPhysicsProperty(JsonElement el, JsonElement? data)
        {
            var arr = data!.Value;
            int world = arr[0].GetInt32();
            return new StaticPhysicsProperty(world);
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


        /*
         * 
         *         {
          "type": "ControlProperty",
          "data": [
            true,
            false,
            false,
            null,
            {
              "OnHover": [ "action.myActions.printHi" ]
            }
          ]
        }
         * 
         * 
         * 
         * 
         */
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

        private static OrbitalCameraProperty ParseOrbitalCameraProperty(JsonElement? data)
        {
            var arr = data!.Value;
            int len = arr.GetArrayLength();

            string targetNodeId = arr[0].GetString();
            float orbitDistance = len > 1 ? arr[1].GetSingle() : 5f;
            float minPitch = len > 2 ? arr[2].GetSingle() : -89f;
            float maxPitch = len > 3 ? arr[3].GetSingle() : 89f;

            return new OrbitalCameraProperty(targetNodeId, orbitDistance, minPitch, maxPitch);
        }


        private static ColliderProperty ParseColliderProperty(JsonElement? data)
        {
            var arr = data!.Value;
            int len = arr.GetArrayLength();

            string colliderId = arr[0].GetString();

            Anchor3D anchor = Anchor3D.Center;
            if (len > 1 && arr[1].ValueKind == JsonValueKind.String)
                anchor = Enum.Parse<Anchor3D>(arr[1].GetString()!, true);

            Transform? localTransform = null;
            if (len > 2 && arr[2].ValueKind == JsonValueKind.Array)
            {
                var lt = arr[2];
                var position = ParseVector3(lt[0].GetString());
                var rotation = ParseVector3(lt[1].GetString());
                var scale = ParseVector3(lt[2].GetString());
                localTransform = new Transform(position, rotation, scale);
            }

            return new ColliderProperty(colliderId, anchor, localTransform);
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
            var config = Engine.Resources.GetResource<StateMachineConfig>(smConfigId);
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

        internal static string Str(JsonElement? data, int index)
            => data!.Value[index].GetString()
               ?? throw new Exception($"[SCENE CONFIG] Expected string at data[{index}]");

        internal static Vector3 ParseVector3(string s)
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

        private static Vector4 ParseColorVector(string rawColor)
        {
            // Split "0, 10, 255, 10" or "0, 10, 255" by comma
            var parts = rawColor.Split(',')
                               .Select(p => float.Parse(p.Trim(), CultureInfo.InvariantCulture))
                               .ToArray();

            if (parts.Length < 3)
                throw new FormatException($"Invalid color format: '{rawColor}'. Expected 3 (RGB) or 4 (RGBA) values.");

            // Convert 0-255 range to normalized 0.0f - 1.0f range for OpenGL
            float r = parts[0] / 255f;
            float g = parts[1] / 255f;
            float b = parts[2] / 255f;
            float a = parts.Length > 3 ? parts[3] / 255f : 1.0f; // Default alpha to 1.0 if omitted

            return new Vector4(r, g, b, a);
        }
    }
}