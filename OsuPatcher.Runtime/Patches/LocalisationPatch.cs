using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using OsuPatcher.Runtime.Constants;
using OsuPatcher.Runtime.Helpers;

namespace OsuPatcher.Runtime.Patches
{
    /// <summary>
    /// Patches the LocalisationManager to modify string retrieval.
    /// </summary>
    [HarmonyPatch]
    public class LocalisationManager
    {
        [HarmonyTargetMethod]
        private static MethodBase Target() => ILPatch.FindMethodBySignature(Patterns.LocalisationManager_Target);

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);
            var checkSliderTitle = il.DefineLabel();
            var original = il.DefineLabel();

            codes[0].labels.Add(original);

            var sliderTitleCheck = new CodeInstruction(OpCodes.Ldarg_0);
            sliderTitleCheck.labels.Add(checkSliderTitle);
            
            codes.InsertRange(0, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldc_I4, OsuConstants.PatcherCategory),
                new CodeInstruction(OpCodes.Bne_Un_S, checkSliderTitle),
                new CodeInstruction(OpCodes.Ldstr, "Patcher"),
                new CodeInstruction(OpCodes.Ret),
                sliderTitleCheck,
                new CodeInstruction(OpCodes.Ldc_I4, OsuConstants.PerformanceCounterScale),
                new CodeInstruction(OpCodes.Bne_Un_S, original),
                new CodeInstruction(OpCodes.Ldstr, "Performance counter scale"),
                new CodeInstruction(OpCodes.Ret)
            });
            
            return codes.AsEnumerable();
        }
    }
}
