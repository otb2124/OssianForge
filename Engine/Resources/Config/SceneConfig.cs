using OssianForge.Engine.Nodes;
using OssianForge.Engine.Nodes.Props;
using System.Numerics;
using System.Text.Json;
using static OssianForge.Engine.Utils.MathUtils;


//TODO: some addional parsing like $math($textAspect(\"im a text brick2\ntest123\", 32, \"font.roboto\")*1)
//data [ "var gl = Engine.Graphics.Batch.OpenGL; gl.Disable(EnableCap.DepthTest); " ]

namespace OssianForge.Engine.Resources.Config
{
    public class SceneConfig : ConfigFile
    {

        public Node Scene;

        public SceneConfig(string id, string path) : base(id, path) { }


        public override void Load()
        {
            base.Load();
            Scene = LoadScene();
        }

        public Node LoadScene()
        {
            string globalPath = CONTENT_FOLDER_PATH + "/" + Path;
            string raw = File.ReadAllText(globalPath);
            using var doc = JsonDocument.Parse(raw);
            return ParseNode(doc.RootElement);
        }

        // ── node ─────────────────────────────────────────────────────────────────

        private static Node ParseNode(JsonElement el)
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
                "CubemapMaterialProperty" => new CubemapMaterialProperty(Str(data, 0), Str(data, 1)),
                "TextureMaterialProperty" => new TextureMaterialProperty(Str(data, 0), Str(data, 1)),
                "TextMaterialProperty" => ParseTextMaterialProperty(data),
                "EmissionProperty" => ParseEmissionProperty(data),
                "ColliderProperty" => new ColliderProperty(Str(data, 0)),
                "PhysicalProperty" => ParsePhysicalProperty(el, data),
                "AnimationProperty" => ParseAnimationProperty(el, data),
                "ControlProperty" => ParseControlProperty(data),
                _ => throw new Exception($"[SCENE CONFIG] Unknown property type '{type}'")
            };
        }

        // ── property parsers ──────────────────────────────────────────────────────

        private static TransformProperty ParseTransformProperty(JsonElement? data)
        {
            if (data == null) return new TransformProperty();

            var arr = data.Value;

            // first element is always the nested [ pos, rot, scale ] array
            var vec3arr = arr[0];
            var position = ParseVector3(vec3arr[0].GetString());
            var rotation = ParseVector3(vec3arr[1].GetString());
            var scale = ParseVector3(vec3arr[2].GetString());
            var transform = new Transform(position, rotation, scale);

            // optional second element is RenderSpace
            if (arr.GetArrayLength() > 1)
            {
                var space = Enum.Parse<RenderSpace>(arr[1].GetString()!, true);
                return new TransformProperty(transform, space);
            }

            return new TransformProperty(transform);
        }

        private static TextMaterialProperty ParseTextMaterialProperty(JsonElement? data)
        {
            var arr = data!.Value;
            string text = arr[0].GetString();
            int size = arr[1].GetInt32();
            var color = ParseVector4(arr[2].GetString());
            string font = arr[3].GetString();
            string shader = arr[4].GetString();
            return new TextMaterialProperty(text, size, color, font, shader);
        }

        private static EmissionProperty ParseEmissionProperty(JsonElement? data)
        {
            var arr = data!.Value;
            var color = ParseVector3(arr[0].GetString());
            float intensity = arr[1].GetSingle();
            float radius = arr[2].GetSingle();
            return new EmissionProperty(new System.Numerics.Vector3(color.X, color.Y, color.Z), intensity, radius);
        }

        private static PhysicalProperty ParsePhysicalProperty(JsonElement el, JsonElement? data)
        {
            var arr = data!.Value;
            bool isStatic = arr[0].GetBoolean();
            bool isDynamic = arr[1].GetBoolean();

            PhysicalProperty prop = arr.GetArrayLength() > 2
                ? new PhysicalProperty(isStatic, isDynamic, arr[2].GetSingle(), arr[3].GetSingle())
                : new PhysicalProperty(isStatic, isDynamic);

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
            if (data == null) return new ControlProperty();

            var arr = data.Value;
            int len = arr.GetArrayLength();

            bool isInteractable = len > 0 ? arr[0].GetBoolean() : true;
            bool isDraggable = len > 1 ? arr[1].GetBoolean() : false;
            bool isDropTarget = len > 2 ? arr[2].GetBoolean() : false;
            string dragGroupId = len > 3 ? arr[3].GetString() : null;

            return new ControlProperty(isInteractable, isDraggable, isDropTarget, dragGroupId);
        }

        // ── helpers ───────────────────────────────────────────────────────────────

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