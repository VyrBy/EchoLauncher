using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EchoLauncher;

public partial class MainWindow : Window
{
    private ElyByAuth authClient;
    private ElyByAuth.AuthSession currentSession;
    private BuildManager buildManager;

    public MainWindow()
    {
        InitializeComponent();
        authClient = new ElyByAuth();
        buildManager = new BuildManager();

        currentSession = authClient.LoadSession();
        UpdateAuthUI();
        LoadBuildsComboBox();

        // Показываем вкладку запуска по умолчанию
        ShowLaunchTab();
    }

    private void LoadBuildsComboBox()
    {
        var builds = buildManager.LoadBuilds();
        BuildsComboBox.ItemsSource = builds;
        BuildsComboBox.DisplayMemberPath = "Name";

        if (builds.Count > 0)
            BuildsComboBox.SelectedIndex = 0;
    }

    private void LoadBuildsListView()
    {
        var builds = buildManager.LoadBuilds();
        BuildsListView.ItemsSource = builds;
    }

    private void UpdateAuthUI()
    {
        if (currentSession != null)
        {
            AuthStatusText.Text = $"Вошли как: {currentSession.Username}";
            LoginButton.Content = "Выйти";
        }
        else
        {
            AuthStatusText.Text = "Не авторизован";
            LoginButton.Content = "Войти";
        }
    }

    // Методы переключения вкладок
    private void ShowLaunchTab()
    {
        LaunchTab.Visibility = Visibility.Visible;
        BuildsTab.Visibility = Visibility.Collapsed;
        ModsTab.Visibility = Visibility.Collapsed;
        SettingsTab.Visibility = Visibility.Collapsed;
        InfoTab.Visibility = Visibility.Collapsed;

        // Обновляем внешний вид кнопок
        UpdateTabButtons("launch");
        StatusText.Text = "Готов к запуску";
    }

    private void ShowBuildsTab()
    {
        LaunchTab.Visibility = Visibility.Collapsed;
        BuildsTab.Visibility = Visibility.Visible;
        ModsTab.Visibility = Visibility.Collapsed;
        SettingsTab.Visibility = Visibility.Collapsed;
        InfoTab.Visibility = Visibility.Collapsed;

        // Загружаем список сборок при открытии вкладки
        LoadBuildsListView();
        UpdateTabButtons("builds");
        StatusText.Text = "Управление сборками";
    }

    private void ShowModsTab()
    {
        LaunchTab.Visibility = Visibility.Collapsed;
        BuildsTab.Visibility = Visibility.Collapsed;
        ModsTab.Visibility = Visibility.Visible;
        SettingsTab.Visibility = Visibility.Collapsed;
        InfoTab.Visibility = Visibility.Collapsed;

        UpdateTabButtons("mods");
        StatusText.Text = "Система модов в разработке";
    }

    private void ShowSettingsTab()
    {
        LaunchTab.Visibility = Visibility.Collapsed;
        BuildsTab.Visibility = Visibility.Collapsed;
        ModsTab.Visibility = Visibility.Collapsed;
        SettingsTab.Visibility = Visibility.Visible;
        InfoTab.Visibility = Visibility.Collapsed;

        UpdateTabButtons("settings");
        StatusText.Text = "Настройки(разработка)";
    }

    private void ShowInfoTab()
    {
        LaunchTab.Visibility = Visibility.Collapsed;
        BuildsTab.Visibility = Visibility.Collapsed;
        ModsTab.Visibility = Visibility.Collapsed;
        SettingsTab.Visibility = Visibility.Collapsed;
        InfoTab.Visibility = Visibility.Visible;

        UpdateTabButtons("info");
        StatusText.Text = "Информация о лаунчере";
    }

    private void UpdateTabButtons(string activeTab)
    {
        // Сбрасываем все кнопки к стандартному виду
        LaunchTabButton.Background = Brushes.Transparent;
        BuildsTabButton.Background = Brushes.Transparent;
        ModsTabButton.Background = Brushes.Transparent;
        SettingsTabButton.Background = Brushes.Transparent;
        InfoTabButton.Background = Brushes.Transparent;

        // Подсвечиваем активную вкладку
        switch (activeTab)
        {
            case "launch":
                LaunchTabButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B4D2B"));
                break;
            case "builds":
                BuildsTabButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B4D2B"));
                break;
            case "mods":
                ModsTabButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B4D2B"));
                break;
            case "settings":
                SettingsTabButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B4D2B"));
                break;
            case "info":
                InfoTabButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B4D2B"));
                break;
        }
    }

    // Обработчики кнопок вкладок
    private void LaunchTabButton_Click(object sender, RoutedEventArgs e)
    {
        ShowLaunchTab();
    }

    private void BuildsTabButton_Click(object sender, RoutedEventArgs e)
    {
        ShowBuildsTab();
    }

    private void ModsTabButton_Click(object sender, RoutedEventArgs e)
    {
        ShowModsTab();
    }

