using OssianForge.Engine.Nodes.Props;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
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
            sky.AddProperty(new MeshProperty("mesh.cube"));
            sky.AddProperty(new MaterialProperty("texture.brick", "shader.skybox", true));


            //light
            var lightNode = new Node();
            lightNode.Name = "light";
            lightNode.AddProperty(new TransformProperty(new Transform(new Vector3(0f, 5f, 0f), Vector3.Zero, new Vector3(10, 10, 10))));
            lightNode.AddProperty(EmissionProperty.White(intensity: 2.0f, radius: 30.0f));
            lightNode.AddProperty(new MeshProperty("mesh.quad", true));
            lightNode.AddProperty(new MaterialProperty("texture.light", "shader.sprite"));

            //objects
            var house = new Node();
            house.Name = "house";
            house.Id = "house";
            house.AddProperty(new TransformProperty(new Transform(new Vector3(0, 0, -9), Vector3.Zero, Vector3.One)));
            house.AddProperty(new MeshProperty("mesh.house"));
            house.AddProperty(new MaterialProperty("texture.house.barrel", "shader.basic"));
            house.AddProperty(new MaterialProperty("texture.brick", "shader.basic"));
            house.AddProperty(new MaterialProperty("texture.house.windows", "shader.basic"));
            house.AddProperty(new MaterialProperty("texture.house.wood", "shader.basic"));
            house.AddProperty(new ColliderProperty("collider.house"));
            //house.AddProperty(new PhysicalProperty(true, false));

            var plane = new Node();
            plane.Id = "plane";
            plane.Name = "plane";
            plane.AddProperty(new TransformProperty(new Transform(new Vector3(0, 0, -9), Vector3.Zero, new Vector3(10, 1, 10))));
            plane.AddProperty(new MeshProperty("mesh.cube"));
            plane.AddProperty(new MaterialProperty("texture.house.wood", "shader.basic"));
            plane.AddProperty(new ColliderProperty("collider.cube"));
            plane.AddProperty(new PhysicalProperty(true, false));

            var ball = new Node();
            ball.Id = "ball";
            ball.Name = "ball";
            ball.AddProperty(new TransformProperty(new Transform(new Vector3(0, 30f, -7f), Vector3.Zero, new Vector3(0.5f, 0.5f, 0.5f))));
            ball.AddProperty(new MeshProperty("mesh.ball"));
            ball.AddProperty(new MaterialProperty("texture.house.wood", "shader.basic"));
            ball.AddProperty(new ColliderProperty("collider.ball"));
            ball.AddProperty(new PhysicalProperty(false, true, 1f, 0.1f));

            var houseBall = new Node();
            houseBall.Name = "houseBall";
            houseBall.Id = "houseBall";
            houseBall.AddProperty(new TransformProperty(new Transform(new Vector3(0, 20, -9), Vector3.Zero, Vector3.One)));
            houseBall.AddProperty(new MeshProperty("mesh.house"));
            houseBall.AddProperty(new MaterialProperty("texture.house.barrel", "shader.basic"));
            houseBall.AddProperty(new MaterialProperty("texture.brick", "shader.basic"));
            houseBall.AddProperty(new MaterialProperty("texture.house.windows", "shader.basic"));
            houseBall.AddProperty(new MaterialProperty("texture.house.wood", "shader.basic"));
            houseBall.AddProperty(new ColliderProperty("collider.house"));
            houseBall.AddProperty(new PhysicalProperty(false, true, 100, 0f));

            var houseBall1 = new Node();
            houseBall1.Name = "houseBall1";
            houseBall1.Id = "houseBall1";
            houseBall1.AddProperty(new TransformProperty(new Transform(new Vector3(0, 30, -9), new Vector3(0, 60, 60), Vector3.One)));
            houseBall1.AddProperty(new MeshProperty("mesh.house"));
            houseBall1.AddProperty(new MaterialProperty("texture.house.barrel", "shader.basic"));
            houseBall1.AddProperty(new MaterialProperty("texture.brick", "shader.basic"));
            houseBall1.AddProperty(new MaterialProperty("texture.house.windows", "shader.basic"));
            houseBall1.AddProperty(new MaterialProperty("texture.house.wood", "shader.basic"));
            houseBall1.AddProperty(new ColliderProperty("collider.house"));
            houseBall1.AddProperty(new PhysicalProperty(false, true, 100, 0f));

            scene.AddChild(sky);
            scene.AddChild(house);
            scene.AddChild(plane);
            scene.AddChild(lightNode);
            //scene.AddChild(ball);
            scene.AddChild(houseBall);
            scene.AddChild(houseBall1);


            tree.AddChild(scene);

            return tree;
        }
    }
}
