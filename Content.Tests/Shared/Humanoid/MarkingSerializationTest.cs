using System.Collections.Generic;
using Content.Shared.Humanoid.Markings;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.Humanoid;

[TestFixture]
[TestOf(typeof(Marking))]
public sealed class MarkingSerializationTest
{
    [Test]
    public void DbStringRoundTripDefaults()
    {
        var marking = new Marking("GenitalVaginaHuman", new List<Color> { Color.Red, Color.Blue });

        var serialized = marking.ToString();
        var parsed = Marking.ParseFromDbString(serialized);

        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed!.MarkingId, Is.EqualTo(marking.MarkingId));
        Assert.That(parsed.MarkingColors, Is.EqualTo(marking.MarkingColors));
        Assert.That(parsed.MarkingScale, Is.EqualTo(1.0f));
        Assert.That(parsed.MarkingOffset, Is.EqualTo(System.Numerics.Vector2.Zero));
    }

    [Test]
    public void DbStringRoundTripTransform()
    {
        var marking = new Marking("GenitalVaginaHuman", new List<Color> { Color.White });
        marking.SetScale(1.75f);
        marking.SetOffset(0.25f, -0.5f);

        var serialized = marking.ToString();
        var parsed = Marking.ParseFromDbString(serialized);

        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed!.MarkingScale, Is.EqualTo(1.75f).Within(0.0001f));
        Assert.That(parsed.MarkingOffset.X, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(parsed.MarkingOffset.Y, Is.EqualTo(-0.5f).Within(0.0001f));
    }

    [Test]
    public void DbStringLegacyFormatIsStillValid()
    {
        var parsed = Marking.ParseFromDbString("GenitalVaginaHuman@#ffffff");

        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed!.MarkingId, Is.EqualTo("GenitalVaginaHuman"));
        Assert.That(parsed.MarkingScale, Is.EqualTo(1.0f));
        Assert.That(parsed.MarkingOffset, Is.EqualTo(System.Numerics.Vector2.Zero));
        Assert.That(parsed.MarkingGlow, Is.Empty.Or.All.EqualTo(0f));
    }

    [Test]
    public void DbStringRoundTripGlow()
    {
        var marking = new Marking("GenitalVaginaHuman", new List<Color> { Color.White, Color.Red });
        marking.SetGlow(0, 0.5f);
        marking.SetGlow(1, 1f);

        var parsed = Marking.ParseFromDbString(marking.ToString());

        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed!.MarkingGlow[0], Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(parsed.MarkingGlow[1], Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void DbStringRoundTripTransformAndGlow()
    {
        var marking = new Marking("GenitalVaginaHuman", new List<Color> { Color.White });
        marking.SetScale(1.5f);
        marking.SetOffset(0.25f, -0.5f);
        marking.SetGlow(0, 0.75f);

        var parsed = Marking.ParseFromDbString(marking.ToString());

        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed!.MarkingScale, Is.EqualTo(1.5f).Within(0.0001f));
        Assert.That(parsed.MarkingOffset.X, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(parsed.MarkingGlow[0], Is.EqualTo(0.75f).Within(0.0001f));
    }

    [Test]
    public void TransformValuesAreClamped()
    {
        var marking = new Marking("GenitalVaginaHuman", new List<Color> { Color.White });
        marking.SetScale(999f);
        marking.SetOffset(99f, -99f);

        Assert.That(marking.MarkingScale, Is.EqualTo(4.0f));
        Assert.That(marking.MarkingOffset.X, Is.EqualTo(2.0f));
        Assert.That(marking.MarkingOffset.Y, Is.EqualTo(-2.0f));
    }

    [Test]
    public void DbStringRoundTripVisibilitySettings()
    {
        var marking = new Marking("GenitalVaginaHuman", new List<Color> { Color.White });
        marking.CanToggleVisible = false;
        marking.OtherCanToggleVisible = true;
        marking.SetCustomName("Bedroom Eyes");

        var parsed = Marking.ParseFromDbString(marking.ToString());

        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed!.CanToggleVisible, Is.False);
        Assert.That(parsed.OtherCanToggleVisible, Is.True);
        Assert.That(parsed.CustomName, Is.EqualTo("Bedroom Eyes"));
    }

    [Test]
    public void DbStringDefaultsVisibilitySettingsWhenAbsent()
    {
        // Legacy strings predate the per-marking visibility settings.
        var parsed = Marking.ParseFromDbString("GenitalVaginaHuman@#ffffff@1.5,0.25,-0.5");

        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed!.CanToggleVisible, Is.True);
        Assert.That(parsed.OtherCanToggleVisible, Is.False);
        Assert.That(parsed.CustomName, Is.Null);
    }

    [Test]
    public void DbStringCustomNameCannotInjectSegments()
    {
        var marking = new Marking("GenitalVaginaHuman", new List<Color> { Color.White });
        marking.SetCustomName("Bad@Name");

        var parsed = Marking.ParseFromDbString(marking.ToString());

        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed!.CustomName, Is.EqualTo("Bad_Name"));
        Assert.That(parsed.CanToggleVisible, Is.True);
    }

    [Test]
    public void DbStringRoundTripWithTransformGlowAndSettings()
    {
        var marking = new Marking("GenitalVaginaHuman", new List<Color> { Color.White, Color.Black });
        marking.SetScale(0.5f);
        marking.SetOffset(-0.25f, 0.75f);
        marking.SetGlow(1, 0.4f);
        marking.CanToggleVisible = false;
        marking.OtherCanToggleVisible = true;
        marking.SetCustomName("Custom");

        var parsed = Marking.ParseFromDbString(marking.ToString());

        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed!.MarkingScale, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(parsed.MarkingOffset.X, Is.EqualTo(-0.25f).Within(0.0001f));
        Assert.That(parsed.MarkingOffset.Y, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(parsed.MarkingGlow[1], Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(parsed.CanToggleVisible, Is.False);
        Assert.That(parsed.OtherCanToggleVisible, Is.True);
        Assert.That(parsed.CustomName, Is.EqualTo("Custom"));
    }
}
