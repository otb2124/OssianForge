using OssianForge.Engine.Nodes.Props;
using OssianForge.Engine.Resources.Scripts;
using OssianForge.Engine.Utils;
using Silk.NET.OpenGL;
using System.Numerics;
using static OssianForge.Engine.Utils.MathUtils;

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
            lightNode.AddProperty(new TransformProperty(new Transform(new Vector3(10f, 5f, 10f), Vector3.Zero, new Vector3(10, 10, 10)), RenderSpace.Billboard));
            lightNode.AddProperty(EmissionProperty.White(intensity: 1f, radius: 30.0f));
            lightNode.AddProperty(new MeshProperty("mesh.quad"));
            lightNode.AddProperty(new TextureMaterialProperty("texture.light", "shader.unlit"));
            //lightNode.AddProperty(Engine.Resources.CreateScriptResourceInstance<NodeProperty>("script.StateProperty", "StateProperty"));
            lightNode.GetProperty<MaterialProperty>().BeginAction = () =>
            {
                var gl = Engine.Graphics.Batch.OpenGL;
                gl.Enable(EnableCap.Blend);
                gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
                gl.DepthMask(false);
            };
            lightNode.GetProperty<MaterialProperty>().EndAction = () =>
            {
                var gl = Engine.Graphics.Batch.OpenGL;
                gl.DepthMask(true);
                gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            };

            var lightNode1 = new Node();
            lightNode1.Name = "light1";
            lightNode1.AddProperty(new TransformProperty(new Transform(new Vector3(-10f, 5f, -10f), Vector3.Zero, new Vector3(10, 10, 10)), RenderSpace.Billboard));
            lightNode1.AddProperty(EmissionProperty.White(intensity: 1f, radius: 30.0f));
            lightNode1.AddProperty(new MeshProperty("mesh.quad"));
            lightNode1.AddProperty(new TextureMaterialProperty("texture.light", "shader.unlit"));
            lightNode1.GetProperty<MaterialProperty>().BeginAction = () =>
            {
                var gl = Engine.Graphics.Batch.OpenGL;
                gl.Enable(EnableCap.Blend);
                gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
                gl.DepthMask(false);
            };
            lightNode1.GetProperty<MaterialProperty>().EndAction = () =>
            {
                var gl = Engine.Graphics.Batch.OpenGL;
                gl.DepthMask(true);
                gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            };

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

            //textBrick
            var textBrick = new Node();
            textBrick.Name = "textBrick";
            textBrick.Id = "textBrick0";
            textBrick.AddProperty(new TransformProperty(new Transform(new Vector3(0f, 5f, 0f), Vector3.Zero, new Vector3(FontUtils.GetAspect("im a text brick\ntest123", 32, "font.roboto") * 1f, 1f, 1f)), RenderSpace.World));
            textBrick.AddProperty(new MeshProperty("mesh.quad"));
            textBrick.AddProperty(new TextMaterialProperty("im a text brick\ntest123", 32, new Vector4(1, 1, 1, 1), "font.roboto", "shader.sdf"));
            textBrick.AddProperty(new ColliderProperty("collider.thickquad"));
            textBrick.AddProperty(new PhysicalProperty(false, true, 1f, 1f));

            var textBrick1 = new Node();
            textBrick1.Name = "textBrick1";
            textBrick1.Id = "textBrick1";
            textBrick1.AddProperty(new TransformProperty(new Transform(new Vector3(0f, 10f, 0f), Vector3.Zero, new Vector3(FontUtils.GetAspect("im a text brick1\ntest123", 32, "font.roboto") * 1f, 1f, 1f)), RenderSpace.World));
            textBrick1.AddProperty(new MeshProperty("mesh.quad"));
            textBrick1.AddProperty(new TextMaterialProperty("im a text brick\ntest123", 32, new Vector4(1, 1, 1, 1), "font.roboto", "shader.sdf"));
            textBrick1.AddProperty(new ColliderProperty("collider.thickquad"));
            textBrick1.AddProperty(new PhysicalProperty(false, true, 1f, 1f));

            var textBrick2 = new Node();
            textBrick2.Name = "textBrick2";
            textBrick2.Id = "textBrick2";
            textBrick2.AddProperty(new TransformProperty(new Transform(new Vector3(0.5f, 8f, 0f), Vector3.Zero, new Vector3(FontUtils.GetAspect("im a text brick2\ntest123", 32, "font.roboto") * 1f, 1f, 1f)), RenderSpace.World));
            textBrick2.AddProperty(new MeshProperty("mesh.quad"));
            textBrick2.AddProperty(new TextMaterialProperty("im a text brick2\ntest123", 32, new Vector4(1, 1, 1, 1), "font.roboto", "shader.sdf"));
            textBrick2.AddProperty(new ColliderProperty("collider.thickquad"));
            textBrick2.AddProperty(new PhysicalProperty(false, true, 1f, 1f));

            var img = new Node();
            img.Name = "image";
            img.AddProperty(new TransformProperty(new Transform(new Vector3(5f, 5f, 0f), Vector3.Zero, Vector3.One)));
            img.AddProperty(new MeshProperty("mesh.quad"));
            img.AddProperty(new TextureMaterialProperty("texture.dices", "shader.unlit"));


            scene.AddChild(camera);
            scene.AddChild(sky);
            scene.AddChild(plane);
            scene.AddChild(ball);
            scene.AddChild(cube);
            scene.AddChild(ball1);
            scene.AddChild(cube1);
            scene.AddChild(remy);

            scene.AddChild(lightNode);
            scene.AddChild(lightNode1);

            scene.AddChild(textBrick);
            scene.AddChild(textBrick1);
            scene.AddChild(textBrick2);
            scene.AddChild(img);















            var scene1 = new Node();
            scene1.Name = "scene1";
            scene1.Id = "scene1";

            var camera1 = new Node();
            camera1.Id = "camera1";
            camera1.Name = "Camera1";
            camera1.AddProperty(new CameraProperty());

            //skybox
            var sky1 = new Node();
            sky1.Name = "Skybox1";
            sky1.AddProperty(new TransformProperty());
            sky1.AddProperty(new MeshProperty("mesh.cube"));
            sky1.AddProperty(new CubemapMaterialProperty("cubemap.skybox.sky", "shader.skybox"));

            //screenspace
            var img1 = new Node();
            img1.Name = "image1";
            img1.AddProperty(new TransformProperty(new Transform(new Vector3(960f, 540f, 0f), new Vector3(0, 0, 120), new Vector3(200, 200, 1)), RenderSpace.ScreenSpace));
            img1.AddProperty(new MeshProperty("mesh.quad"));
            img1.AddProperty(new TextureMaterialProperty("texture.brick", "shader.unlit"));
            img1.GetProperty<MaterialProperty>().BeginAction = () =>
            {
                var gl = Engine.Graphics.Batch.OpenGL;
                gl.Disable(EnableCap.DepthTest);
            };
            img1.GetProperty<MaterialProperty>().EndAction = () =>
            {
                var gl = Engine.Graphics.Batch.OpenGL;
                gl.Enable(EnableCap.DepthTest);
            };

            var text1 = new Node();
            text1.Name = "text1";
            text1.Id = "text1";
            text1.AddProperty(new TransformProperty(new Transform(new Vector3(0f, 200f, 0f), Vector3.Zero, new Vector3(400, 200, 20)), RenderSpace.ScreenSpace));
            text1.AddProperty(new MeshProperty("mesh.quad"));
            //text1.AddProperty(new TextureMaterialProperty("texture.brick", "shader.unlit"));
            text1.AddProperty(new TextMaterialProperty("testing screenspace\ntakes time", 32, new Vector4(1, 1, 1, 1), "font.roboto", "shader.sdf"));
            text1.GetProperty<MaterialProperty>().BeginAction = () =>
            {
                var gl = Engine.Graphics.Batch.OpenGL;
                gl.Disable(EnableCap.DepthTest);
            };
            text1.GetProperty<MaterialProperty>().EndAction = () =>
            {
                var gl = Engine.Graphics.Batch.OpenGL;
                gl.Enable(EnableCap.DepthTest);
            };
            text1.AddProperty(new ColliderProperty("collider.thickquad"));
            text1.AddProperty(new PhysicalProperty(false, true, 1f, 1f));
            text1.GetProperty<PhysicalProperty>().SetWorld(1);

            var img2 = new Node();
            img2.Name = "image2";
            img2.Id = "img2";
            img2.AddProperty(new TransformProperty(new Transform(new Vector3(250f, 500f, 0f), Vector3.Zero, new Vector3(200, 200, 20)), RenderSpace.ScreenSpace));
            img2.AddProperty(new MeshProperty("mesh.quad"));
            img2.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.unlit"));
            img2.GetProperty<MaterialProperty>().BeginAction = () =>
            {
                var gl = Engine.Graphics.Batch.OpenGL;
                gl.Disable(EnableCap.DepthTest);
            };
            img2.GetProperty<MaterialProperty>().EndAction = () =>
            {
                var gl = Engine.Graphics.Batch.OpenGL;
                gl.Enable(EnableCap.DepthTest);
            };
            img2.AddProperty(new ColliderProperty("collider.thickquad"));
            img2.AddProperty(new PhysicalProperty(false, true, 1f, 1f));
            img2.GetProperty<PhysicalProperty>().SetWorld(1);

            var img3 = new Node();
            img3.Name = "img3";
            img3.Id = "img3";
            img3.AddProperty(new TransformProperty(new Transform(new Vector3(200f, 900f, 0f), Vector3.Zero, new Vector3(200, 200, 20)), RenderSpace.ScreenSpace));
            img3.AddProperty(new MeshProperty("mesh.quad"));
            img3.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.unlit"));
            img3.GetProperty<MaterialProperty>().BeginAction = () =>
            {
                var gl = Engine.Graphics.Batch.OpenGL;
                gl.Disable(EnableCap.DepthTest);
            };
            img3.GetProperty<MaterialProperty>().EndAction = () =>
            {
                var gl = Engine.Graphics.Batch.OpenGL;
                gl.Enable(EnableCap.DepthTest);
            };
            img3.AddProperty(new ColliderProperty("collider.thickquad"));
            img3.AddProperty(new PhysicalProperty(false, true, 1f, 1f));
            img3.GetProperty<PhysicalProperty>().SetWorld(1);


            var screenBottom = new Node();
            screenBottom.Name = "screenBottom";
            screenBottom.Id = "screenBottom";
            screenBottom.AddProperty(new TransformProperty(new Transform(new Vector3(0f, 0f, 0f), Vector3.Zero, new Vector3(700, 50, 50)), RenderSpace.ScreenSpace));
            screenBottom.AddProperty(new MeshProperty("mesh.quad"));
            screenBottom.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.unlit"));
            screenBottom.GetProperty<MaterialProperty>().BeginAction = () =>
            {
                var gl = Engine.Graphics.Batch.OpenGL;
                gl.Disable(EnableCap.DepthTest);
            };
            screenBottom.GetProperty<MaterialProperty>().EndAction = () =>
            {
                var gl = Engine.Graphics.Batch.OpenGL;
                gl.Enable(EnableCap.DepthTest);
            };
            screenBottom.AddProperty(new ColliderProperty("collider.thickquad"));
            screenBottom.AddProperty(new PhysicalProperty(true, false));
            screenBottom.GetProperty<PhysicalProperty>().SetWorld(1);


            var remy1 = new Node();
            remy1.Id = "remy1";
            remy1.Name = "remy1";
            remy1.AddProperty(new TransformProperty(new Transform(new Vector3(900, 100f, 0), new Vector3(0, 180, 0), new Vector3(100, 100, 1)), RenderSpace.ScreenSpace));
            remy1.AddProperty(new MeshProperty("mesh.remy"));
            remy1.AddProperty(new TextureMaterialProperty("texture.house.barrel", "shader.unlit"));
            remy1.AddProperty(new TextureMaterialProperty("texture.brick", "shader.unlit"));
            remy1.AddProperty(new TextureMaterialProperty("texture.house.windows", "shader.unlit"));
            remy1.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.unlit"));
            remy1.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.unlit"));
            remy1.AddProperty(new TextureMaterialProperty("texture.house.wood", "shader.unlit"));
            //remy1.AddProperty(new ColliderProperty("collider.remy"));
            //remy1.AddProperty(new PhysicalProperty(false, true, 1f, 1f));
            remy1.AddProperty(new AnimationProperty("animation.remy"));

            remy1.GetProperty<AnimationProperty>().Play("remy.backflip", true, 2f);





            scene1.AddChild(camera1);
            scene1.AddChild(sky1);
            scene1.AddChild(img1);
            scene1.AddChild(img3);
            scene1.AddChild(text1);
            scene1.AddChild(img2);
            scene1.AddChild(screenBottom);
            scene1.AddChild(remy1);





            tree.AddChild(scene1);

            return tree;
        }
    }
}
