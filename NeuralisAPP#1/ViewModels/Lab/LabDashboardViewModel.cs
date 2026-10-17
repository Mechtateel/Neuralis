using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using NeuralisAPP_1.Models.Lab;
using NeuralisAPP_1.Services;

namespace NeuralisAPP_1.ViewModels.Lab;

public sealed class LabDashboardViewModel : INotifyPropertyChanged
{
    private readonly LabService _lab;
    private bool _isBusy;
    private bool _loadFailed;
    private bool _showPatientRosterHint;
    private int _processedToday;
    private int _inQueue;
    private string _lastSyncDisplay = "—";
    private bool _apiHealthy;

    public LabDashboardViewModel(string token)
    {
        _lab = new LabService(token);
        OpenRecentFileCommand = new Command<LabRecentFileViewModel>(
            async f => await OpenRecentAsync(f),
            f => f != null);
    }

    public ObservableCollection<LabPatientRowViewModel> Patients { get; } = new();

    public ObservableCollection<LabRecentFileViewModel> RecentFiles { get; } = new();

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

    public bool ShowPatientRosterHint
    {
        get => _showPatientRosterHint;
        private set
        {
            if (_showPatientRosterHint == value)
                return;
            _showPatientRosterHint = value;
            OnPropertyChanged();
        }
    }

#if DEBUG
    public bool ShowDebugHint => true;
#else
    public bool ShowDebugHint => false;
#endif

    public int PendingCount { get; private set; }

    public string PendingBadgeText => $"{PendingCount} Pending";

    public int ProcessedToday
    {
        get => _processedToday;
        private set
        {
            if (_processedToday == value)
                return;
            _processedToday = value;
            OnPropertyChanged();
        }
    }

    public int InQueue
    {
        get => _inQueue;
        private set
        {
            if (_inQueue == value)
                return;
            _inQueue = value;
            OnPropertyChanged();
        }
    }

    public string LastSyncDisplay
    {
        get => _lastSyncDisplay;
        private set
        {
            if (_lastSyncDisplay == value)
                return;
            _lastSyncDisplay = value;
            OnPropertyChanged();
        }
    }

    public bool ApiHealthy
    {
        get => _apiHealthy;
        private set
        {
            if (_apiHealthy == value)
                return;
            _apiHealthy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConnectionDotColor));
            OnPropertyChanged(nameof(ApiStatusLabel));
            OnPropertyChanged(nameof(ApiStatusLabelColor));
        }
    }

    public Color ConnectionDotColor =>
        ApiHealthy ? Color.FromArgb("#34D399") : Color.FromArgb("#f87171");

    public string ApiStatusLabel => ApiHealthy ? "OPTIMAL" : "OFFLINE";

    public Color ApiStatusLabelColor =>
        ApiHealthy ? Color.FromArgb("#34D399") : Color.FromArgb("#f87171");

    public ICommand OpenRecentFileCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task LoadAsync()
    {
        IsBusy = true;
        LoadFailed = false;
        try
        {
            var patientsTask = _lab.GetAssignedPatientsAsync();
            var filesTask = _lab.GetLabFilesAsync(10);
            var statsTask = _lab.GetLabStatsAsync();
            await Task.WhenAll(patientsTask, filesTask, statsTask);

            var list = await patientsTask;
            Patients.Clear();
            foreach (var dto in list.OrderBy(p => p.FullName))
                Patients.Add(LabPatientRowViewModel.FromDto(dto));

            var recent = await filesTask;
            RecentFiles.Clear();
            foreach (var r in recent)
                RecentFiles.Add(LabRecentFileViewModel.FromDto(r));

            var stats = await statsTask;

            PendingCount = Patients.Count(p => !p.HasUploadedResults);
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(PendingBadgeText));

            ProcessedToday = stats.ProcessedToday;
            InQueue = PendingCount;

            ApiHealthy = true;
            LastSyncDisplay = DateTime.Now.ToString("T");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LabDashboard] Load failed: {ex}");
            Patients.Clear();
            RecentFiles.Clear();
            ProcessedToday = 0;
            InQueue = 0;
            PendingCount = 0;
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(PendingBadgeText));
            LoadFailed = true;
            ApiHealthy = false;
        }
        finally
        {
            IsBusy = false;
            RefreshPatientRosterHint();
        }
    }

    private async Task OpenRecentAsync(LabRecentFileViewModel? file)
    {
        if (file == null)
            return;

        try
        {
            await _lab.OpenAttachmentAsync(file.AttachmentId, file.FileName);
        }
        catch (Exception ex)
        {
            var page = Shell.Current?.CurrentPage;
            if (page != null)
                await page.DisplayAlert("Open file", ex.Message, "OK");
        }
    }

    private void RefreshPatientRosterHint()
    {
        ShowPatientRosterHint = !LoadFailed && Patients.Count <= 1;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
