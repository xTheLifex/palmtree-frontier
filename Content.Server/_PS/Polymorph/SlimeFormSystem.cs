using Content.Server._PS.Interactions;
using Content.Server.Polymorph.Components;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Server._PS.Polymorph;

/// <summary>
/// Palmtree: slimeperson polymorph bookkeeping.
/// </summary>
/// <remarks>
/// The polymorph system parks the original body (inventory, ID card, bank account, organs,
/// records) and restores it untouched on revert, so nothing is lost. This system only copies the
/// bank account component and the interaction panel preferences onto the slime form so banking and
/// ERP consent keep working while transformed. The bank balance is profile-authoritative (the
/// component is a cache), so the copy cannot duplicate money; the original body keeps its own
/// component.
/// </remarks>
public sealed class SlimeFormSystem : EntitySystem
{
    private static readonly EntProtoId SlimeFormProto = "MobSlimeForm";

    public override void Initialize()
    {
        base.Initialize();

        // PolymorphedEvent is raised on the entity being polymorphed, which is polymorphable.
        SubscribeLocalEvent<PolymorphableComponent, PolymorphedEvent>(OnPolymorphed);
    }

    private void OnPolymorphed(Entity<PolymorphableComponent> ent, ref PolymorphedEvent args)
    {
        // On revert the original body already has everything; nothing to copy back.
        if (args.IsRevert)
            return;

        // Only apply to the slime form, leaving other polymorphs (admin smites etc.) untouched.
        if (MetaData(args.NewEntity).EntityPrototype?.ID != SlimeFormProto.Id)
            return;

        if (HasComp<BankAccountComponent>(args.OldEntity))
        {
            // The balance is profile-authoritative and the bank system refreshes the component cache
            // from it on init/attach, so the form just needs the component; nothing to copy.
            EnsureComp<BankAccountComponent>(args.NewEntity);
        }

        if (TryComp<InteractionStateComponent>(args.OldEntity, out var oldState))
        {
            var newState = EnsureComp<InteractionStateComponent>(args.NewEntity);
            newState.Consent = oldState.Consent;
            newState.LewdSounds = oldState.LewdSounds;
            newState.UseArousalMultiplier = oldState.UseArousalMultiplier;
            newState.ArousalMultiplier = oldState.ArousalMultiplier;
            newState.UseMoaningMultiplier = oldState.UseMoaningMultiplier;
            newState.MoaningMultiplier = oldState.MoaningMultiplier;
            newState.Favorites.Clear();
            newState.Favorites.AddRange(oldState.Favorites);
        }
    }
}
