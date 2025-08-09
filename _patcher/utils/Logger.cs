﻿using _patcher.Patch;
using HarmonyLib;
using System.Reflection;

namespace _patcher.utils
{
    internal class Logger
    {
        public static void log(
            string message,
            string color = "Red",
            int timer = 15000)
        {
            using (var writer = new System.IO.StreamWriter("Logs/patcher.txt", true))
                writer.WriteLine(message);

            NotificationManager.ShowMessage(
                message,
                AccessTools.TypeByName("Microsoft.Xna.Framework.Graphics.Color")?
                    .GetProperty(color, BindingFlags.Public | BindingFlags.Static)?
                    .GetValue(null),
                timer
            );
        }
    }
}