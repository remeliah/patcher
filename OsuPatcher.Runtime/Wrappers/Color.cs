using OsuPatcher.Runtime.Helpers;

namespace OsuPatcher.Runtime.Wrappers
{
    internal struct Color
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public Color(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
            A = 255;
        }

        public static readonly Color White = new Color(255, 255, 255);

        internal object ToXnaColor()
            => Xna.CreateColor(R, G, B, A);
    }
}
