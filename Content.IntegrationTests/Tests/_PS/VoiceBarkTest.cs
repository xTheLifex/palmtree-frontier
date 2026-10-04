using Content.Server.Speech;
using Content.Shared._PS.Speech;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared.Speech;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._PS;

/// <summary>
/// Palmtree: a character's selected voice bark overrides the species speech sounds without
/// modifying the underlying SpeechComponent.
/// </summary>
[TestFixture]
public sealed class VoiceBarkTest
{
    [Test]
    public async Task ProfileVoiceOverridesSpeciesVoice()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human").WithVoiceBark("Bass");

            var mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(mob);
            server.System<SharedHumanoidAppearanceSystem>().LoadProfile(mob, profile, humanoid);

            Assert.That(entMan.TryGetComponent<SpeechComponent>(mob, out var speech), Is.True);
            Assert.That(speech!.SpeechSounds, Is.EqualTo(new ProtoId<SpeechSoundsPrototype>("Alto")),
                "The species speech sounds should be left untouched.");
            Assert.That(entMan.TryGetComponent<VoiceBarkOverrideComponent>(mob, out var voiceBark), Is.True);
            Assert.That(voiceBark!.SpeechSounds, Is.EqualTo(new ProtoId<SpeechSoundsPrototype>("Bass")));

            // The speech sound system must actually use the override.
            var speechSoundSystem = server.System<SpeechSoundSystem>();
            var bass = protoMan.Index<SpeechSoundsPrototype>("Bass");
            Assert.That(speechSoundSystem.GetSpeechSound((mob, speech), "Hello!"), Is.SameAs(bass.ExclaimSound));

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DefaultVoiceClearsOverride()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human").WithVoiceBark("Bass");
            var mob = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var humanoid = entMan.GetComponent<HumanoidAppearanceComponent>(mob);
            var humanoidSystem = server.System<SharedHumanoidAppearanceSystem>();

            humanoidSystem.LoadProfile(mob, profile, humanoid);
            Assert.That(entMan.HasComponent<VoiceBarkOverrideComponent>(mob), Is.True);

            // Re-loading a default profile must restore the species voice.
            humanoidSystem.LoadProfile(mob, profile.WithVoiceBark(null), humanoid);
            Assert.That(entMan.HasComponent<VoiceBarkOverrideComponent>(mob), Is.False);

            var speech = entMan.GetComponent<SpeechComponent>(mob);
            Assert.That(speech.SpeechSounds, Is.EqualTo(new ProtoId<SpeechSoundsPrototype>("Alto")));

            entMan.DeleteEntity(mob);
        });

        await pair.CleanReturnAsync();
    }
}
