using System.Linq;
using Content.Shared.DoAfter;
using Content.Shared.FloofStation;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.FloofStation.ModifyUndies;

/// <summary>
/// Palmtree/Floof: lets a humanoid show or hide their undergarment and genital markings.
/// </summary>
/// <remarks>
/// No consent system is present on this fork, so only the owner of a mob may toggle its markings.
/// </remarks>
public sealed class ModifyUndiesSystem : EntitySystem
{
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;

    public static readonly VerbCategory UndiesCat =
        new("verb-categories-undies", "/Textures/Interface/VerbIcons/undies.png");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ModifyUndiesComponent, GetVerbsEvent<Verb>>(AddModifyUndiesVerb);
        SubscribeLocalEvent<ModifyUndiesComponent, ModifyUndiesDoAfterEvent>(ToggleUndies);
    }

    private void AddModifyUndiesVerb(EntityUid uid, ModifyUndiesComponent component, GetVerbsEvent<Verb> args)
    {
        if (args.Hands == null || !args.CanAccess || !args.CanInteract)
            return;

        if (!TryComp<HumanoidAppearanceComponent>(args.Target, out var humApp))
            return;

        // Without a consent system in place, only the owner can toggle their own markings.
        if (args.User != args.Target)
            return;

        foreach (var marking in humApp.MarkingSet.Markings.Values.SelectMany(markingList => markingList))
        {
            if (!_markingManager.TryGetMarking(marking, out var mProt))
                continue;

            var partSlot = mProt.BodyPart;
            if (!component.BodyPartTargets.Contains(partSlot))
                continue;

            var localizedName = Loc.GetString($"marking-{mProt.ID}");
            var isVisible = marking.Visible;
            if (mProt.Sprites.Count < 1)
                continue;

            var icon = partSlot switch
            {
                HumanoidVisualLayers.UndergarmentTop => new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/bra.png")),
                HumanoidVisualLayers.UndergarmentBottom => new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/underpants.png")),
                HumanoidVisualLayers.Genital => new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/love.png")),
                _ => mProt.Sprites.FirstOrDefault() ?? new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/undies.png"))
            };

            var verb = new Verb
            {
                Text = Loc.GetString(
                    "modify-undies-verb-text",
                    ("undies", localizedName),
                    ("isVisible", isVisible)),
                Icon = icon,
                Category = UndiesCat,
                Act = () =>
                {
                    var ev = new ModifyUndiesDoAfterEvent(mProt.ID, isVisible);
                    var doAfterArgs = new DoAfterArgs(
                        EntityManager,
                        args.User,
                        1f,
                        ev,
                        args.Target,
                        args.Target,
                        used: args.User)
                    {
                        Hidden = false,
                        MovementThreshold = 0,
                        RequireCanInteract = true,
                        BlockDuplicate = true
                    };

                    _popupSystem.PopupCoordinates(
                        Loc.GetString(
                            "marking-toggle-self-start",
                            ("marking-name", localizedName),
                            ("verb", isVisible ? "hide" : "show")),
                        Transform(args.Target).Coordinates,
                        Filter.Entities(args.Target),
                        true,
                        PopupType.Medium);

                    var rufthleAudio = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");
                    _audio.PlayEntity(
                        rufthleAudio,
                        Filter.Entities(args.User, args.Target),
                        args.Target,
                        false,
                        AudioParams.Default.WithVariation(2f).WithVolume(0.5f));

                    _doAfterSystem.TryStartDoAfter(doAfterArgs);
                },
            };

            args.Verbs.Add(verb);
        }
    }

    private void ToggleUndies(
        EntityUid uid,
        ModifyUndiesComponent component,
        ModifyUndiesDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!_markingManager.Markings.TryGetValue(args.MarkingId, out var prototype))
            return;

        if (!TryComp<HumanoidAppearanceComponent>(args.Target, out var humApp))
            return;

        _humanoid.SetMarkingVisibility(uid, humApp, args.MarkingId, !args.IsVisible);

        _popupSystem.PopupCoordinates(
            Loc.GetString(
                "marking-toggle-self",
                ("marking-name", Loc.GetString($"marking-{prototype.ID}")),
                ("verb", args.IsVisible ? "hide" : "show")),
            Transform(args.Target.Value).Coordinates,
            Filter.Entities(args.Target.Value),
            true,
            PopupType.Medium);

        var rufthleAudio = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");
        _audio.PlayEntity(
            rufthleAudio,
            Filter.Entities(args.User, args.Target.Value),
            args.Target.Value,
            false,
            AudioParams.Default.WithVariation(0.5f).WithVolume(0.5f));
    }
}
