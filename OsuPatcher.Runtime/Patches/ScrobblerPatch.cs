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
    /// patch trackgrabber
    /// </summary>
    [HarmonyPatch]
    internal class ScrobblerPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase Target() => ILPatch.FindMethodBySignature(Patterns.Scrobbler_Target);

        /// <summary>
        /// NOTE: this disables Lastfm (Scrobbler::Update) entirely.
        ///       Lastfm Scrobbler is basically a hidden "anticheat", 
        ///       the client sents flag 1 << 9 to the server because of this patcher, i don't want that since its annoying.
        ///       note that this most likely ban-able on some server if the server handles lastfm.
        /// </summary>
        [HarmonyPrefix]
        private static bool Prefix() => false;
    }
}
