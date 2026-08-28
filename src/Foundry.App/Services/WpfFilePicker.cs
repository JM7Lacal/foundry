using Foundry.Presentation.Services;
using Microsoft.Win32;

namespace Foundry.App.Services;

/// <summary>Implementacion de <see cref="IFilePicker"/> con los dialogos nativos de Windows.</summary>
public sealed class WpfFilePicker : IFilePicker
{
    public string? PickOpenFile(string filter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            CheckFileExists = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSaveFile(string filter, string? suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = suggestedFileName ?? string.Empty,
            OverwritePrompt = true,
            // Nunca ofrecer por defecto el directorio del .exe (ahi vive el ejemplo y bin/ se
            // regenera en cada build, llevandose el trabajo).
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
