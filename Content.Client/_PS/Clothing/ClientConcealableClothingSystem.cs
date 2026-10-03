using Content.Shared._PS.Clothing;
using Content.Shared.Clothing;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Client._PS.Clothing;

public sealed class ClientConcealableClothingSystem : SharedConcealableClothingSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ConcealableClothingComponent, EquipmentVisualsUpdatedEvent>(OnVisualsUpdated);
        SubscribeLocalEvent<ConcealableClothingComponent, GetEquipmentVisualsEvent>(OnGetVisuals);
    }

    private void OnGetVisuals(EntityUid uid, ConcealableClothingComponent component, GetEquipmentVisualsEvent args)
    {
        if (!component.IsConcealed)
            return;

        args.Layers.Clear();
    }

    private void OnVisualsUpdated(EntityUid uid, ConcealableClothingComponent component, EquipmentVisualsUpdatedEvent args)
    {
        if (!component.IsConcealed)
            return;

        if (!TryComp(args.Equipee, out SpriteComponent? sprite))
            return;

        foreach (var key in args.RevealedLayers)
        {
            _sprite.RemoveLayer((args.Equipee, sprite), key);
        }

        args.RevealedLayers.Clear();
    }
}
