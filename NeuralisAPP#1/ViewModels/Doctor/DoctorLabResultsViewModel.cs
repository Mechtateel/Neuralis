using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Maui.ApplicationModel;
using NeuralisAPP_1.Localization;
using NeuralisAPP_1.Models.Lab;
using NeuralisAPP_1.Services;

namespace NeuralisAPP_1.ViewModels.Doctor;

public sealed class DoctorLabResultsViewModel : INotifyPropertyChanged
{
    private readonly Dictionary<Guid, LabResultAnalysisApiDto> _analysisCache = new();

    private LabService? _lab;
    private LabResultsListRowViewModel? _selectedResult;
    private bool _isBusy;
    private bool _isLoadingDetail;
    private bool _detailLoadFailed;
    private bool _isAnalysisLoading;
    private bool _analysisLoadFailed;
    private LabResultAnalysisApiDto? _currentAnalysis;
    private string _detailTitleLine = string.Empty;
    private string _detailTestName = string.Empty;

    public ObservableCollection<LabResultsListRowViewModel> Results { get; } = new();

    public ObservableCollection<LabResultValueRowViewModel> DetailValues { get; } = new();

    public string ResultsHeader => LabUiStrings.Results;

    public string DetailedViewTitle => LabUiStrings.DetailedView;

    public string LaboratoryValuesHeader => LabUiStrings.LaboratoryValues;

    public string LabViewLabel => LabUiStrings.View;

    public string LabDownloadLabel => LabUiStrings.Download;

    public string LabAnalyzeLabel => LabUiStrings.Analyze;

    public string AiInsightsTitle => LabUiStrings.NeuralisAiInsights;

    public string AiCloseLabel => LabUiStrings.Close;

    public string SummarySectionTitle => LabUiStrings.Summary;

    public string FindingsSectionTitle => LabUiStrings.Findings;

    public string RecommendationsSectionTitle => LabUiStrings.Recommendations;

    public string AiLaboratoryInterpretationTitle => LabUiStrings.AiLaboratoryInterpretation;

    public string AiLaboratoryInterpretationDisclaimer => LabUiStrings.EducationalInterpretationOnly;

    public string AnalysisLoadingMessage => LabUiStrings.GeneratingAiLabInterpretation;

    public string LabInterpretationFallbackHintText => LabUiStrings.AiLabInterpretationFallbackHint;

    public string PrioritySectionTitle => LabUiStrings.Priority;

    public string FailedToLoadMessage => LabUiStrings.FailedToLoadData;

    public string SelectPromptMessage => LabUiStrings.SelectLabResultPrompt;

    public string NoStructuredValuesMessage => LabUiStrings.NoStructuredValues;

