using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using NeuralisAPP_1.Localization;
using NeuralisAPP_1.Models.Lab;
using NeuralisAPP_1.Services;

namespace NeuralisAPP_1.ViewModels.Lab;

public sealed class LabResultEntryViewModel : INotifyPropertyChanged
{
    public static readonly string[] SuggestedTags = { "Imaging", "Blood", "Genomic", "MRI", "CT" };

    private readonly LabService _lab;
    private LabPatientRowViewModel? _selectedPatient;
    private string _description = string.Empty;
    private string _fileLabel = "No file selected — tap the zone or browse";
    private string? _pickedFilePath;
    private string? _pickedContentType;
    private string? _pickedDisplayName;
    private bool _isBusy;
    private bool _isUploading;
    private bool _loadFailed;

    public LabResultEntryViewModel(string token)
    {
        _lab = new LabService(token);
        LoadCommand = new Command(async () => await LoadAsync());
        PickFileCommand = new Command(async () => await PickFileAsync());
        UploadCommand = new Command(async () => await UploadAsync(), () => CanUpload);
        AddTagCommand = new Command<string>(AddTag, t => !string.IsNullOrWhiteSpace(t));
        RemoveTagCommand = new Command<string>(RemoveTag);
        OpenHistoryFileCommand = new Command<LabResultHistoryRowViewModel>(
            async row => await OpenHistoryAsync(row),
            row => row != null);
    }

    public ObservableCollection<LabPatientRowViewModel> Patients { get; } = new();

    public ObservableCollection<string> ActiveTags { get; } = new();

    public ObservableCollection<LabResultHistoryRowViewModel> UploadHistory { get; } = new();

