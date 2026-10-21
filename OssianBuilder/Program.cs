using System.Runtime.InteropServices;

namespace OssianForge.Builder;

internal static class Program
{
    private const string Help = @"OssianBuilder - turns the built OssianForge engine into a shippable game folder.

It never compiles anything: it copies the engine build (without OssianCooker / OssianBuilder / pdbs / raw content),
cooks the content into a pak, renames the exe, patches its icon and version info, and writes a pak-mode Ossian.ini.

Usage:
  OssianBuilder        [options]        same as build (no command given)
  OssianBuilder build  [options]        make the game folder
  OssianBuilder clean  [options]        delete the game folder (and a leftover temp folder) from the output

Options:
  --ini <file>            settings file (default: nearest Build.ini above cwd / exe)
  --name <text>           app name: window title + product name (required)
  --exe-name <name>       exe file name without .exe (default: app name without spaces)
  --icon <file>           .ico or .png (<= 256x256); default keeps the engine's icon
  --version <a.b.c.d>     file/product version (default: 1.0.0.0)
  --company <text>  --copyright <text>  --description <text>
  --console | --no-console   keep / hide the console window (default: keep)
  --content <dir>         raw Content folder to cook (default: Content)
  --manifest <path>       resources.json, relative to --content (default: ConfigFiles/Core/resources.json)
  --tree <path>           content-relative tree.json override
  --pak-name <file>       pak file name in the build (default: Content.pak)
  --prebuilt-pak <file>   use this pak instead of running the cooker
  --cook-ini <file>       Cook.ini handed to the cooker
  --allow-errors          let the cooker write the pak even if some assets failed
  --template <dir>        built engine folder (default: found above this tool)
  --cooker <file>         OssianCooker.exe (default: <template>\OssianCooker\OssianCooker.exe)
  --out, -o <dir>         output folder; the game goes to <out>\<exe name>\ (default: dist)
  --zip                   also write <out>\<exe name>-<version>.zip
  --exclude <globs>       extra comma-separated patterns to leave out (repeatable)
  --force                 replace / delete an output folder that does not look like a previous build
  --dry-run               validate and show what would be copied, write nothing
  --json                  one JSON object per line on stdout (for the Manager); the cooker's events are forwarded
  --verbose, -v           debug output

Exit codes: 0 ok, 1 build failed (cooker, patching, disk), 2 usage or missing input.";


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

    public static int Run(string[] args)
    {
        if (args.Length > 0 && args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Help);
            return 0;
        }

        // No command (or only options): build, using Build.ini for everything not given on the command line.
        if (args.Length == 0 || args[0].StartsWith('-'))
            args = new[] { "build" }.Concat(args).ToArray();

        try
        {
            var options = BuildOptions.Parse(args, 1);
            Log.Json = options.Json;
            Log.Verbose = options.Verbose;

            return args[0].ToLowerInvariant() switch
            {
                "build" => Packager.Build(options),
                "clean" => Packager.Clean(options),
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