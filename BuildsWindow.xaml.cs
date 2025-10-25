using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace EchoLauncher;

public partial class BuildsWindow : Window
{
    private BuildManager buildManager;

    public BuildsWindow()
    {
        InitializeComponent();
        buildManager = new BuildManager();
        LoadBuilds();
    }

    private void LoadBuilds()
    {
        var builds = buildManager.LoadBuilds();
        BuildsListView.ItemsSource = builds;
    }

    private void AddBuildButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddBuildWindow();
        if (dialog.ShowDialog() == true)
        {
            try
            {
                buildManager.AddBuild(dialog.BuildName, dialog.Version, dialog.BuildType);
                LoadBuilds(); // Перезагружаем список
                MessageBox.Show("Сборка успешно создана!", "Успех");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания сборки: {ex.Message}", "Ошибка");
            }
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadBuilds();
    }

    private void DeleteBuildButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedBuild = BuildsListView.SelectedItem as BuildManager.MinecraftBuild;
        if (selectedBuild == null)
        {
            MessageBox.Show("Выберите сборку для удаления", "Ошибка");
            return;
        }

        var result = MessageBox.Show($"Удалить сборку '{selectedBuild.Name}'? Папка также будет удалена.",
                                   "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                buildManager.DeleteBuild(selectedBuild.Name);
                LoadBuilds();
                MessageBox.Show("Сборка удалена", "Успех");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка");
            }
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedBuild = BuildsListView.SelectedItem as BuildManager.MinecraftBuild;
        if (selectedBuild != null)
        {
            var buildPath = buildManager.GetBuildPath(selectedBuild.Name);
            if (Directory.Exists(buildPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", buildPath);
            }
            else
            {
                MessageBox.Show("Папка сборки не найдена", "Ошибка");
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}