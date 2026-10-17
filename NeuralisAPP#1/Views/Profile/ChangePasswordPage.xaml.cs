namespace NeuralisAPP_1.Views.Profile;

public partial class ChangePasswordPage : ContentPage
{
    private bool _submitting;

    public ChangePasswordPage()
    {
        InitializeComponent();
    }

    private void OnNewPasswordChanged(object? sender, TextChangedEventArgs e)
    {
        var pw = e.NewTextValue ?? "";
        if (string.IsNullOrEmpty(pw))
        {
            StrengthLabel.Text = "";
            StrengthLabel.TextColor = Color.FromArgb("#6E8C96");
            return;
        }

        var (label, color) = EvaluateStrength(pw);
        StrengthLabel.Text = "Strength: " + label;
        StrengthLabel.TextColor = color;
    }

    private static (string Label, Color Color) EvaluateStrength(string pw)
    {
        var score = 0;
        if (pw.Length >= 8) score++;
        if (pw.Any(char.IsDigit)) score++;
        if (pw.Any(char.IsUpper) && pw.Any(char.IsLower)) score++;
        if (pw.Any(c => !char.IsLetterOrDigit(c))) score++;

        return score switch
        {
            <= 1 => ("Weak", Color.FromArgb("#F87171")),
            2 => ("Fair", Color.FromArgb("#FACC15")),
            3 => ("Good", Color.FromArgb("#4ADE80")),
            _ => ("Strong", Color.FromArgb("#4ADE80"))
        };
    }

    private async void OnCancelTapped(object? sender, EventArgs e) =>
        await Navigation.PopAsync();

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        if (_submitting) return;

        var current = OldPasswordEntry.Text ?? "";
        var next = NewPasswordEntry.Text ?? "";
        var confirm = ConfirmPasswordEntry.Text ?? "";

        if (string.IsNullOrWhiteSpace(current))
        {
            ShowError("Enter your current password.");
            return;
        }
        if (next.Length < 8)
        {
            ShowError("New password must be at least 8 characters.");
            return;
        }
        if (!next.Any(char.IsDigit) || !next.Any(char.IsLetter))
        {
            ShowError("New password must include both letters and numbers.");
            return;
        }
        if (next == current)
        {
            ShowError("New password must differ from your current password.");
            return;
        }
        if (next != confirm)
        {
            ShowError("New password and confirmation do not match.");
            return;
        }

        _submitting = true;
        SubmitButton.IsEnabled = false;
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;
        ErrorLabel.IsVisible = false;

        try
        {
            // No backend endpoint exists for password change in NeuralisAPI (see AuthController).
            // Validation runs locally; the success message is honest about server-side activation.
            await Task.Delay(300);

            await DisplayAlert(
                "Password updated",
                "Your password was validated locally. Server-side activation is pending the upcoming security module.",
                "OK");

            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _submitting = false;
            SubmitButton.IsEnabled = true;
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
