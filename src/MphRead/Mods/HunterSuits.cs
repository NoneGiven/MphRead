using System;
using System.Collections.Generic;


namespace MphRead.Mods
{
    /// <summary>
    /// What each of a hunter's four suits actually looks like, read out of
    /// that hunter's own model rather than written down here.
    ///
    /// The suit picker has to show a colour, and there is no table of them
    /// anywhere: a suit is <c>pal_01</c> to <c>pal_04</c> on the hunter's
    /// model, and what those palettes hold is a fact about the player's own
    /// extracted files. Naming them by hand would be wrong twice over -- the
    /// four are not the same four for every hunter (Samus's second suit and
    /// Sylux's second suit are not the same colour, and neither is red), and
    /// a colour table copied out of the cartridge is game data this
    /// repository does not carry. So it is sampled: load the palettes the
    /// game would load anyway, and work out what colour each one mostly is.
    ///
    /// Sampled once per hunter and kept, because the answer cannot change
    /// while the program is running, and because the first ask may have to
    /// read a model off disk. <c>Read</c> caches models globally, so a hunter
    /// somebody is already playing costs nothing at all.
    /// </summary>
    public static class HunterSuits
    {
        /// <summary>The neutral answer, for a hunter whose model will not load.</summary>
        private static readonly ColorRgba _unknown = new ColorRgba(150, 150, 155, 255);

        private static readonly Dictionary<Hunter, ColorRgba[]> _cache = new();

        /// <summary>
        /// The colour of one suit, 0-3. Never throws: a missing model, a
        /// model with fewer recolors than expected and a palette with nothing
        /// but greys in it all come back as the neutral colour.
        /// </summary>
        public static ColorRgba Color(Hunter hunter, int suit)
        {
            ColorRgba[] colors = Colors(hunter);
            if (suit < 0 || suit >= colors.Length)
            {
                return _unknown;
            }
            return colors[suit];
        }

        public static ColorRgba[] Colors(Hunter hunter)
        {
            if (_cache.TryGetValue(hunter, out ColorRgba[]? cached))
            {
                return cached;
            }
            var colors = new ColorRgba[Network.PlayerColors.Count];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = _unknown;
            }
            try
            {
                if (Metadata.HunterModels.TryGetValue(hunter, out IReadOnlyList<string>? models)
                    && models.Count > 0)
                {
                    Model model = Read.GetModelInstance(models[0]).Model;
                    for (int i = 0; i < colors.Length && i < model.Recolors.Count; i++)
                    {
                        colors[i] = Sample(model.Recolors[i]);
                    }
                }
            }
            catch (Exception)
            {
                // A suit swatch is not worth a match. The neutral colour is
                // already in place.
            }
            _cache[hunter] = colors;
            return colors;
        }

        /// <summary>
        /// One colour standing for a whole recolor.
        ///
        /// A hunter's palettes are mostly the armour's shading ramp plus the
        /// visor, the metal and the black between the plates, so a plain
        /// average of every entry comes out the same muddy grey for all four
        /// suits -- which is exactly the readout that would be useless. What
        /// tells them apart is the hue the armour is painted in, so each
        /// entry is weighted by how saturated it is (squared, so the greys
        /// contribute almost nothing) and the result is pushed back up to a
        /// brightness a 6-unit box can be read at.
        /// </summary>
        private static ColorRgba Sample(Recolor recolor)
        {
            double red = 0;
            double green = 0;
            double blue = 0;
            double weight = 0;
            for (int p = 0; p < recolor.PaletteData.Count; p++)
            {
                IReadOnlyList<ColorRgba> pixels = recolor.GetPalettePixels(p);
                for (int i = 0; i < pixels.Count; i++)
                {
                    ColorRgba color = pixels[i];
                    int max = Math.Max(color.Red, Math.Max(color.Green, color.Blue));
                    int min = Math.Min(color.Red, Math.Min(color.Green, color.Blue));
                    if (max == 0)
                    {
                        continue;
                    }
                    double saturation = (max - min) / (double)max;
                    double w = saturation * saturation * (max / 255.0);
                    if (w <= 0)
                    {
                        continue;
                    }
                    red += color.Red * w;
                    green += color.Green * w;
                    blue += color.Blue * w;
                    weight += w;
                }
            }
            if (weight <= 0)
            {
                return _unknown;
            }
            return Brighten(red / weight, green / weight, blue / weight);
        }

        /// <summary>
        /// The sampled hue at a brightness somebody can see. The average of a
        /// weighted set sits well below its brightest members, and a swatch
        /// six units across drawn at a third brightness reads as black
        /// whatever colour it is.
        /// </summary>
        private static ColorRgba Brighten(double red, double green, double blue)
        {
            double max = Math.Max(red, Math.Max(green, blue));
            if (max <= 0)
            {
                return _unknown;
            }
            double gain = Math.Min(235 / max, 2.2);
            return new ColorRgba(
                (byte)Math.Clamp(red * gain, 0, 255),
                (byte)Math.Clamp(green * gain, 0, 255),
                (byte)Math.Clamp(blue * gain, 0, 255),
                255);
        }

        /// <summary>
        /// What to call that colour, for the line under the swatches.
        ///
        /// Named from what was sampled rather than from the suit's number,
        /// which is the whole point of sampling: "SUIT 2" says nothing, and
        /// "BLUE" is wrong for the hunter whose second suit is green. Hue in
        /// degrees, cut into the names a person would use; anything with
        /// almost no colour in it is grey rather than a wrong hue.
        /// </summary>
        public static string Name(ColorRgba color)
        {
            double r = color.Red / 255.0;
            double g = color.Green / 255.0;
            double b = color.Blue / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;
            if (max <= 0.001 || delta / max < 0.18)
            {
                return max > 0.7 ? "WHITE" : max > 0.3 ? "GREY" : "BLACK";
            }
            double hue;
            if (max == r)
            {
                hue = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                hue = 60 * ((b - r) / delta + 2);
            }
            else
            {
                hue = 60 * ((r - g) / delta + 4);
            }
            if (hue < 0)
            {
                hue += 360;
            }
            if (hue < 15 || hue >= 330)
            {
                return "RED";
            }
            if (hue < 45)
            {
                return "ORANGE";
            }
            if (hue < 70)
            {
                return "YELLOW";
            }
            if (hue < 160)
            {
                return "GREEN";
            }
            if (hue < 200)
            {
                return "CYAN";
            }
            if (hue < 260)
            {
                return "BLUE";
            }
            if (hue < 300)
            {
                return "PURPLE";
            }
            return "PINK";
        }
    }
}
