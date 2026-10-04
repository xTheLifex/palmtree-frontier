using Content.Shared.Speech;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._PS.Speech;

/// <summary>
/// Palmtree: overrides the voice bark (speech sounds) of an entity with the one selected in its
/// character profile. <see cref="SpeechComponent.SpeechSounds"/> is left untouched so removing
/// this component restores the species/default voice.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VoiceBarkOverrideComponent : Component
{
    /// <summary>
    /// Speech sounds to use instead of <see cref="SpeechComponent.SpeechSounds"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<SpeechSoundsPrototype>? SpeechSounds;
}
