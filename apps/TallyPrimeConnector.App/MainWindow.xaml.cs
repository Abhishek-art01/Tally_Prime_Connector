using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TallyPrimeConnector.App;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _scrollAnimationTimer;
    private double _targetVerticalOffset;
    private ScrollViewer? _scrollAnimationTarget;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _scrollAnimationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _scrollAnimationTimer.Tick += SmoothScroll;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var workArea = SystemParameters.WorkArea;
        var availableWidth = Math.Max(1, workArea.Width - 32);
        var availableHeight = Math.Max(1, workArea.Height - 32);

        MaxWidth = workArea.Width;
        MaxHeight = workArea.Height;
        Width = Math.Min(Width, availableWidth);
        Height = Math.Min(Height, availableHeight);
        Left = workArea.Left + ((workArea.Width - Width) / 2);
        Top = workArea.Top + ((workArea.Height - Height) / 2);
    }

    private void ContentScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var originalElement = e.OriginalSource as DependencyObject;
        var nestedScrollableControl = (DependencyObject?)FindAncestor<ListBox>(originalElement) ?? FindAncestor<DataGrid>(originalElement);
        var scrollTarget = nestedScrollableControl is null ? ContentScrollViewer : FindDescendant<ScrollViewer>(nestedScrollableControl) ?? ContentScrollViewer;
        if (scrollTarget.ScrollableHeight <= 0)
        {
            scrollTarget = ContentScrollViewer;
        }

        var currentTarget = _scrollAnimationTimer.IsEnabled && ReferenceEquals(_scrollAnimationTarget, scrollTarget) ? _targetVerticalOffset : scrollTarget.VerticalOffset;
        _scrollAnimationTarget = scrollTarget;
        _targetVerticalOffset = Math.Clamp(currentTarget - (e.Delta * 0.5), 0, scrollTarget.ScrollableHeight);
        _scrollAnimationTimer.Start();
        e.Handled = true;
    }

    private void SmoothScroll(object? sender, EventArgs e)
    {
        if (_scrollAnimationTarget is null)
        {
            _scrollAnimationTimer.Stop();
            return;
        }

        var distance = _targetVerticalOffset - _scrollAnimationTarget.VerticalOffset;
        if (Math.Abs(distance) < 0.5)
        {
            _scrollAnimationTarget.ScrollToVerticalOffset(_targetVerticalOffset);
            _scrollAnimationTimer.Stop();
            return;
        }

        _scrollAnimationTarget.ScrollToVerticalOffset(_scrollAnimationTarget.VerticalOffset + (distance * 0.28));
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T ancestor)
            {
                return ancestor;
            }

            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }

        return null;
    }

    private static T? FindDescendant<T>(DependencyObject element) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            var child = VisualTreeHelper.GetChild(element, index);
            if (child is T descendant)
            {
                return descendant;
            }

            var nestedDescendant = FindDescendant<T>(child);
            if (nestedDescendant is not null)
            {
                return nestedDescendant;
            }
        }

        return null;
    }

    private void LedgerSearchBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (LedgerPopup != null) LedgerPopup.IsOpen = true;
    }

    private void LedgerSearchBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (LedgerPopup != null && !LedgerPopup.IsOpen) LedgerPopup.IsOpen = true;
    }

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (LedgerPopup != null && LedgerPopup.IsOpen)
        {
            var target = e.OriginalSource as DependencyObject;
            if (target != null && FindAncestor<TextBox>(target) != LedgerSearchBox && !IsDescendant(LedgerPopup.Child, target))
            {
                LedgerPopup.IsOpen = false;
            }
        }
    }

    private static bool IsDescendant(DependencyObject? ancestor, DependencyObject? element)
    {
        if (ancestor == null) return false;
        while (element != null)
        {
            if (ReferenceEquals(element, ancestor)) return true;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        return false;
    }
}
