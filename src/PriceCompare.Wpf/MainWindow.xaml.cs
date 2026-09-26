using System.Windows;
using PriceCompare.Wpf.ViewModels;

namespace PriceCompare.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
