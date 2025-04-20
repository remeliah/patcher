using System;
using System.Reflection;
using _patcher.utils;
using System.Reflection.Emit;
using HarmonyLib;
using _patcher.Helpers;

namespace _patcher.Options
{
    internal class Options
    {
        public static Config config = Config._load();
        public static void InitializeOptions(object instance)
        {
            CheckBox alwaysShowMisses = new CheckBox("Patch Relax/Autopilot",
                "Removes relax/autopilot limitation, Allows you to see miss, Combo break sound and ranking panel.",
                config.PatchRelax,
                new EventHandler(config.TogglePatchRelax));
            TextBox transitionTime = new TextBox("Transition Time",
                config.transitionTime.ToString());
            /*
            CheckBox csChanger = new CheckBox("0.0 Circle Size",
                "Makes Circle Size on maps to 0 (Disables score submission!)",
                config.csChange,
                new EventHandler(config.ToggleCsChange));

            CheckBox disableScoreSub = new CheckBox("Disable Score Submission",
                "Prevents score submitting when activated.",
                config.DisableScoreSubmission,
                new EventHandler(config.ToggleDisableScoreSub));
            */
            Array optionsChildren = Element.createArray(
                alwaysShowMisses,
                transitionTime);
                //csChanger,
                //disableScoreSub);

            Section section = new Section("Patches");
            section.SetChildren(optionsChildren);
            Array sectionChildren = Element.createArray(section);
            Category category = new Category(FontAwesome.heart_o, OsuString.TabGameplay);
            category.SetChildren(sectionChildren);

            // add to elements
            Add(instance, category);
        }

        public static void ReloadElements(object instance, bool jumptoTop = true)
            => BaseReloadElements.Invoke(instance, new object[] { jumptoTop });

        public static void Add(object instance, Element element)
            => BaseAddElement.Invoke(instance, new object[] { element._v });

        #region optsign
        private static readonly MethodBase BaseReloadElements = ILPatch.FindMethodBySignature(new[]
        {
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Brtrue_S,
            OpCodes.Ret,
            OpCodes.Ldarg_0,
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
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Callvirt,
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Callvirt,
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Ldc_I4_1,
            OpCodes.Callvirt,
            OpCodes.Ldarg_0,
            OpCodes.Call,
            OpCodes.Ldarg_0,
            OpCodes.Call,
            OpCodes.Ret
        });

        private static readonly MethodBase BaseAddElement = ILPatch.FindMethodBySignature(new[]
        {
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Ldarg_1,
            OpCodes.Callvirt,
            OpCodes.Ldarg_1,
            OpCodes.Callvirt,
            OpCodes.Brfalse_S,
            OpCodes.Ldarg_1,
            OpCodes.Callvirt,
            OpCodes.Callvirt,
            OpCodes.Stloc_0,
            OpCodes.Br_S,
            OpCodes.Ldloc_0,
            OpCodes.Callvirt,
            OpCodes.Stloc_1,
            OpCodes.Ldloc_1,
            OpCodes.Ldarg_0,
            OpCodes.Ldftn,
            OpCodes.Newobj,
            OpCodes.Callvirt,
            OpCodes.Ldarg_0,
            OpCodes.Ldloc_1,
            OpCodes.Call,
            OpCodes.Ldloc_0,
            OpCodes.Callvirt,
            OpCodes.Brtrue_S,
            OpCodes.Leave_S,
            OpCodes.Ldloc_0,
            OpCodes.Brfalse_S,
            OpCodes.Ldloc_0,
            OpCodes.Callvirt,
            OpCodes.Endfinally,
            OpCodes.Ldarg_1,
            OpCodes.Isinst,
            OpCodes.Stloc_2,
            OpCodes.Ldloc_2,
            OpCodes.Brfalse_S,
            OpCodes.Ldloc_2,
            OpCodes.Ldarg_0,
            OpCodes.Ldftn,
            OpCodes.Newobj,
            OpCodes.Newobj,
            OpCodes.Stloc_3,
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Ldloc_3,
            OpCodes.Callvirt,
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Ldloc_3,
            OpCodes.Ldfld,
            OpCodes.Callvirt,
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Ldarg_1,
            OpCodes.Ldfld,
            OpCodes.Callvirt,
            OpCodes.Ret,
        });
        #endregion
    }

    [HarmonyPatch]
    internal class PatchOptionsMenu
    {
        private static readonly OpCode[] Signature = new OpCode[]
        {
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Ldc_I4_S,
            OpCodes.Call,
            OpCodes.Callvirt,
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Ldc_I4_S,
            OpCodes.Call,
            OpCodes.Callvirt,
        };

        [HarmonyTargetMethod]
        private static MethodBase Target() => ILPatch.FindMethodBySignature(Signature);

        [HarmonyPostfix]
        private static void Postfix(object __instance)
        {
            try
            {
                Options.InitializeOptions(__instance);

                // TODO: fix crashes the game?
                // maybe need to sleep for a few seconds or
                // maybe need to call this on a different thread
                // Options.ReloadElements(true);
            }
            catch (Exception ex)
            {
                Logger.log(ex.ToString());
            }
        }
    }
}