using System.Windows;
using System.Windows.Controls;

namespace EchoLauncher
{
    public partial class LoginWindow : Window
    {
        public string Username => UsernameTextBox?.Text;
        public string Password => PasswordBox?.Password;

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show("Заполните все поля", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}