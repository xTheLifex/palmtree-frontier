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
    public async Task EjaculationSpawnsCumDecals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var drip = server.System<DrippingCumSystem>();
            var mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

            // 30 units is the default organ volume, which maps to the second puddle stage.
            // Two climaxes should leave two decals (no replacing the previous one).
            drip.SpawnCumDecals(mob, 30);
            drip.SpawnCumDecals(mob, 30);

            var xform = entMan.GetComponent<TransformComponent>(mob);
            var query = xform.LocalPosition; // decals are centered on the mob now
            var decals = server.System<DecalSystem>()
                .GetDecalsInRange(xform.GridUid!.Value, query, 1f)
                .ToList();

            Assert.That(decals.Any(d => d.Decal.Id == "SemenPuddle2"), Is.True,
                "Ejaculation should place the full SPLURT cum decals.");
            Assert.That(decals.Count(d => d.Decal.Id.StartsWith("SemenPuddle")), Is.GreaterThanOrEqualTo(2),
                "Each ejaculation should add a new decal instead of replacing the previous one.");
            Assert.That(decals.Any(d =>
                    (d.Decal.Coordinates + new Vector2(0.5f, 0.5f) - xform.LocalPosition).Length() < 0.5f), Is.True,
                "Cum decals should be centered on the mob, not offset up-right.");
            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReceiverSideAndSelfInteractions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var proto = server.ResolveDependency<IPrototypeManager>();
            var appearance = server.System<SharedHumanoidAppearanceSystem>();
            var interactions = server.System<InteractionPanelSystem>();
            var drip = server.System<DrippingCumSystem>();

            var user = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var target = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(1f, 0f)));

            var userSettings = new GenitalOrganSettings();
            userSettings.Set(GenitalType.Vagina, new GenitalOrganData { Prototype = "VaginaHuman", Size = 1 });

            var targetSettings = new GenitalOrganSettings();
            targetSettings.Set(GenitalType.Penis, new GenitalOrganData { Prototype = "PenisHuman", Size = 3 });

            appearance.LoadProfile(user, HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(userSettings));
            appearance.LoadProfile(target, HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(targetSettings));

            // Receiver-side penetration: the vagina owner rides the penis owner, and the target's
            // climax fills the actor instead of being discarded.
            var ride = proto.Index<InteractionPrototype>("Ride");
            Assert.That(interactions.CanPerform(ride, user, target, false), Is.True);

            var targetState = interactions.EnsureState(target);
            interactions.SetLust(target, targetState, targetState.LustTolerance * 3 + 1);
            interactions.EnsureState(user).LastInteractionTime = TimeSpan.MinValue;

            Assert.That(interactions.TryPerform(user, target, ride), Is.True);
            Assert.That(drip.GetSemen(user), Is.GreaterThan(0),
                "The penis owner's climax during a receiver-side act should fill the actor.");

            // Manual Climax is usable on yourself and on others, with no adjacency/orgasm gate.
            var climax = proto.Index<InteractionPrototype>("Climax");
            Assert.That(interactions.CanPerform(climax, user, user, false), Is.True);
            Assert.That(interactions.CanPerform(climax, user, target, false), Is.True);

            // Self masturbation is self-only.
            var masturbate = proto.Index<InteractionPrototype>("MasturbateVagina");
            Assert.That(interactions.CanPerform(masturbate, user, user, false), Is.True);
            Assert.That(interactions.CanPerform(masturbate, user, target, false), Is.False);

            // A male manual climax with no partner leaves the full cum decals on their own tile.
            var male = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var maleSettings = new GenitalOrganSettings();
            maleSettings.Set(GenitalType.Penis, new GenitalOrganData { Prototype = "PenisHuman", Size = 3 });
            appearance.LoadProfile(male, HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(maleSettings));

            var maleState = interactions.EnsureState(male);
            interactions.SetLust(male, maleState, 0);
            Assert.That(interactions.TryPerform(male, male, climax), Is.True);

            var maleXform = entMan.GetComponent<TransformComponent>(male);
            var maleQuery = maleXform.LocalPosition; // decals are centered on the mob now
            var decals = server.System<DecalSystem>()
                .GetDecalsInRange(maleXform.GridUid!.Value, maleQuery, 1f)
                .ToList();

            Assert.That(decals.Any(d => d.Decal.Id.StartsWith("SemenPuddle")), Is.True,
                "A manual climax should place the full cum decals.");

            entMan.DeleteEntity(user);
            entMan.DeleteEntity(target);
            entMan.DeleteEntity(male);
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
    public async Task TogglingOrgansOffAndOnRecreatesThem()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var appearance = server.System<SharedHumanoidAppearanceSystem>();
            var organSystem = server.System<GenitalOrganSystem>();

            var mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var baseProfile = HumanoidCharacterProfile.DefaultWithSpecies("Human");

            var enabled = new GenitalOrganSettings();
            enabled.Set(GenitalType.Balls, new GenitalOrganData { Prototype = "BallsSheath", Size = 1 });

            // Enable, disable, re-enable: the organ must come back each time.
            appearance.LoadProfile(mob, baseProfile.WithGenitals(enabled));
            Assert.That(organSystem.TryGetOrgan(mob, GenitalType.Balls, out _, out _), Is.True);
            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(mob);
            Assert.That(HasGenitalRenderMarking(humanoid), Is.True, "Balls should render when enabled.");

            appearance.LoadProfile(mob, baseProfile.WithGenitals(new GenitalOrganSettings()));
            Assert.That(organSystem.TryGetOrgan(mob, GenitalType.Balls, out _, out _), Is.False);
            Assert.That(HasGenitalRenderMarking(humanoid), Is.False, "Balls should stop rendering when disabled.");

            appearance.LoadProfile(mob, baseProfile.WithGenitals(enabled));
            Assert.That(organSystem.TryGetOrgan(mob, GenitalType.Balls, out _, out _), Is.True,
                "Re-enabling an organ should recreate it (slot reuse).");
            Assert.That(HasGenitalRenderMarking(humanoid), Is.True, "Balls should render again when re-enabled.");

            // Switching size must update the render marking.
            var resized = new GenitalOrganSettings();
            resized.Set(GenitalType.Balls, new GenitalOrganData { Prototype = "BallsSheath", Size = 3 });
            appearance.LoadProfile(mob, baseProfile.WithGenitals(resized));
            Assert.That(organSystem.TryGetOrgan(mob, GenitalType.Balls, out _, out var comp), Is.True);
            Assert.That(comp.Size, Is.EqualTo(3));
            Assert.That(HasGenitalRenderMarking(humanoid), Is.True);

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BallsAreFlaccidOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var appearance = server.System<SharedHumanoidAppearanceSystem>();
            var organSystem = server.System<GenitalOrganSystem>();
            var genitals = server.System<GenitalSystem>();

            var mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var settings = new GenitalOrganSettings();
            settings.Set(GenitalType.Balls, new GenitalOrganData { Prototype = "BallsSheath", Size = 1 });
            settings.Set(GenitalType.Penis, new GenitalOrganData { Prototype = "PenisHuman", Size = 3 });
            appearance.LoadProfile(mob, HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(settings));

            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(mob);

            // Balls have no aroused sprite: arousal is rejected and the marking stays flaccid.
            var ballsBefore = humanoid.MarkingSet.Markings[MarkingCategories.Genital]
                .First(m => m.MarkingId.Contains("Balls")).MarkingId;
            Assert.That(organSystem.SetAroused(mob, GenitalType.Balls, true), Is.False);
            var ballsAfter = humanoid.MarkingSet.Markings[MarkingCategories.Genital]
                .First(m => m.MarkingId.Contains("Balls")).MarkingId;
            Assert.That(ballsAfter, Is.EqualTo(ballsBefore));
            Assert.That(ballsAfter, Does.Not.EndWith("Alt"));

            // Penis still arouses normally.
            Assert.That(organSystem.SetAroused(mob, GenitalType.Penis, true), Is.True);
            var penis = humanoid.MarkingSet.Markings[MarkingCategories.Genital]
                .First(m => m.MarkingId.Contains("Penis")).MarkingId;
            Assert.That(penis, Does.EndWith("-1"));

            // The in-game panel disables the arousal toggle for balls but not for the penis.
            var entries = genitals.GetGenitalEntries(mob, true);
            Assert.That(entries.First(e => e.Type == GenitalType.Balls).CanToggleArousal, Is.False);
            Assert.That(entries.First(e => e.Type == GenitalType.Penis).CanToggleArousal, Is.True);

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeightWidthClampsAndApplies()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var appearance = server.System<SharedHumanoidAppearanceSystem>();

            // Sliders max out at double the standard size and clamp below at half.
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithHeight(3f)
                .WithWidth(0.1f);

            Assert.That(profile.Height, Is.EqualTo(HumanoidCharacterProfile.MaxHeight));
            Assert.That(profile.Width, Is.EqualTo(HumanoidCharacterProfile.MinWidth));

            var mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            appearance.LoadProfile(mob, profile);
            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(mob);

            Assert.That(humanoid.Height, Is.EqualTo(HumanoidCharacterProfile.MaxHeight));
            Assert.That(humanoid.Width, Is.EqualTo(HumanoidCharacterProfile.MinWidth));

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MultiPulseClimax()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid male = default;

        await server.WaitAssertion(() =>
        {
            var proto = server.ResolveDependency<IPrototypeManager>();
            var appearance = server.System<SharedHumanoidAppearanceSystem>();
            var interactions = server.System<InteractionPanelSystem>();

            male = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

            var settings = new GenitalOrganSettings { SemenVolume = 300 };
            settings.Set(GenitalType.Penis, new GenitalOrganData { Prototype = "PenisHuman", Size = 3 });
            appearance.LoadProfile(male, HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(settings));

            var climax = proto.Index<InteractionPrototype>("Climax");
            var state = interactions.EnsureState(male);
            interactions.SetLust(male, state, 0);
            state.LastInteractionTime = TimeSpan.MinValue;

            // 300u = 10 pulses of 30u; the first fires immediately.
            Assert.That(interactions.TryPerform(male, male, climax), Is.True);
            Assert.That(state.ClimaxPulsesLeft, Is.EqualTo(9));

            // Mid-climax the actor cannot start another climax...
            Assert.That(interactions.CanPerform(climax, male, male, false), Is.False);
            Assert.That(interactions.TryPerform(male, male, climax), Is.False);

            // ...and interactions do not add lust.
            var masturbate = proto.Index<InteractionPrototype>("MasturbatePenis");
            state.LastInteractionTime = TimeSpan.MinValue;
            var lustBefore = interactions.GetLust(male, state);
            Assert.That(interactions.TryPerform(male, male, masturbate), Is.True);
            Assert.That(interactions.GetLust(male, state), Is.EqualTo(lustBefore).Within(0.001));
        });

        // Let the remaining 9 pulses fire (0.6s apart).
        await server.WaitRunTicks(200);

        await server.WaitAssertion(() =>
        {
            var interactions = server.System<InteractionPanelSystem>();
            var state = interactions.EnsureState(male);
            Assert.That(state.ClimaxPulsesLeft, Is.EqualTo(0), "The pulse sequence should finish.");

            var xform = entMan.GetComponent<TransformComponent>(male);
            var query = xform.LocalPosition; // decals are centered on the mob now
            var decals = server.System<DecalSystem>()
                .GetDecalsInRange(xform.GridUid!.Value, query, 1f)
                .ToList();

            Assert.That(decals.Count(d => d.Decal.Id.StartsWith("SemenPuddle")), Is.GreaterThanOrEqualTo(10),
                "A 300u climax should leave ten pulses worth of decals.");

            entMan.DeleteEntity(male);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FemaleClimaxSpawnsFemDecals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid female = default;

        await server.WaitAssertion(() =>
        {
            var proto = server.ResolveDependency<IPrototypeManager>();
            var appearance = server.System<SharedHumanoidAppearanceSystem>();
            var interactions = server.System<InteractionPanelSystem>();

            female = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

            // Vagina-only character; 60u of "fluid per climax" = two 30u pulses.
            var settings = new GenitalOrganSettings { SemenVolume = 60 };
            settings.Set(GenitalType.Vagina, new GenitalOrganData { Prototype = "VaginaHuman", Size = 1 });
            appearance.LoadProfile(female, HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(settings));

            var climax = proto.Index<InteractionPrototype>("Climax");
            var state = interactions.EnsureState(female);
            interactions.SetLust(female, state, 0);
            state.LastInteractionTime = TimeSpan.MinValue;

            Assert.That(interactions.TryPerform(female, female, climax), Is.True);
            Assert.That(state.ClimaxPulseFemale, Is.True);
            Assert.That(state.ClimaxPulsesLeft, Is.EqualTo(1), "60u should leave one more pulse.");
            Assert.That(state.ClimaxPulseInterval, Is.EqualTo(TimeSpan.FromSeconds(1)),
                "Female climax pulses are slower (1s).");
        });

        // Let the second pulse fire (1s later).
        await server.WaitRunTicks(60);

        await server.WaitAssertion(() =>
        {
            var xform = entMan.GetComponent<TransformComponent>(female);
            var query = xform.LocalPosition; // decals are centered on the mob now
            var decals = server.System<DecalSystem>()
                .GetDecalsInRange(xform.GridUid!.Value, query, 1f)
                .ToList();

            Assert.That(decals.Count(d => d.Decal.Id.StartsWith("FemPuddle")), Is.EqualTo(2),
                "Female climaxes should leave fem decals.");
            Assert.That(decals.Any(d => d.Decal.Id.StartsWith("SemenPuddle")), Is.False,
                "Female climaxes should not use the semen decals.");

            entMan.DeleteEntity(female);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HairAlphaIsPreserved()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human");
            var color = new Color(255, 0, 0, 128);
            profile = profile.WithCharacterAppearance(profile.Appearance.WithHairColor(color));
            profile.EnsureValid(null!, IoCManager.Instance!);

            Assert.That(profile.Appearance.HairColor.AByte, Is.EqualTo(color.AByte),
                "Hair alpha should survive profile validation.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SlimeMarkingsCanBeColored()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var proto = server.ResolveDependency<IPrototypeManager>();
            var markingManager = server.ResolveDependency<MarkingManager>();

            // Slime hair/facial hair no longer follow the skin color...
            Assert.That(markingManager.MustMatchSkin("SlimePerson", HumanoidVisualLayers.Hair, out _, proto), Is.False);
            Assert.That(markingManager.MustMatchSkin("SlimePerson", HumanoidVisualLayers.FacialHair, out _, proto), Is.False);

            // ...and slime markings can be recolored.
            Assert.That(proto.Index<MarkingPrototype>("SlimeNose").ForcedColoring, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MechSuitsAreAvailableToContractors()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var proto = server.ResolveDependency<IPrototypeManager>();

            foreach (var id in new[]
                     {
                         "ClothingUniformMechSuitRed",
                         "ClothingUniformMechSuitWhite",
                         "ClothingUniformMechSuitBlue",
                     })
            {
                Assert.That(proto.HasIndex<EntityPrototype>(id), Is.True, $"{id} should exist.");
            }

            Assert.That(proto.HasIndex<LoadoutGroupPrototype>("ContractorMechSuit"), Is.True);
            Assert.That(proto.Index<LoadoutGroupPrototype>("ContractorMechSuit").MinLimit, Is.EqualTo(0),
                "The mech suit category is optional.");

            var contractor = proto.Index<RoleLoadoutPrototype>("JobContractor");
            Assert.That(contractor.Groups.Any(g => g.Id == "ContractorMechSuit"), Is.True,
                "The contractor loadout should list the mech suit category.");
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

    [Test]
    public async Task RemoteGenitalsRenderOnClient()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var client = pair.Client;
        var testMap = await pair.CreateTestMap();
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();

        NetEntity netMob = default;

        await server.WaitPost(() =>
        {
            var mob = sEntMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

            var settings = new GenitalOrganSettings();
            settings.Set(GenitalType.Penis, new GenitalOrganData { Prototype = "PenisHuman", Size = 3 });

            server.System<SharedHumanoidAppearanceSystem>().LoadProfile(mob,
                HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(settings));

            netMob = sEntMan.GetNetEntity(mob);
        });

        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var mob = cEntMan.GetEntity(netMob);
            var humanoid = cEntMan.GetComponent<HumanoidAppearanceComponent>(mob);

            Assert.That(humanoid.MarkingSet.TryGetCategory(MarkingCategories.Genital, out var markings), Is.True,
                "The observer should receive the genital render markings.");
            var marking = markings!.First();

            var markingManager = client.ResolveDependency<MarkingManager>();
            Assert.That(markingManager.Markings.TryGetValue(marking.MarkingId, out var proto), Is.True);

            var sprite = cEntMan.GetComponent<SpriteComponent>(mob);
            var visible = false;

            foreach (var spec in proto!.Sprites)
            {
                if (spec is not SpriteSpecifier.Rsi rsi)
                    continue;

                var layerId = $"{marking.MarkingId}-{rsi.RsiState}";
                if (sprite.LayerMapTryGet(layerId, out var index) && sprite[index].Visible)
                {
                    visible = true;
                    break;
                }
            }

            Assert.That(visible, Is.True,
                "A remote naked character's genitals should render on the client.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrganRenderFollowsVisibilityAndClothing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var appearance = server.System<SharedHumanoidAppearanceSystem>();
            var organSystem = server.System<GenitalOrganSystem>();
            var inventory = server.System<Content.Shared.Inventory.InventorySystem>();

            var mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

            var settings = new GenitalOrganSettings();
            settings.Set(GenitalType.Breasts, new GenitalOrganData
            {
                Prototype = "BreastsCoyote",
                Size = 1,
                Visibility = GenitalVisibility.HiddenByJumpsuit,
                Offset = new Vector2(0.25f, -0.5f),
                Scale = 1.5f,
                Glow = 0.5f,
            });

            appearance.LoadProfile(mob, HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(settings));
            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(mob);

            Assert.That(HasGenitalRenderMarking(humanoid), Is.True, "Breasts should render while naked.");

            // Organ transforms flow into the render marking (like marking offsets/scale).
            var renderMarking = humanoid.MarkingSet.Markings[MarkingCategories.Genital].First();
            Assert.That(renderMarking.MarkingOffset, Is.EqualTo(new Vector2(0.25f, -0.5f)),
                "Organ offsets should reach the render marking.");
            Assert.That(renderMarking.MarkingScale, Is.EqualTo(1.5f),
                "Organ scale should reach the render marking.");
            Assert.That(renderMarking.MarkingGlow[0], Is.EqualTo(0.5f),
                "Organ glow should reach the render marking.");

            // HiddenByUnderwear + toggling the undergarment marking exercises the visibility event
            // and must not crash the render rebuild (dictionary mutation regression).
            organSystem.SetVisibility(mob, GenitalType.Breasts, GenitalVisibility.HiddenByUnderwear);
            humanoid.MarkingSet.AddBack(MarkingCategories.UndergarmentTop,
                new Marking("TestUndergarment", new List<Color> { Color.White }) { Visible = true });
            organSystem.SyncRender(mob);
            Assert.That(HasGenitalRenderMarking(humanoid), Is.False, "Visible undergarment should hide breasts.");
            appearance.SetMarkingVisibility(mob, humanoid, "TestUndergarment", false);
            Assert.That(HasGenitalRenderMarking(humanoid), Is.True, "Hiding the undergarment should reveal breasts.");
            humanoid.MarkingSet.RemoveCategory(MarkingCategories.UndergarmentTop);
            organSystem.SetVisibility(mob, GenitalType.Breasts, GenitalVisibility.HiddenByJumpsuit);

            // HiddenByJumpsuit: the organ sprite must be removed entirely (kimono case), not just
            // rely on sprite-layer overlap.
            var jumpsuit = entMan.SpawnEntity("ClothingUniformJumpsuitColorGrey", testMap.GridCoords);
            Assert.That(inventory.TryEquip(mob, jumpsuit, "jumpsuit", true, true), Is.True);
            Assert.That(HasGenitalRenderMarking(humanoid), Is.False, "Jumpsuit should hide breasts.");

            // Never hidden renders over clothing.
            organSystem.SetVisibility(mob, GenitalType.Breasts, GenitalVisibility.NeverHidden);
            Assert.That(HasGenitalRenderMarking(humanoid), Is.True, "Never hidden should render over clothing.");

            // Always hidden never renders.
            organSystem.SetVisibility(mob, GenitalType.Breasts, GenitalVisibility.AlwaysHidden);
            Assert.That(HasGenitalRenderMarking(humanoid), Is.False, "Always hidden should not render.");

            inventory.TryUnequip(mob, "jumpsuit", out _, true, true);
            organSystem.SetVisibility(mob, GenitalType.Breasts, GenitalVisibility.HiddenByJumpsuit);
            Assert.That(HasGenitalRenderMarking(humanoid), Is.True, "Breasts should render again when undressed.");

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BreastDetailColorTargetsNipples()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var proto = server.ResolveDependency<IPrototypeManager>();
            var appearance = server.System<SharedHumanoidAppearanceSystem>();

            // Coyote breasts have a single color group; Splurt breasts have a separate nipple layer.
            Assert.That(GenitalOrganSystem.CountColorGroups(proto.Index<MarkingPrototype>("GenitalBreastsRoundA")),
                Is.EqualTo(1));
            Assert.That(GenitalOrganSystem.CountColorGroups(proto.Index<MarkingPrototype>("PSGenitalBreasts7")),
                Is.EqualTo(2));

            var mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var settings = new GenitalOrganSettings();
            settings.Set(GenitalType.Breasts, new GenitalOrganData
            {
                Prototype = "BreastsSplurt",
                Size = 8, // PSGenitalBreasts7
                Color = Color.Red,
                DetailColor = Color.Blue,
                Glow = 0.25f,
                DetailGlow = 0.75f,
            });

            appearance.LoadProfile(mob, HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(settings));
            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(mob);

            Assert.That(humanoid.MarkingSet.TryGetCategory(MarkingCategories.Genital, out var markings), Is.True);
            var marking = markings!.First();

            // Sprite order is FRONT_primary, FRONT_secondary, BEHIND_primary, BEHIND_secondary.
            Assert.That(marking.MarkingColors[0], Is.EqualTo(Color.Red));
            Assert.That(marking.MarkingColors[1], Is.EqualTo(Color.Blue),
                "The secondary sprite (nipples) should use the detail color.");
            Assert.That(marking.MarkingGlow[0], Is.EqualTo(0.25f));
            Assert.That(marking.MarkingGlow[1], Is.EqualTo(0.75f),
                "The secondary sprite (nipples) should use the detail glow.");

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    private static bool HasGenitalRenderMarking(HumanoidAppearanceComponent humanoid)
    {
        return humanoid.MarkingSet.TryGetCategory(MarkingCategories.Genital, out var markings) && markings.Count > 0;
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
