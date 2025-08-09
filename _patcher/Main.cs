using System;
using System.Linq;
using _patcher.Patch;
using _patcher.utils;
using HarmonyLib;

namespace _patcher
{
    public class Main
    {
        private static Harmony _harmony = new Harmony("osu_patcher.ano");

        // entry point
        public static int Initialize(string st)
        {
            try
            {
                // server checks so they dont get banned on bancho
                var args = Environment.GetCommandLineArgs();
#if Debug
                if (!args.Contains("-devserver") || string.IsNullOrEmpty(args.SkipWhile(arg => arg != "-devserver")
                    .Skip(1)
                    .FirstOrDefault()) ||
                    args.SkipWhile(arg => arg != "-devserver")
                        .Skip(1)
                        .First()
                        .Contains("ppy.sh"))
                {
                    // can just use Environment.Exit(-1); but this is better
                    GameBase.BeginExit();
                    return 1;
                }
#endif

                // now patchall
                _harmony.PatchAll(typeof(Main).Assembly);

                NotificationManager.ShowMessageMassive("osu! patched!",
                    5000,
                    NotificationManager.NotificationType.Warning
                );
            }
            catch (Exception e)
            {
                Logger.log(e.ToString());
            }

            return 0;
        }
    }
}
