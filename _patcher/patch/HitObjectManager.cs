using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using _patcher.Helpers;

namespace _patcher.patch
{
    /// <summary>
    /// replace Beatmap.DifficultyCircleSize to 0.0
    /// (double)this.#=zKxNpgX8O2MNb.#=zRaUzLuImJpsHEkro2uz63yCfU9xY => 0.0
    /// TODO: this shouldnt exist? used this only to have fun offline..
    /// maybe remove or.. put this on options in the future
    /// </summary>
    /*
    [HarmonyPatch]
    internal class HitObjectManager
    {
        // osu.GameplayElements.HitObjectManager:UpdateVariables
        // #=zw2qkIgvffJvjpi_MXw== =
        // (float)((double)(#=zQMixGeiZApFL2kp9eA==.#=zn9PStvb_ADQG.#=zwIRECVYCr9zN() / 8f) *
        // (1.0 - 0.699999988079071 * \u0005\u2000\u2001\u2006\u2003\u2005\u200A\u2000\u2009\u2002\u2008\u2003\u2007\u2004\u2005\u2002\u2006\u2009(
        // (double)this.#=zKxNpgX8O2MNb.#=zRaUzLuImJpsHEkro2uz63yCfU9xY)));
        private static readonly OpCode[] Signature =
        {
            OpCodes.Ldarg_0,
            OpCodes.Ldsfld,
            OpCodes.Callvirt,
            OpCodes.Ldc_R4,
            OpCodes.Div,
            OpCodes.Conv_R8,
            OpCodes.Ldc_R8,
            OpCodes.Ldc_R8,
            OpCodes.Ldarg_0,
            OpCodes.Ldarg_0,
            OpCodes.Ldfld, // beatmap
            OpCodes.Ldfld, // difficultycirclesize
            OpCodes.Conv_R8,
            OpCodes.Callvirt
        };

        [HarmonyTargetMethod]
        private static MethodBase Target() => ILPatch.FindMethodBySignature(Signature);

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            // insert call after convr8
            codes.Insert(86, 
                new CodeInstruction(OpCodes.Call,
                typeof(HitObjectManager).GetMethod(nameof(CSChange)))
            );

            return codes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CSChange(double DifficultyCircleSize)
        {
            // AASPJFAFOPKAJSIOFJOASIJFIOASJFOPASOF
            if (Options.Options.config.csChange)
                return 0.0; // maybe in the future im gon use with OptionSlider / Textbox

            return DifficultyCircleSize;
        }
    }
    */

    /// <summary>
    /// patch relax miss
    /// </summary>
    [HarmonyPatch]
    internal class PatchRelaxMiss
    {
        // osu.GameplayElements.HitObjectManager::Hit
        /*
           if (#=zM8XZInS60bPkwTMTkrfebdLZOZcSyFcV90Pe2PH5kYLP == (#=zM8XZInS60bPkwTMTkrfebdLZOZcSyFcV90Pe2PH5kYLP)(-131072) && 
           !#=zS_AS2zptucP0wp1z7HOrPzQb$3ab.#=z98ZION9Ll4et$efEiA== && 
           !#=zS_AS2zptucP0wp1z7HOrPzQb$3ab.#=zcxdiu2iP13Mis9wLlw==) 
         */
        private static readonly OpCode[] Signature = new[]
        {
            OpCodes.Ldarg_1,
            OpCodes.Ldc_I4_8,
            OpCodes.Callvirt,
            OpCodes.Stloc_S,
            OpCodes.Ldloc_0,
            OpCodes.Ldc_I4,
            OpCodes.Bne_Un,
            OpCodes.Ldsfld,
            OpCodes.Brtrue,
            OpCodes.Ldsfld,
            OpCodes.Brtrue
        };

        [HarmonyTargetMethod]
        private static MethodBase Target() => ILPatch.FindMethodBySignature(Signature);

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            codes.RemoveAt(664);
            codes.InsertRange(665, new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Or),
                new CodeInstruction(OpCodes.Call,
                    typeof(PatchRelaxMiss)
                    .GetMethod(nameof(PatchRelax), BindingFlags.Public | BindingFlags.Static)),
                new CodeInstruction(OpCodes.And)
            });

            return codes.AsEnumerable();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PatchRelax() => !Options.Options.config.PatchRelax;
    }
}