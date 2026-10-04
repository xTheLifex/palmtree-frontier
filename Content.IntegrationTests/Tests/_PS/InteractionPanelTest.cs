using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Content.Server._PS.Interactions;
using Content.Server._PS.Organs;
using Content.Server.Decals;
using Content.Shared._PS.Interactions;
using Content.Shared._PS.Organs;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
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

            var user = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
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
            receiver = entMan.SpawnEntity("MobHuman", testMap.GridCoords);

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
            var query = xform.LocalPosition + new Vector2(0.5f, 0.5f);
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
            var mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords);

            // 30 units is the default organ volume, which maps to the second puddle stage.
            // Two climaxes should leave two decals (no replacing the previous one).
            drip.SpawnCumDecals(mob, 30);
            drip.SpawnCumDecals(mob, 30);

            var xform = entMan.GetComponent<TransformComponent>(mob);
            var query = xform.LocalPosition + new Vector2(0.5f, 0.5f);
            var decals = server.System<DecalSystem>()
                .GetDecalsInRange(xform.GridUid!.Value, query, 1f)
                .ToList();

            Assert.That(decals.Any(d => d.Decal.Id == "SemenPuddle2"), Is.True,
                "Ejaculation should place the full SPLURT cum decals.");
            Assert.That(decals.Count(d => d.Decal.Id.StartsWith("SemenPuddle")), Is.GreaterThanOrEqualTo(2),
                "Each ejaculation should add a new decal instead of replacing the previous one.");
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

            var user = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
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
            var male = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var maleSettings = new GenitalOrganSettings();
            maleSettings.Set(GenitalType.Penis, new GenitalOrganData { Prototype = "PenisHuman", Size = 3 });
            appearance.LoadProfile(male, HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(maleSettings));

            var maleState = interactions.EnsureState(male);
            interactions.SetLust(male, maleState, 0);
            Assert.That(interactions.TryPerform(male, male, climax), Is.True);

            var maleXform = entMan.GetComponent<TransformComponent>(male);
            var maleQuery = maleXform.LocalPosition + new Vector2(0.5f, 0.5f);
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
            var mob = sEntMan.SpawnEntity("MobHuman", testMap.GridCoords);

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

            var mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords);

            var settings = new GenitalOrganSettings();
            settings.Set(GenitalType.Breasts, new GenitalOrganData
            {
                Prototype = "BreastsCoyote",
                Size = 1,
                Visibility = GenitalVisibility.HiddenByJumpsuit,
            });

            appearance.LoadProfile(mob, HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(settings));
            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(mob);

            Assert.That(HasGenitalRenderMarking(humanoid), Is.True, "Breasts should render while naked.");

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

            var mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var settings = new GenitalOrganSettings();
            settings.Set(GenitalType.Breasts, new GenitalOrganData
            {
                Prototype = "BreastsSplurt",
                Size = 8, // PSGenitalBreasts7
                Color = Color.Red,
                DetailColor = Color.Blue,
            });

            appearance.LoadProfile(mob, HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(settings));
            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(mob);

            Assert.That(humanoid.MarkingSet.TryGetCategory(MarkingCategories.Genital, out var markings), Is.True);
            var marking = markings!.First();

            // Sprite order is FRONT_primary, FRONT_secondary, BEHIND_primary, BEHIND_secondary.
            Assert.That(marking.MarkingColors[0], Is.EqualTo(Color.Red));
            Assert.That(marking.MarkingColors[1], Is.EqualTo(Color.Blue),
                "The secondary sprite (nipples) should use the detail color.");

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
