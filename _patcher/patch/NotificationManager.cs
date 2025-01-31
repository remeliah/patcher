using System;
using System.Reflection.Emit;
using System.Reflection;
using _patcher.Helpers;

namespace _patcher.patch
{
    internal class NotificationManager
    {
        // osu.Graphics.Notifications.NotificationManager::ShowMessage
        private static readonly MethodBase BaseShowMessage = ILPatch.FindMethodBySignature(new[]
        {
            OpCodes.Ldarg_1,
            OpCodes.Stfld,
            OpCodes.Ldloc_0,
            OpCodes.Ldarg_3,
            OpCodes.Stfld,
            OpCodes.Ldsfld,
            OpCodes.Ldloc_0,
            OpCodes.Ldftn,
            OpCodes.Newobj
        });

        // osu.Graphics.Notifications.NotificationManager::ShowMessageMassive
        private static readonly MethodBase BaseShowMessageMassive = ILPatch.FindMethodBySignature(new[]
        {
            OpCodes.Ldarg_1,
            OpCodes.Stfld,
            OpCodes.Ldloc_0,
            OpCodes.Ldarg_2,
            OpCodes.Stfld,
            OpCodes.Ldloc_0,
            OpCodes.Ldfld,
            OpCodes.Brtrue_S,
            OpCodes.Ret,
        });

        public static void ShowMessage(
            string message,
            object color,
            int time = 5000) 
         => BaseShowMessage.Invoke(null, new object[] { message, color, time, null });
        
        public static void ShowMessageMassive(
            string message,
            int time = 5000,
            NotificationType notificationType = NotificationType.Info)
        => BaseShowMessageMassive.Invoke(null, new object[] { message, time, notificationType });
        
        // TODO: remove? only used this once..
        // see later if i use ShowMessageMassive again
        public enum NotificationType
        {
            Info,
            Warning,
            Error
        }
    }
}
