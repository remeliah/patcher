using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using HoLLy.ManagedInjector;
using OsuPatcher.Shared;

namespace OsuPatcher.Cli
{
    internal class Program
    {
        private const string DefaultServer = "refx.online";
        private const string RuntimeDllName = "OsuPatcher.Runtime.dll";
        private const string RuntimeEntryType = "OsuPatcher.Runtime.Main";
        private static string ConfigPath;
        
        public static void Main(string[] args)
        {
            var headless = args.Length > 0;

            try
            {
                var options = Options.Parse(args);
                ConfigPath = GetConfigPath(options.ConfigPath);
                var config = ConfigStore.Load(ConfigPath);
                var osuPath = GetOsuPath(options.OsuPath);
                var patcherPath = GetPatcherPath(options.PatcherPath);
                var server = string.IsNullOrWhiteSpace(options.Server)
                    ? config.GetString("Server", DefaultServer)
                    : options.Server.Trim();

                if (!string.IsNullOrWhiteSpace(options.Server))
                    ConfigStore.Update(ConfigPath, "Server", server);

                var osuProc = Process.Start(new ProcessStartInfo
                {
                    FileName = osuPath,
                    Arguments = $"-devserver {server}",
                    UseShellExecute = false
                });
                
                if (osuProc == null)
                    throw new Exception("failed to start osu!");
                
                osuProc.WaitForInputIdle();
                Thread.Sleep(2000);
                
                using (var proc = new InjectableProcess((uint)osuProc.Id))
                    InjectRuntime(proc, patcherPath);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                if (!headless && Environment.UserInteractive)
                    Console.Read();

                Environment.ExitCode = 1;
            }
        }

        private static string GetConfigPath(string providedPath)
        {
            if (!string.IsNullOrWhiteSpace(providedPath))
                return Path.GetFullPath(providedPath.Trim('"'));

            return ConfigStore.DefaultPath;
        }

        private static string GetPatcherPath(string providedPath)
        {
            if (!string.IsNullOrWhiteSpace(providedPath))
            {
                var path = Path.GetFullPath(providedPath.Trim('"'));
                if (!File.Exists(path))
                    throw new FileNotFoundException("runtime patcher DLL path invalid.", path);

                return path;
            }

            var patcherPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RuntimeDllName);
            if (!File.Exists(patcherPath))
                throw new FileNotFoundException("runtime patcher DLL was not found next to patcher-cli.exe.", patcherPath);

            return patcherPath;
        }

        private static void InjectRuntime(InjectableProcess proc, string patcherPath)
        {
            proc.Inject(patcherPath, RuntimeEntryType, "Initialize");
        }

        /// <summary>
        /// retrieves the stored osu!.exe path from <c>ConfigPath</c> / prompts the user to enter it
        /// </summary>
        /// <returns>
        /// absolute file path to <c>osu!.exe</c>
        /// </returns>
        /// <exception cref="FileNotFoundException">
        /// throws when stored path doesnt point to an existing file
        /// </exception>
        private static string GetOsuPath(string providedPath)
        {
            if (!string.IsNullOrWhiteSpace(providedPath))
            {
                var providedOsuPath = Path.GetFullPath(providedPath.Trim('"'));
                if (!File.Exists(providedOsuPath))
                    throw new FileNotFoundException("osu!.exe path invalid.", providedOsuPath);

                WriteConfig(providedOsuPath);
                return providedOsuPath;
            }

            var config = ConfigStore.Load(ConfigPath);
            var savedPath = config.GetString("OsuPath");

            if (!string.IsNullOrWhiteSpace(savedPath))
            {
                if (File.Exists(savedPath))
                    return savedPath;

                Console.WriteLine("saved osu! path not found, re-entering...");
            }

            Console.Write("enter full path to osu!.exe (ex: D:\\osu!\\osu!.exe): ");
            var path = Console.ReadLine()?.Trim('"');

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("osu!.exe path invalid.", path);

            WriteConfig(path);
            
            return path;
        }

        private static void WriteConfig(string osuPath)
        {
            ConfigStore.Update(ConfigPath, "OsuPath", osuPath);
        }

        private sealed class Options
        {
            public string OsuPath { get; private set; }
            public string PatcherPath { get; private set; }
            public string Server { get; private set; }
            public string ConfigPath { get; private set; }

            public static Options Parse(string[] args)
            {
                var options = new Options();

                for (var i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--osu":
                            options.OsuPath = ReadValue(args, ref i, "--osu");
                            break;

                        case "--patcher":
                            options.PatcherPath = ReadValue(args, ref i, "--patcher");
                            break;

                        case "--config":
                            options.ConfigPath = ReadValue(args, ref i, "--config");
                            break;

                        case "--server":
                        case "--devserver":
                        case "-devserver":
                            options.Server = ReadValue(args, ref i, "--server");
                            break;
                    }
                }

                return options;
            }

            private static string ReadValue(string[] args, ref int index, string name)
            {
                if (index + 1 >= args.Length)
                    throw new ArgumentException($"{name} requires a value");

                index++;
                return args[index];
            }
        }
        
    }
}
