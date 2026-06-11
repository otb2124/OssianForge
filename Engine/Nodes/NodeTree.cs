using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Resources.Scripts;
using System.Numerics;
using static OssianForge.Engine.Utils.Math;

namespace OssianForge.Engine.Nodes
{
    public static class NodeTree
    {

        public static Node GetTree()
        {
            var tree = new Node();
            tree.Name = "tree";

            var scene = new Node();
            scene.Name = "scene";
            scene.Id = "scene";

            var camera = new Node();
            camera.Id = "camera";
            camera.Name = "Camera";
            camera.AddProperty(new CameraProperty());

            //skybox
            var sky = new Node();
            sky.Name = "Skybox";
            sky.AddProperty(new TransformProperty());
            sky.AddProperty(new MeshProperty("mesh.cube"));
            sky.AddProperty(new CubemapMaterialProperty("cubemap.skybox.sky", "shader.skybox"));

            //var inst = Engine.Resources.CreateScriptResourceInstance<NodeProperty>("script.StateProperty", "StateProperty");

            //light
            var lightNode = new Node();
            lightNode.Name = "light";
            lightNode.AddProperty(new TransformProperty(new Transform(new Vector3(10f, 5f, 10f), Vector3.Zero, new Vector3(10, 10, 10))));
            lightNode.AddProperty(EmissionProperty.White(intensity: 1f, radius: 30.0f));
            lightNode.AddProperty(new MeshProperty("mesh.quad", true));
            lightNode.AddProperty(new TextureMaterialProperty("texture.light", "shader.sprite"));
            //lightNode.AddProperty(Engine.Resources.CreateScriptResourceInstance<NodeProperty>("script.StateProperty", "StateProperty"));

            var lightNode1 = new Node();
            lightNode1.Name = "light1";
            lightNode1.AddProperty(new TransformProperty(new Transform(new Vector3(-10f, 5f, -10f), Vector3.Zero, new Vector3(10, 10, 10))));
            lightNode1.AddProperty(EmissionProperty.White(intensity: 1f, radius: 30.0f));
            lightNode1.AddProperty(new MeshProperty("mesh.quad", true));
            lightNode1.AddProperty(new TextureMaterialProperty("texture.light", "shader.sprite"));

            //objects
            var plane = new Node();
            plane.Id = "plane";
            plane.Name = "plane";
            plane.AddProperty(new TransformProperty(new Transform(new Vector3(0, 0, 0), Vector3.Zero, new Vector3(20, 0.5f, 20))));
            plane.AddProperty(new MeshProperty("mesh.cube"));
            plane.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.basic"));
            plane.AddProperty(new ColliderProperty("collider.cube"));
            plane.AddProperty(new PhysicalProperty(true, false));

            var ball = new Node();
            ball.Id = "ball";
            ball.Name = "ball";
            ball.AddProperty(new TransformProperty(new Transform(new Vector3(0, 32f, -2f), Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f))));
            ball.AddProperty(new MeshProperty("mesh.ball"));
            ball.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.basic"));
            ball.AddProperty(new ColliderProperty("collider.ball"));
            ball.AddProperty(new PhysicalProperty(false, true, 1f, 1f));

            var cube = new Node();
            cube.Id = "cube";
            cube.Name = "cube";
            cube.AddProperty(new TransformProperty(new Transform(new Vector3(0.5f, 30f, -2f), Vector3.Zero, new Vector3(1f, 0.5f, 0.5f))));
            cube.AddProperty(new MeshProperty("mesh.cube"));
            cube.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.basic"));
            cube.AddProperty(new ColliderProperty("collider.cube"));
            cube.AddProperty(new PhysicalProperty(false, true, 1f, 1f));

            var ball1 = new Node();
            ball1.Id = "ball1";
            ball1.Name = "ball1";
            ball1.AddProperty(new TransformProperty(new Transform(new Vector3(0.25f, 40f, -2f), Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f))));
            ball1.AddProperty(new MeshProperty("mesh.ball"));
            ball1.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.basic"));
            ball1.AddProperty(new ColliderProperty("collider.ball"));
            ball1.AddProperty(new PhysicalProperty(false, true, 1f, 1f));

            var cube1 = new Node();
            cube1.Id = "cube1";
            cube1.Name = "cube1";
            cube1.AddProperty(new TransformProperty(new Transform(new Vector3(0, 41f, -2f), new Vector3(10, 0, 0), new Vector3(0.5f, 2f, 0.5f))));
            cube1.AddProperty(new MeshProperty("mesh.cube"));
            cube1.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.basic"));
            cube1.AddProperty(new ColliderProperty("collider.cube"));
            cube1.AddProperty(new PhysicalProperty(false, true, 1f, 1f));

            var remy = new Node();
            remy.Id = "remy";
            remy.Name = "remy";
            remy.AddProperty(new TransformProperty(new Transform(new Vector3(0, 1f, -5), Vector3.Zero, Vector3.One)));
            remy.AddProperty(new MeshProperty("mesh.remy"));
            remy.AddProperty(new TextureMaterialProperty("texture.house.barrel", "shader.basic"));
            remy.AddProperty(new TextureMaterialProperty("texture.brick", "shader.basic"));
            remy.AddProperty(new TextureMaterialProperty("texture.house.windows", "shader.basic"));
            remy.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.basic"));
            remy.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.basic"));
            remy.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.basic"));
            remy.AddProperty(new ColliderProperty("collider.remy"));
            remy.AddProperty(new PhysicalProperty(false, true, 1f, 1f));
            remy.AddProperty(new AnimationProperty("animation.remy"));
            
            remy.GetProperty<AnimationProperty>().Play("remy.backflip", true, 2f);

            //text
            var text = new Node();
            text.Name = "text";
            text.AddProperty(new TransformProperty(new Transform(new Vector3(0f, 5f, 0f), Vector3.Zero, Vector3.One)));
            text.AddProperty(new MeshProperty("mesh.quad", true));

            
            text.AddProperty(new TextMaterialProperty("font.roboto", "shader.sdf")
            {
                Content = "Hello World",
                FontSize = 64f,
                Color = new Vector4(1, 1, 1, 1),
                TextureWidth = 512,
                TextureHeight = 128
            });

            scene.AddChild(camera);
            scene.AddChild(sky);
            scene.AddChild(plane);
            scene.AddChild(lightNode);
            scene.AddChild(lightNode1);
            scene.AddChild(ball);
            scene.AddChild(cube);
            scene.AddChild(ball1);
            scene.AddChild(cube1);
            scene.AddChild(remy);
            scene.AddChild(text);


            tree.AddChild(scene);

            return tree;
        }
    }
}
