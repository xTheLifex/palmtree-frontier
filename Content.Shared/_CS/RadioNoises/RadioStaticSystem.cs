using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._CS.RadioNoises;

/// <summary>
/// Plays static noise on radios when they receive a message, and stores the squelch/volume settings.
/// Ported from Coyote.
/// </summary>
public sealed class RadioStaticSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = null!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = null!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = null!;

    public static readonly VerbCategory RadioSquelchCat =
        new("verb-categories-radiosquelch", null);

    public static readonly VerbCategory RadioVolumeCat =
        new("verb-categories-radiovolume", null);

    public readonly List<float> RadioVolumeList =
        new()
        {
            0f, -1f, -2f, -3f, -4f, -5f, -6f, -7f, -8f, -9f,
        };

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<RadioStaticComponent, DoRadioStaticEvent>(OnRadioReceive);
        SubscribeLocalEvent<RadioStaticComponent, GetVerbsEvent<Verb>>(AddRadioSquelchListVerb);
    }

    private void OnRadioReceive(
        EntityUid uid,
        RadioStaticComponent component,
        DoRadioStaticEvent args)
    {
        if (IsSquelched(component, args.Channel, true))
            return;

        // First, get the correct sound pack based on the channel.
        var soundPack = component.SoundPack;
        if (!_prototype.HasIndex(soundPack))
        {
            Logger.Warning(
                $"RadioStaticComponent on {ToPrettyString(uid)} has an invalid DEFAULT sound pack: {soundPack}");
            return;
        }

        if (args.DegradationParams != null && args.DegradationParams.GenerifyStatic)
        {
            // If the channel has been genericised, use the generic sound.
            soundPack = component.GenericSoundPack;
        }
        else if (component.DepartmentSoundPacks.TryGetValue(args.Channel, out var departmentSoundPack))
        {
            if (!_prototype.HasIndex(departmentSoundPack))
            {
                Logger.Warning(
                    $"RadioStaticComponent on {ToPrettyString(uid)} has an invalid department sound pack: {departmentSoundPack}");
            }
            else
            {
                soundPack = departmentSoundPack;
            }
        }

        // Judge the intent of the message (question, exclamation, stutter, mumble...).
        var intent = GetIntentFromMessage(args.Message);

        PlaySound(
            uid,
            args.Receiver,
            soundPack,
            intent,
            component);
    }

    private void PlaySound(EntityUid uid,
        EntityUid? receiver,
        ProtoId<RadioStaticPrototype> soundPack,
        RadioStaticIntent intent,
        RadioStaticComponent component)
    {
        if (!_prototype.TryIndex(soundPack, out RadioStaticPrototype? radProt))
        {
            Logger.Warning($"RadioStaticComponent on {ToPrettyString(uid)} has an invalid sound pack: {soundPack}");
            return;
        }

        SoundSpecifier? sound;
        switch (intent)
        {
            case RadioStaticIntent.Ask:
                sound = radProt.AskSound;
                break;
            case RadioStaticIntent.Stutter:
                sound = radProt.StutterSound;
                break;
            case RadioStaticIntent.Yell:
                sound = radProt.YellSound;
                break;
            case RadioStaticIntent.Exclamation:
                sound = radProt.ExclaimSound;
                break;
            case RadioStaticIntent.Mumble:
                sound = radProt.MumbleSound;
                break;
            case RadioStaticIntent.Say:
            default:
                sound = radProt.SaySound;
                break;
        }

        // The intent-specific sound may be unset; fall back to the say sound.
        sound ??= radProt.SaySound;

        // If it's a secret sound, find the owner of the radio and play it for them only.
        if (radProt.Secret)
        {
            if (receiver is null)
                return;

            var hearer = receiver.Value;
            _audioSystem.PlayEntity(
                sound,
                hearer,
                hearer,
                AudioParams.Default.WithVolume(component.Volume));
            return;
        }

        _audioSystem.PlayPredicted(
            sound,
            uid,
            null,
            AudioParams.Default.WithVolume(component.Volume));
    }

    /// <summary>
    /// Gets the intent of the radio message based on its content.
    /// See <see cref="RadioStaticIntent"/> for the intents.
    /// </summary>
    private static RadioStaticIntent GetIntentFromMessage(string message)
    {
        if (message.EndsWith("?"))
            return RadioStaticIntent.Ask;

        // More than one exclamation mark is a yell.
        if (message.EndsWith("!!"))
            return RadioStaticIntent.Yell;

        if (message.EndsWith("!"))
            return RadioStaticIntent.Exclamation;

        if (message.EndsWith("--"))
            return RadioStaticIntent.Stutter;

        if (message.EndsWith("..."))
            return RadioStaticIntent.Mumble;

        return RadioStaticIntent.Say;
    }

    /// <summary>
    /// Makes a list of verbs that allow you to squelch the radio noise for a specific channel.
    /// </summary>
    private void AddRadioSquelchListVerb(EntityUid uid, RadioStaticComponent component, GetVerbsEvent<Verb> args)
    {
        // At the top, add a verb to toggle squelch for all channels.
        Verb toggleAllVerb = new()
        {
            Text = Loc.GetString(
                $"radio-squelch-verb-toggle-all-{(component.OmniSquelch ? "squelch" : "unsquelch")}"),
            Category = RadioSquelchCat,
            Act = () =>
            {
                ToggleOmniSquelch(component, args.User);
            },
            Disabled = false,
            Message = null
        };
        args.Verbs.Add(toggleAllVerb);

        // If it's just some handheld radio, we don't need the per-channel verbs.
        if (!TryComp<EncryptionKeyHolderComponent>(uid, out var encHolder))
            return;

        // Get a list of all the channels that this radio can hear.
        foreach (var chammel in encHolder.Channels)
        {
            var quelched = IsSquelched(component, chammel);
            Verb verb = new()
            {
                Text = Loc.GetString(
                    $"radio-squelch-verb-{(quelched ? "unsquelch" : "squelch")}",
                    ("channel", chammel)),
                Category = RadioSquelchCat,
                Act = () =>
                {
                    ToggleSquelch(component, chammel, args.User);
                },
                Disabled = false,
                Message = null
            };
            args.Verbs.Add(verb);
        }

        // And now, add verbs to change the volume of the radio.
        foreach (var volume in RadioVolumeList)
        {
            // Turn the volume into a two-digit string: 10, 09... 01.
            var adjVolume = (int) (10f + volume);
            var volumeString = adjVolume.ToString("00");
            Verb volumeVerb = new()
            {
                Text = Loc.GetString(
                    "radio-volume-verb",
                    ("volume", volumeString)),
                Category = RadioVolumeCat,
                Disabled = Math.Abs(volume - component.Volume) < 0.1f,
                Act = () =>
                {
                    SetVolume(component, volume, args.User);
                },
                Message = Loc.GetString("radio-volume-verb-message", ("volume", adjVolume)),
            };
            args.Verbs.Add(volumeVerb);
        }
    }

    /// <summary>
    /// Toggles the squelch state of a radio for a specific channel.
    /// </summary>
    private void ToggleSquelch(
        RadioStaticComponent component,
        string channel,
        EntityUid? user = null)
    {
        var isAlreadySquelched = IsSquelched(component, channel);
        if (isAlreadySquelched)
            component.SquelchedChannels.Remove(channel);
        else
            component.SquelchedChannels.Add(channel);

        if (user is { } u)
        {
            _popupSystem.PopupEntity(
                Loc.GetString(
                    $"radio-squelch-{(isAlreadySquelched ? "unsquelched" : "squelched")}",
                    ("channel", channel)),
                u,
                u);
        }
    }

    /// <summary>
    /// Sets the volume of a radio.
    /// </summary>
    private void SetVolume(
        RadioStaticComponent component,
        float volume,
        EntityUid? user = null)
    {
        component.Volume = volume;

        if (user is { } u)
        {
            _popupSystem.PopupEntity(
                Loc.GetString("radio-volume-verb-popup", ("volume", volume)),
                u,
                u);
        }
    }

    /// <summary>
    /// Toggles the omni-squelch state of a radio.
    /// </summary>
    private void ToggleOmniSquelch(
        RadioStaticComponent component,
        EntityUid? user = null)
    {
        component.OmniSquelch = !component.OmniSquelch;

        if (user is { } u)
        {
            _popupSystem.PopupEntity(
                Loc.GetString($"radio-squelch-omni-{(component.OmniSquelch ? "enabled" : "disabled")}"),
                u,
                u);
        }
    }

    /// <summary>
    /// Checks if a radio is squelched for a specific channel.
    /// </summary>
    private static bool IsSquelched(
        RadioStaticComponent component,
        string channel,
        bool checkOmniSquelch = false)
    {
        if (checkOmniSquelch && component.OmniSquelch)
            return true;

        return component.SquelchedChannels.Contains(channel);
    }
}

