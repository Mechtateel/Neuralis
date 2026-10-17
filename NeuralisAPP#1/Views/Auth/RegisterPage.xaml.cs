using NeuralisAPP_1.Services;
using NeuralisAPP_1.Models.Auth;
using NeuralisAPP_1.Localization;

namespace NeuralisAPP_1.Views.Auth;

public partial class RegisterPage : ContentPage
{
    private readonly AuthService _authService = new();

    public RegisterPage()
    {
        InitializeComponent();
        ApplyLanguage();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        CreateAccountLabel.Text = T.Get("Create account", "Создать аккаунт", "Тіркелгі ашу");
        FullNameEntry.Placeholder = T.Get("Full name", "Полное имя", "Толық аты");
        EmailEntry.Placeholder = T.Get("Email", "Электронная почта", "Электрондық пошта");
        PasswordEntry.Placeholder = T.Get("Password", "Пароль", "Құпия сөз");
        ConfirmPasswordEntry.Placeholder = T.Get("Confirm password", "Подтвердите пароль", "Құпия сөзді растаңыз");
        RegisterButton.Text = T.Get("Register", "Регистрация", "Тіркелу");
        AlreadyHaveAccountLabel.Text = T.Get("Already have an account? ", "Уже есть аккаунт? ", "Аккаунтыңыз бар ма? ");
        LoginLabel.Text = T.Get("Login", "Войти", "Кіру");
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        try
        {
            var request = new RegisterRequest
            {
                FullName = FullNameEntry.Text,
                Email = EmailEntry.Text,
                Password = PasswordEntry.Text
            };

            // 1️⃣ регистрация
            await _authService.RegisterAsync(request);

            // 2️⃣ автологин
            var token = await _authService.Login(
                request.Email,
                request.Password
            );

            await SecureStorage.SetAsync("access_token", token);

            var me = await _authService.GetMe(token);

            App.CurrentUser = me;

            await Shell.Current.GoToAsync("//Main");

        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }


    private async void OnLoginTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Login");
    }
}
