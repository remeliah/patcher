using _patcher.Helpers;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace _patcher.Options
{
    internal class Element
    {
        public object _v { get; set; }
        public Element(object v) => _v = v;

        private static readonly MethodBase BaseSetChildren = ILPatch.FindMethodBySignature(new[]
        {
            OpCodes.Ldarg_0,
            OpCodes.Ldarg_1,
            OpCodes.Stfld,
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Brfalse_S,
            OpCodes.Ldarg_0,
            OpCodes.Ldfld,
            OpCodes.Callvirt,
            OpCodes.Stloc_0,
            OpCodes.Br_S,
            OpCodes.Ldloc_0,
            OpCodes.Callvirt,
            OpCodes.Stloc_1,
            OpCodes.Ldloc_1,
            OpCodes.Ldarg_0,
            OpCodes.Stfld,
            OpCodes.Ldarg_0,
            OpCodes.Call,
            OpCodes.Brfalse_S,
            OpCodes.Ldloc_1,
            OpCodes.Ldc_I4_1,
            OpCodes.Callvirt,
            OpCodes.Ldloc_0,
            OpCodes.Callvirt,
            OpCodes.Brtrue_S,
            OpCodes.Leave_S,
            OpCodes.Ldloc_0,
            OpCodes.Brfalse_S,
            OpCodes.Ldloc_0,
            OpCodes.Callvirt,
            OpCodes.Endfinally,

        });

        public static Array createArray(params Element[] elements)
        {
            Array array = Array.CreateInstance(elements
                .First()
                ._v.GetType()
                .BaseType, 
                elements.Length);

            for (int i = 0; i < elements.Length; i++)
                array.SetValue(elements[i]._v, i);
            
            return array;
        }

        public void SetChildren(Array children) 
            => BaseSetChildren.Invoke(_v, new object[] { children });
    }
}
