using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Runtime.CompilerServices;
using _patcher.Helpers;
using HarmonyLib;

namespace _patcher.patch
{
    [HarmonyPatch]
    internal class Score
    {
        private static readonly OpCode[] Signature = new[]
        {
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Brfalse_S,
            OpCodes.Ldarg_0,
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Call,
            OpCodes.Ldc_I4,
            OpCodes.Or,
            OpCodes.Call,
            OpCodes.Stfld
        };

        [HarmonyTargetMethod]
        private static MethodBase Target() => ILPatch.FindMethodBySignature(Signature);

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            /*
             if (this.#=zt1XLrypEA3vr() > (#=zpwwvYWpMgGaNCsHugSYKgXwSvTSPg_JVvsC4YZJNNDvu)0)
		        {
			        return;
		        }
            we insert DisableScoreSubmission to the if statement so,
            it prevents submitting the cs0 score
             */
            var codes = new List<CodeInstruction>(instructions);
            var idx = codes.FindIndex(i => i.opcode == OpCodes.Ble_S);

            if (idx != -1)
            {
                var retlabel = codes[idx].operand;
                codes.InsertRange(idx + 1, new[]
                {
                    new CodeInstruction(OpCodes.Call, typeof(Score).GetMethod(nameof(DisableScoreSubmission))),
                    new CodeInstruction(OpCodes.Brtrue_S, retlabel)
                });
            }

            return codes.AsEnumerable();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DisableScoreSubmission() => !Options.Options.config.csChange;
    }
}
