using System;
using System.Linq;
using HarmonyLib;
using OsuPatcher.Runtime.Utils;
using OsuPatcher.Runtime.Wrappers;

namespace OsuPatcher.Runtime
{
    public class Main
    {
        private static readonly Harmony Harmony = new Harmony("osu_patcher.ano");
        private const string DefaultServer = "refx.online";

        internal static string Server { get; private set; } = DefaultServer;
        internal static bool IsRefx => string.Equals(Server, "refx.online", StringComparison.OrdinalIgnoreCase);
        internal static bool IsAkat => string.Equals(Server, "akatsuki.gg", StringComparison.OrdinalIgnoreCase);

        public static int Initialize(string st)
        {
            try
            {
                Server = ResolveServer(st);

                // now patchall
                Harmony.PatchAll(typeof(Main).Assembly);

                NotificationManager.ShowMessageMassive("osu! patched!",
                    5000,
                    NotificationManager.NotificationType.Warning
                );

                if (IsAkat)
                {
                    NotificationManager.ShowMessage("Warning: This patcher disables cheat flags",
                        "Red",
                        10000
                    );

                    NotificationManager.ShowMessage("Playing on Akatsuki might result in account restrictions. Use at your own risk.",
                        "Red",
                        10000
                    );
                }
            }
            catch (Exception e)
            {
                Logger.log(e.ToString());
            }

            return 0;
        }

        private static string ResolveServer(string injectedArgument)
        {
            var args = Environment.GetCommandLineArgs()
                .Concat((injectedArgument ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                .ToArray();

            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-devserver", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]))
                    return args[i + 1].Trim();
            }

            return DefaultServer;
        }
    }
}
