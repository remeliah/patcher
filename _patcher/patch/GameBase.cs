using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using _patcher.Helpers;
using HarmonyLib;
using _patcher.Options;
using System.Runtime.CompilerServices;
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

    [HarmonyPatch]
    internal class PatchTransition
    {
        /// <summary>
        /// Updates the transition time for the game
        /// 100 -> 200
        /// </summary>
        private static readonly OpCode[] Signature = new[]
        {
            OpCodes.Ldsfld,
            OpCodes.Ldc_I4_2,
            OpCodes.Bne_Un_S,
            OpCodes.Ldsfld,
            OpCodes.Ldc_R8,
            OpCodes.Ble_Un,
            OpCodes.Ldsfld,
            OpCodes.Call,
            OpCodes.Brfalse_S,
            OpCodes.Ldsfld,
            OpCodes.Callvirt
        };

        [HarmonyTargetMethod]
        private static MethodBase Target() => ILPatch.FindMethodBySignature(Signature);

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            codes.RemoveAt(127);
            codes.Insert(127, new CodeInstruction(OpCodes.Call,
                typeof(PatchTransition)
                .GetMethod(nameof(TransitionTime), BindingFlags.Public | BindingFlags.Static)));

            return codes.AsEnumerable();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float TransitionTime()
        {
            if (!Options.Options.config.TransitionTime) 
                return 100;

            return 200;
        }
    }
}