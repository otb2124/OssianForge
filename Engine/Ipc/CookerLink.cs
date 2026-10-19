using OssianForge.Content;
using OssianForge.Engine.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Ipc
{
    static public class CookerLink
    {

        public static IContentSource Source { get; private set; } = new DiskContentSource(Resource.CONTENT_FOLDER_PATH);

        public static bool IsCooked => Source.IsCooked;

        public static void InitializeResources()
        {
            if (Environment.GetEnvironmentVariable("OSSIAN_CONTENT") == "loose") return;

            var packPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Content.pak");
            if (!System.IO.File.Exists(packPath)) return;

            Source = new Content.Pack.PackContentSource(packPath);
            Console.WriteLine($"[CONTENT] Using cooked pack: {packPath}");
        }
    }
}
