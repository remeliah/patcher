using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace _patcher.Helpers
{
    internal sealed class ILPatch
    {
        private static readonly Module OsuModule = AppDomain.CurrentDomain.GetAssemblies()
            .SingleOrDefault(a => a.GetName().Name == "osu!")
            .GetModules()
            .SingleOrDefault();

        #region goblok
        internal static MethodInfo FindMethodBySignature(OpCode[] signature) =>
            signature.Length <= 0 ? null : OsuModule.GetTypes()
                .SelectMany(type => type.GetRuntimeMethods())
                .FirstOrDefault(method =>
                    new ILReader(method.GetMethodBody()?.GetILAsByteArray() ?? Array.Empty<byte>())
                        .GetOpCodes()
                        .Aggregate(0, (seq, op) =>
                            // check kalo urutan opcode cocok sama signature
                            seq == signature.Length ? seq : // kalo cocok semua, stop
                                op == signature[seq] ? seq + 1 : // kalo cocok (tapi blm smua), lanjutin ke opcode lain\
                                0) == signature.Length // kalo urutan opcode udh sesuai sama signature, return method
                );

        public static ConstructorInfo FindConstructorBySignature(OpCode[] signature) =>
            // logicnya sama kaya FindMethodBySignature, cuma ini buat constructor
            signature.Length <= 0 ? null : OsuModule.GetTypes()
                .SelectMany(t => t.GetConstructors(BindingFlags.Instance | 
                                                    BindingFlags.Static | 
                                                    BindingFlags.Public | 
                                                    BindingFlags.NonPublic))
                .FirstOrDefault(m => m.GetMethodBody()?.GetILAsByteArray() is byte[] instr &&
                    new ILReader(instr)
                        .GetOpCodes()
                        .Aggregate((0, false), (acc, op) =>
                                acc.Item2 ? acc : (op == signature[acc.Item1] ?
                                    (acc.Item1 + 1 >= signature.Length ? (acc.Item1, true) : (acc.Item1 + 1, false)) :
                                    (0, false))).Item2
                );
        #endregion
    }
}
