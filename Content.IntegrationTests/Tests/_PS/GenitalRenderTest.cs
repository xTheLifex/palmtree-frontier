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
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.IoC;

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
    /// Palmtree: an exposed south-facing belly is a torso organ and must stay below the breasts.
    /// It used to remain on the raw Genital layer while uncovered breasts moved below the neck.
    /// </summary>
    [Test]
    [NonParallelizable] // NsfwGate is a process-wide static.
    public async Task ExposedSouthBellyRendersBelowBreasts()
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

            var serverEntMan = server.ResolveDependency<IEntityManager>();
            server.System<SharedHumanoidAppearanceSystem>().LoadProfile(attached.Value,
                HumanoidCharacterProfile.DefaultWithSpecies("Human").WithGenitals(settings));

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
                Assert.That(penisLayer, Is.LessThan(bellyLayer),
                    "the penis must render below the belly");
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
