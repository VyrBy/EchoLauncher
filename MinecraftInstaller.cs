using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installers;
using CmlLib.Core.ModLoaders.FabricMC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EchoLauncher
{
    public class MinecraftInstaller
    {
        private string gamePath;
        private MinecraftPath minecraftPath;
        private MinecraftLauncher launcher;
        private string authlibInjectorPath;
        private ForgeInstaller forgeInstaller;
        private FabricInstaller fabricInstaller;

        public MinecraftInstaller(string buildName)
        {
            var launcherPath = AppContext.BaseDirectory;
            gamePath = Path.Combine(launcherPath, "instances", buildName);

            minecraftPath = new MinecraftPath(gamePath);
            launcher = new MinecraftLauncher(minecraftPath);

            // Инициализируем установщики только когда они нужны
            forgeInstaller = null;
            fabricInstaller = null;

            // Создаем папку authlib
            var authlibDir = Path.Combine(launcherPath, "authlib");
            Directory.CreateDirectory(authlibDir);
            authlibInjectorPath = Path.Combine(authlibDir, "authlib-injector-1.2.5.jar");
        }

        // Проверка установлена ли версия
        public bool IsVersionInstalled(string versionName)
        {
            var versionPath = Path.Combine(minecraftPath.Versions, versionName);
            return Directory.Exists(versionPath);
        }

        // Скачивание authlib-injector
        public async Task DownloadAuthlibInjectorAsync()
        {
            try
            {
                // Ожидаемый SHA-256 хеш для authlib-injector 1.2.5
                const string expectedSha256 = "3bc9ebdc583b36abd2a65b626c4b9f35f21177fbf42a851606eaaea3fd42ee0f";

                if (File.Exists(authlibInjectorPath))
                {
                    // Проверяем хеш существующего файла
                    var existingFileHash = await ComputeSha256HashAsync(authlibInjectorPath);
                    if (existingFileHash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine("Authlib-injector 1.2.5 уже скачан и хеш совпадает");
                        return;
                    }
                    else
                    {
                        Debug.WriteLine($"Authlib-injector невалиден. Ожидаемый хеш: {expectedSha256}, полученный: {existingFileHash}");
                        File.Delete(authlibInjectorPath);
                    }
                }

                using var httpClient = new HttpClient();

                // Список зеркал для скачивания версии 1.2.5
                var urls = new[]
                {
                    "https://authlib-injector.yushi.moe/artifact/53/authlib-injector-1.2.5.jar",
                    "https://github.com/yushijinhun/authlib-injector/releases/download/v1.2.5/authlib-injector-1.2.5.jar",
                    "https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/53/authlib-injector-1.2.5.jar"
                };

                Exception lastException = null;

                foreach (var url in urls)
                {
                    try
                    {
                        Debug.WriteLine($"Пробуем скачать authlib-injector 1.2.5 с: {url}");

                        var response = await httpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsByteArrayAsync();

                            // Проверяем размер файла
                            if (content.Length < 1000)
                            {
                                Debug.WriteLine($"Файл слишком маленький: {content.Length} bytes");
                                continue;
                            }

                            // Проверяем SHA-256
                            var downloadedHash = ComputeSha256Hash(content);
                            if (!downloadedHash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                            {
                                Debug.WriteLine($"Хеш не совпадает! Ожидаемый: {expectedSha256}, полученный: {downloadedHash}");
                                continue;
                            }

                            // Сохраняем файл
                            await File.WriteAllBytesAsync(authlibInjectorPath, content);
                            Debug.WriteLine($"Authlib-injector 1.2.5 успешно скачан и проверен! Размер: {content.Length} bytes, SHA-256: {downloadedHash}");

                            return;
                        }
                        else
                        {
                            Debug.WriteLine($"HTTP ошибка: {response.StatusCode} для {url}");
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        Debug.WriteLine($"Ошибка при скачивании с {url}: {ex.Message}");
                    }
                }

                throw new Exception($"Не удалось скачать валидный authlib-injector 1.2.5 ни с одного источника. Последняя ошибка: {lastException?.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Критическая ошибка загрузки authlib-injector: {ex}");
                throw new Exception($"Ошибка загрузки authlib-injector: {ex.Message}");
            }
        }

        // Метод для вычисления SHA-256 хеша из byte[]
        private string ComputeSha256Hash(byte[] data)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(data);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        // Метод для вычисления SHA-256 хеша файла
        private async Task<string> ComputeSha256HashAsync(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        // Установка версии Minecraft с поддержкой Fabric и Forge
        public async Task<string> EnsureInstalledAsync(string versionName, string buildType, IProgress<InstallProgress> progress = null)
        {
            try
            {
                // Если версия уже установлена
                if (IsVersionInstalled(versionName))
                {
                    progress?.Report(new InstallProgress { Message = "Версия уже установлена", Progress = 100 });
                    return versionName;
                }

                progress?.Report(new InstallProgress { Message = "Подготовка установки...", Progress = 10 });

                // Скачиваем authlib-injector
                progress?.Report(new InstallProgress { Message = "Скачивание authlib-injector...", Progress = 15 });
                await DownloadAuthlibInjectorAsync();

                // Устанавливаем обработчики прогресса для launcher
                launcher.FileProgressChanged += (sender, args) =>
                {
                    if (args.TotalTasks > 0)
                    {
                        int installProgress = (int)(args.ProgressedTasks * 100 / args.TotalTasks);
                        progress?.Report(new InstallProgress
                        {
                            Message = $"Установка: {args.Name}",
                            Progress = Math.Min(90, 15 + (int)(installProgress * 0.75))
                        });
                    }
                };

                // Устанавливаем версию в зависимости от типа
                string installedVersion = versionName;

                if (buildType.ToLower() == "vanilla")
                {
                    // Устанавливаем ванильную версию
                    await launcher.InstallAsync(versionName);
                }
                else if (buildType.ToLower() == "fabric")
                {
                    installedVersion = await InstallFabricAsync(versionName, progress);
                }
                else if (buildType.ToLower() == "forge")
                {
                    installedVersion = await InstallForgeAsync(versionName, progress);
                }
                else
                {
                    throw new Exception($"Неизвестный тип сборки: {buildType}");
                }

                progress?.Report(new InstallProgress { Message = "Установка завершена", Progress = 100 });

                return installedVersion;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка установки Minecraft: {ex.Message}", ex);
            }
        }

        // Установка Fabric с использованием официального установщика
        private async Task<string> InstallFabricAsync(string minecraftVersion, IProgress<InstallProgress> progress = null)
        {
            try
            {
                progress?.Report(new InstallProgress { Message = "Инициализация Fabric установщика...", Progress = 20 });

                // Инициализируем Fabric установщик
                fabricInstaller = new FabricInstaller(new HttpClient());

                progress?.Report(new InstallProgress { Message = "Установка Fabric...", Progress = 50 });

                // Устанавливаем Fabric используя официальный метод
                var fabricVersionName = await fabricInstaller.Install(minecraftVersion, minecraftPath);

                progress?.Report(new InstallProgress { Message = "Завершение установки Fabric...", Progress = 80 });

                // Устанавливаем зависимости
                await launcher.InstallAsync(fabricVersionName);

                return fabricVersionName;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка установки Fabric: {ex.Message}", ex);
            }
        }

        // Установка Forge с использованием официального установщика
        private async Task<string> InstallForgeAsync(string minecraftVersion, IProgress<InstallProgress> progress = null)
        {
            try
            {
                progress?.Report(new InstallProgress { Message = "Инициализация Forge установщика...", Progress = 20 });

                // Инициализируем Forge установщик
                forgeInstaller = new ForgeInstaller(launcher);

                progress?.Report(new InstallProgress { Message = "Получение информации о Forge...", Progress = 30 });

                // Получаем доступные версии Forge
                var forgeVersions = await forgeInstaller.GetForgeVersions(minecraftVersion);

                if (!forgeVersions.Any())
                {
                    throw new Exception($"Не найдены версии Forge для Minecraft {minecraftVersion}");
                }

                // Выбираем рекомендованную или последнюю версию
                var selectedForge = forgeVersions.FirstOrDefault(v => v.IsRecommendedVersion) ??
                                  forgeVersions.FirstOrDefault(v => v.IsLatestVersion) ??
                                  forgeVersions.First();

                progress?.Report(new InstallProgress { Message = $"Установка Forge {selectedForge.ForgeVersionName}...", Progress = 60 });

                // Устанавливаем Forge
                var installOptions = new ForgeInstallOptions
                {
                    FileProgress = new Progress<InstallerProgressChangedEventArgs>(e =>
                    {
                        progress?.Report(new InstallProgress
                        {
                            Message = $"Forge: {e.Name}",
                            Progress = 60 + (int)(e.ProgressedTasks * 30.0 / e.TotalTasks)
                        });
                    }),
                    SkipIfAlreadyInstalled = false
                };

                var forgeVersionName = await forgeInstaller.Install(selectedForge, installOptions);

                // ForgeInstaller не устанавливает все зависимости, нужно докачать
                progress?.Report(new InstallProgress { Message = "Завершение установки Forge...", Progress = 95 });
                await launcher.InstallAsync(forgeVersionName);

                return forgeVersionName;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка установки Forge: {ex.Message}", ex);
            }
        }

        // Получить список доступных версий для конкретного типа
        public async Task<string[]> GetAvailableVersionsAsync(string buildType = "vanilla")
        {
            try
            {
                // Используем встроенный метод получения версий
                var versions = await launcher.GetAllVersionsAsync();

                if (buildType.ToLower() == "vanilla")
                {
                    return versions
                        .Where(v => !v.Name.Contains("forge", StringComparison.OrdinalIgnoreCase) &&
                                   !v.Name.Contains("fabric", StringComparison.OrdinalIgnoreCase))
                        .Select(v => v.Name)
                        .ToArray();
                }
                else if (buildType.ToLower() == "fabric")
                {
                    // Для Fabric используем официальный метод получения поддерживаемых версий
                    try
                    {
                        fabricInstaller = new FabricInstaller(new HttpClient());
                        var fabricVersions = await fabricInstaller.GetSupportedVersionNames();
                        return fabricVersions.ToArray();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ошибка получения версий Fabric: {ex.Message}");
                        // Fallback на популярные версии
                        return new[] { "1.20.4", "1.20.1", "1.19.4", "1.19.2", "1.18.2", "1.17.1", "1.16.5" };
                    }
                }
                else if (buildType.ToLower() == "forge")
                {
                    // Для Forge возвращаем версии, для которых есть Forge
                    var forgeVersions = new List<string>();

                    // Проверяем популярные версии на наличие Forge
                    var popularVersions = new[] { "1.20.1", "1.19.2", "1.18.2", "1.17.1", "1.16.5", "1.12.2" };

                    foreach (var version in popularVersions)
                    {
                        try
                        {
                            // Инициализируем Forge установщик только когда нужен
                            var tempForgeInstaller = new ForgeInstaller(launcher);
                            var forgeVersionsForMinecraft = await tempForgeInstaller.GetForgeVersions(version);
                            if (forgeVersionsForMinecraft != null && forgeVersionsForMinecraft.Any())
                            {
                                forgeVersions.Add(version);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Ошибка при проверке Forge для версии {version}: {ex.Message}");
                            // Пропускаем версии без Forge или с ошибками
                        }
                    }

                    // Если не нашли версий с Forge, возвращаем популярные версии как fallback
                    if (!forgeVersions.Any())
                    {
                        return popularVersions;
                    }

                    return forgeVersions.ToArray();
                }

                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка получения версий для {buildType}: {ex.Message}");

                // Fallback версии
                return GetFallbackVersions(buildType);
            }
        }

        private string[] GetFallbackVersions(string buildType)
        {
            if (buildType.ToLower() == "fabric")
            {
                return new[]
                {
                    "1.20.4", "1.20.1", "1.19.4", "1.19.2", "1.18.2",
                    "1.17.1", "1.16.5", "1.15.2", "1.14.4"
                };
            }
            else if (buildType.ToLower() == "forge")
            {
                return new[]
                {
                    "1.20.1", "1.19.2", "1.18.2", "1.17.1", "1.16.5",
                    "1.15.2", "1.14.4", "1.13.2", "1.12.2"
                };
            }
            else
            {
                // Vanilla
                return new[]
                {
                    "1.20.4", "1.20.1", "1.19.4", "1.18.2", "1.17.1",
                    "1.16.5", "1.15.2", "1.14.4", "1.13.2", "1.12.2"
                };
            }
        }

        // Остальные методы без изменений
        public async Task<LaunchResult> LaunchMinecraftAsync(string versionName, ElyByAuth.AuthSession session)
        {
            try
            {
                Debug.WriteLine($"Начало запуска Minecraft версии: {versionName}");

                // Проверяем что authlib-injector скачан и валиден
                if (!File.Exists(authlibInjectorPath))
                {
                    Debug.WriteLine("Authlib-injector не найден, скачиваем...");
                    await DownloadAuthlibInjectorAsync();
                }

                // Проверяем что версия существует
                if (!IsVersionInstalled(versionName))
                {
                    throw new Exception($"Версия {versionName} не установлена");
                }

                // Создаем сессию для Ely.by
                var minecraftSession = new MSession
                {
                    AccessToken = session.AccessToken,
                    UUID = session.UUID,
                    Username = session.Username
                };

                Debug.WriteLine($"Создана сессия для: {session.Username}");

                // Создаем список дополнительных JVM аргументов
                var extraJvmArguments = new List<MArgument>();
                extraJvmArguments.Add(new MArgument($"-javaagent:{authlibInjectorPath}=https://authserver.ely.by"));

                Debug.WriteLine("Добавлены JVM аргументы для authlib-injector");

                // Создаем настройки запуска
                var launchOption = new MLaunchOption
                {
                    Session = minecraftSession,
                    MaximumRamMb = 4096,
                    MinimumRamMb = 2048,
                    ScreenWidth = 1280,
                    ScreenHeight = 720,
                    ServerIp = null,
                    ExtraJvmArguments = extraJvmArguments.ToArray()
                };

                Debug.WriteLine("Созданы настройки запуска, строим процесс...");

                // Строим процесс и запускаем
                var process = await launcher.BuildProcessAsync(versionName, launchOption);

                Debug.WriteLine($"Процесс создан, аргументы: {process.StartInfo.Arguments}");

                // Настраиваем перехват вывода
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = false;

                // События для перехвата вывода
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        Debug.WriteLine($"Minecraft Output: {e.Data}");
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        Debug.WriteLine($"Minecraft Error: {e.Data}");
                    }
                };

                process.Start();
                Debug.WriteLine("Процесс запущен!");

                // Начинаем перехват вывода
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Ждем немного и проверяем состояние
                await Task.Delay(3000);

                if (process.HasExited)
                {
                    var exitCode = process.ExitCode;
                    var output = outputBuilder.ToString();
                    var error = errorBuilder.ToString();

                    Debug.WriteLine($"Процесс завершился с кодом: {exitCode}");
                    Debug.WriteLine($"Output: {output}");
                    Debug.WriteLine($"Error: {error}");

                    return new LaunchResult
                    {
                        Success = false,
                        ErrorMessage = $"Minecraft завершился сразу. Код: {exitCode}\nOutput: {output}\nError: {error}",
                        Process = process
                    };
                }
                else
                {
                    Debug.WriteLine("Процесс работает нормально, Minecraft запущен!");
                    return new LaunchResult
                    {
                        Success = true,
                        Process = process
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка в LaunchMinecraftAsync: {ex}");
                return new LaunchResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ErrorException = ex
                };
            }
        }

        public class InstallProgress
        {
            public string Message { get; set; }
            public int Progress { get; set; }
        }

        public class LaunchResult
        {
            public bool Success { get; set; }
            public string ErrorMessage { get; set; }
            public Exception ErrorException { get; set; }
            public System.Diagnostics.Process Process { get; set; }
        }
    }
}