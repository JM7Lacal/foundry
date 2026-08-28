using System.ComponentModel;
using System.Windows;
using Foundry.Presentation.ViewModels;

namespace Foundry.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            // Chequeo de validacion completo al salir.
            viewModel.ValidateCommand.Execute(null);

            if (viewModel.ErrorCount > 0
                && !Confirm($"Hay {viewModel.ErrorCount} error(es) de validacion (ver el panel de abajo). "
                           + "¿Cerrar de todos modos?"))
            {
                e.Cancel = true;
            }
            else if (viewModel.IsDirty
                     && !Confirm("Hay cambios sin guardar. ¿Cerrar de todos modos?"))
            {
                e.Cancel = true;
            }
        }

        base.OnClosing(e);
    }

    private static bool Confirm(string message) =>
        MessageBox.Show(message, "Foundry", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
}