/// <summary>
/// Event raised on a radio when it receives a message and should play static.
/// </summary>
[ByRefEvent]
public sealed class DoRadioStaticEvent(
    EntityUid radioUid,
    EntityUid sender,
    EntityUid? receiver,
    string channel,
    string message,
    RadioDegradationParams? degradationParams = null
    ) : EntityEventArgs
{
    public EntityUid RadioUid = radioUid;
    public EntityUid Sender = sender;
    public EntityUid? Receiver = receiver;
    public string Channel = channel;
    public string Message = message;
    public RadioDegradationParams? DegradationParams = degradationParams;
}

/// <summary>
/// Describes how badly a radio message was degraded by range, and which pieces should be muddied.
/// </summary>
public sealed class RadioDegradationParams(
    float wordDropPercentage,
    float letterDropPercentage,
    int fontSizeDecrease,
    bool generifyChannel,
    bool generifyStatic,
    bool generifyName,
    bool dropMessage,
    bool dropMessageEntirely
)
{
    public float WordDropPercentage = wordDropPercentage;
    public float LetterDropPercentage = letterDropPercentage;
    public int FontSizeDecrease = fontSizeDecrease;
    public bool GenerifyChannel = generifyChannel;
    public bool GenerifyStatic = generifyStatic;
    public bool GenerifyName = generifyName;
    public bool DropMessage = dropMessage;
    public bool DropMessageEntirely = dropMessageEntirely;
    public string? NameOverride = null;
    public Color? ColorOverride = null;
    public int? FontSizeOverride = null;
    public bool Whisperfy = false;
}
