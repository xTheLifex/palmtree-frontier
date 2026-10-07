using Content.Shared.Chat;
using Content.Shared.Radio;

namespace Content.Server.Radio;

[ByRefEvent]
public readonly record struct RadioReceiveEvent(string Message, EntityUid MessageSource, RadioChannelPrototype Channel, EntityUid RadioSource, MsgChatMessage ChatMsg, RadioMessageDataHolder? MessageDataHolder = null);

/// <summary>
/// Event raised on the parent entity of a headset radio when a radio message is received
/// </summary>
[ByRefEvent]
public readonly record struct HeadsetRadioReceiveRelayEvent(RadioReceiveEvent RelayedEvent);

/// <summary>
/// Use this event to cancel sending message per receiver
/// </summary>
[ByRefEvent]
public record struct RadioReceiveAttemptEvent(RadioChannelPrototype Channel, EntityUid RadioSource, EntityUid RadioReceiver)
{
    public readonly RadioChannelPrototype Channel = Channel;
    public readonly EntityUid RadioSource = RadioSource;
    public readonly EntityUid RadioReceiver = RadioReceiver;
    public bool Cancelled = false;
}

/// <summary>
/// Use this event to cancel sending message to every receiver
/// </summary>
[ByRefEvent]
public record struct RadioSendAttemptEvent(RadioChannelPrototype Channel, EntityUid RadioSource)
{
    public readonly RadioChannelPrototype Channel = Channel;
    public readonly EntityUid RadioSource = RadioSource;
    public bool Cancelled = false;
}

/// <summary>
/// The pieces of a radio message, kept around so range degradation can rebuild it.
/// Palmtree/Coyote: Shortband range degradation.
/// </summary>
public sealed class RadioMessageDataHolder(
    string locBase,
    Color color,
    string fontType,
    int fontSize,
    string verb,
    string channelText,
    string name,
    string message,
    EntityUid? sender,
    RadioChannelPrototype channel)
{
    /// <summary>
    /// The localization base used to format the radio message.
    /// </summary>
    public string LocBase = locBase;

    /// <summary>
    /// The channel color.
    /// </summary>
    public Color Color = color;

    /// <summary>
    /// The font used by the speaker's speech style.
    /// </summary>
    public string FontType = fontType;

    /// <summary>
    /// The font size of the speech style.
    /// </summary>
    public int FontSize = fontSize;

    /// <summary>
    /// The verb used in the message ("says", "yells", ...).
    /// </summary>
    public string Verb = verb;

    /// <summary>
    /// The piece of text identifying the radio channel.
    /// </summary>
    public string ChannelText = channelText;

    /// <summary>
    /// The name of the speaker.
    /// </summary>
    public string Name = name;

    /// <summary>
    /// The message content.
    /// </summary>
    public string Message = message;

    /// <summary>
    /// The speaker's entity.
    /// </summary>
    public EntityUid? Sender = sender;

    /// <summary>
    /// The channel the message was sent on.
    /// </summary>
    public RadioChannelPrototype Channel = channel;
}
