using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

namespace EchoLauncher
{
    public class ElyByAuth
    {
        private const string AUTH_URL = "https://authserver.ely.by/auth/authenticate";
        private const string REFRESH_URL = "https://authserver.ely.by/auth/refresh";
        private const string SESSION_FILE = "session.json";

        private HttpClient httpClient;
        private string sessionFilePath;

        public ElyByAuth()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "EchoLauncher/1.0");

            var launcherPath = AppContext.BaseDirectory;
            Directory.CreateDirectory(launcherPath);
            sessionFilePath = Path.Combine(launcherPath, SESSION_FILE);
        }

        public class AuthSession
        {
            public string AccessToken { get; set; }
            public string ClientToken { get; set; }
            public string Username { get; set; }
            public string UUID { get; set; }
            public string DisplayName { get; set; }
            public string ElyByAccessToken { get; set; } // Добавляем Ely.by токен
        }

        private class AuthResponse
        {
            public string AccessToken { get; set; }
            public string ClientToken { get; set; }
            public SelectedProfile SelectedProfile { get; set; }
            public User User { get; set; }
        }

        private class SelectedProfile
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        private class User
        {
            public string Id { get; set; }
            public Properties[] Properties { get; set; }
        }

        private class Properties
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        public async Task<AuthSession> LoginAsync(string username, string password)
        {
            try
            {
                var payload = new
                {
                    username = username,
                    password = password,
                    clientToken = Guid.NewGuid().ToString(),
                    requestUser = true,
                    agent = new
                    {
                        name = "Minecraft",
                        version = 1
                    }
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(AUTH_URL, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var authData = JsonConvert.DeserializeObject<AuthResponse>(responseJson);

                    // Извлекаем Ely.by токен из свойств пользователя
                    string elyByToken = null;
                    if (authData.User?.Properties != null)
                    {
                        foreach (var prop in authData.User.Properties)
                        {
                            if (prop.Name == "elyToken")
                            {
                                elyByToken = prop.Value;
                                break;
                            }
                        }
                    }

                    return new AuthSession
                    {
                        AccessToken = authData.AccessToken,
                        ClientToken = authData.ClientToken,
                        Username = authData.SelectedProfile.Name,
                        UUID = authData.SelectedProfile.Id,
                        DisplayName = authData.SelectedProfile.Name,
                        ElyByAccessToken = elyByToken
                    };
                }
                else
                {
                    var errorJson = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Ошибка авторизации: {errorJson}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Не удалось войти: {ex.Message}");
            }
        }

        // Сохранение сессии
        public void SaveSession(AuthSession session)
        {
            try
            {
                var json = JsonConvert.SerializeObject(session, Formatting.Indented);
                File.WriteAllText(sessionFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения сессии: {ex.Message}");
            }
        }

        // Загрузка сессии
        public AuthSession LoadSession()
        {
            try
            {
                if (File.Exists(sessionFilePath))
                {
                    var json = File.ReadAllText(sessionFilePath);
                    return JsonConvert.DeserializeObject<AuthSession>(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки сессии: {ex.Message}");
            }
            return null;
        }

        // Удаление сессии
        public void DeleteSession()
        {
            try
            {
                if (File.Exists(sessionFilePath))
                    File.Delete(sessionFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка удаления сессии: {ex.Message}");
            }
        }
    }
}