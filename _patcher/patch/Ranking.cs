using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using _patcher.Helpers;
using System.Runtime.CompilerServices;

namespace _patcher.patch
{
    /// <summary>
    /// patch relax's ranking score screen
    /// </summary>
    [HarmonyPatch]
    internal class PatchAutoSaveRelaxScores
    {
        // osu.GameModes.Ranking.Ranking::loadLocalUserScore
        /* 	
            Mods mods = #=zS_AS2zptucP0wp1z7HOrPzQb$3ab.#=zT1XDrLO5K8XN.#=zYI457PYyRc56;
			Mods mods2 = Mods.Relax;
			Mods mods3 = mods;
			if ((mods3 & mods2) <= Mods.None)
         */
        private static readonly OpCode[] Signature =
        {
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Ldfld,
            OpCodes.Brfalse,
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Brtrue,
            OpCodes.Ldsfld,
            OpCodes.Ldfld,
            OpCodes.Call,
            OpCodes.Ldc_I4,
            OpCodes.Stloc_2,
            OpCodes.Stloc_1,
            OpCodes.Ldloc_1,
            OpCodes.Ldloc_2,
            OpCodes.And,
            OpCodes.Ldc_I4_0,
            OpCodes.Cgt,
            OpCodes.Brtrue,
            OpCodes.Ldsfld,
            OpCodes.Ldfld,
            OpCodes.Call,
            OpCodes.Ldc_I4,
            OpCodes.Stloc_2,
            OpCodes.Stloc_1,
            OpCodes.Ldloc_1,
            OpCodes.Ldloc_2,
            OpCodes.And,
            OpCodes.Ldc_I4_0,
            OpCodes.Cgt,
            OpCodes.Brtrue
        };

        [HarmonyTargetMethod]
        private static MethodBase Target() => ILPatch.FindMethodBySignature(Signature);

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            codes.InsertRange(33, new[]
            {
                new CodeInstruction(OpCodes.Call, typeof(PatchAutoSaveRelaxScores)
                    .GetMethod(nameof(PatchRelax), BindingFlags.Public | BindingFlags.Static)),
                new CodeInstruction(OpCodes.And)
            });

            codes.InsertRange(21, new[]
            {
                new CodeInstruction(OpCodes.Call, typeof(PatchAutoSaveRelaxScores)
                    .GetMethod(nameof(PatchRelax), BindingFlags.Public | BindingFlags.Static)),
                new CodeInstruction(OpCodes.And)
            });

            return codes.AsEnumerable();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PatchRelax() => !Options.Options.config.PatchRelax;
    }
}
