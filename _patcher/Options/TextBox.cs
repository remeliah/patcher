using System;
using System.Reflection;
using System.Reflection.Emit;
using _patcher.Helpers;

namespace _patcher.Options
{
    internal class TextBox : Element
    {
        private static ConstructorInfo bindableCons;

        private static readonly ConstructorInfo BaseTextBox = ILPatch.FindConstructorBySignature(new[]
        {
            OpCodes.Ldarg_0,
            OpCodes.Ldarg_1,
            OpCodes.Ldarg_3,
            OpCodes.Call,
            OpCodes.Ldloc_0,
            OpCodes.Ldarg_0,
            OpCodes.Stfld,
            OpCodes.Ldarg_0,
            OpCodes.Ldloc_0,
            OpCodes.Ldfld,
            OpCodes.Stfld
        });

        public TextBox(string title, string initial, bool passwordBox = false)
            : base(CreateTextBoxInstance(title, initial, passwordBox))
        {
        }

        private static object CreateTextBoxInstance(string title, string initial, bool passwordBox = false)
        {
            if (bindableCons == null)
            {
                bindableCons = BaseTextBox
                    .GetParameters()[1]
                    .ParameterType
                    .GetConstructor(new[]
                    {
                        typeof(string)
                    });
            }

            object str = bindableCons.Invoke(new object[] { initial });

            return BaseTextBox.Invoke(new object[]
            {
                title,
                str,
                passwordBox
            });
        }
    }
}