    public LabResultsListRowViewModel? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (_selectedResult == value)
                return;
            _selectedResult = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedResult));
            OnPropertyChanged(nameof(ShowSelectPrompt));
            OnPropertyChanged(nameof(ShowAnalysisError));
            OnPropertyChanged(nameof(ShowAnalysisHint));
            _ = OnSelectedResultChangedAsync();
        }
    }

    public bool HasSelectedResult => SelectedResult != null;

    public bool ShowSelectPrompt => SelectedResult == null;

    public string DetailTitleLine
    {
        get => _detailTitleLine;
        private set
        {
            if (_detailTitleLine == value)
                return;
            _detailTitleLine = value;
            OnPropertyChanged();
        }
    }

    public string DetailTestName
    {
        get => _detailTestName;
        private set
        {
            if (_detailTestName == value)
                return;
            _detailTestName = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoadingDetail
    {
        get => _isLoadingDetail;
        private set
        {
            if (_isLoadingDetail == value)
                return;
            _isLoadingDetail = value;
            OnPropertyChanged();
        }
    }

    public bool DetailLoadFailed
    {
        get => _detailLoadFailed;
        private set
        {
            if (_detailLoadFailed == value)
                return;
            _detailLoadFailed = value;
            OnPropertyChanged();
        }
    }

    public string DetailErrorMessage => LabUiStrings.FailedToLoadDetail;

    public bool ShowValuesSection => HasSelectedResult && !IsLoadingDetail && !DetailLoadFailed && DetailValues.Count > 0;

    public bool ShowNoValuesMessage =>
        HasSelectedResult && !IsLoadingDetail && !DetailLoadFailed && DetailValues.Count == 0;

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

    private bool _loadFailed;

    public bool IsAnalysisLoading
    {
        get => _isAnalysisLoading;
        private set
        {
            if (_isAnalysisLoading == value)
                return;
            _isAnalysisLoading = value;
            OnPropertyChanged();
            NotifyLabInterpretationVisibility();
        }
    }

    public bool AnalysisLoadFailed
    {
        get => _analysisLoadFailed;
        private set
        {
            if (_analysisLoadFailed == value)
                return;
            _analysisLoadFailed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowAnalysisError));
            NotifyLabInterpretationVisibility();
        }
    }

    public string AnalysisErrorMessage => LabUiStrings.FailedToLoadAnalysis;

    public bool ShowAnalysisError => HasSelectedResult && AnalysisLoadFailed && !IsAnalysisLoading;

    public LabResultAnalysisApiDto? CurrentAnalysis
    {
        get => _currentAnalysis;
        private set
        {
            if (ReferenceEquals(_currentAnalysis, value))
                return;
            _currentAnalysis = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AnalysisSummaryText));
            OnPropertyChanged(nameof(AnalysisPriorityText));
            OnPropertyChanged(nameof(FindingsDisplay));
            OnPropertyChanged(nameof(RecommendationsDisplay));
            OnPropertyChanged(nameof(ShowAiContent));
            OnPropertyChanged(nameof(ShowNoAiAnalysisMessage));
            OnPropertyChanged(nameof(ShowAnalysisHint));
            OnPropertyChanged(nameof(ShowAnalysisPriorityBlock));
            OnPropertyChanged(nameof(ShowFindingsSection));
            OnPropertyChanged(nameof(AiLaboratoryInterpretationText));
            NotifyLabInterpretationVisibility();
        }
    }

    public string AnalysisSummaryText => CurrentAnalysis?.Summary ?? string.Empty;

    public string AnalysisPriorityText => CurrentAnalysis?.Priority ?? string.Empty;

    public string FindingsDisplay =>
        CurrentAnalysis?.Findings is { Count: > 0 }
            ? string.Join(Environment.NewLine, CurrentAnalysis.Findings.Select(f => $"• {f}"))
            : string.Empty;

    public string RecommendationsDisplay =>
        CurrentAnalysis?.Recommendations is { Count: > 0 }
            ? string.Join(Environment.NewLine, CurrentAnalysis.Recommendations.Select(r => $"• {r}"))
            : string.Empty;

    public bool ShowAiContent =>
        CurrentAnalysis != null
        && (!string.IsNullOrWhiteSpace(CurrentAnalysis.Summary)
            || !string.IsNullOrWhiteSpace(CurrentAnalysis.Priority)
            || (CurrentAnalysis.Findings?.Count > 0)
            || (CurrentAnalysis.Recommendations?.Count > 0));

    public bool ShowNoAiAnalysisMessage =>
        CurrentAnalysis != null
        && !ShowAiContent
        && !IsAnalysisLoading
        && !AnalysisLoadFailed;

    public bool ShowAnalysisHint =>
        HasSelectedResult && CurrentAnalysis == null && !IsAnalysisLoading && !AnalysisLoadFailed;

    public string AnalysisHintText => LabUiStrings.TapAnalyzeForInsights;

    public string NoAiAnalysisText => LabUiStrings.NoAiAnalysisAvailable;

    /// <summary>Only highlight when review tier is elevated (abnormal labs).</summary>
    public bool ShowAnalysisPriorityBlock =>
        ShowAiContent
        && CurrentAnalysis is { Priority: var p }
        && (string.Equals(p, "HIGH", StringComparison.OrdinalIgnoreCase)
            || string.Equals(p, "MEDIUM", StringComparison.OrdinalIgnoreCase));

    public bool ShowFindingsSection =>
        CurrentAnalysis?.Findings is { Count: > 0 };

    public string AiLaboratoryInterpretationText =>
        string.IsNullOrWhiteSpace(CurrentAnalysis?.AiLaboratoryInterpretation)
            ? LabUiStrings.AiLabInterpretationUnavailable
            : CurrentAnalysis.AiLaboratoryInterpretation.Trim();

    public bool ShowAiLaboratoryInterpretationSection =>
        HasSelectedResult
        && CurrentAnalysis != null
        && !IsAnalysisLoading
        && !AnalysisLoadFailed;

    public bool ShowLabInterpretationFallbackHint =>
        ShowAiLaboratoryInterpretationSection
        && AiLaboratoryInterpretationText.Equals(
            LabUiStrings.AiLabInterpretationUnavailable,
            StringComparison.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void AttachToken(string token) => _lab = new LabService(token);

    public void DismissAiPanel()
    {
        CurrentAnalysis = null;
        AnalysisLoadFailed = false;
    }

    public async Task LoadResultsAsync()
    {
        if (_lab == null)
            return;

        IsBusy = true;
        LoadFailed = false;
        try
        {
            var list = await _lab.GetLabResultsAsync();
            var prevId = SelectedResult?.Id;
            Results.Clear();
            foreach (var dto in list)
                Results.Add(LabResultsListRowViewModel.FromDto(dto));

            if (Results.Count == 0)
                SelectedResult = null;
            else if (prevId is { } pid)
                SelectedResult = Results.FirstOrDefault(r => r.Id == pid) ?? Results[0];
            else
                SelectedResult = Results[0];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DoctorLabResults] list: {ex}");
            LoadFailed = true;
            Results.Clear();
            SelectedResult = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OnSelectedResultChangedAsync()
    {
        DetailValues.Clear();
        DetailLoadFailed = false;
        DetailTitleLine = string.Empty;
        DetailTestName = string.Empty;
        AnalysisLoadFailed = false;
        OnPropertyChanged(nameof(ShowAnalysisError));
        OnPropertyChanged(nameof(ShowValuesSection));
        OnPropertyChanged(nameof(ShowNoValuesMessage));

        if (_lab == null || SelectedResult == null)
        {
            CurrentAnalysis = null;
            return;
        }

        if (_analysisCache.TryGetValue(SelectedResult.Id, out var cached))
            CurrentAnalysis = cached;
        else
            CurrentAnalysis = null;

        IsLoadingDetail = true;
        try
        {
            var detail = await _lab.GetLabResultDetailAsync(SelectedResult.Id);
            if (detail == null)
            {
                DetailLoadFailed = true;
                return;
            }

            DetailTestName = detail.TestName;
            DetailTitleLine = $"{detail.FileName} · {detail.UploadedAtUtc.ToLocalTime():g}";

            DetailValues.Clear();
            if (detail.Values is { Count: > 0 })
            {
                foreach (var v in detail.Values)
                    DetailValues.Add(LabResultValueRowViewModel.FromDto(v));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DoctorLabResults] detail: {ex}");
            DetailLoadFailed = true;
        }
        finally
        {
            IsLoadingDetail = false;
            OnPropertyChanged(nameof(ShowValuesSection));
            OnPropertyChanged(nameof(ShowNoValuesMessage));
        }
    }

    public async Task ViewSelectedAsync()
    {
        if (_lab == null || SelectedResult == null)
            return;
        try
        {
            await _lab.OpenAttachmentAsync(SelectedResult.Id, SelectedResult.FileName);
        }
        catch (Exception ex)
        {
            await AlertAsync(LabUiStrings.LabResultsTitle, ex.Message);
        }
    }

    public async Task DownloadSelectedAsync()
    {
        if (_lab == null || SelectedResult == null)
            return;
        try
        {
            var outcome = await _lab.SaveLabFileForUserAsync(SelectedResult.Id, SelectedResult.FileName);
            var page = Shell.Current?.CurrentPage;
            if (page == null)
                return;
            var msg = outcome.UsedOsShare
                ? LabUiStrings.ShareOpenedForSave
                : LabUiStrings.FileSavedTo(outcome.SavedPath!);
            await MainThread.InvokeOnMainThreadAsync(() =>
                page.DisplayAlert(LabUiStrings.Download, msg, LabUiStrings.Ok));
        }
        catch (Exception ex)
        {
            await AlertAsync(LabUiStrings.Download, ex.Message);
        }
    }

    public async Task AnalyzeSelectedAsync()
    {
        if (_lab == null || SelectedResult == null)
            return;

        var id = SelectedResult.Id;
        if (_analysisCache.TryGetValue(id, out var hit))
        {
            CurrentAnalysis = hit;
            AnalysisLoadFailed = false;
            return;
        }

        IsAnalysisLoading = true;
        AnalysisLoadFailed = false;
        OnPropertyChanged(nameof(ShowAnalysisHint));
        OnPropertyChanged(nameof(ShowAnalysisError));
        try
        {
            var dto = await _lab.GetLabResultAnalysisAsync(id);
            if (dto == null)
            {
                AnalysisLoadFailed = true;
                CurrentAnalysis = null;
                return;
            }

            _analysisCache[id] = dto;
            CurrentAnalysis = dto;
            System.Diagnostics.Debug.WriteLine(
                $"[LAB AI] ViewModel received value: len={dto.AiLaboratoryInterpretation?.Length ?? 0} " +
                $"showSection={ShowAiLaboratoryInterpretationSection} textPreview={(dto.AiLaboratoryInterpretation?.Length > 0 ? dto.AiLaboratoryInterpretation[..Math.Min(40, dto.AiLaboratoryInterpretation.Length)] : "(empty)")}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DoctorLabResults] analysis: {ex}");
            AnalysisLoadFailed = true;
            CurrentAnalysis = null;
        }
        finally
        {
            IsAnalysisLoading = false;
            OnPropertyChanged(nameof(ShowAnalysisHint));
            OnPropertyChanged(nameof(ShowAnalysisError));
            OnPropertyChanged(nameof(ShowNoAiAnalysisMessage));
            NotifyLabInterpretationVisibility();
            System.Diagnostics.Debug.WriteLine(
                $"[LAB AI] UI binding updated: ShowAiLaboratoryInterpretationSection={ShowAiLaboratoryInterpretationSection} " +
                $"IsAnalysisLoading={IsAnalysisLoading} AnalysisLoadFailed={AnalysisLoadFailed}");
        }
    }

    private void NotifyLabInterpretationVisibility()
    {
        OnPropertyChanged(nameof(ShowAiLaboratoryInterpretationSection));
        OnPropertyChanged(nameof(ShowLabInterpretationFallbackHint));
        OnPropertyChanged(nameof(AiLaboratoryInterpretationText));
    }

    private static Task AlertAsync(string title, string message)
    {
        var page = Shell.Current?.CurrentPage;
        if (page == null)
            return Task.CompletedTask;
        return MainThread.InvokeOnMainThreadAsync(() =>
            page.DisplayAlert(title, message, LabUiStrings.Ok));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
