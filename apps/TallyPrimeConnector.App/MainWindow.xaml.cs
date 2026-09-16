using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TallyPrimeConnector.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void TitleBar_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.OriginalSource is DependencyObject source && FindVisualParent<Button>(source) is not null)
        {
            return;
        }

        if (eventArgs.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The pointer was released before WPF began the drag operation.
        }
    }

    private void Minimize_OnClick(object sender, RoutedEventArgs eventArgs) => WindowState = WindowState.Minimized;

    private void Maximize_OnClick(object sender, RoutedEventArgs eventArgs) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private static T? FindVisualParent<T>(DependencyObject source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T matching)
            {
                return matching;
            }
        }

        return null;
    }
}
