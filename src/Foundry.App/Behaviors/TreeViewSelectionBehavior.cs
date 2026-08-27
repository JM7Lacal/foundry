using System.Windows;
using System.Windows.Controls;

namespace Foundry.App.Behaviors;

/// <summary>
/// Propiedad adjunta que expone <see cref="TreeView.SelectedItem"/> (que es de solo lectura y no
/// bindeable) como un binding <c>OneWayToSource</c> hacia el ViewModel. Evita poner logica en el
/// code-behind de la ventana.
/// </summary>
public static class TreeViewSelectionBehavior
{
    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.RegisterAttached(
            "SelectedItem",
            typeof(object),
            typeof(TreeViewSelectionBehavior),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedItemChanged));

    public static object? GetSelectedItem(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(SelectedItemProperty);
    }

    public static void SetSelectedItem(DependencyObject element, object? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(SelectedItemProperty, value);
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TreeView treeView)
        {
            return;
        }

        treeView.SelectedItemChanged -= HandleSelectedItemChanged;
        treeView.SelectedItemChanged += HandleSelectedItemChanged;
    }

    private static void HandleSelectedItemChanged(object? sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (sender is DependencyObject d)
        {
            SetSelectedItem(d, e.NewValue);
        }
    }
}
