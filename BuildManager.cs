using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EchoLauncher;

public class BuildManager
{
    private string buildsFilePath;
    private string instancesPath;

    public BuildManager()
    {
        var launcherPath = AppContext.BaseDirectory;
        buildsFilePath = Path.Combine(launcherPath, "builds.json");
        instancesPath = Path.Combine(launcherPath, "instances");

        // Создаем директории если не существуют
        Directory.CreateDirectory(launcherPath);
        Directory.CreateDirectory(instancesPath);
    }

    public class MinecraftBuild
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public string InstallPath { get; set; }
    }

    public void AddBuild(string name, string version, string type)
    {
        // Валидация входных данных
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Название сборки не может быть пустым");

        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Версия не может быть пустой");

        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Тип сборки не может быть пустым");

        // Проверка на запрещенные символы в имени
        var invalidChars = Path.GetInvalidFileNameChars();
        if (name.IndexOfAny(invalidChars) >= 0)
            throw new Exception("Название сборки содержит запрещенные символы");

        var builds = LoadBuilds();

        if (builds.Any(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new Exception($"Сборка с именем '{name}' уже существует");

        var build = new MinecraftBuild
        {
            Name = name.Trim(),
            Version = version,
            Type = type,
            CreatedAt = DateTime.Now,
            InstallPath = GetBuildPath(name)
        };

        builds.Add(build);
        SaveBuilds(builds);

        // Создаем директорию для сборки
        Directory.CreateDirectory(build.InstallPath);
    }

    public void DeleteBuild(string name)
    {
        var builds = LoadBuilds();
        var build = builds.FirstOrDefault(b => b.Name == name);

        if (build != null)
        {
            builds.Remove(build);
            SaveBuilds(builds);

            // Удаляем директорию если существует
            if (Directory.Exists(build.InstallPath))
            {
                Directory.Delete(build.InstallPath, true);
            }
        }
    }

    public List<MinecraftBuild> LoadBuilds()
    {
        if (!File.Exists(buildsFilePath))
            return new List<MinecraftBuild>();

        try
        {
            var json = File.ReadAllText(buildsFilePath);
            return JsonSerializer.Deserialize<List<MinecraftBuild>>(json) ?? new List<MinecraftBuild>();
        }
        catch
        {
            return new List<MinecraftBuild>();
        }
    }

    private void SaveBuilds(List<MinecraftBuild> builds)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(builds, options);
        File.WriteAllText(buildsFilePath, json);
    }

    public string GetBuildPath(string buildName)
    {
        return Path.Combine(instancesPath, buildName);
    }

    public async Task<string[]> GetAvailableVersionsAsync(string buildType)
    {
        try
        {
            // Пробуем получить актуальные версии
            var installer = new MinecraftInstaller("temp_versions");
            return await installer.GetAvailableVersionsAsync();
        }

        catch
        {
            // Fallback версии
            return new[]
            {
                "1.20.4", "1.20.1", "1.19.4", "1.18.2", "1.17.1",
                "1.16.5", "1.15.2", "1.14.4", "1.13.2", "1.12.2"
            };
        }
    }

}