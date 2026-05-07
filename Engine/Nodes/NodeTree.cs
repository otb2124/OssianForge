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
            lightNode.AddProperty(new TransformProperty(new Transform(new Vector3(0f, 5f, 0f), Vector3.Zero, Vector3.One)));
            lightNode.AddProperty(LightProperty.White(intensity: 2.0f, radius: 40.0f));
            //lightNode.AddProperty(new SpriteProperty("texturefile.light", new Vector2(10, 10)));

            //objects
            var house = new Node();
            house.Name = "house";
            house.AddProperty(new TransformProperty(new Transform(new Vector3(0, 0, -10), Vector3.Zero, Vector3.One)));
            house.AddProperty(new MeshProperty("mesh.house"));
            house.AddProperty(new MaterialProperty("texture.house.barrel", "shader.basic"));
            house.AddProperty(new MaterialProperty("texture.brick", "shader.basic"));
            house.AddProperty(new MaterialProperty("texture.house.windows", "shader.basic"));
            house.AddProperty(new MaterialProperty("texture.house.wood", "shader.basic"));

            var plane = new Node();
            plane.Name = "plane";
            plane.AddProperty(new TransformProperty(new Transform(new Vector3(0, 0, 0), Vector3.Zero, new Vector3(50, 1, 50))));
            plane.AddProperty(new MeshProperty("mesh.plane"));
            plane.AddProperty(new MaterialProperty("texture.house.wood", "shader.basic"));

            scene.AddChild(sky);
            scene.AddChild(lightNode);
            scene.AddChild(house);
            scene.AddChild(plane);

            tree.AddChild(scene);

            return tree;
        }
    }
}
