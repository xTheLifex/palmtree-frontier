using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server._PS.Interactions;
using Content.Server._PS.Organs;
using Content.Server.Decals;
using Content.Shared._PS.Interactions;
using Content.Shared._PS.Organs;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: server-side checks for the ported interaction panel (organ requirements, consent,
/// cooldowns, lust and cum plumbing).
/// </summary>
[TestFixture]
public sealed class InteractionPanelTest
{
    [Test]
    public async Task RequirementsConsentAndCooldown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var proto = server.ResolveDependency<IPrototypeManager>();
            var appearance = server.System<SharedHumanoidAppearanceSystem>();
            var genitals = server.System<GenitalSystem>();
            var organSystem = server.System<GenitalOrganSystem>();
            var interactions = server.System<InteractionPanelSystem>();

            var user = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var target = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(1f, 0f)));

            // Configure organs through the character profile (the player-facing path).
            var userSettings = new GenitalOrganSettings();
            userSettings.Set(GenitalType.Penis, new GenitalOrganData { Prototype = "PenisHuman", Size = 3 });

            var targetSettings = new GenitalOrganSettings();
            targetSettings.Set(GenitalType.Vagina, new GenitalOrganData { Prototype = "VaginaHuman", Size = 1 });

            appearance.LoadProfile(user, HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(userSettings));
            appearance.LoadProfile(target, HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(targetSettings));

            Assert.That(organSystem.TryGetOrgan(user, GenitalType.Penis, out _, out _), Is.True);
            Assert.That(organSystem.TryGetOrgan(target, GenitalType.Vagina, out _, out _), Is.True);

            Assert.That(genitals.GetExposure(user, GenitalType.Penis), Is.EqualTo(GenitalExposure.Exposed));
            Assert.That(genitals.GetExposure(target, GenitalType.Vagina), Is.EqualTo(GenitalExposure.Exposed));

            var fuck = proto.Index<InteractionPrototype>("Fuck");
            Assert.That(interactions.CanPerform(fuck, user, target, false), Is.True);

            // Consent gate (session toggle, defaults on).
            interactions.EnsureState(target).Consent = false;
            Assert.That(interactions.CanPerform(fuck, user, target, false), Is.False);
            interactions.EnsureState(target).Consent = true;

            // Clothing gate: wearing a jumpsuit hides the organs (default: HiddenByJumpsuit).
            var jumpsuit = entMan.SpawnEntity("ClothingUniformJumpsuitColorGrey", testMap.GridCoords);
            var inventory = server.System<Content.Shared.Inventory.InventorySystem>();
            Assert.That(inventory.TryEquip(target, jumpsuit, "jumpsuit", true, true), Is.True);
            Assert.That(interactions.CanPerform(fuck, user, target, false), Is.False);
            inventory.TryUnequip(target, "jumpsuit", out _, true, true);

            // Visibility rules (SPLURT/Sandstorm port).
            var userHumanoid = entMan.GetComponent<HumanoidAppearanceComponent>(user);
            var targetHumanoid = entMan.GetComponent<HumanoidAppearanceComponent>(target);

            // Never hidden: exposed even in a jumpsuit.
            organSystem.SetVisibility(user, GenitalType.Penis, GenitalVisibility.NeverHidden);
            Assert.That(genitals.GetExposure(user, GenitalType.Penis), Is.EqualTo(GenitalExposure.Exposed));
            inventory.TryEquip(user, jumpsuit, "jumpsuit", true, true);
            Assert.That(genitals.GetExposure(user, GenitalType.Penis), Is.EqualTo(GenitalExposure.Exposed));
            inventory.TryUnequip(user, "jumpsuit", out _, true, true);

            // Always hidden: never exposed.
            organSystem.SetVisibility(user, GenitalType.Penis, GenitalVisibility.AlwaysHidden);
            Assert.That(genitals.GetExposure(user, GenitalType.Penis), Is.EqualTo(GenitalExposure.Unexposed));

            // Hidden by underwear: exposed bare, hidden with a visible undergarment.
            organSystem.SetVisibility(target, GenitalType.Vagina, GenitalVisibility.HiddenByUnderwear);
            Assert.That(genitals.GetExposure(target, GenitalType.Vagina), Is.EqualTo(GenitalExposure.Exposed));
            targetHumanoid.MarkingSet.AddBack(MarkingCategories.UndergarmentBottom,
                new Marking("TestUndergarment", new List<Color> { Color.White }) { Visible = true });
            Assert.That(genitals.GetExposure(target, GenitalType.Vagina), Is.EqualTo(GenitalExposure.Unexposed));

            // Back to the default rules for the interaction below.
            organSystem.SetVisibility(user, GenitalType.Penis, GenitalVisibility.HiddenByJumpsuit);
            organSystem.SetVisibility(target, GenitalType.Vagina, GenitalVisibility.HiddenByJumpsuit);
            targetHumanoid.MarkingSet.RemoveCategory(MarkingCategories.UndergarmentBottom);

            // Perform, gain lust, then verify the cooldown blocks an immediate repeat.
            Assert.That(interactions.TryPerform(user, target, fuck), Is.True);
            Assert.That(interactions.GetLust(user, interactions.EnsureState(user)), Is.GreaterThan(0));
            Assert.That(interactions.TryPerform(user, target, fuck), Is.False,
                "The interaction cooldown should block an immediate repeat.");

            // Force a climax inside the target's vagina and verify the semen reservoir fills.
            var userState = interactions.EnsureState(user);
            interactions.SetLust(user, userState, userState.LustTolerance * 3 + 1);
            userState.LastInteractionTime = TimeSpan.MinValue;
            Assert.That(interactions.TryPerform(user, target, fuck), Is.True);
            Assert.That(server.System<DrippingCumSystem>().GetSemen(target), Is.GreaterThan(0));

            // Bound UI state and messages must be NetSerializable; serialize them to catch
            // missing attributes/fields at test time instead of at runtime.
            var serializer = server.ResolveDependency<IRobustSerializer>();
            var state = interactions.BuildState(user, interactions.EnsureState(user));
            state.Target = entMan.GetNetEntity(target);

            Serialize(serializer, state);
            Serialize(serializer, new InteractionSelectedMessage("Kiss"));
            Serialize(serializer, new InteractionToggleAutoMessage("Kiss"));
            Serialize(serializer, new InteractionSetPaceMessage(1f));
            Serialize(serializer, new InteractionToggleFavoriteMessage("Kiss"));
            Serialize(serializer, new InteractionGenitalArouseMessage(GenitalType.Penis, true));
            Serialize(serializer, new InteractionGenitalSetVisibilityMessage(GenitalType.Penis, GenitalVisibility.NeverHidden));
            Serialize(serializer, new InteractionSetConsentMessage(false));
            Serialize(serializer, new InteractionSetSoundsMessage(false));
            Serialize(serializer, new InteractionSetArousalMultiplierMessage(true, 150));
            Serialize(serializer, new InteractionSetMoaningMultiplierMessage(true, 50));

            entMan.DeleteEntity(user);
            entMan.DeleteEntity(target);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClimaxInsideVaginaStartsDrip()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid receiver = default;

        await server.WaitAssertion(() =>
        {
            var drip = server.System<DrippingCumSystem>();
            receiver = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

            drip.AddSemen(receiver, 6);
            Assert.That(drip.GetSemen(receiver), Is.EqualTo(6));
        });

        // Let the drip tick (1 unit/second) and verify droplet decals actually appear.
        await server.WaitRunTicks(70);

        await server.WaitAssertion(() =>
        {
            var xform = entMan.GetComponent<TransformComponent>(receiver);
            Assert.That(xform.GridUid, Is.Not.Null, "The receiver should be standing on a grid.");

            // Decals are placed where the mob stands (plus scatter), so query around their position.
            var query = xform.LocalPosition; // decals are centered on the mob now
            var decals = server.System<DecalSystem>()
                .GetDecalsInRange(xform.GridUid!.Value, query, 1f)
                .ToList();

            Assert.That(decals.Any(d => d.Decal.Id.StartsWith("SemenDrip")), Is.True,
                "Dripping semen should place droplet decals on the ground.");
            Assert.That(decals.Any(d => d.Decal.Id.StartsWith("SemenPuddle")), Is.False,
                "Drips must never escalate to the full cum decals.");

            entMan.DeleteEntity(receiver);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LegacyGenitalMarkingsConvertToOrgans()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var appearance = server.System<SharedHumanoidAppearanceSystem>();

            // Coyote-style export: old genital markings plus cross-species markings.
            const string yaml = """
forkId: ps14
version: 1
profile:
  species: Human
  appearance:
    markings:
    - markingId: PSGenitalBreasts7
      markingColor: ['#FFFFFFFF', '#FFFFFFFF', '#FFFFFFFF', '#FFFFFFFF']
    - markingId: GenitalVaginaHuman
      markingColor: ['#FFFFFFFF']
    - markingId: GenitalButt1
      markingColor: ['#FFFFFFFF', '#FFFFFFFF']
    - markingId: TailBats
      markingColor: ['#FFFFFFFF', '#FFFFFFFF', '#FFFFFFFF', '#FFFFFFFF']
    - markingId: VulpEar
      markingColor: ['#FFFFFFFF', '#FFFFFFFF']
""";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
            var profile = appearance.FromStream(stream, null!);

            // Genital markings become organs.
            Assert.That(profile.Genitals.Get(GenitalType.Breasts)?.Prototype, Is.EqualTo("BreastsSplurt"));
            Assert.That(profile.Genitals.Get(GenitalType.Breasts)?.Size, Is.EqualTo(8));
            Assert.That(profile.Genitals.Get(GenitalType.Vagina)?.Prototype, Is.EqualTo("VaginaHuman"));
            Assert.That(profile.Genitals.Get(GenitalType.Butt)?.Prototype, Is.EqualTo("ButtStandard"));

            // Non-genital markings (including cross-species tails/ears) survive validation.
            Assert.That(profile.Appearance.Markings.Any(m => m.MarkingId == "TailBats"), Is.True,
                "Tail markings should survive import with the raised Human marking limits.");
            Assert.That(profile.Appearance.Markings.Any(m => m.MarkingId == "VulpEar"), Is.True);

            // The old genital markings are gone from the appearance.
            Assert.That(profile.Appearance.Markings.Any(m =>
                m.MarkingId.StartsWith("Genital") || m.MarkingId.StartsWith("PSGenital")), Is.False);

            // Offset/scale/glow round-trip through the DB string (colors omitted).
            var dbSettings = new GenitalOrganSettings();
            dbSettings.Set(GenitalType.Penis, new GenitalOrganData
            {
                Prototype = "PenisHuman",
                Size = 3,
                Offset = new Vector2(0.25f, -0.5f),
                Scale = 1.5f,
                Glow = 0.5f,
                DetailGlow = 0.25f,
            });
            var parsed = GenitalOrganSettings.FromDbString(dbSettings.ToDbString());
            Assert.That(parsed.Get(GenitalType.Penis)?.Offset, Is.EqualTo(new Vector2(0.25f, -0.5f)));
            Assert.That(parsed.Get(GenitalType.Penis)?.Scale, Is.EqualTo(1.5f));
            Assert.That(parsed.Get(GenitalType.Penis)?.Glow, Is.EqualTo(0.5f));
            Assert.That(parsed.Get(GenitalType.Penis)?.DetailGlow, Is.EqualTo(0.25f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StalePendingProfileDoesNotWipeGenitals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid mob = default;

        await server.WaitAssertion(() =>
        {
            var appearance = server.System<SharedHumanoidAppearanceSystem>();
            var organSystem = server.System<GenitalOrganSystem>();

            // Create the mob without running ComponentInit/MapInit so the body is not ready and the
            // first (default, genital-less) profile event gets queued as pending.
            mob = entMan.CreateEntityUninitialized("MobHuman", testMap.GridCoords);
            entMan.EventBus.RaiseLocalEvent(mob,
                new HumanoidProfileAppliedEvent(mob, HumanoidCharacterProfile.DefaultWithSpecies("Human")));

            // Finish initializing the body, then apply the real profile with genitals.
            entMan.InitializeAndStartEntity(mob);

            var settings = new GenitalOrganSettings();
            settings.Set(GenitalType.Penis, new GenitalOrganData { Prototype = "PenisHuman", Size = 3 });
            appearance.LoadProfile(mob, HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(settings));

            Assert.That(organSystem.TryGetOrgan(mob, GenitalType.Penis, out _, out _), Is.True,
                "The real profile should create the penis organ.");
        });

        // The stale pending (default) profile must not re-sync over the real one on a later tick.
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var organSystem = server.System<GenitalOrganSystem>();
            Assert.That(organSystem.TryGetOrgan(mob, GenitalType.Penis, out _, out _), Is.True,
                "A stale pending profile must not wipe the configured genitals.");

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    private static void Serialize<T>(IRobustSerializer serializer, T value)
    {
        using var stream = new MemoryStream();
        serializer.Serialize(stream, value!);
        stream.Position = 0;
        var copy = serializer.Deserialize<T>(stream);
        Assert.That(copy, Is.Not.Null);
    }
}
