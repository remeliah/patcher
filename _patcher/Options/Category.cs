using _patcher.Helpers;
using System.Reflection;
using System.Reflection.Emit;

namespace _patcher.Options
{
    internal class Category : Element
    {
        private static readonly ConstructorInfo BaseCategoryConstructor = ILPatch.FindConstructorBySignature(new[]
        {
            OpCodes.Ldarg_0,
            OpCodes.Ldarga_S,
            OpCodes.Constrained,
            OpCodes.Callvirt,
            OpCodes.Call,
            OpCodes.Ldarg_0,
            OpCodes.Ldarga_S,
            OpCodes.Constrained,
            OpCodes.Callvirt,
            OpCodes.Call
        });

        public Category(FontAwesome icon, OsuString title)
            : base(CreateCategoryInstance(icon, title))
        {
        }

        private static object CreateCategoryInstance(FontAwesome icon, OsuString title)
            => BaseCategoryConstructor.Invoke(new object[] { title, icon });
    }
}