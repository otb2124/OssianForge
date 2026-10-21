using OssianForge.Content;
using OssianForge.Content.Pack;
using OssianForge.Engine.Resources.Config;
using System;
using System.IO;

namespace OssianForge.Engine.Ipc
{
    static public class CookerLink
    {
        /// <summary>Active content settings (defaults until <see cref="InitializeResources"/> runs).</summary>
        public static ContentSettings Settings { get; private set; } = new ContentSettings();

        /// <summary>
        /// Absolute path of the raw Content folder (ConfigFile.Save and Assimp need it).
        /// In pak mode there is no raw folder configured, so this is the default "Content" folder.
        /// </summary>
        public static string ContentFolder { get; private set; } = ResolveDirectory(Settings.Path);

        public static IContentSource Source { get; private set; } = new DiskContentSource(ContentFolder);

        public static bool IsCooked => Source.IsCooked;

        /// <summary>Path of resources.json inside the active source.</summary>
        public static string ResourcesConfigPath => Settings.ResourcesConfigPath;

        /// <summary>Real file-system path for a content-relative path. Only meaningful for loose files.</summary>
        public static string GetDiskPath(string contentRelativePath)
            => System.IO.Path.Combine(ContentFolder, PathUtil.NormalizeSlashes(contentRelativePath));

        public static void InitializeResources()
        {
            var settings = ReadSettings();

            IContentSource next;
            string folder;

            if (settings.Mode == ContentMode.Pak)
            {
                string pak = ResolveFile(settings.Path);
                if (!File.Exists(pak))
                    throw new FileNotFoundException($"[CONTENT] Pak not found: '{pak}'", pak);

                next = new PackContentSource(pak);
                folder = ResolveDirectory(ContentSettings.DefaultFolder);
                Console.WriteLine($"[CONTENT] Using cooked pack: {pak} (resources: {settings.ResourcesConfigPath})");
            }
            else
            {
                folder = ResolveDirectory(settings.Path);
                if (!Directory.Exists(folder))
                    throw new DirectoryNotFoundException($"[CONTENT] Content folder not found: '{folder}'");

                next = new DiskContentSource(folder);
                Console.WriteLine($"[CONTENT] Using raw folder: {folder} (resources: {settings.ResourcesConfigPath})");
            }

            Settings = settings;
            ContentFolder = folder;
            Source = next;
        }

        // ── settings ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads Ossian.ini straight from disk (it is never cooked, so no source is needed).
        /// Missing file = defaults (raw, ./Content).
        /// </summary>
        private static ContentSettings ReadSettings()
        {
            string ini = ResolveFile(ContentSettings.IniFileName);
            if (!File.Exists(ini))
            {
                Console.WriteLine($"[CONTENT] {ContentSettings.IniFileName} not found, using defaults.");
                return new ContentSettings();
            }

            Console.WriteLine($"[CONTENT] Settings: {ini}");
            return ContentSettings.FromIniFile(ini);
        }

        // ── path resolution ──────────────────────────────────────────────────────

        // Relative paths are tried against the working directory first, then the exe directory.
        private static string ResolveDirectory(string path) => Resolve(path, Directory.Exists);
        private static string ResolveFile(string path) => Resolve(path, File.Exists);

        private static string Resolve(string path, Func<string, bool> exists)
        {
            if (System.IO.Path.IsPathRooted(path)) return System.IO.Path.GetFullPath(path);

            string cwd = System.IO.Path.GetFullPath(path);
            if (exists(cwd)) return cwd;

            string exeDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, path));
            return exists(exeDir) ? exeDir : cwd;
        }
    }
}