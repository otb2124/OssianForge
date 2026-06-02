using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Utils.Console
{
    public interface ICommand
    {
        string Name { get; }
        string Description { get; }
        void Execute(CommandContext context);
    }

    public class CommandContext
    {
        public string Raw { get; }
        public string CommandName { get; }
        public IReadOnlyDictionary<string, string> Args { get; }
        public IReadOnlyList<string> Flags { get; }

        public CommandContext(string raw, string commandName,
            Dictionary<string, string> args, List<string> flags)
        {
            Raw = raw;
            CommandName = commandName;
            Args = args;
            Flags = flags;
        }

        // "add node name:myNode" → Get("name") == "myNode"
        public string Get(string key, string fallback = null)
            => Args.TryGetValue(key, out var val) ? val
             : Args.FirstOrDefault(kv => kv.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value
             ?? fallback;

        public bool Has(string key)
            => Args.Keys.Any(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));

        public bool HasFlag(string flag) => Flags.Contains(flag);

        public T GetAs<T>(string key, T fallback = default)
        {
            if (!Args.TryGetValue(key, out var val)) return fallback;
            try { return (T)Convert.ChangeType(val, typeof(T)); }
            catch { return fallback; }
        }
    }
}
