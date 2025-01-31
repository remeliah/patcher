using System;
using System.Reflection;
using System.Reflection.Emit;
using _patcher.Helpers;

namespace _patcher.Options
{
    internal class Section : Element
    {
        private static readonly ConstructorInfo BaseSection = ILPatch.FindConstructorBySignature(new[]
        {
            OpCodes.Ldarg_0,
            OpCodes.Ldarg_1,
            OpCodes.Callvirt,
            OpCodes.Ldc_R4,
            OpCodes.Ldc_R4,
            OpCodes.Ldc_R4,
            OpCodes.Newobj,
            OpCodes.Ldc_R4,
            OpCodes.Ldc_I4_1,
        });

        public Section(string title) 
            : base(CreateSectionInstance(title))
        {
        }

        private static object CreateSectionInstance(string title) => 
            BaseSection.Invoke(new object[] { title, null });
    }
}