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
        if (DataContext is MainViewModel { IsDirty: true }
            && MessageBox.Show(
                "Hay cambios sin guardar. ¿Cerrar de todos modos?",
                "Foundry",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }
}
