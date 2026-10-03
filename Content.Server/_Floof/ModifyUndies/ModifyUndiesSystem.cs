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
/// Palmtree/Floof: lets a humanoid show or hide any of their markings in-game.
/// </summary>
/// <remarks>
/// Which markings can be toggled is opt-in per marking through
/// <see cref="Marking.CanToggleVisible"/> (by the owner) and
/// <see cref="Marking.OtherCanToggleVisible"/> (by other players). No consent system is present
/// on this fork, so the per-marking opt-in is the only gate for other players.
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

        var isMine = args.User == args.Target;

        foreach (var marking in humApp.MarkingSet.Markings.Values.SelectMany(markingList => markingList))
        {
            if (!_markingManager.TryGetMarking(marking, out var mProt))
                continue;

            if (!CanToggle(marking, isMine))
                continue;

            var localizedName = GetMarkingDisplayName(marking, mProt);
            var isVisible = marking.Visible;
            if (mProt.Sprites.Count < 1)
                continue;

            var icon = mProt.BodyPart switch
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

                    var verbText = isVisible ? "hide" : "show";
                    if (isMine)
                    {
                        _popupSystem.PopupCoordinates(
                            Loc.GetString(
                                "marking-toggle-self-start",
                                ("marking-name", localizedName),
                                ("verb", verbText)),
                            Transform(args.Target).Coordinates,
                            Filter.Entities(args.Target),
                            true,
                            PopupType.Medium);
                    }
                    else
                    {
                        _popupSystem.PopupCoordinates(
                            Loc.GetString(
                                "marking-toggle-other-start",
                                ("marking-name", localizedName),
                                ("verb", verbText)),
                            Transform(args.Target).Coordinates,
                            Filter.Entities(args.User),
                            true,
                            PopupType.Medium);

                        _popupSystem.PopupCoordinates(
                            Loc.GetString(
                                "marking-toggle-by-other-start",
                                ("marking-name", localizedName),
                                ("verb", verbText),
                                ("other", Identity.Entity(args.User, EntityManager))),
                            Transform(args.Target).Coordinates,
                            Filter.Entities(args.Target),
                            true,
                            PopupType.MediumCaution);
                    }

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

    private static bool CanToggle(Marking marking, bool isMine)
    {
        // The per-marking opt-in doubles as consent: other players can only toggle markings
        // whose owner explicitly allowed it in the character editor.
        return isMine ? marking.CanToggleVisible : marking.OtherCanToggleVisible;
    }

    private string GetMarkingDisplayName(Marking marking, MarkingPrototype prototype)
    {
        return string.IsNullOrWhiteSpace(marking.CustomName)
            ? Loc.GetString($"marking-{prototype.ID}")
            : marking.CustomName;
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

        Marking? targetMarking = null;
        foreach (var markingList in humApp.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (marking.MarkingId != args.MarkingId)
                    continue;

                targetMarking = marking;
                break;
            }

            if (targetMarking != null)
                break;
        }

        if (targetMarking is null)
            return;

        var isMine = args.User == args.Target;
        if (!CanToggle(targetMarking, isMine))
            return;

        var localizedName = GetMarkingDisplayName(targetMarking, prototype);
        var verbText = args.IsVisible ? "hide" : "show";

        _humanoid.SetMarkingVisibility(uid, humApp, args.MarkingId, !args.IsVisible);

        if (isMine)
        {
            _popupSystem.PopupCoordinates(
                Loc.GetString(
                    "marking-toggle-self",
                    ("marking-name", localizedName),
                    ("verb", verbText)),
                Transform(args.Target.Value).Coordinates,
                Filter.Entities(args.Target.Value),
                true,
                PopupType.Medium);
        }
        else
        {
            _popupSystem.PopupCoordinates(
                Loc.GetString(
                    "marking-toggle-other",
                    ("marking-name", localizedName),
                    ("verb", verbText)),
                Transform(args.Target.Value).Coordinates,
                Filter.Entities(args.User),
                true,
                PopupType.Medium);

            _popupSystem.PopupCoordinates(
                Loc.GetString(
                    "marking-toggle-by-other",
                    ("marking-name", localizedName),
                    ("verb", verbText),
                    ("other", Identity.Entity(args.User, EntityManager))),
                Transform(args.Target.Value).Coordinates,
                Filter.Entities(args.Target.Value),
                true,
                PopupType.MediumCaution);
        }

        var rufthleAudio = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");
        _audio.PlayEntity(
            rufthleAudio,
            Filter.Entities(args.User, args.Target.Value),
            args.Target.Value,
            false,
            AudioParams.Default.WithVariation(0.5f).WithVolume(0.5f));
    }
}
