using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using _patcher.Helpers;

namespace _patcher.Patch
{
    /// <summary>
    /// patch relax combo break
    /// </summary>
    [HarmonyPatch]
    internal class PatchRelaxComboBreak
    {
        // osu.GameModes.Play.Rulesets.Ruleset::IncreaseScoreHit
        /* 
           if (this.#=z$kEf6vEzCgIu.#=zpQm6Nqd6BI0u() > 20 && !#=zS_AS2zptucP0wp1z7HOrPzQb$3ab.#=z98ZION9Ll4et$efEiA== && 
           !#=zS_AS2zptucP0wp1z7HOrPzQb$3ab.#=zcxdiu2iP13Mis9wLlw==)
         */
        private static readonly OpCode[] Signature = new[]
        {
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Callvirt,
            OpCodes.Ldc_I4_S,
            OpCodes.Ble_S,
            OpCodes.Ldsfld,
            OpCodes.Brtrue_S,
            OpCodes.Ldsfld,
            OpCodes.Brtrue_S
        };

        [HarmonyTargetMethod]
        private static MethodBase Target() => ILPatch.FindMethodBySignature(Signature);

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            codes.RemoveAt(1558);
            codes.InsertRange(1559, new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Or),
                new CodeInstruction(OpCodes.Call,
                    typeof(PatchRelaxComboBreak)
                    .GetMethod(nameof(PatchRelax), BindingFlags.Public | BindingFlags.Static)),
                new CodeInstruction(OpCodes.And)
            });

            return codes.AsEnumerable();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PatchRelax() => !Options.Options.config.PatchRelax;
    }
}
