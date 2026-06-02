using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OssianForge.Engine.Utils.Console
{
    public class DebugConsole
    {
        private readonly Dictionary<string, ICommand> _commands = new();
        private Thread _inputThread;
        private bool _running;

        public DebugConsole()
        {
            // Register built-in commands
            Register(new AddCommand());
            Register(new RemoveNodeCommand());
            Register(new ListNodesCommand());
            Register(new SetPropertyCommand());
            Register(new HelpCommand(_commands));
        }

        public void Register(ICommand command)
        {
            _commands[command.Name.ToLower()] = command;
        }

        public void Start()
        {
            _running = true;
            _inputThread = new Thread(InputLoop)
            {
                IsBackground = true,
                Name = "DebugConsoleThread"
            };
            _inputThread.Start();
        }

        public void Stop() => _running = false;

        public void Execute(string input)
        {
            var context = CommandParser.Parse(input);
            if (context == null) return;

            if (_commands.TryGetValue(context.CommandName, out var command))
            {
                try
                {
                    command.Execute(context);
                }
                catch (Exception ex)
                {
                    Write($"[ERROR] {ex.Message}", ConsoleColor.Red);
                }
            }
            else
            {
                Write($"Unknown command '{context.CommandName}'. Type 'help' for a list.", ConsoleColor.Yellow);
            }
        }

        private void InputLoop()
        {
            while (_running)
            {
                System.Console.ForegroundColor = ConsoleColor.Cyan;
                System.Console.Write("> ");
                System.Console.ResetColor();

                var input = System.Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input))
                    Execute(input);
            }
        }

        public static void Write(string message, ConsoleColor color = ConsoleColor.White)
        {
            System.Console.ForegroundColor = color;
            System.Console.WriteLine(message);
            System.Console.ResetColor();
        }
    }
}
