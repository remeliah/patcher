using System;
using OsuPatcher.Shared;

namespace OsuPatcher.Runtime
{
    internal class Config
    {
        public bool PatchRelax { get; set; } = true;
        public bool TransitionTime { get; set; } = true;
        public bool PerformanceCalculator { get; set; } = true;
        public double PerformanceCounterScale { get; set; } = 1.1;

        internal static Config _load()
        {
            ConfigStore store = ConfigStore.Load();
            return new Config
            {
                PatchRelax = store.GetBool(nameof(PatchRelax), true),
                TransitionTime = store.GetBool(nameof(TransitionTime), true),
                PerformanceCalculator = store.GetBool(nameof(PerformanceCalculator), true),
                PerformanceCounterScale = store.GetDouble(nameof(PerformanceCounterScale), 1.1)
            };
        }

        private void ToggleSetting(string propName)
        {
            // HACK: FLIP the value of the auto property directly
            var prop = GetType()
                .GetProperty(propName);
            
            if (prop == null) return;

            var curr = (bool)prop.GetValue(this);
            prop.SetValue(this, !curr);

            ConfigStore.Update(ConfigStore.DefaultPath, propName, !curr);
        }

        public void TogglePatchRelax(object sender, EventArgs e) => ToggleSetting(nameof(PatchRelax));
        public void ToggleTransitionTime(object sender, EventArgs e) => ToggleSetting(nameof(TransitionTime));
        public void TogglePerformanceCalculator(object sender, EventArgs e) => ToggleSetting(nameof(PerformanceCalculator));

        public void SetPerformanceCounterScale(double value)
        {
            PerformanceCounterScale = value;
            ConfigStore.Update(ConfigStore.DefaultPath, nameof(PerformanceCounterScale), value);
        }
    }
}
