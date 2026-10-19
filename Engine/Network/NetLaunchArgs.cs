namespace OssianForge.Engine.Network
{
    /// <summary>
    /// Command line: --host [port] | --join address[:port] | --name Bob
    /// Call Configure first thing in App, like ManagerLink.Configure.
    /// </summary>
    public static class NetLaunchArgs
    {
        public static bool Host { get; private set; }
        public static string? JoinAddress { get; private set; }
        public static int Port { get; private set; } = Network.DefaultPort;
        public static string? Name { get; private set; }

        public static void Configure(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--host":
                        Host = true;
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int port))
                        {
                            Port = port;
                            i++;
                        }
                        break;
                    case "--join":
                        if (i + 1 < args.Length) JoinAddress = args[++i];
                        break;
                    case "--name":
                        if (i + 1 < args.Length) Name = args[++i];
                        break;
                }
            }
        }
    }
}