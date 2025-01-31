using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using _patcher.Helpers;
namespace _patcher.patch
{
    internal class GameBase
    {
        // osu.GameBase:BeginExit
        // #=zTp6JhLFlT$nSTXDxMw==:#=zzb6bonY=
        private static readonly MethodBase BaseBeginExit = ILPatch.FindMethodBySignature(new[]
        {
            OpCodes.Ldsfld,
            OpCodes.Ldfld,
            OpCodes.Ldsfld,
            OpCodes.Dup,
            OpCodes.Brtrue_S,
            OpCodes.Pop,
            OpCodes.Ldsfld,
            OpCodes.Ldftn,
            OpCodes.Newobj,
            OpCodes.Dup,
            OpCodes.Stsfld,
            OpCodes.Callvirt,
            OpCodes.Callvirt,
            OpCodes.Ldc_I4_0
        });
        
        public static void BeginExit(
            bool forceConfirm = false)
         => BaseBeginExit.Invoke(null, new object[] { forceConfirm });
    }
}