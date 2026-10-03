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
}
