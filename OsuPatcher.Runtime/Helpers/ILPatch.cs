using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace OsuPatcher.Runtime.Helpers
{
    internal sealed class ILPatch
    {
        private static readonly Module OsuModule = AppDomain.CurrentDomain.GetAssemblies()
            .SingleOrDefault(a => a.GetName().Name == "osu!")
            .GetModules()
            .SingleOrDefault();
        
        /// <summary>
        /// Cari method pake IL Opcodes (kalo match)
        /// </summary>
        internal static MethodInfo FindMethodBySignature(OpCode[] signature)
        {
            if (signature == null || signature.Length == 0 || OsuModule == null)
                return null;

            return OsuModule.GetTypes()
                .SelectMany(t => t.GetRuntimeMethods())
                .FirstOrDefault(m => MatchesSignature(m, signature));
        }

        /// <summary>
        /// Cari constructor pake IL Opcodes (kalo match)
        /// </summary>
        public static ConstructorInfo FindConstructorBySignature(OpCode[] signature)
        {
            if (signature == null || signature.Length == 0 || OsuModule == null)
                return null;

            return OsuModule.GetTypes()
                .SelectMany(t => t.GetConstructors(
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic))
                .FirstOrDefault(ctor => MatchesSignature(ctor, signature));
        }

        internal static ConstructorInfo FindConstructorByShape(Func<ConstructorInfo, bool> predicate)
        {
            if (predicate == null || OsuModule == null)
                return null;

            return OsuModule.GetTypes()
                .SelectMany(type => type.GetConstructors(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic))
                .FirstOrDefault(predicate);
        }

        internal static bool MatchesSignature(MethodBase method, OpCode[] signature)
        {
            if (method == null || signature == null || signature.Length == 0)
                return false;

            var body = method.GetMethodBody()?.GetILAsByteArray();
            if (body == null)
                return false;

            int index = 0;
            foreach (var opcode in new ILReader(body).GetOpCodes())
            {
                if (opcode == signature[index])
                {
                    if (++index == signature.Length)
                        return true;
                }
                else
                {
                    index = opcode == signature[0] ? 1 : 0;
                }
            }

            return false;
        }
    }
}
