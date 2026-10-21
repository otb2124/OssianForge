using Silk.NET.Assimp;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;

namespace OssianForge.Cooker;

internal static class Program
{
    private const string Help = @"OssianCooker - turns source assets into a runtime content pack.

Usage:
  OssianCooker        [options]        same as cook (no command given)
  OssianCooker cook[options]        cook everything listed in resources.json(+ raw/scripts globs) into a pack
  OssianCooker list[pack] list pack entries
  OssianCooker verify[pack]           read every entry and parse every cooked blob
  OssianCooker clean[options]        delete the import cache

Options:
  --ini<file> settings file (default: nearest Cook.ini above cwd / exe)
  --content<dir> Content folder(default: Content)
  --out, -o<file> Output pack(default: Content.pak)
  --manifest<path> resources.json, relative to --content       (default: ConfigFiles/Core/resources.json)
  --config<file> cook.json(raw/scripts/exclude globs)       (default: <content>/cook.json if present)
  --engine-dir<dir> Built engine folder(OssianForge.dll + deps); required when scripts are cooked
  --cache<dir> Import cache folder(default: <content parent>/.ossian/cache)
  --no-cache Ignore and do not write the cache
  --no-compress Do not LZ4-compress pack entries
  --jobs, -j<n> Parallel imports(default: CPU count)
  --allow-errors Write the pack even if some assets failed
  --json One JSON object per line on stdout(for the Manager)
  --verbose, -v Debug output

Exit codes: 0 ok, 1 asset errors / bad pack, 2 usage or missing input.";

    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Help);
            return 0;
        }

        // No command (or only options): cook, using Cook.ini for everything not given on the command line.
        if (args.Length == 0 || args[0].StartsWith('-'))
            args = new[] { "cook" }.Concat(args).ToArray();

        try
        {
            var options = CookOptions.Parse(args, 1);
            Log.Json = options.Json;
            Log.Verbose = options.Verbose;
            if (options.IniPath != null) Log.Info($"Using {options.IniPath}");
            foreach (var w in options.IniWarnings) Log.Warn(w);

            return args[0].ToLowerInvariant() switch
            {
                "cook" => CookRunner.Cook(options),
                "list" => Commands.List(options),
                "verify" => Commands.Verify(options),
                "clean" => Commands.Clean(options),
                _ => Usage($"Unknown command: {args[0]}")
            };
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Log.Error("", ex.ToString());
            return 1;
        }
    }

    private static int Usage(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine(Help);
        return 2;
    }
}