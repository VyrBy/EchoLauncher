using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace EchoLauncher;

public partial class AddBuildWindow : Window
{
    private BuildManager buildManager;

    public string BuildName => NameTextBox.Text;
    public string BuildType => (TypeComboBox.SelectedItem as ComboBoxItem)?.Tag.ToString();
    public string Version => VersionComboBox.SelectedItem as string;

    public AddBuildWindow()
    {
        InitializeComponent();
        buildManager = new BuildManager();
        TypeComboBox.SelectedIndex = 0;
    }

    private async void TypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        await LoadVersionsAsync();
    }

    private async Task LoadVersionsAsync()
    {
        if (TypeComboBox.SelectedItem is ComboBoxItem selectedType)
        {
            try
            {
                VersionComboBox.IsEnabled = false;
                var type = selectedType.Tag.ToString();

                // Получаем версии для конкретного типа
                var installer = new MinecraftInstaller("temp");
                var versions = await installer.GetAvailableVersionsAsync(type);

                if (versions != null && versions.Length > 0)
                {
                    VersionComboBox.ItemsSource = versions;
                    VersionComboBox.SelectedIndex = 0;
                }
                else
                {
                    // Fallback на локальные версии
                    var buildManager = new BuildManager();
                    var localVersions = await buildManager.GetAvailableVersionsAsync(type);
                    VersionComboBox.ItemsSource = localVersions;
                    if (localVersions.Any())
                        VersionComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                // Fallback на заглушечные версии
                var buildManager = new BuildManager();
                var versions = await buildManager.GetAvailableVersionsAsync(selectedType.Tag.ToString());
                VersionComboBox.ItemsSource = versions;
                if (versions.Any())
                    VersionComboBox.SelectedIndex = 0;
            }
            finally
            {
                VersionComboBox.IsEnabled = true;
            }
        }
    }


    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadVersionsAsync();
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(BuildName))
        {
            MessageBox.Show("Введите название сборки", "Ошибка");
            return;
        }

        if (string.IsNullOrWhiteSpace(Version))
        {
            MessageBox.Show("Выберите версию Minecraft", "Ошибка");
            return;
        }

        DialogResult = true;
        Close();
    }
}