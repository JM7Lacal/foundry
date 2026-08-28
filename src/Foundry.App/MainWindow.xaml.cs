using System;
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

    /// <summary>
    /// En pantallas chicas (portátiles 1366×768) el tamaño "restaurado" por defecto es más alto
    /// que el área de trabajo y la barra de título queda fuera de pantalla. Se recorta al
    /// <see cref="SystemParameters.WorkArea"/> antes de mostrar la ventana.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var work = SystemParameters.WorkArea;
        Width = Math.Min(Width, work.Width);
        Height = Math.Min(Height, work.Height);
    }

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
