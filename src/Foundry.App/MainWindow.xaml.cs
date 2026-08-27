using System.Windows;
using Foundry.App.ViewModels;

namespace Foundry.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
