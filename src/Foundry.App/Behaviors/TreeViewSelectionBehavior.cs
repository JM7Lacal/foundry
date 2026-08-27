using System.Windows;
using System.Windows.Controls;

namespace Foundry.App.Behaviors;

/// <summary>
/// Expone <see cref="TreeView.SelectedItem"/> (de solo lectura, no bindeable) como un binding
/// <c>OneWayToSource</c> hacia el ViewModel, sin logica en el code-behind de la ventana.
/// </summary>
/// <remarks>
/// Hacen falta dos propiedades adjuntas: <see cref="TrackSelectionProperty"/> se pone a
/// <c>True</c> literal en el XAML y su callback engancha el evento; <see cref="SelectedItemProperty"/>
/// lleva el binding y recibe el valor. Con una sola propiedad el binding <c>OneWayToSource</c>
/// nunca escribe el target, asi que el callback no correria.
/// </remarks>
public static class TreeViewSelectionBehavior
{
    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.RegisterAttached(
            "SelectedItem",
            typeof(object),
            typeof(TreeViewSelectionBehavior),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty TrackSelectionProperty =
        DependencyProperty.RegisterAttached(
            "TrackSelection",
            typeof(bool),
            typeof(TreeViewSelectionBehavior),
            new PropertyMetadata(false, OnTrackSelectionChanged));

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

    public static bool GetTrackSelection(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(TrackSelectionProperty) is true;
    }

    public static void SetTrackSelection(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(TrackSelectionProperty, value);
    }

    private static void OnTrackSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TreeView treeView)
        {
            return;
        }

        treeView.SelectedItemChanged -= HandleSelectedItemChanged;

        if (e.NewValue is true)
        {
            treeView.SelectedItemChanged += HandleSelectedItemChanged;
        }
    }

    private static void HandleSelectedItemChanged(object? sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (sender is DependencyObject d)
        {
            SetSelectedItem(d, e.NewValue);
        }
    }
}
