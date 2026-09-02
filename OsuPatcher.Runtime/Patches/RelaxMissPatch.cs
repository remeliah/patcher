using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using OsuPatcher.Runtime.Constants;
using OsuPatcher.Runtime.Helpers;

namespace OsuPatcher.Runtime.Patches
{
    /// <summary>
    /// patch relax miss
    /// </summary>
    [HarmonyPatch]
    internal class PatchRelaxMiss
    {
        [HarmonyTargetMethod]
        private static MethodBase Target() => ILPatch.FindMethodBySignature(Patterns.PatchRelaxMiss_Target);

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            codes.RemoveAt(668);
            codes.Insert(668, new CodeInstruction(
                OpCodes.Call,
                typeof(PatchRelaxMiss)
                    .GetMethod(nameof(PatchRelax), BindingFlags.Public | BindingFlags.Static)
            ));
            codes.RemoveRange(670, 2);

            return codes.AsEnumerable();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PatchRelax() => !Options.Options.Config.PatchRelax;
    }
}
