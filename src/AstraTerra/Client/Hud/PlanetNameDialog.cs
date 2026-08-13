using Vintagestory.API.Client;

namespace AstraTerra.Client.Hud;

/// <summary>
/// Asks an observer what they want to call a wandering star they have just written down.
/// </summary>
/// <remarks>
/// Deliberately offers no suggestion. The mod knows the planet as Mars; the observer does not, and
/// the whole point of identifying one is that the name is theirs.
/// </remarks>
public sealed class PlanetNameDialog : GuiDialog
{
    private const string InputKey = "name";
    private readonly string planetId;
    private readonly Action<string, string?> onRename;
    private string currentName;

    public PlanetNameDialog(ICoreClientAPI api, string planetId, string? currentName, Action<string, string?> onRename)
        : base(api)
    {
        this.planetId = planetId;
        this.onRename = onRename;
        this.currentName = currentName ?? string.Empty;
        Compose();
    }

    public override string ToggleKeyCombinationCode => null!;

    public override bool UnregisterOnClose => true;

    private void Compose()
    {
        var dialogBounds = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, -220, -95, 440, 190);
        var labelBounds = ElementBounds.Fixed(20, 45, 400, 28);
        var inputBounds = ElementBounds.Fixed(20, 78, 400, 32);
        var clearButtonBounds = ElementBounds.Fixed(145, 130, 120, 35);
        var saveButtonBounds = ElementBounds.Fixed(285, 130, 135, 35);

        SingleComposer = capi.Gui
            .CreateCompo("astraterra-name-planet", dialogBounds)
            .AddShadedDialogBG(ElementBounds.Fill, true)
            .AddDialogTitleBar("Name Wandering Star", () => TryClose())
            .AddStaticText("A star that does not keep its place", CairoFont.WhiteSmallText(), labelBounds)
            .AddTextInput(inputBounds, value => currentName = value, CairoFont.TextInput(), InputKey)
            .AddButton("Clear", ClearName, clearButtonBounds, EnumButtonStyle.Normal)
            .AddButton("Save", SaveName, saveButtonBounds, EnumButtonStyle.Normal)
            .Compose();

        SingleComposer.GetTextInput(InputKey).SetValue(currentName);
    }

    private bool ClearName()
    {
        currentName = string.Empty;
        SingleComposer.GetTextInput(InputKey).SetValue(currentName);
        return true;
    }

    private bool SaveName()
    {
        onRename(planetId, currentName);
        TryClose();
        return true;
    }
}