    private void SettingsTabButton_Click(Object sender, RoutedEventArgs e)
    {
        ShowSettingsTab();
    }

    private void InfoTabButton_Click(object sender, RoutedEventArgs e)
    {
        ShowInfoTab();
    }

    // Методы для вкладки сборок
    private void AddBuildButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddBuildWindow();
        if (dialog.ShowDialog() == true)
        {
            try
            {
                buildManager.AddBuild(dialog.BuildName, dialog.Version, dialog.BuildType);
                LoadBuildsListView(); // Обновляем список сборок
                LoadBuildsComboBox(); // Обновляем комбобокс на вкладке запуска
                MessageBox.Show("Сборка успешно создана!", "Успех");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания сборки: {ex.Message}", "Ошибка");
            }
        }
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
                LoadBuildsListView();
                LoadBuildsComboBox();
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

    private void RefreshBuildsButton_Click(object sender, RoutedEventArgs e)
    {
        LoadBuildsListView();
        MessageBox.Show("Список сборок обновлен", "Обновление",
                      MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // Остальные методы без изменений
    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (currentSession != null)
        {
            // Выход
            currentSession = null;
            authClient.DeleteSession();
            UpdateAuthUI();
            MessageBox.Show("Вы вышли из аккаунта");
        }
        else
        {
            var loginDialog = new LoginWindow();
            if (loginDialog.ShowDialog() == true)
            {
                try
                {
                    StatusText.Text = "Авторизация...";
                    currentSession = await authClient.LoginAsync(loginDialog.Username, loginDialog.Password);

                    // Сохраняем сессию
                    authClient.SaveSession(currentSession);

                    UpdateAuthUI();
                    StatusText.Text = "Авторизация успешна!";
                    MessageBox.Show($"Добро пожаловать, {currentSession.Username}!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка авторизации: {ex.Message}", "Ошибка",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusText.Text = "Ошибка авторизации";
                }
            }
        }
    }

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        // Проверяем авторизацию перед запуском
        if (currentSession == null)
        {
            MessageBox.Show("Сначала войдите в аккаунт!", "Ошибка",
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await LaunchMinecraftAsync(sender);
    }

    private async Task LaunchMinecraftAsync(object sender)
    {
        var button = (Button)sender;
        button.IsEnabled = false;

        try
        {
            if (currentSession == null)
            {
                MessageBox.Show("Сначала войдите в аккаунт!", "Ошибка");
                return;
            }

            // Получаем выбранную сборку
            var selectedBuild = BuildsComboBox.SelectedItem as BuildManager.MinecraftBuild;
            if (selectedBuild == null)
            {
                MessageBox.Show("Выберите сборку для запуска!", "Ошибка");
                return;
            }

            string buildName = selectedBuild.Name;
            string version = selectedBuild.Version;
            string buildType = selectedBuild.Type;

            StatusText.Text = "Проверка установки Minecraft...";
            Debug.WriteLine($"Запуск сборки: {buildName}, версия: {version}");

            // Устанавливаем/проверяем Minecraft
            var installer = new MinecraftInstaller(buildName);
            var progress = new Progress<MinecraftInstaller.InstallProgress>(p =>
            {
                ProgressBar.Value = p.Progress;
                StatusText.Text = p.Message;
                Debug.WriteLine($"Прогресс: {p.Progress}% - {p.Message}");
            });

            var installedVersion = await installer.EnsureInstalledAsync(version, buildType, progress);

            // Проверяем успешность установки
            if (string.IsNullOrEmpty(installedVersion))
            {
                throw new Exception("Не удалось установить Minecraft");
            }

            StatusText.Text = "Запуск Minecraft...";
            ProgressBar.Value = 0;
            Debug.WriteLine($"Установлена версия: {installedVersion}, запуск...");

            // Запускаем Minecraft
            var launchResult = await installer.LaunchMinecraftAsync(installedVersion, currentSession);

            if (launchResult.Success)
            {
                StatusText.Text = "Minecraft запущен!";
                Debug.WriteLine("Minecraft успешно запущен!");

                // Показываем сообщение только на короткое время
                await Task.Delay(2000);
                StatusText.Text = "Готов к работе";
            }
            else
            {
                Debug.WriteLine($"Ошибка запуска: {launchResult.ErrorMessage}");

                // Показываем детальную ошибку
                var errorMessage = launchResult.ErrorMessage;
                if (errorMessage.Length > 500) // Ограничиваем длину для MessageBox
                    errorMessage = errorMessage.Substring(0, 500) + "...";

                MessageBox.Show($"Ошибка запуска Minecraft:\n{errorMessage}", "Ошибка запуска",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                throw new Exception(launchResult.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Исключение при запуске: {ex}");
            MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка",
                          MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Ошибка запуска";
        }
        finally
        {
            button.IsEnabled = true;
            ProgressBar.Value = 0;
        }
    }


}