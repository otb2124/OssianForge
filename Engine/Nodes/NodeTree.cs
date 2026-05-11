using OssianForge.Engine.Nodes.Props;
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

            //skybox
            var sky = new Node();
            sky.Name = "Skybox";
            sky.AddProperty(new TransformProperty());
            sky.AddProperty(new MeshProperty("mesh.ball"));
            sky.AddProperty(new CubemapMaterialProperty("cubemap.skybox.sky", "shader.skybox"));


            //light
            var lightNode = new Node();
            lightNode.Name = "light";
            lightNode.AddProperty(new TransformProperty(new Transform(new Vector3(10f, 5f, 10f), Vector3.Zero, new Vector3(10, 10, 10))));
            lightNode.AddProperty(EmissionProperty.White(intensity: 1f, radius: 30.0f));
            lightNode.AddProperty(new MeshProperty("mesh.quad", true));
            lightNode.AddProperty(new TextureMaterialProperty("texture.light", "shader.sprite"));

            var lightNode1 = new Node();
            lightNode1.Name = "light1";
            lightNode1.AddProperty(new TransformProperty(new Transform(new Vector3(-10f, 5f, -10f), Vector3.Zero, new Vector3(10, 10, 10))));
            lightNode1.AddProperty(EmissionProperty.White(intensity: 1f, radius: 30.0f));
            lightNode1.AddProperty(new MeshProperty("mesh.quad", true));
            lightNode1.AddProperty(new TextureMaterialProperty("texture.light", "shader.sprite"));

            //objects
            var house = new Node();
            house.Name = "house";
            house.AddProperty(new TransformProperty(new Transform(new Vector3(0, 0.622f, -9), Vector3.Zero, Vector3.One)));
            house.AddProperty(new MeshProperty("mesh.house"));
            house.AddProperty(new TextureMaterialProperty("texture.house.barrel", "shader.basic"));
            house.AddProperty(new TextureMaterialProperty("texture.brick", "shader.basic"));
            house.AddProperty(new TextureMaterialProperty("texture.house.windows", "shader.basic"));
            house.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.basic"));
            house.AddProperty(new ColliderProperty("collider.house"));
            house.AddProperty(new PhysicalProperty(true, false));

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
            ball.AddProperty(new TransformProperty(new Transform(new Vector3(0, 32f, -7f), Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f))));
            ball.AddProperty(new MeshProperty("mesh.ball"));
            ball.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.basic"));
            ball.AddProperty(new ColliderProperty("collider.ball"));
            ball.AddProperty(new PhysicalProperty(false, true, 1f, 1f));

            var cube = new Node();
            cube.Id = "cube";
            cube.Name = "cube";
            cube.AddProperty(new TransformProperty(new Transform(new Vector3(0.5f, 30f, -7f), Vector3.Zero, new Vector3(1f, 0.5f, 0.5f))));
            cube.AddProperty(new MeshProperty("mesh.cube"));
            cube.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.basic"));
            cube.AddProperty(new ColliderProperty("collider.cube"));
            cube.AddProperty(new PhysicalProperty(false, true, 1f, 1f));

            var ball1 = new Node();
            ball1.Id = "ball1";
            ball1.Name = "ball1";
            ball1.AddProperty(new TransformProperty(new Transform(new Vector3(0.25f, 40f, -7f), Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f))));
            ball1.AddProperty(new MeshProperty("mesh.ball"));
            ball1.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.basic"));
            ball1.AddProperty(new ColliderProperty("collider.ball"));
            ball1.AddProperty(new PhysicalProperty(false, true, 1f, 1f));

            var cube1 = new Node();
            cube1.Id = "cube1";
            cube1.Name = "cube1";
            cube1.AddProperty(new TransformProperty(new Transform(new Vector3(0, 41f, -7f), new Vector3(10, 0, 0), new Vector3(0.5f, 2f, 0.5f))));
            cube1.AddProperty(new MeshProperty("mesh.cube"));
            cube1.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.basic"));
            cube1.AddProperty(new ColliderProperty("collider.cube"));
            cube1.AddProperty(new PhysicalProperty(false, true, 1f, 1f));

            var houseBall1 = new Node();
            houseBall1.Id = "houseBall1";
            houseBall1.Name = "houseBall1";
            houseBall1.AddProperty(new TransformProperty(new Transform(new Vector3(0, 20f, -5), Vector3.Zero, Vector3.One)));
            houseBall1.AddProperty(new MeshProperty("mesh.house"));
            houseBall1.AddProperty(new TextureMaterialProperty("texture.house.barrel", "shader.basic"));
            houseBall1.AddProperty(new TextureMaterialProperty("texture.brick", "shader.basic"));
            houseBall1.AddProperty(new TextureMaterialProperty("texture.house.windows", "shader.basic"));
            houseBall1.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.basic"));
            houseBall1.AddProperty(new ColliderProperty("collider.house"));
            houseBall1.AddProperty(new PhysicalProperty(false, true, 1f, 1f));


            scene.AddChild(sky);
            scene.AddChild(house);
            scene.AddChild(plane);
            scene.AddChild(lightNode);
            scene.AddChild(lightNode1);
            scene.AddChild(ball);
            scene.AddChild(cube);
            scene.AddChild(ball1);
            scene.AddChild(cube1);
            scene.AddChild(houseBall1);


            tree.AddChild(scene);

            return tree;
        }
    }
}
