using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Utils.Console
{
    public static class CommandParser
    {
        // Input:  "add node name:myNode static:true"
        // Output: CommandName="add", Args={node="", name="myNode", static="true"}
        public static CommandContext Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var tokens = input.Trim().Split(' ',
                StringSplitOptions.RemoveEmptyEntries);

            var commandName = tokens[0].ToLower();
            var args = new Dictionary<string, string>();
            var flags = new List<string>();

            for (int i = 1; i < tokens.Length; i++)
            {
                var token = tokens[i];

                if (token.Contains(':'))
                {
                    // key:value pair
                    var split = token.Split(':', 2);
                    args[split[0]] = split[1];
                }
                else if (token.StartsWith("--"))
                {
                    // --flag style
                    flags.Add(token[2..].ToLower());
                }
                else
                {
                    // bare word — treated as a valueless arg
                    args[token.ToLower()] = "";
                }
            }

            return new CommandContext(input, commandName, args, flags);
        }
    }
}
