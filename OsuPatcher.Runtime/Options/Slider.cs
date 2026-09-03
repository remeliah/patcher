using System;
using System.Linq;
using System.Reflection;
using OsuPatcher.Runtime.Helpers;

namespace OsuPatcher.Runtime.Options
{
    /// <summary>
    /// Represents a slider element in the options menu.
    /// </summary>
    internal sealed class Slider : Element
    {
        private static readonly ConstructorInfo BaseSlider =
            ResolveSliderConstructor();

        internal Slider(int title, double initial, double min, double max, string unit,
            Action<double> onChanged)
            : this(CreateSlider(title, initial, min, max, unit), onChanged)
        {
        }

        private Slider(SliderState state, Action<double> onChanged)
            : base(state.Instance)
        {
            if (onChanged == null || state.ValueField == null)
                return;

            var eventField = state.Binding.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .SingleOrDefault(field => field.FieldType == typeof(EventHandler));

            if (eventField == null)
                return;

            EventHandler handler = (sender, args) =>
                onChanged(Convert.ToDouble(state.ValueField.GetValue(state.Binding)));

            var current = eventField.GetValue(state.Binding) as Delegate;
            eventField.SetValue(state.Binding, Delegate.Combine(current, handler));
        }

        private static SliderState CreateSlider(int title, double initial, double min,
            double max, string unit)
        {
            var sliderParameters = BaseSlider.GetParameters();
            Type bindingType = sliderParameters[1].ParameterType;
            var bindingConstructor = GetBindingConstructor(bindingType);

            var bindingParameters = bindingConstructor.GetParameters();
            object binding = bindingParameters.Length == 3
                ? bindingConstructor.Invoke(new object[] { initial, min, max })
                : bindingConstructor.Invoke(new object[] { initial });

            if (bindingParameters.Length == 1)
                ConfigureBindingRange(bindingType, binding, initial, min, max);

            FieldInfo valueField = bindingType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(field => field.FieldType == typeof(double))
                .OrderBy(field => field.MetadataToken)
                .FirstOrDefault();

            bindingType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(field => field.FieldType == typeof(double))
                .OrderBy(field => field.MetadataToken)
                .FirstOrDefault()
                ?.SetValue(binding, initial);

            object sliderTitle;
            if (sliderParameters[0].ParameterType.IsEnum)
                sliderTitle = Enum.ToObject(sliderParameters[0].ParameterType, title);
            else if (sliderParameters[0].ParameterType == typeof(string))
                sliderTitle = "Performance counter scale";
            else
                sliderTitle = Convert.ChangeType(title, sliderParameters[0].ParameterType);

            object instance = BaseSlider.Invoke(new[]
            {
                sliderTitle,
                binding,
                unit,
                true,
            });

            return new SliderState(instance, binding, valueField);
        }

        private static ConstructorInfo ResolveSliderConstructor()
        {
            Type baseElementType = CheckBox.BaseElementType;
            if (baseElementType == null)
                return null;

            return ILPatch.FindConstructorByShape(constructor =>
                constructor.DeclaringType?.BaseType == baseElementType &&
                IsSliderConstructor(constructor));
        }

        private static bool IsSliderConstructor(ConstructorInfo constructor)
        {
            var parameters = constructor.GetParameters();
            if (parameters.Length != 4 ||
                parameters[2].ParameterType != typeof(string) ||
                parameters[3].ParameterType != typeof(bool))
                return false;

            Type titleType = parameters[0].ParameterType;
            bool supportedTitle = titleType.IsEnum ||
                                  titleType == typeof(string) ||
                                  titleType == typeof(int);

            return supportedTitle && GetBindingConstructor(parameters[1].ParameterType) != null;
        }

        private static ConstructorInfo GetBindingConstructor(Type bindingType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            return bindingType.GetConstructor(
                       flags,
                       null,
                       new[] { typeof(double), typeof(double), typeof(double) },
                       null)
                   ?? bindingType.GetConstructor(
                       flags,
                       null,
                       new[] { typeof(double) },
                       null);
        }

        private static void ConfigureBindingRange(Type bindingType, object binding,
            double initial, double min, double max)
        {
            var doubleSetters = bindingType
                .GetMethods(BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly)
                .Where(method =>
                    method.ReturnType == typeof(void) &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType == typeof(double))
                .OrderBy(method => method.MetadataToken)
                .ToArray();

            if (doubleSetters.Length < 2)
                throw new MissingMethodException("BindableDouble range setters were not found");

            doubleSetters[0].Invoke(binding, new object[] { min });
            doubleSetters[1].Invoke(binding, new object[] { max });

            if (doubleSetters.Length >= 3)
                doubleSetters[2].Invoke(binding, new object[] { initial });
        }

        private sealed class SliderState
        {
            internal readonly object Instance;
            internal readonly object Binding;
            internal readonly FieldInfo ValueField;

            internal SliderState(object instance, object binding, FieldInfo valueField)
            {
                Instance = instance;
                Binding = binding;
                ValueField = valueField;
            }
        }
    }
}
