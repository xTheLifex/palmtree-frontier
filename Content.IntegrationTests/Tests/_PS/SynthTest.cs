#nullable enable
using System;
using System.Linq;
using Content.Server.Atmos.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.FixedPoint;
using Content.Shared._PS.Synth;
using Content.Shared._PS.Synth.DeadStartupButton;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Medical.Healing;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: synth physiology - sealed chassis (no pressure damage), rebootable when dead, and the
/// synthetic repair topicals actually accept the Synth damage container.
/// </summary>
[TestFixture]
public sealed class SynthTest
{
    [Test]
    public async Task SynthPhysiology()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();

        EntityUid synth = default;
        EntityUid wreck = default;
        EntityUid medic = default;

        await server.WaitPost(() =>
        {
            var damageable = entMan.System<DamageableSystem>();
            var blunt = protoMan.Index<DamageTypePrototype>("Blunt");
            var heat = protoMan.Index<DamageTypePrototype>("Heat");
            var doAfter = entMan.System<SharedDoAfterSystem>();

            medic = entMan.SpawnEntity("MobHuman", map.GridCoords);

            // A synth that died with modest overkill can be rebooted.
            synth = entMan.SpawnEntity("MobSynth", map.GridCoords);
            damageable.TryChangeDamage(synth, new DamageSpecifier(blunt, 800));

            // A wrecked chassis fails to reboot.
            wreck = entMan.SpawnEntity("MobSynth", map.GridCoords);
            damageable.TryChangeDamage(wreck, new DamageSpecifier(heat, 1000));

            foreach (var target in new[] { synth, wreck })
            {
                doAfter.TryStartDoAfter(new DoAfterArgs(entMan, medic, TimeSpan.FromSeconds(0.1),
                    new SharedDeadStartupButtonSystem.OnDoAfterButtonPressedEvent(), target, target: target));
            }
        });

        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var mobState = entMan.System<MobStateSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<SynthComponent>(synth), Is.True, "MobSynth should be a synth");
                Assert.That(entMan.HasComponent<PressureImmunityComponent>(synth), Is.True,
                    "synths should be immune to pressure damage in space");
                Assert.That(entMan.HasComponent<DeadStartupButtonComponent>(synth), Is.True,
                    "synths should have the reboot button");
                Assert.That(entMan.GetComponent<FlammableComponent>(synth).Damage.GetTotal(), Is.EqualTo(FixedPoint2.Zero),
                    "synth fire damage should be visual only (lava and liquid plasma must not hurt synths)");
                Assert.That(mobState.IsDead(synth), Is.False, "reboot should revive a synth with modest overkill");
                Assert.That(mobState.IsDead(wreck), Is.True, "a wrecked chassis should fail to reboot");
            });

            // The synthetic repair topicals accept the Synth damage container.
            foreach (var item in new[] { "RepairPatch", "RepairSpray", "RepairSprayPlus" })
            {
                var uid = entMan.SpawnEntity(item, map.GridCoords);
                var healing = entMan.GetComponent<HealingComponent>(uid);
                Assert.That(healing.DamageContainers?.Any(x => x.ToString() == "Synth"), Is.True,
                    $"{item} should heal the Synth damage container");
                entMan.DeleteEntity(uid);
            }

            entMan.DeleteEntity(synth);
            entMan.DeleteEntity(wreck);
            entMan.DeleteEntity(medic);
        });

        await pair.CleanReturnAsync();
    }
}
