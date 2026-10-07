using Robust.Shared.Prototypes;

namespace Content.Shared._CS.RadioNoises;

/// <summary>
/// This is a component that is used to play static noise on radios.
/// When added to something, and it gets an event that said that hey
/// this thing got a radio message, it will play some kind of static noise.
/// </summary>
[RegisterComponent]
public sealed partial class RadioStaticComponent : Component
{
    /// <summary>
    /// The prototype of the soundpack that will be played when a radio message is received.
    /// </summary>
    [DataField("baseSoundpack")]
    public ProtoId<RadioStaticPrototype> SoundPack = "RadioStaticDefault";

    /// <summary>
    /// If the radio has been intentionally genericised, use this soundpack instead.
    /// </summary>
    [DataField("genericSoundpack")]
    public ProtoId<RadioStaticPrototype> GenericSoundPack = "RadioStaticGeneric";

    /// <summary>
    /// The prototypes for the various departments.
    /// For instance if you get a message from the security department,
    /// it will play the security department's static noise, if it exists.
    /// Otherwise it will play the default static noise.
    /// Uses the IDs found in radio_channels.yml.
    /// </summary>
    [DataField("departmentSoundPacks")]
    public Dictionary<string, ProtoId<RadioStaticPrototype>> DepartmentSoundPacks = new();

    /// <summary>
    /// Which channels are squelched, since hearing kshht kshht all the time
    /// might get tiring.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public HashSet<string> SquelchedChannels = [];

    /// <summary>
    /// If this is true, the radio will not play any static noise at all.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool OmniSquelch = false;

    /// <summary>
    /// How loud the volume is. Values should be 0 or negative.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float Volume = 0f;
}
