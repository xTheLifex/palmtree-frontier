using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Humanoid.Markings
{
    [DataDefinition]
    [Serializable, NetSerializable]
    public sealed partial class Marking : IEquatable<Marking>, IComparable<Marking>, IComparable<string>
    {
        [DataField("markingColor")]
        private List<Color> _markingColors = new();

        // Palmtree/Coyote Start: advanced marking editor data
        [DataField("scale")]
        private float _markingScale = 1.0f;

        [DataField("offsetX")]
        private float _markingOffsetX;

        [DataField("offsetY")]
        private float _markingOffsetY;

        [DataField("glowLevels")]
        private List<float> _markingGlow = new();

        [DataField("glow")]
        private float _legacyGlow;
        // Palmtree/Coyote End

        private Marking()
        {
        }

        public Marking(string markingId,
            List<Color> markingColors)
        {
            MarkingId = markingId;
            _markingColors = markingColors;
            _markingGlow = CreateGlowLevels(markingColors.Count);
        }

        public Marking(string markingId,
            IReadOnlyList<Color> markingColors)
            : this(markingId, new List<Color>(markingColors))
        {
        }

        public Marking(string markingId, int colorCount)
        {
            MarkingId = markingId;
            List<Color> colors = new();
            for (int i = 0; i < colorCount; i++)
                colors.Add(Color.White);
            _markingColors = colors;
            _markingGlow = CreateGlowLevels(colorCount);
        }

        public Marking(Marking other)
        {
            MarkingId = other.MarkingId;
            _markingColors = new(other.MarkingColors);
            Visible = other.Visible;
            Forced = other.Forced;
            // Palmtree/Coyote Start
            _markingScale = other._markingScale;
            _markingOffsetX = other._markingOffsetX;
            _markingOffsetY = other._markingOffsetY;
            _markingGlow = new(other.MarkingGlow);
            _legacyGlow = other._legacyGlow;
            // Palmtree/Coyote End
        }

        // Palmtree/Coyote: copies a marking while resizing its color list.
        public Marking(Marking marking, int colorCount) : this(marking)
        {
            List<Color> colors = new();
            for (int i = 0; i < colorCount; i++)
                colors.Add(Color.White);
            _markingColors = colors;
            _markingGlow = NormalizeGlowLevels(marking.MarkingGlow, colorCount, marking._legacyGlow);
        }

        // Palmtree/Coyote: copy a marking with a new color list, preserving scale/offset/glow.
        public Marking(Marking marking, List<Color> markingColors) : this(marking)
        {
            _markingColors = markingColors;
            _markingGlow = NormalizeGlowLevels(marking.MarkingGlow, markingColors.Count, marking._legacyGlow);
        }

        public Marking(Marking marking, IReadOnlyList<Color> markingColors) : this(marking)
        {
            _markingColors = new(markingColors);
            _markingGlow = NormalizeGlowLevels(marking.MarkingGlow, _markingColors.Count, marking._legacyGlow);
        }

        /// <summary>
        ///     ID of the marking prototype.
        /// </summary>
        [DataField("markingId", required: true)]
        public string MarkingId { get; private set; } = default!;

        /// <summary>
        ///     All colors currently on this marking.
        /// </summary>
        [ViewVariables]
        public IReadOnlyList<Color> MarkingColors => _markingColors;

        // Palmtree/Coyote Start: advanced marking editor
        [ViewVariables]
        public float MarkingScale => _markingScale;

        [ViewVariables]
        public Vector2 MarkingOffset => new(_markingOffsetX, _markingOffsetY);

        [ViewVariables]
        public IReadOnlyList<float> MarkingGlow => _markingGlow;
        // Palmtree/Coyote End

        /// <summary>
        ///     If this marking is currently visible.
        /// </summary>
        [DataField("visible")]
        public bool Visible = true;

        /// <summary>
        ///     If this marking should be forcefully applied, regardless of points.
        /// </summary>
        [ViewVariables]
        public bool Forced;

        public void SetColor(int colorIndex, Color color) =>
            _markingColors[colorIndex] = color;

        // Palmtree/Coyote Start: advanced marking editor
        public void SetScale(float scale)
        {
            _markingScale = Math.Clamp(scale, 0.1f, 4.0f);
        }

        public void SetOffset(float x, float y)
        {
            _markingOffsetX = Math.Clamp(x, -2f, 2f);
            _markingOffsetY = Math.Clamp(y, -2f, 2f);
        }

        public void SetGlow(int glowIndex, float glow)
        {
            if (glowIndex < 0 || glowIndex >= _markingGlow.Count)
                return;

            var normalizedGlow = Math.Clamp(glow, 0f, 1f);
            _markingGlow[glowIndex] = normalizedGlow;
            _legacyGlow = normalizedGlow;
        }
        // Palmtree/Coyote End

        public void SetColor(Color color)
        {
            for (int i = 0; i < _markingColors.Count; i++)
            {
                _markingColors[i] = color;
            }
        }

        public int CompareTo(Marking? marking)
        {
            if (marking == null)
            {
                return 1;
            }

            return string.Compare(MarkingId, marking.MarkingId, StringComparison.Ordinal);
        }

        public int CompareTo(string? markingId)
        {
            if (markingId == null)
                return 1;

            return string.Compare(MarkingId, markingId, StringComparison.Ordinal);
        }

        public bool Equals(Marking? other)
        {
            if (other == null)
            {
                return false;
            }
            return MarkingId.Equals(other.MarkingId)
                && _markingColors.SequenceEqual(other._markingColors)
                && _markingGlow.SequenceEqual(other._markingGlow)
                && Visible.Equals(other.Visible)
                && Forced.Equals(other.Forced)
                // Palmtree/Coyote Start
                && _markingScale == other._markingScale
                && _markingOffsetX == other._markingOffsetX
                && _markingOffsetY == other._markingOffsetY;
                // Palmtree/Coyote End
        }

        // VERY BIG TODO: TURN THIS INTO JSONSERIALIZER IMPLEMENTATION


        // look this could be better but I don't think serializing
        // colors is the correct thing to do
        //
        // this is still janky imo but serializing a color and feeding
        // it into the default JSON serializer (which is just *fine*)
        // doesn't seem to have compatible interfaces? this 'works'
        // for now but should eventually be improved so that this can,
        // in fact just be serialized through a convenient interface
        new public string ToString()
        {
            // reserved character
            string sanitizedName = this.MarkingId.Replace('@', '_');
            List<string> colorStringList = new();
            foreach (Color color in _markingColors)
                colorStringList.Add(color.ToHex());

            var result = $"{sanitizedName}@{String.Join(',', colorStringList)}";

            // Palmtree/Coyote: append advanced editor data only when it differs from defaults,
            // so old saved strings remain valid. The glow segment is prefixed with 'g' so it is
            // distinguishable from the transform segment.
            if (_markingScale != 1.0f || _markingOffsetX != 0f || _markingOffsetY != 0f)
            {
                result += "@"
                    + _markingScale.ToString(CultureInfo.InvariantCulture) + ","
                    + _markingOffsetX.ToString(CultureInfo.InvariantCulture) + ","
                    + _markingOffsetY.ToString(CultureInfo.InvariantCulture);
            }

            if (_markingGlow.Any(glow => glow > 0f))
            {
                result += "@g" + String.Join(',', _markingGlow.Select(
                    glow => glow.ToString(CultureInfo.InvariantCulture)));
            }

            return result;
        }

        public static Marking? ParseFromDbString(string input)
        {
            if (input.Length == 0) return null;
            var split = input.Split('@');
            if (split.Length is < 2 or > 4) return null;
            List<Color> colorList = new();
            foreach (string color in split[1].Split(','))
                colorList.Add(Color.FromHex(color));

            var marking = new Marking(split[0], colorList);

            // Palmtree/Coyote: optional advanced editor data.
            var nextSegment = 2;
            if (split.Length > 2 && !split[2].StartsWith('g'))
            {
                var transform = split[2].Split(',');
                if (transform.Length == 3
                    && float.TryParse(transform[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)
                    && float.TryParse(transform[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetX)
                    && float.TryParse(transform[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetY))
                {
                    marking.SetScale(scale);
                    marking.SetOffset(offsetX, offsetY);
                }

                nextSegment = 3;
            }

            if (split.Length > nextSegment && split[nextSegment].StartsWith('g'))
            {
                var glowValues = split[nextSegment][1..].Split(',');
                for (var i = 0; i < glowValues.Length; i++)
                {
                    if (float.TryParse(glowValues[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var glow))
                        marking.SetGlow(i, glow);
                }
            }

            return marking;
        }

        // Palmtree/Coyote: glow helpers.
        private static List<float> CreateGlowLevels(int count)
        {
            var glowLevels = new List<float>(count);
            for (var i = 0; i < count; i++)
            {
                glowLevels.Add(0f);
            }

            return glowLevels;
        }

        private static List<float> NormalizeGlowLevels(IEnumerable<float>? source, int count, float fallback)
        {
            var normalizedFallback = Math.Clamp(fallback, 0f, 1f);
            var sourceList = source?.Select(value => Math.Clamp(value, 0f, 1f)).ToList() ?? new List<float>();

            if (sourceList.Count > count)
                sourceList.RemoveRange(count, sourceList.Count - count);

            while (sourceList.Count < count)
            {
                sourceList.Add(normalizedFallback);
            }

            return sourceList;
        }
    }
}
