using Content.Shared._PS.CCVar;
using Content.Shared.Audio.Jukebox;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;

namespace Content.Client._PS.Audio;

/// <summary>
/// Palmtree: applies the client-side jukebox volume option to every jukebox audio stream.
/// </summary>
/// <remarks>
/// The server sends the stream's base volume in <see cref="AudioComponent.Params"/>. The audio
/// system copies that value back into <see cref="AudioComponent.Volume"/> every frame, so the
/// slider has to be applied to the params, not the live volume. Keep the original base around so
/// repeated slider changes do not stack.
/// </remarks>
public sealed class JukeboxVolumeSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly Dictionary<EntityUid, float> _baseVolumes = new();
    private float _volumeOffset;

    public override void Initialize()
    {
        base.Initialize();

        // Audio streams are started during the audio system's frame update. Run before it so a new
        // stream never gets a frame of full-volume playback before the slider is applied. Frame
        // updates also run outside prediction, so SetVolume does not dirty the networked component.
        UpdatesBefore.Add(typeof(Robust.Client.Audio.AudioSystem));

        Subs.CVar(_cfg, PSCVars.JukeboxVolume, OnVolumeChanged, true);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);
    }

    private void OnVolumeChanged(float value)
    {
        _volumeOffset = SharedAudioSystem.GainToVolume(value);
        ApplyVolume();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        var query = EntityQueryEnumerator<JukeboxComponent>();
        while (query.MoveNext(out _, out var jukebox))
        {
            if (jukebox.AudioStream is not { } stream ||
                !TryComp<AudioComponent>(stream, out var audio))
            {
                continue;
            }

            // Copy the struct out first: reading a field off Params directly is treated as an
            // execute access by the access analyzer.
            var parameters = audio.Params;

            if (!_baseVolumes.TryGetValue(stream, out var baseVolume))
            {
                baseVolume = parameters.Volume;
                _baseVolumes[stream] = baseVolume;
            }

            var target = baseVolume + _volumeOffset;
            if (parameters.Volume.Equals(target))
                continue;

            // SetVolume dirties the networked AudioComponent while the client is predicting, and
            // the next predicted-entity reset then restores the server's full volume. That loop
            // makes the music jump between volumes (audible as pops), so keep this local change
            // out of the prediction dirty set. The component is still synced normally once we
            // restore the flag.
            var sync = audio.NetSyncEnabled;
            audio.NetSyncEnabled = false;
            try
            {
                _audio.SetVolume(stream, target, audio);
            }
            finally
            {
                audio.NetSyncEnabled = sync;
            }
        }
    }

    private void OnEntityTerminating(ref EntityTerminatingEvent args)
    {
        _baseVolumes.Remove(args.Entity.Owner);
    }
}
