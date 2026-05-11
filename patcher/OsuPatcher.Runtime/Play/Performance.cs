using OsuPatcher.Runtime.Graphics;
using OsuPatcher.Runtime.Rosu;
using System;
using System.Linq;
using System.Reflection;

namespace OsuPatcher.Runtime.Play
{
    internal class Performance
    {
        private readonly Calculator _ppCalculator;
        private readonly pSpriteText[] _ppSpriteTexts;
        private int currentPP;
        private int previousPP;
        private int lastScoreState;
        private bool hasScoreState;

        public Performance(object beatmap, MethodInfo beatmapStream, uint mods, pSpriteText[] spriteTexts)
            : this(beatmap, beatmapStream, mods)
        {
            _ppSpriteTexts = spriteTexts;
        }

        public Performance(object beatmap, MethodInfo beatmapStream, uint mods)
        {
            _ppCalculator = new Calculator(beatmap, beatmapStream, mods);
        }

        public void UpdatePerformance(object score, float accuracy, int legacyScore, int maxCombo, int playMode)
        {
            int scoreState = GetScoreState(score, accuracy, legacyScore, maxCombo, playMode);

            if (!hasScoreState || lastScoreState != scoreState)
            {
                double pp = _ppCalculator.CalculateScore(score, accuracy, legacyScore, maxCombo, playMode);

                if (double.IsNaN(pp) || double.IsInfinity(pp) || pp < 0.0)
                    pp = 0.0;

                currentPP = (int)Math.Round(pp);
                lastScoreState = scoreState;
                hasScoreState = true;
            }

            if (previousPP != currentPP && _ppSpriteTexts != null)
                foreach (var spriteText in _ppSpriteTexts)
                    spriteText.Text = currentPP.ToString();

            previousPP = currentPP;
        }

        private static int GetScoreState(object score, float accuracy, int legacyScore, int maxCombo, int playMode)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + accuracy.GetHashCode();
                hash = hash * 31 + legacyScore;
                hash = hash * 31 + maxCombo;
                hash = hash * 31 + playMode;

                foreach (var field in score.GetType()
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(f => f.FieldType == typeof(ushort)))
                {
                    hash = hash * 31 + (ushort)field.GetValue(score);
                }

                return hash;
            }
        }
    }
}
