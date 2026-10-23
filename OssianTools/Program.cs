using System.Runtime.InteropServices;
using OssianForge.Tools.Build;
using OssianForge.Tools.Cook;

namespace OssianForge.Tools;

internal static class Program
{
    private const string Help = @"OssianTools - cooks content and builds shippable games for OssianForge.

Usage:
  OssianTools cook   [options]        cook everything listed in resources.json (+ raw/scripts globs) into a pack
  OssianTools build  [options]        turn the built engine into a game folder (cooks the content itself)
  OssianTools list   [pack]           list pack entries
  OssianTools verify [pack]           read every entry and parse every cooked blob
  OssianTools clean  cache|build      delete the import cache / the game folder (and a leftover temp folder)

Settings come from Tools.ini (command line > Tools.ini > built-in defaults). Relative paths in it are resolved
from its folder, searching parent folders if not found there.

Options shared by cook and build:
  --ini <file>            settings file (default: nearest Tools.ini above cwd / exe)
  --content <dir>         Content folder (default: Content)
  --manifest <path>       resources.json, relative to --content (default: ConfigFiles/Core/resources.json)
  --allow-errors          write the pack even if some assets failed
  --jobs, -j <n>          parallel imports (default: CPU count)
  --no-cache              ignore and do not write the import cache
  --no-compress           do not LZ4-compress pack entries
  --json                  one JSON object per line on stdout (for the Manager)
  --verbose, -v           debug output

cook / clean cache only:
  --out, -o <file>        output pack (default: Content.pak)
  --config <file>         cook.json (raw/scripts/exclude globs) (default: <content>/cook.json if present)
  --engine-dir <dir>      built engine folder (OssianForge.dll + deps); required when scripts are cooked
  --cache <dir>           import cache folder (default: <content parent>/.ossian/cache)

build / clean build only:
  --name <text>           app name: window title + product name (required)
  --exe-name <n>          exe file name without .exe (default: app name without spaces)
  --icon <file>           .ico or .png (<= 256x256); default keeps the engine's icon
  --version <a.b.c.d>     file/product version (default: 1.0.0.0)
  --company <text>  --copyright <text>  --description <text>
  --console | --no-console   keep / hide the console window (default: keep)
  --tree <path>           content-relative tree.json override
  --pak-name <file>       pak file name in the build (default: Content.pak)
  --prebuilt-pak <file>   use this pak instead of cooking
  --template <dir>        built engine folder (default: found above this tool)
  --redist <dir>          copy this folder into the game (name: Redist.Folder, default 'redist'); --no-redist skips it
  --out, -o <dir>         output folder; the game goes to <out>\\<exe name>\\ (default: dist)
  --zip                   also write <out>\\<exe name>-<version>.zip
  --exclude <globs>       extra comma-separated patterns to leave out (repeatable)
  --force                 replace / delete an output folder that does not look like a previous build
  --dry-run               validate and show what would be copied, write nothing

Exit codes: 0 ok, 1 failed (assets, cooker, patching, disk), 2 usage or missing input.";

    [DllImport("kernel32.dll")]
    private static extern uint GetConsoleProcessList(uint[] list, uint count);

    // If we're the only process attached to the console, it was opened just for us (double-click).
    private static bool OwnsConsole()
        => OperatingSystem.IsWindows() && GetConsoleProcessList(new uint[2], 2) == 1;

    private static int Main(string[] args)
    {
        int code = Run(args);
        bool json = Array.IndexOf(args, "--json") >= 0;
        if (!json && OwnsConsole())
        {
            Console.WriteLine();
            Console.WriteLine(code == 0 ? "Done. Press any key to close..." : $"Failed (exit code {code}). Press any key to close...");
            Console.ReadKey(true);
        }
        return code;
    }

    private static int Run(string[] args)
    {
        if (args.Length > 0 && args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Help);
            return 0;
        }
        if (args.Length == 0)
            return Usage("No command given.");

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "cook" => WithCook(args, 1, CookRunner.Cook),
                "list" => WithCook(args, 1, Commands.List),
                "verify" => WithCook(args, 1, Commands.Verify),
                "build" => WithBuild(args, 1, Packager.Build),
                "clean" => Clean(args),
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

    private static int Clean(string[] args)
    {
        string target = args.Length > 1 ? args[1].ToLowerInvariant() : "";
        return target switch
        {
            "cache" => WithCook(args, 2, Commands.Clean),
            "build" => WithBuild(args, 2, Packager.Clean),
            _ => Usage("clean needs a target: 'cache' or 'build'.")
        };
    }

    private static int WithCook(string[] args, int start, Func<CookOptions, int> run)
    {
        var o = CookOptions.Parse(args, start);
        Log.Json = o.Json;
        Log.Verbose = o.Verbose;
        if (o.IniPath != null) Log.Info($"Using {o.IniPath}");
        foreach (var w in o.IniWarnings) Log.Warn(w);
        return run(o);
    }

    private static int WithBuild(string[] args, int start, Func<BuildOptions, int> run)
    {
        var o = BuildOptions.Parse(args, start);
        Log.Json = o.Json;
        Log.Verbose = o.Verbose;
        return run(o);
    }

    private static int Usage(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine(Help);
        return 2;
    }
}
