using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using _patcher.Helpers;

namespace _patcher.patch
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
            for (int i = 0; i <= codes.Count - Signature.Length; i++)
            {
                if (codes.Skip(i)
                    .Take(Signature.Length)
                    .Select((code, index) => code.opcode == Signature[index])
                    .All(match => match))
                {
                    // TODO: remove Relaxing2 too, again fuck autopilot nobody plays it
                    codes.RemoveAt(i + Signature.Length - 4); // remove ldsfld
                    codes.Insert(i + Signature.Length - 4, // insert ShowMisses 
                        new CodeInstruction(OpCodes.Call,
                        typeof(PatchRelaxComboBreak).GetMethod(nameof(PatchRelax), BindingFlags.Public | BindingFlags.Static)));

                    break;
                }
            }

            return codes.AsEnumerable();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PatchRelax() => !Options.Options.config.PatchRelax();
    }
}
