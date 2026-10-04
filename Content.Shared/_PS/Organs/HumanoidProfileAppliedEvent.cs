using Content.Shared.Preferences;

namespace Content.Shared._PS.Organs;

/// <summary>
/// Raised on a mob after <c>SharedHumanoidAppearanceSystem.LoadProfile</c> has applied a character
/// profile (spawn or lobby preview). Used by <see cref="GenitalOrganSystem"/> so it can insert the
/// profile's genital organs without creating a dependency cycle with the appearance system.
/// </summary>
public sealed class HumanoidProfileAppliedEvent(EntityUid uid, HumanoidCharacterProfile? profile) : EntityEventArgs
{
    public readonly EntityUid Uid = uid;
    public readonly HumanoidCharacterProfile? Profile = profile;
}
