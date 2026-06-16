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
                "PointEmissionProperty" => ParsePointEmissionProperty(data),
                "SunEmissionProperty" => ParseSunEmissionProperty(data),
                "SpotEmissionProperty" => ParseSpotEmissionProperty(data),
                "EmissionProperty" => ParsePointEmissionProperty(data),
                "ColliderProperty" => new ColliderProperty(Str(data, 0)),
                "PhysicalProperty" => ParsePhysicalProperty(el, data),
                "AnimationProperty" => ParseAnimationProperty(el, data),
                "ControlProperty" => ParseControlProperty(data),
                "SoundProperty" => ParseSoundProperty(data),
                "ScriptProperty" => new ScriptProperty(Str(data, 0)),
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
            Pivot pivot = Pivot.MiddleCenter;

            if (len > 1) space = Enum.Parse<RenderSpace>(arr[1].GetString()!, true);
            if (len > 2) anchor = Enum.Parse<Anchor>(arr[2].GetString()!, true);
            if (len > 3) pivot = Enum.Parse<Pivot>(arr[3].GetString()!, true);

            return new TransformProperty(transform, space, anchor, pivot);
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

        private static SoundProperty ParseSoundProperty(JsonElement? data)
        {
            var arr = data!.Value;
            string resId = arr[0].GetString();
            bool autoPlay = arr.GetArrayLength() > 1 && arr[1].GetBoolean();

            return new SoundProperty(resId) { AutoPlay = autoPlay };
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