using System.Linq;
using Content.Client._PS.Consent;
using Content.Client._PS.Humanoid;
using Content.Client.Humanoid;
using Content.Shared._PS.Consent;
using Content.Shared._PS.Interactions;
using Content.Shared._PS.Organs;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: regression tests for organ-driven genital rendering. The server is the only side that
/// may rebuild the <see cref="MarkingCategories.Genital"/> marking category; network clients render
/// the networked marking set as-is.
/// </summary>
[TestFixture]
public sealed class GenitalRenderTest
{
    /// <summary>
    /// Robust ejects contained entities from their containers when a network client detaches an
    /// entity for PVS, which fires <see cref="Content.Shared.Body.Events.OrganRemovedFromBodyEvent"/>
    /// on the client. The client used to rebuild the genital markings from local organ state at that
    /// point; with no organs found it stripped the category, and because the client's marking set is
    /// also its cached component state object the wipe stuck through PVS re-entry (genitals
    /// disappeared for observers who walked away and returned, or joined as a ghost).
    /// </summary>
    [Test]
    public async Task ClientDoesNotRebuildOrganRenderMarkings()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var client = pair.Client;

        var serverEntMan = server.ResolveDependency<IEntityManager>();
        var serverPlayers = server.ResolveDependency<IPlayerManager>();

        EntityUid mob = default;
        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            Assert.That(attached, Is.Not.Null);
            mob = attached.Value;

            var appearance = server.System<SharedHumanoidAppearanceSystem>();
            var organSystem = server.System<GenitalOrganSystem>();

            var settings = new GenitalOrganSettings();
            settings.Set(GenitalType.Penis, new GenitalOrganData
            {
                Prototype = "PenisHuman",
                Size = 3,
                Visibility = GenitalVisibility.NeverHidden,
            });
            settings.Set(GenitalType.Balls, new GenitalOrganData
            {
                Prototype = "BallsSingle",
                Size = 1,
                Visibility = GenitalVisibility.NeverHidden,
            });

            appearance.LoadProfile(mob,
                HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(settings));

            Assert.That(organSystem.TryGetOrgan(mob, GenitalType.Penis, out _, out _), Is.True);
            Assert.That(HasGenitalMarking(serverEntMan.GetComponent<HumanoidAppearanceComponent>(mob)),
                Is.True, "the server must render the organ marking");

            // Marking layers inserted at the same index render newest-first, so organs are added in
            // reverse priority order. The penis must therefore appear before the balls in the list
            // (and above them in the final sprite stack).
            Assert.That(serverEntMan.GetComponent<HumanoidAppearanceComponent>(mob).MarkingSet
                    .TryGetCategory(MarkingCategories.Genital, out var genitalMarkings), Is.True);
            var genitalMarkingList = genitalMarkings.ToList();
            var penisIndex = genitalMarkingList.FindIndex(m => m.MarkingId == "Genital-Penis-Human-3-0");
            var ballsIndex = genitalMarkingList.FindIndex(m => m.MarkingId == "Genital-Balls-Single1");
            Assert.That(penisIndex, Is.GreaterThanOrEqualTo(0), "the penis render marking must exist");
            Assert.That(ballsIndex, Is.GreaterThanOrEqualTo(0), "the balls render marking must exist");
            Assert.That(penisIndex, Is.LessThan(ballsIndex),
                "organs must be added back-to-front so the penis renders above the balls");
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientEntMan = client.ResolveDependency<IEntityManager>();
            var mob = client.Session.AttachedEntity;
            Assert.That(mob, Is.Not.Null);

            var humanoid = clientEntMan.GetComponent<HumanoidAppearanceComponent>(mob.Value);
            Assert.That(humanoid.MarkingSet.TryGetCategory(MarkingCategories.Genital, out var markings), Is.True,
                "the server's organ marking must replicate to the client");
            Assert.That(markings, Is.Not.Empty, "the server's organ marking must replicate to the client");

            // Snapshot the networked marking objects; the client must leave them alone.
            var before = markings.ToList();

            client.System<GenitalOrganSystem>().SyncRender(mob.Value);

