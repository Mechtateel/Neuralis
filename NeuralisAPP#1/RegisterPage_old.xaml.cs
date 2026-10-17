namespace NeuralisAPP_1.Views;

public partial class RegisterPage : ContentPage
{
    public RegisterPage()
    {
        InitializeComponent();
    }

    private void OnLoginTapped(object sender, EventArgs e)
    {
        Application.Current.MainPage = new LoginPage();
    }

    private void OnRegisterClicked(object sender, EventArgs e)
    {
        Application.Current.MainPage = new ();
    }
}
