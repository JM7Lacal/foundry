using System.Windows;
using Foundry.Presentation.Services;

namespace Foundry.App.Services;

/// <summary>Implementacion de <see cref="IDialogService"/> con <see cref="MessageBox"/>.</summary>
public sealed class WpfDialogService : IDialogService
{
    public bool Confirm(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void Inform(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
}