            Assert.That(humanoid.MarkingSet.TryGetCategory(MarkingCategories.Genital, out var after), Is.True,
                "a network client must keep the server's genital render markings");
            Assert.That(after.Count, Is.EqualTo(before.Count),
                "a network client must not rebuild the genital render markings");
            for (var i = 0; i < before.Count; i++)
            {
                Assert.That(after[i], Is.SameAs(before[i]),
                    "the client must leave the networked marking objects untouched");
            }
        });

        // Strip the organs again before returning the pair: deleting organ-bearing mobs during
        // pool recycling trips a DEBUG-only client body assert.
        await server.WaitAssertion(() =>
        {
            var appearance = server.System<SharedHumanoidAppearanceSystem>();
            appearance.LoadProfile(mob, HumanoidCharacterProfile.DefaultWithSpecies("Human"));
        });

        await pair.RunTicksSync(5);

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Palmtree: an exposed south-facing butt uses the dedicated GenitalUnder sublayer, which sits
    /// above body/leg markings but below the breast/neck group.
    /// </summary>
    [Test]
    [NonParallelizable] // NsfwGate is a process-wide static.
    public async Task SouthExposedButtUsesGenitalUnderLayer()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var client = pair.Client;
        var serverPlayers = server.ResolveDependency<IPlayerManager>();

        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            Assert.That(attached, Is.Not.Null);

            var settings = new GenitalOrganSettings();
            settings.Set(GenitalType.Breasts, new GenitalOrganData
            {
                Prototype = "BreastsSplurt",
                Size = 1,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });
            settings.Set(GenitalType.Butt, new GenitalOrganData
            {
                Prototype = "ButtStandard",
                Size = 1,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });

            server.System<SharedHumanoidAppearanceSystem>().LoadProfile(attached.Value,
                HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(settings));
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientEntMan = client.ResolveDependency<IEntityManager>();
            var mob = client.Session.AttachedEntity;
            Assert.That(mob, Is.Not.Null);

            var previousNsfwState = NsfwGate.State;
            NsfwGate.SetState(NsfwConsentState.Allowed);

            try
            {
                var appearance = client.System<HumanoidAppearanceSystem>();
                var direction = clientEntMan.EnsureComponent<HumanoidLayerDirectionComponent>(mob.Value);
                direction.Direction = RsiDirection.South;
                appearance.RefreshSprite(mob.Value);

                var sprite = clientEntMan.GetComponent<SpriteComponent>(mob.Value);
                var spriteSystem = client.System<SpriteSystem>();

                Assert.That(spriteSystem.LayerMapTryGet((mob.Value, sprite),
                    "GenitalButt1-butt_pair_2_0_FRONT", out var buttLayer, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((mob.Value, sprite),
                    "PSGenitalBreasts0-m_breasts_pair_0_FRONT_primary", out var breastLayer, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((mob.Value, sprite),
                    HumanoidVisualLayers.LLeg, out var legLayer, false), Is.True);

                Assert.That(buttLayer, Is.GreaterThan(legLayer),
                    "butt must stay above body/leg layers");
                Assert.That(buttLayer, Is.LessThan(breastLayer),
                    "breasts must render above butt facing south");
            }
            finally
            {
                clientEntMan.RemoveComponent<HumanoidLayerDirectionComponent>(mob.Value);
                NsfwGate.SetState(previousNsfwState);
            }
        });

        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            if (attached != null)
            {
                server.System<SharedHumanoidAppearanceSystem>().LoadProfile(attached.Value,
                    HumanoidCharacterProfile.DefaultWithSpecies("Human"));
            }
        });

        await pair.RunTicksSync(5);
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Palmtree: exposed organs use the explicit priority stack in every direction. South keeps
    /// belly below breasts and above the groin/limbs; east/west put the belly between the far and
    /// near hand layers while preserving the same organ order.
    /// </summary>
    [Test]
    [NonParallelizable] // NsfwGate is a process-wide static.
    public async Task ExposedOrganOrderCoversDirections()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true, // This test strips the dummy's clothing/undergarments.
        });

        var server = pair.Server;
        var client = pair.Client;
        var serverPlayers = server.ResolveDependency<IPlayerManager>();

        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            Assert.That(attached, Is.Not.Null);

            var settings = new GenitalOrganSettings();
            settings.Set(GenitalType.Breasts, new GenitalOrganData
            {
                Prototype = "BreastsSplurt",
                Size = 1,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });
            settings.Set(GenitalType.Belly, new GenitalOrganData
            {
                Prototype = "BellyTummy",
                Size = 1,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });
            settings.Set(GenitalType.Vagina, new GenitalOrganData
            {
                Prototype = "VaginaHuman",
                Size = 1,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });
            settings.Set(GenitalType.Butt, new GenitalOrganData
            {
                Prototype = "ButtStandard",
                Size = 1,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });
            settings.Set(GenitalType.Balls, new GenitalOrganData
            {
                Prototype = "BallsSingle",
                Size = 1,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });
            settings.Set(GenitalType.Penis, new GenitalOrganData
            {
                Prototype = "PenisHuman",
                Size = 3,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });

            var appearance = HumanoidCharacterAppearance.DefaultWithSpecies("Human");
            appearance.Markings.Add(new Marking("SpeciesAdaptorVulpkaninLHand", 1));
            appearance.Markings.Add(new Marking("SpeciesAdaptorVulpkaninRHand", 1));

            var serverEntMan = server.ResolveDependency<IEntityManager>();
            server.System<SharedHumanoidAppearanceSystem>().LoadProfile(attached.Value,
                HumanoidCharacterProfile.DefaultWithSpecies("Human")
                    .WithCharacterAppearance(appearance)
                    .WithGenitals(settings));

            var humanoid = serverEntMan.GetComponent<HumanoidAppearanceComponent>(attached.Value);
            humanoid.UndergarmentTop = null;
            humanoid.UndergarmentBottom = null;
            humanoid.MarkingSet.RemoveCategory(MarkingCategories.UndergarmentTop);
            humanoid.MarkingSet.RemoveCategory(MarkingCategories.UndergarmentBottom);
            serverEntMan.Dirty(attached.Value, humanoid);

            // The test dummy spawns with a jumpsuit; remove it so the exposed-south path is used.
            if (serverEntMan.TryGetComponent<InventoryComponent>(attached.Value, out var inventory))
            {
                var inventorySystem = server.System<InventorySystem>();
                foreach (var slot in new[] { "jumpsuit", "outerClothing" })
                {
                    if (inventorySystem.TryGetSlotEntity(attached.Value, slot, out var worn, inventory))
                        serverEntMan.QueueDeleteEntity(worn.Value);
                }
            }
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientEntMan = client.ResolveDependency<IEntityManager>();
            var attached = client.Session.AttachedEntity;
            Assert.That(attached, Is.Not.Null);

            var appearance = client.System<HumanoidAppearanceSystem>();
            var direction = clientEntMan.EnsureComponent<HumanoidLayerDirectionComponent>(attached.Value);

            var sprite = clientEntMan.GetComponent<SpriteComponent>(attached.Value);
            var spriteSystem = client.System<SpriteSystem>();

            var previousNsfwState = NsfwGate.State;
            NsfwGate.SetState(NsfwConsentState.Allowed);

            try
            {
                direction.Direction = RsiDirection.South;
                appearance.RefreshSprite(attached.Value);

                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "GenitalBellyTummy1-belly_1_tummy", out var bellyLayer, false), Is.True,
                    "the belly marking must render");
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "PSGenitalBreasts0-m_breasts_pair_0_FRONT_primary", out var breastLayer, false), Is.True,
                    "the breast marking must render");
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "GenitalVaginaHuman-vagina_human_1_0_FRONT", out var vaginaLayer, false), Is.True,
                    "the vagina marking must render");
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "GenitalButt1-butt_pair_2_0_FRONT", out var buttLayer, false), Is.True,
                    "the butt marking must render");
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "Genital-Balls-Single1-testicles_single_1_0_FRONT", out var ballsLayer, false), Is.True,
                    "the balls marking must render");
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "Genital-Penis-Human-3-0-penis_human_3_0_FRONT", out var penisLayer, false), Is.True,
                    "the penis marking must render");
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.LArm, out var armLayer, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.LHand, out var handLayer, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.LFoot, out var footLayer, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.Chest, out var bodyLayer, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "neck", out var neckLayer, false), Is.True);

                Assert.That(buttLayer, Is.LessThan(bellyLayer),
                    "the belly must cover the butt");
                Assert.That(vaginaLayer, Is.LessThan(bellyLayer),
                    "the belly must cover the vagina");
                Assert.That(vaginaLayer, Is.LessThan(ballsLayer),
                    "the vagina must render below the balls");
                Assert.That(ballsLayer, Is.LessThan(penisLayer),
                    "the balls must render below the penis");
                Assert.That(bellyLayer, Is.LessThan(penisLayer),
                    "the penis must render above the belly in the exposed south stack");
                Assert.That(breastLayer, Is.LessThan(penisLayer),
                    "the penis must render above exposed breasts in the exposed south stack");
                Assert.That(bellyLayer, Is.GreaterThan(armLayer),
                    "the belly must render above the arms");
                Assert.That(bellyLayer, Is.GreaterThan(handLayer),
                    "the belly must render above the hands");
                Assert.That(bellyLayer, Is.GreaterThan(footLayer),
                    "the belly must render above the feet");
                Assert.That(bellyLayer, Is.GreaterThan(bodyLayer),
                    "the belly must render above the body");
                Assert.That(bellyLayer, Is.LessThan(breastLayer),
                    "the belly must render below the breasts when facing south");
                Assert.That(breastLayer, Is.LessThan(neckLayer),
                    "exposed breasts must stay below neckwear so scarves cover them");
                Assert.That(bellyLayer, Is.LessThan(neckLayer),
                    "the exposed organ stack must stay below neckwear");

                direction.Direction = RsiDirection.East;
                appearance.RefreshSprite(attached.Value);

                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "GenitalBellyTummy1-belly_1_tummy", out var eastBelly, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "GenitalButt1-butt_pair_2_0_FRONT", out var eastButt, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "PSGenitalBreasts0-m_breasts_pair_0_FRONT_primary", out var eastBreast, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.RHand, out var eastRightHand, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.LHand, out var eastLeftHand, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "SpeciesAdaptorVulpkaninLHand-l_hand", out var eastLeftAdaptor, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "SpeciesAdaptorVulpkaninRHand-r_hand", out var eastRightAdaptor, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "neck", out var eastNeck, false), Is.True);

                Assert.That(eastButt, Is.LessThan(eastBelly),
                    "the south/east priority order must not change");
                Assert.That(eastBelly, Is.GreaterThan(eastLeftHand),
                    "east belly must render above the left/far hand");
                Assert.That(eastBelly, Is.GreaterThan(eastLeftAdaptor),
                    "east belly must render above the left/far hand adaptor");
                Assert.That(eastBelly, Is.LessThan(eastRightHand),
                    "east belly must render below the right/near hand");
                Assert.That(eastBelly, Is.LessThan(eastRightAdaptor),
                    "east belly must render below the right/near hand adaptor");
                Assert.That(eastBelly, Is.LessThan(eastBreast),
                    "the belly must stay below breasts in side views");
                Assert.That(eastBreast, Is.LessThan(eastNeck),
                    "neckwear must stay above the side organ stack");

                direction.Direction = RsiDirection.West;
                appearance.RefreshSprite(attached.Value);

                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "GenitalBellyTummy1-belly_1_tummy", out var westBelly, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "GenitalButt1-butt_pair_2_0_FRONT", out var westButt, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "PSGenitalBreasts0-m_breasts_pair_0_FRONT_primary", out var westBreast, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.LHand, out var westLeftHand, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.RHand, out var westRightHand, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "SpeciesAdaptorVulpkaninRHand-r_hand", out var westRightAdaptor, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "SpeciesAdaptorVulpkaninLHand-l_hand", out var westLeftAdaptor, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "neck", out var westNeck, false), Is.True);

                Assert.That(westButt, Is.LessThan(westBelly),
                    "the west priority order must not change");
                Assert.That(westBelly, Is.GreaterThan(westRightHand),
                    "west belly must render above the right/far hand");
                Assert.That(westBelly, Is.GreaterThan(westRightAdaptor),
                    "west belly must render above the right/far hand adaptor");
                Assert.That(westBelly, Is.LessThan(westLeftHand),
                    "west belly must render below the left/near hand");
                Assert.That(westBelly, Is.LessThan(westLeftAdaptor),
                    "west belly must render below the left/near hand adaptor");
                Assert.That(westBelly, Is.LessThan(westBreast),
                    "the belly must stay below breasts in side views");
                Assert.That(westBreast, Is.LessThan(westNeck),
                    "neckwear must stay above the side organ stack");

                direction.Direction = RsiDirection.North;
                appearance.RefreshSprite(attached.Value);

                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "PSGenitalBreasts0-m_breasts_pair_0_FRONT_primary", out var northBreast, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.Chest, out var northChest, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.TailBehind, out var northTailBehind, false), Is.True);

                Assert.That(sprite[northBreast].Visible, Is.True,
                    "breasts must stay visible for sideboob when facing north");
                Assert.That(northBreast, Is.LessThan(northChest),
                    "breasts must render behind the body when facing north");
                Assert.That(northBreast, Is.GreaterThan(northTailBehind),
                    "breasts must sit above the TailBehind base layer");

                // In-game the direction comes from the transform rather than the editor override.
                clientEntMan.RemoveComponent<HumanoidLayerDirectionComponent>(attached.Value);
                client.System<SharedTransformSystem>().SetLocalRotation(attached.Value, Direction.North.ToAngle());
                appearance.RefreshSprite(attached.Value);

                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "PSGenitalBreasts0-m_breasts_pair_0_FRONT_primary", out var transformBreast, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.Chest, out var transformChest, false), Is.True);
                Assert.That(sprite[transformBreast].Visible, Is.True);
                Assert.That(transformBreast, Is.LessThan(transformChest),
                    "breasts must render behind the body from a transform-driven north view");

                // The in-game camera/eye rotation can make worldRotation + eyeRotation negative.
                // The renderer normalizes that angle; layer ordering must normalize it too, or a
                // north-facing character is treated as West and the north move never runs.
                var eyeRotation = client.ResolveDependency<IEyeManager>().CurrentEye.Rotation;
                var transformSystem = client.System<SharedTransformSystem>();
                var xform = clientEntMan.GetComponent<TransformComponent>(attached.Value);
                var currentWorld = transformSystem.GetWorldRotation(xform);
                var desiredWorld = new Angle(-Math.PI) - eyeRotation;
                transformSystem.SetLocalRotation(attached.Value,
                    xform.LocalRotation + (desiredWorld - currentWorld));
                appearance.RefreshSprite(attached.Value);

                Assert.That(appearance.GetScreenDirection(attached.Value), Is.EqualTo(RsiDirection.North),
                    "a negative on-screen angle must still resolve to north");
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "PSGenitalBreasts0-m_breasts_pair_0_FRONT_primary", out var negativeBreast, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.Chest, out var negativeChest, false), Is.True);
                Assert.That(negativeBreast, Is.LessThan(negativeChest),
                    "north organs must move behind the body for a negative on-screen angle");

                // Legacy or server-generated markings may not carry the undergarment flag. They
                // must still move behind the body because every non-butt genital organ is front-mounted.
                var humanoid = clientEntMan.GetComponent<HumanoidAppearanceComponent>(attached.Value);
                if (humanoid.MarkingSet.TryGetCategory(MarkingCategories.Genital, out var genitalMarkings))
                {
                    foreach (var marking in genitalMarkings)
                    {
                        if (marking.MarkingId.Contains("Breasts", StringComparison.OrdinalIgnoreCase))
                            marking.RenderUnderTopUndergarment = false;
                    }
                }

                appearance.RefreshSprite(attached.Value);

                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "PSGenitalBreasts0-m_breasts_pair_0_FRONT_primary", out var legacyBreast, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.Chest, out var legacyChest, false), Is.True);
                Assert.That(legacyBreast, Is.LessThan(legacyChest),
                    "flagless breast markings must still render behind the body facing north");

                // "Never hidden" organs draw over clothing, but a front-mounted breast must still be
                // behind the body from the back view instead of floating over the torso.
                if (humanoid.MarkingSet.TryGetCategory(MarkingCategories.Genital, out genitalMarkings))
                {
                    foreach (var marking in genitalMarkings)
                    {
                        if (marking.MarkingId.Contains("Breasts", StringComparison.OrdinalIgnoreCase))
                            marking.RenderOverClothing = true;
                    }
                }

                appearance.RefreshSprite(attached.Value);

                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "PSGenitalBreasts0-m_breasts_pair_0_FRONT_primary", out var neverHiddenBreast, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.Chest, out var neverHiddenChest, false), Is.True);
                Assert.That(neverHiddenBreast, Is.LessThan(neverHiddenChest),
                    "never-hidden breasts must still render behind the body facing north");

                foreach (var marking in genitalMarkings)
                {
                    if (marking.MarkingId.Contains("Breasts", StringComparison.OrdinalIgnoreCase))
                        marking.RenderOverClothing = false;
                }

                appearance.RefreshSprite(attached.Value);
            }
            finally
            {
                clientEntMan.RemoveComponent<HumanoidLayerDirectionComponent>(attached.Value);
                NsfwGate.SetState(previousNsfwState);
            }
        });

        // Palmtree: if another system re-applies an organ layer above the body while facing north,
        // the per-frame north pass must restore it without waiting for a rotation.
        // Rotate on the server so the client keeps facing north across ticks (a client-only
        // rotation is overwritten by the next transform state).
        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            Assert.That(attached, Is.Not.Null);
            server.System<SharedTransformSystem>().SetLocalRotation(attached.Value, Direction.North.ToAngle());
        });

        await pair.RunTicksSync(5);

        var nsfwStateBeforeSafetyPass = NsfwConsentState.Allowed;
        await client.WaitAssertion(() =>
        {
            var clientEntMan = client.ResolveDependency<IEntityManager>();
            var attached = client.Session.AttachedEntity;
            Assert.That(attached, Is.Not.Null);

            nsfwStateBeforeSafetyPass = NsfwGate.State;
            NsfwGate.SetState(NsfwConsentState.Allowed);

            var sprite = clientEntMan.GetComponent<SpriteComponent>(attached.Value);
            var spriteSystem = client.System<SpriteSystem>();

            const string breastId = "PSGenitalBreasts0-m_breasts_pair_0_FRONT_primary";
            Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite), breastId, out var breast, false), Is.True);
            Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite), HumanoidVisualLayers.Chest, out var chest, false), Is.True);

            Assert.That(spriteSystem.RemoveLayer((attached.Value, sprite), breast, out var breastLayer, false), Is.True);
            var bumped = spriteSystem.AddLayer((attached.Value, sprite), breastLayer!, chest);
            spriteSystem.LayerMapSet((attached.Value, sprite), breastId, bumped);

            Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite), HumanoidVisualLayers.Chest, out var shiftedChest, false), Is.True);
            Assert.That(bumped, Is.GreaterThan(shiftedChest),
                "the test must place the breast above the body first");
        });

        // Keep NSFW allowed across the ticks: toggling it would rebuild the sprite and hide the
        // very defect this check is meant to catch.
        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var clientEntMan = client.ResolveDependency<IEntityManager>();
            var attached = client.Session.AttachedEntity;
            Assert.That(attached, Is.Not.Null);

            var sprite = clientEntMan.GetComponent<SpriteComponent>(attached.Value);
            var spriteSystem = client.System<SpriteSystem>();

            try
            {
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "PSGenitalBreasts0-m_breasts_pair_0_FRONT_primary", out var breast, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.Chest, out var chest, false), Is.True);
                Assert.That(breast, Is.LessThan(chest),
                    "the north safety pass must pull a re-applied breast back behind the body");
            }
            finally
            {
                NsfwGate.SetState(nsfwStateBeforeSafetyPass);
            }
        });

        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            if (attached != null)
            {
                server.System<SharedHumanoidAppearanceSystem>().LoadProfile(attached.Value,
                    HumanoidCharacterProfile.DefaultWithSpecies("Human"));
            }
        });

        await pair.RunTicksSync(5);
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Palmtree: removing the jumpsuit changes whether organs are exposed, but the marking set is
    /// unchanged. The client must rebuild locally so the organ stack moves without waiting for a
    /// rotation (this also fixes stale backpack/strap occlusion after undressing).
    /// </summary>
    [Test]
    [NonParallelizable] // NsfwGate is a process-wide static.
    public async Task UndressRefreshesExposedOrganLayers()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });

        var server = pair.Server;
        var client = pair.Client;
        var serverPlayers = server.ResolveDependency<IPlayerManager>();

        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            Assert.That(attached, Is.Not.Null);

            var settings = new GenitalOrganSettings();
            settings.Set(GenitalType.Breasts, new GenitalOrganData
            {
                Prototype = "BreastsSplurt",
                Size = 1,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });
            settings.Set(GenitalType.Belly, new GenitalOrganData
            {
                Prototype = "BellyTummy",
                Size = 1,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });

            server.System<SharedHumanoidAppearanceSystem>().LoadProfile(attached.Value,
                HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(settings));
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientEntMan = client.ResolveDependency<IEntityManager>();
            var attached = client.Session.AttachedEntity;
            Assert.That(attached, Is.Not.Null);

            var appearance = client.System<HumanoidAppearanceSystem>();
            var direction = clientEntMan.EnsureComponent<HumanoidLayerDirectionComponent>(attached.Value);
            var sprite = clientEntMan.GetComponent<SpriteComponent>(attached.Value);
            var spriteSystem = client.System<SpriteSystem>();

            var previousNsfwState = NsfwGate.State;
            NsfwGate.SetState(NsfwConsentState.Allowed);

            try
            {
                direction.Direction = RsiDirection.South;
                appearance.RefreshSprite(attached.Value);

                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "GenitalBellyTummy1-belly_1_tummy", out var clothedBelly, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.UndergarmentBottom, out var underBottom, false), Is.True);
                Assert.That(clothedBelly, Is.LessThan(underBottom),
                    "a clothed belly must stay clamped below the undergarment band");
            }
            finally
            {
                clientEntMan.RemoveComponent<HumanoidLayerDirectionComponent>(attached.Value);
                NsfwGate.SetState(previousNsfwState);
            }
        });

        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            Assert.That(attached, Is.Not.Null);

            var serverEntMan = server.ResolveDependency<IEntityManager>();
            var inventory = server.System<InventorySystem>();
            if (serverEntMan.TryGetComponent<InventoryComponent>(attached.Value, out var inv)
                && inventory.TryGetSlotEntity(attached.Value, "jumpsuit", out var jumpsuit, inv))
            {
                serverEntMan.QueueDeleteEntity(jumpsuit.Value);
            }
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientEntMan = client.ResolveDependency<IEntityManager>();
            var attached = client.Session.AttachedEntity;
            Assert.That(attached, Is.Not.Null);

            var sprite = clientEntMan.GetComponent<SpriteComponent>(attached.Value);
            var spriteSystem = client.System<SpriteSystem>();

            var previousNsfwState = NsfwGate.State;
            NsfwGate.SetState(NsfwConsentState.Allowed);

            try
            {
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    "GenitalBellyTummy1-belly_1_tummy", out var exposedBelly, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.UndergarmentBottom, out var underBottom, false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet((attached.Value, sprite),
                    HumanoidVisualLayers.GenitalUnder, out var genitalUnder, false), Is.True);

                Assert.That(exposedBelly, Is.GreaterThan(underBottom),
                    "undressing must rebuild the belly onto the exposed organ stack without a rotation");
                Assert.That(exposedBelly, Is.GreaterThan(genitalUnder),
                    "the exposed belly must sit above GenitalUnder");
            }
            finally
            {
                NsfwGate.SetState(previousNsfwState);
            }
        });

        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            if (attached != null)
            {
                server.System<SharedHumanoidAppearanceSystem>().LoadProfile(attached.Value,
                    HumanoidCharacterProfile.DefaultWithSpecies("Human"));
            }
        });

        await pair.RunTicksSync(5);
        await pair.CleanReturnAsync();
    }

    private static bool HasGenitalMarking(HumanoidAppearanceComponent humanoid)
    {
        return humanoid.MarkingSet.TryGetCategory(MarkingCategories.Genital, out var markings)
               && markings.Count > 0;
    }

    /// <summary>
    /// Palmtree: in side views organs use the clamped under-clothing layer path. The penis must
    /// stay above the balls, and a marking's own sprites (breast + nipple) must keep their order.
    /// </summary>
    [Test]
    [NonParallelizable] // NsfwGate is a process-wide static and other tests update it.
    public async Task SideViewOrganLayersKeepTheirOrder()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var client = pair.Client;

        var serverPlayers = server.ResolveDependency<IPlayerManager>();

        EntityUid mob = default;
        await server.WaitAssertion(() =>
        {
            var attached = serverPlayers.Sessions.Single().AttachedEntity;
            Assert.That(attached, Is.Not.Null);
            mob = attached.Value;

            var appearance = server.System<SharedHumanoidAppearanceSystem>();

            var settings = new GenitalOrganSettings();
            settings.Set(GenitalType.Penis, new GenitalOrganData
            {
                Prototype = "PenisHuman",
                Size = 3,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });
            settings.Set(GenitalType.Balls, new GenitalOrganData
            {
                Prototype = "BallsSingle",
                Size = 1,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });
            settings.Set(GenitalType.Breasts, new GenitalOrganData
            {
                Prototype = "BreastsSplurt",
                Size = 1,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });
            settings.Set(GenitalType.Butt, new GenitalOrganData
            {
                Prototype = "ButtStandard",
                Size = 1,
                Visibility = GenitalVisibility.VisibleUnderClothes,
            });

            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(settings);
            profile.Appearance.Markings.Add(new Marking("UndergarmentBottomBriefs", 1));
            appearance.LoadProfile(mob, profile);
        });

        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            var clientEntMan = client.ResolveDependency<IEntityManager>();
            var mob = client.Session.AttachedEntity;
            Assert.That(mob, Is.Not.Null);

            var previousNsfwState = NsfwGate.State;
            NsfwGate.SetState(NsfwConsentState.Allowed);

            try
            {
                var appearance = client.System<HumanoidAppearanceSystem>();
                var direction = clientEntMan.EnsureComponent<HumanoidLayerDirectionComponent>(mob.Value);
                direction.Direction = RsiDirection.East;
                appearance.RefreshSprite(mob.Value);

                var sprite = clientEntMan.GetComponent<SpriteComponent>(mob.Value);
                var spriteSystem = client.System<SpriteSystem>();

                Assert.That(spriteSystem.LayerMapTryGet(
                    (mob.Value, sprite),
                    "Genital-Penis-Human-3-0-penis_human_3_0_FRONT",
                    out var penisLayer,
                    false), Is.True, "the penis layer must exist in the side view");
                Assert.That(spriteSystem.LayerMapTryGet(
                    (mob.Value, sprite),
                    "Genital-Balls-Single1-testicles_single_1_0_FRONT",
                    out var ballsLayer,
                    false), Is.True, "the balls layer must exist in the side view");

                Assert.That(penisLayer, Is.GreaterThan(ballsLayer),
                    "the penis must render above the balls when facing east or west");

                Assert.That(spriteSystem.LayerMapTryGet(
                    (mob.Value, sprite),
                    "PSGenitalBreasts0-m_breasts_pair_0_FRONT_primary",
                    out var breastLayer,
                    false), Is.True, "the breast layer must exist in the side view");
                Assert.That(penisLayer, Is.LessThan(breastLayer),
                    "east/west keep the normal priority order: penis below breasts");
                Assert.That(spriteSystem.LayerMapTryGet(
                    (mob.Value, sprite),
                    "PSGenitalBreasts0-m_breasts_pair_0_FRONT_secondary",
                    out var nippleLayer,
                    false), Is.True, "the nipple layer must exist in the side view");

                Assert.That(nippleLayer, Is.GreaterThan(breastLayer),
                    "nipples must render above breasts when facing east or west");

                Assert.That(spriteSystem.LayerMapTryGet(
                    (mob.Value, sprite),
                    "GenitalButt1-butt_pair_2_0_FRONT",
                    out var buttLayer,
                    false), Is.True, "the butt layer must exist in the side view");
                Assert.That(spriteSystem.LayerMapTryGet(
                    (mob.Value, sprite),
                    "UndergarmentBottomBriefs-briefs",
                    out var underwearLayer,
                    false), Is.True, "the bottom undergarment layer must exist in the side view");

                Assert.That(buttLayer, Is.LessThan(underwearLayer),
                    "bottom organs must render under back undergarments");

                Assert.That(spriteSystem.LayerMapTryGet(
                    (mob.Value, sprite),
                    "neck",
                    out var eastNeckLayer,
                    false), Is.True, "the neck layer must exist in the side view");
                Assert.That(spriteSystem.LayerMapTryGet(
                    (mob.Value, sprite),
                    HumanoidVisualLayers.Hair,
                    out var eastHairLayer,
                    false), Is.True, "the hair layer must exist in the side view");

                Assert.That(eastNeckLayer, Is.GreaterThan(breastLayer),
                    "neck clothing must render above breasts in every direction");
                Assert.That(eastNeckLayer, Is.LessThan(eastHairLayer),
                    "hair must stay above neck clothing in every direction");

                direction.Direction = RsiDirection.South;
                appearance.RefreshSprite(mob.Value);

                Assert.That(spriteSystem.LayerMapTryGet(
                    (mob.Value, sprite),
                    "GenitalButt1-butt_pair_2_0_FRONT",
                    out var southButtLayer,
                    false), Is.True);
                Assert.That(spriteSystem.LayerMapTryGet(
                    (mob.Value, sprite),
                    "UndergarmentBottomBriefs-briefs",
                    out var southUnderwearLayer,
                    false), Is.True);

                Assert.That(southButtLayer, Is.LessThan(southUnderwearLayer),
                    "bottom organs must also render under back undergarments facing south");

                Assert.That(spriteSystem.LayerMapTryGet(
                    (mob.Value, sprite),
                    HumanoidVisualLayers.LLeg,
                    out var southLegLayer,
                    false), Is.True, "the left-leg layer must exist");
                Assert.That(southButtLayer, Is.GreaterThan(southLegLayer),
                    "clamped bottom organs must stay above leg markings");

                Assert.That(spriteSystem.LayerMapTryGet(
                    (mob.Value, sprite),
                    "neck",
                    out var neckLayer,
                    false), Is.True, "the neck layer must exist");
                Assert.That(spriteSystem.LayerMapTryGet(
                    (mob.Value, sprite),
                    "PSGenitalBreasts0-m_breasts_pair_0_FRONT_primary",
                    out var southBreastLayer,
                    false), Is.True, "the breast layer must exist facing south");

                Assert.That(neckLayer, Is.GreaterThan(southBreastLayer),
                    "neck clothing must render above breasts facing south");

                Assert.That(spriteSystem.LayerMapTryGet(
                    (mob.Value, sprite),
                    HumanoidVisualLayers.Hair,
                    out var hairLayer,
                    false), Is.True, "the hair layer must exist");
                Assert.That(neckLayer, Is.LessThan(hairLayer),
                    "hair must stay above neck clothing in every direction");
            }
            finally
            {
                clientEntMan.RemoveComponent<HumanoidLayerDirectionComponent>(mob.Value);
                NsfwGate.SetState(previousNsfwState);
            }
        });

        // Strip the organs again before returning the pair: deleting organ-bearing mobs during
        // pool recycling trips a DEBUG-only client body assert.
        await server.WaitAssertion(() =>
        {
            var appearance = server.System<SharedHumanoidAppearanceSystem>();
            appearance.LoadProfile(mob, HumanoidCharacterProfile.DefaultWithSpecies("Human"));
        });

        await pair.RunTicksSync(5);

        await pair.CleanReturnAsync();
    }
}
