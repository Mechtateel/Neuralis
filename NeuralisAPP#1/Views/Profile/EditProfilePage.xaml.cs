using System.Globalization;
using NeuralisAPP_1.Models.Auth;
using NeuralisAPP_1.Models.Profile;
using NeuralisAPP_1.ViewModels.Profile;

namespace NeuralisAPP_1.Views.Profile;

public partial class EditProfilePage : ContentPage
{
    private readonly ProfileViewModel _vm;
    private bool _saving;

    public EditProfilePage(ProfileViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        Populate();
    }

    private void Populate()
    {
        NameReadonlyLabel.Text = string.IsNullOrWhiteSpace(_vm.DisplayName) ? "—" : _vm.DisplayName;
        EmailReadonlyLabel.Text = _vm.Email;

        PhoneEntry.Text = _vm.Phone;
        GenderPicker.SelectedItem = string.IsNullOrWhiteSpace(_vm.Gender) ? "Prefer not to say" : _vm.Gender;
        EmergencyEntry.Text = _vm.EmergencyContact;
        BioEditor.Text = _vm.Bio;
        ClinicEntry.Text = _vm.DoctorClinic;
        AvailableSwitch.IsToggled = _vm.IsAvailable;

        if (DateTime.TryParseExact(_vm.DateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
            DobPicker.Date = dob;
        else
            DobPicker.Date = DateTime.Today.AddYears(-25);

        // Doctor-only block (verified doctors and lab technicians)
        DoctorOnlyCard.IsVisible = _vm.Role == UserRole.Doctor;
    }

    private async void OnCancelTapped(object? sender, EventArgs e) =>
        await Navigation.PopAsync();

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_saving) return;

        var phone = PhoneEntry.Text?.Trim() ?? "";
        if (phone.Length > 0 && phone.Length < 5)
        {
            ShowError("Phone number looks too short.");
            return;
        }
        var emergency = EmergencyEntry.Text?.Trim() ?? "";
        var bio = BioEditor.Text?.Trim() ?? "";
        var clinic = ClinicEntry.Text?.Trim() ?? "";
        var gender = (GenderPicker.SelectedItem as string) ?? "";
        if (string.Equals(gender, "Prefer not to say", StringComparison.OrdinalIgnoreCase))
            gender = "";

        var edit = new ProfilePreferences
        {
            Phone = phone,
            DateOfBirth = DobPicker.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Gender = gender,
            EmergencyContact = emergency,
            Bio = bio,
            DoctorClinic = clinic,
            IsAvailable = AvailableSwitch.IsToggled
        };

        _saving = true;
        SaveButton.IsEnabled = false;
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;
        ErrorLabel.IsVisible = false;

        try
        {
            // Local persistence is synchronous; the await keeps the UX honest and ready for a future API call.
            await Task.Delay(150);
            _vm.ApplyEdit(edit);
            await DisplayAlert("Profile saved", "Your profile changes have been saved locally on this device.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _saving = false;
            SaveButton.IsEnabled = true;
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