    public LabPatientRowViewModel? SelectedPatient
    {
        get => _selectedPatient;
        set
        {
            if (_selectedPatient == value)
                return;
            _selectedPatient = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ContextDisplayLines));
            _ = LoadHistoryAsync();
            RefreshUploadCommand();
            OnPropertyChanged(nameof(CanUpload));
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (_description == value)
                return;
            _description = value;
            OnPropertyChanged();
        }
    }

    public string FileLabel
    {
        get => _fileLabel;
        private set
        {
            if (_fileLabel == value)
                return;
            _fileLabel = value;
            OnPropertyChanged();
            RefreshUploadCommand();
            OnPropertyChanged(nameof(CanUpload));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
                return;
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public bool IsUploading
    {
        get => _isUploading;
        private set
        {
            if (_isUploading == value)
                return;
            _isUploading = value;
            OnPropertyChanged();
            RefreshUploadCommand();
            OnPropertyChanged(nameof(CanUpload));
        }
    }

    public bool LoadFailed
    {
        get => _loadFailed;
        private set
        {
            if (_loadFailed == value)
                return;
            _loadFailed = value;
            OnPropertyChanged();
        }
    }

    public bool CanUpload =>
        SelectedPatient != null
        && !IsUploading
        && !string.IsNullOrWhiteSpace(_pickedFilePath)
        && File.Exists(_pickedFilePath)
        && !string.IsNullOrWhiteSpace(_pickedContentType)
        && _pickedContentType != "application/octet-stream";

    public ICommand LoadCommand { get; }
    public ICommand PickFileCommand { get; }
    public ICommand UploadCommand { get; }
    public ICommand AddTagCommand { get; }
    public ICommand RemoveTagCommand { get; }
    public ICommand OpenHistoryFileCommand { get; }

    /// <summary>Multi-line context for left panel (bound as single label with line breaks).</summary>
    public string ContextDisplayLines
    {
        get
        {
            if (SelectedPatient == null)
                return "Select a patient to load clinical context.";

            return
                $"Patient\n{SelectedPatient.FullName}\n\n" +
                $"Email\n{SelectedPatient.EmailLine}\n\n" +
                $"Patient ID\n{SelectedPatient.PatientUserId:D}";
        }
    }

    public int UploadHistoryCount => UploadHistory.Count;

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadAsync()
    {
        IsBusy = true;
        LoadFailed = false;
        try
        {
            var list = await _lab.GetAssignedPatientsAsync();
            Patients.Clear();
            foreach (var dto in list.OrderBy(p => p.FullName))
                Patients.Add(LabPatientRowViewModel.FromDto(dto));

            if (Patients.Count > 0 && (SelectedPatient == null || !Patients.Contains(SelectedPatient)))
                SelectedPatient = Patients[0];
            else if (Patients.Count == 0)
                SelectedPatient = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LabResultEntry] Load: {ex}");
            Patients.Clear();
            SelectedPatient = null;
            LoadFailed = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ApplyPreselectedPatient(Guid patientId)
    {
        var row = Patients.FirstOrDefault(p => p.PatientUserId == patientId);
        if (row != null)
            SelectedPatient = row;
    }

    private async Task LoadHistoryAsync()
    {
        UploadHistory.Clear();
        OnPropertyChanged(nameof(UploadHistoryCount));
        if (SelectedPatient == null)
            return;

        try
        {
            var list = await _lab.GetUploadsForPatientAsync(SelectedPatient.PatientUserId);
            foreach (var dto in list)
                UploadHistory.Add(LabResultHistoryRowViewModel.FromDto(dto));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LabResultEntry] History: {ex}");
        }

        OnPropertyChanged(nameof(UploadHistoryCount));
    }

    private void AddTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;
        var t = tag.Trim();
        if (ActiveTags.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)))
            return;
        if (t.Length > 40)
            return;
        ActiveTags.Add(t);
    }

    private void RemoveTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;
        var match = ActiveTags.FirstOrDefault(x => x == tag);
        if (match != null)
            ActiveTags.Remove(match);
    }

    private async Task PickFileAsync()
    {
        try
        {
            var types = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.iOS, new[] { "public.image", "com.adobe.pdf" } },
                { DevicePlatform.MacCatalyst, new[] { "public.image", "com.adobe.pdf" } },
                { DevicePlatform.Android, new[] { "image/*", "application/pdf" } },
                { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf" } },
                { DevicePlatform.Tizen, new[] { "*/*" } }
            });

            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Clinical asset",
                FileTypes = types
            });

            if (result == null)
                return;

            var fi = new FileInfo(result.FullPath);
            if (fi.Length > 12L * 1024 * 1024)
            {
                FileLabel = "File exceeds 12 MB limit";
                _pickedFilePath = null;
                _pickedContentType = null;
                _pickedDisplayName = null;
                RefreshUploadCommand();
                OnPropertyChanged(nameof(CanUpload));
                return;
            }

            _pickedFilePath = result.FullPath;
            _pickedDisplayName = result.FileName;
            var ext = Path.GetExtension(result.FileName).ToLowerInvariant();
            _pickedContentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            FileLabel = result.FileName;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LabResultEntry] Pick: {ex}");
        }
    }

    private async Task UploadAsync()
    {
        if (SelectedPatient == null)
        {
            await AlertAsync("Select patient first");
            return;
        }

        if (string.IsNullOrWhiteSpace(_pickedFilePath) || !File.Exists(_pickedFilePath))
        {
            await AlertAsync("Please choose a file");
            return;
        }

        if (!CanUpload)
            return;

        IsUploading = true;
        try
        {
            var bytes = await File.ReadAllBytesAsync(_pickedFilePath);
            await using var stream = new MemoryStream(bytes);
            var tagList = ActiveTags.ToList();
            var uploadResult = await _lab.UploadAsync(
                SelectedPatient.PatientUserId,
                Description.Trim(),
                tagList,
                stream,
                _pickedDisplayName ?? "upload",
                _pickedContentType!);

            if (!uploadResult.IsSuccess)
            {
                await AlertAsync(uploadResult.ErrorMessage ?? "Upload failed, try again");
                return;
            }

            _pickedFilePath = null;
            _pickedDisplayName = null;
            _pickedContentType = null;
            FileLabel = "No file selected — tap the zone or browse";
            Description = string.Empty;
            ActiveTags.Clear();

            await AlertAsync(LabUiStrings.UploadSuccessful);

            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LabResultEntry] Upload: {ex}");
            await AlertAsync(!string.IsNullOrWhiteSpace(ex.Message) ? ex.Message : "Upload failed, try again");
        }
        finally
        {
            IsUploading = false;
        }
    }

    private async Task OpenHistoryAsync(LabResultHistoryRowViewModel? row)
    {
        if (row == null)
            return;
        try
        {
            await _lab.OpenAttachmentAsync(row.AttachmentId, row.FileName);
        }
        catch (Exception ex)
        {
            await AlertAsync(ex.Message);
        }
    }

    private static Task AlertAsync(string message)
    {
        var page = Shell.Current?.CurrentPage;
        if (page == null)
            return Task.CompletedTask;
        return MainThread.InvokeOnMainThreadAsync(() =>
            page.DisplayAlert(LabUiStrings.ResultEntryDialogTitle, message, LabUiStrings.Ok));
    }

    private void RefreshUploadCommand()
    {
        if (UploadCommand is Command c)
            c.ChangeCanExecute();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
