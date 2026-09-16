using System.Windows;
namespace TallyPrimeConnector.App;
public partial class MainWindow : Window { public MainWindow(MainViewModel viewModel) { InitializeComponent(); DataContext = viewModel; } }
