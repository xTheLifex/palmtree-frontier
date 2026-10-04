using System.Linq;
using Content.Shared._PS.Interactions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._PS.Organs;

/// <summary>
/// Profile-persisted genital organ configuration (one entry per <see cref="GenitalType"/>)
/// plus the semen volume produced per climax.
/// </summary>
/// <remarks>
/// Persisted in the DB as a compact string (<see cref="ToDbString"/>/<see cref="FromDbString"/>),
/// mirroring how markings are stored, so both SQLite and Postgres only need a text column.
/// </remarks>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class GenitalOrganSettings
{
    public const int DefaultSemenVolume = 30;
    public const int MinSemenVolume = 1;
    public const int MaxSemenVolume = 200;

    [DataField("organs")]
    public List<GenitalOrganData> Organs = new();

    [DataField("semenVolume")]
    public int SemenVolume = DefaultSemenVolume;

    public GenitalOrganData? Get(GenitalType type)
    {
        return Organs.FirstOrDefault(o => o.Type == type);
    }

    public void Set(GenitalType type, GenitalOrganData? data)
    {
        Organs.RemoveAll(o => o.Type == type);

        if (data != null)
        {
            data.Type = type;
            Organs.Add(data);
        }
    }

    public void Remove(GenitalType type)
    {
        Organs.RemoveAll(o => o.Type == type);
    }

    public GenitalOrganSettings Clone()
    {
        var clone = new GenitalOrganSettings
        {
            SemenVolume = SemenVolume,
        };

        foreach (var organ in Organs)
            clone.Organs.Add(organ.Clone());

        return clone;
    }

    public bool MemberwiseEquals(GenitalOrganSettings? other)
    {
        if (other == null)
            return false;

        if (SemenVolume != other.SemenVolume || Organs.Count != other.Organs.Count)
            return false;

        var ours = Organs.OrderBy(o => o.Type).ToList();
        var theirs = other.Organs.OrderBy(o => o.Type).ToList();

        for (var i = 0; i < ours.Count; i++)
        {
            if (!ours[i].MemberwiseEquals(theirs[i]))
                return false;
        }

        return true;
    }

    /// <summary>Clamps semen volume and drops entries whose catalog prototype/size no longer exists.</summary>
    public void Validate(IPrototypeManager prototypes)
    {
        SemenVolume = Math.Clamp(SemenVolume, MinSemenVolume, MaxSemenVolume);

        var valid = new List<GenitalOrganData>();
        foreach (var organ in Organs)
        {
            if (!prototypes.HasIndex<GenitalOrganPrototype>(organ.Prototype))
                continue;

            var catalog = prototypes.Index<GenitalOrganPrototype>(organ.Prototype);
            if (catalog.GenitalType != organ.Type)
                continue;

            if (organ.Size < 1 || organ.Size > catalog.Sizes.Count)
                continue;

            if (valid.Any(o => o.Type == organ.Type))
                continue;

            if (!Enum.IsDefined(organ.Visibility))
                organ.Visibility = GenitalVisibility.HiddenByJumpsuit;

            valid.Add(organ);
        }

        Organs = valid;
    }

    public string ToDbString()
    {
        var parts = new List<string>();
        foreach (var organ in Organs)
        {
            var part = $"{organ.Type}:{organ.Prototype}:{organ.Size}:{organ.Visibility}";
            if (organ.Color != null || organ.DetailColor != null)
            {
                part += $":{organ.Color?.ToHex() ?? "-"}:{organ.DetailColor?.ToHex() ?? "-"}";
            }

            parts.Add(part);
        }

        parts.Add($"semen={SemenVolume}");
        return string.Join(';', parts);
    }

    public static GenitalOrganSettings FromDbString(string? raw)
    {
        var settings = new GenitalOrganSettings();

        if (string.IsNullOrWhiteSpace(raw))
            return settings;

        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("semen=", StringComparison.Ordinal))
            {
                if (int.TryParse(part.Substring(6), out var volume))
                    settings.SemenVolume = Math.Clamp(volume, MinSemenVolume, MaxSemenVolume);
                continue;
            }

            var halves = part.Split(':');
            if (halves.Length is < 3 or > 6)
                continue;

            if (!Enum.TryParse<GenitalType>(halves[0], ignoreCase: true, out var type))
                continue;

            if (!int.TryParse(halves[2], out var size))
                continue;

            var visibility = GenitalVisibility.HiddenByJumpsuit;
            if (halves.Length >= 4 &&
                Enum.TryParse<GenitalVisibility>(halves[3], ignoreCase: true, out var parsedVisibility))
            {
                visibility = parsedVisibility;
            }

            Color? color = null;
            Color? detailColor = null;
            if (halves.Length >= 5)
                color = ParseColor(halves[4]);
            if (halves.Length >= 6)
                detailColor = ParseColor(halves[5]);

            settings.Set(type, new GenitalOrganData
            {
                Type = type,
                Prototype = halves[1],
                Size = Math.Max(1, size),
                Visibility = visibility,
                Color = color,
                DetailColor = detailColor,
            });
        }

        return settings;
    }

    private static Color? ParseColor(string value)
    {
        if (value.Length == 0 || value == "-")
            return null;

        return Color.TryFromHex(value);
    }

    public override bool Equals(object? obj)
    {
        return obj is GenitalOrganSettings other && MemberwiseEquals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SemenVolume);

        foreach (var organ in Organs.OrderBy(o => o.Type))
        {
            hash.Add(organ.Type);
            hash.Add(organ.Prototype);
            hash.Add(organ.Size);
        }

        return hash.ToHashCode();
    }
}

/// <summary>One selected genital organ: catalog type, 1-based size and visibility rule.</summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class GenitalOrganData
{
    [DataField("type")]
    public GenitalType Type;

    [DataField("prototype")]
    public string Prototype = string.Empty;

    [DataField("size")]
    public int Size = 1;

    /// <summary>SPLURT-style exposure rule for this organ.</summary>
    [DataField("visibility")]
    public GenitalVisibility Visibility = GenitalVisibility.HiddenByJumpsuit;

    /// <summary>Primary sprite color; null follows the mob's skin color.</summary>
    [DataField("color")]
    public Color? Color;

    /// <summary>Secondary/detail sprite color (e.g. nipples); null falls back to <see cref="Color"/>.</summary>
    [DataField("detailColor")]
    public Color? DetailColor;

    public GenitalOrganData Clone()
    {
        return new GenitalOrganData
        {
            Type = Type,
            Prototype = Prototype,
            Size = Size,
            Visibility = Visibility,
            Color = Color,
            DetailColor = DetailColor,
        };
    }

    public bool MemberwiseEquals(GenitalOrganData? other)
    {
        return other != null &&
               Type == other.Type &&
               Prototype == other.Prototype &&
               Size == other.Size &&
               Visibility == other.Visibility &&
               Nullable.Equals(Color, other.Color) &&
               Nullable.Equals(DetailColor, other.DetailColor);
    }
}
