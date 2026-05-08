using AstraTerra.Constellations;
using Vintagestory.API.Client;

namespace AstraTerra.Client.Hud;

public sealed class ConstellationNameDialog : GuiDialog
{
    private const string InputKey = "name";
    private readonly ConstellationRecord record;
    private readonly Action<int, string?> onRename;
    private string currentName;

    public ConstellationNameDialog(ICoreClientAPI api, ConstellationRecord record, Action<int, string?> onRename)
        : base(api)
    {
        this.record = record;
        this.onRename = onRename;
        currentName = record.Name ?? string.Empty;
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
            .CreateCompo("astraterra-name-constellation", dialogBounds)
            .AddShadedDialogBG(ElementBounds.Fill, true)
            .AddDialogTitleBar("Name Constellation", () => TryClose())
            .AddStaticText($"Constellation #{record.Id}", CairoFont.WhiteSmallText(), labelBounds)
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
        onRename(record.Id, currentName);
        TryClose();
        return true;
    }
}
