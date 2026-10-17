using System.Globalization;
using NeuralisAPP_1.Models.Auth;
using NeuralisAPP_1.Models.News;
using NeuralisAPP_1.Services;

namespace NeuralisAPP_1.Views.Admin;

public partial class AdminNewsEditorPage : ContentPage
{
    private readonly NewsService _newsService;
    private readonly Guid? _id;
    private NewsItemDto? _loaded;
    private bool _suppressDirty;
    private bool _isPreview;

    public event EventHandler<NewsItemDto>? Saved;
    public event EventHandler<Guid>? Deleted;

    private static readonly (string Slug, string Label)[] Categories =
    {
        ("ai_research",      "AI research"),
        ("clinical_trials",  "Clinical trials"),
        ("health_tips",      "Health tips"),
        ("biotech",          "Bio-tech"),
        ("announcement",     "Announcement"),
        ("advisory",         "Advisory"),
        ("general",          "General"),
    };

    private static readonly (int Code, string Label)[] StatusOptions =
    {
        (0, "Draft"),
        (1, "Published"),
        (2, "Archived"),
    };

    private static readonly (int Code, string Label)[] PriorityOptions =
    {
        (0, "Normal"),
        (1, "Important"),
        (2, "Critical alert"),
    };

    private static readonly (string Resource, string Label)[] CoverChoices =
    {
        ("",                 "Auto-pick from category"),
        ("neuralis.png",     "Neuralis · primary"),
        ("neuralis_logo.png","Neuralis · logo"),
        ("neuralis_logo1.png","Neuralis · alt mark"),
    };

    public AdminNewsEditorPage(Guid? id)
    {
        InitializeComponent();
        _id = id;
        _newsService = new NewsService(AuthService.CurrentToken);

        foreach (var c in Categories) CategoryPicker.Items.Add(c.Label);
        foreach (var s in StatusOptions) StatusPicker.Items.Add(s.Label);
        foreach (var p in PriorityOptions) PriorityPicker.Items.Add(p.Label);
        foreach (var c in CoverChoices) CoverPicker.Items.Add(c.Label);

        StatusPicker.SelectedIndexChanged += (_, _) => UpdateStatusChip();
        PublishDatePicker.Date = DateTime.Today;

        TitleEntry.TextChanged += OnAnyFieldChanged;
        SubtitleEntry.TextChanged += OnAnyFieldChanged;
        SummaryEditor.TextChanged += OnAnyFieldChanged;
        ContentEditor.TextChanged += OnAnyFieldChanged;
        TagsEntry.TextChanged += OnAnyFieldChanged;

        if (_id == null)
        {
            StatusPicker.SelectedIndex = 0; // Draft
            PriorityPicker.SelectedIndex = 0;
            CategoryPicker.SelectedIndex = Array.FindIndex(Categories, c => c.Slug == "general");
            CoverPicker.SelectedIndex = 0;
            HeaderTitleLabel.Text = "New article";
            MetadataLabel.Text = "New draft · not saved yet.";
            ApplyAuthor();
            UpdateStatusChip();
            UpdateWordCount(ContentEditor.Text);
        }
        else
        {
            DeleteButton.IsVisible = true;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_id != null && _loaded == null)
            await LoadAsync(_id.Value);
    }

    private async Task LoadAsync(Guid id)
    {
        HeaderLoading.IsVisible = true;
        HeaderLoading.IsRunning = true;
        try
        {
            var item = await _newsService.GetByIdForAdminAsync(id);
            if (item == null)
            {
                await DisplayAlert("Article", "Article not found.", "OK");
                await Navigation.PopAsync();
                return;
            }
            _loaded = item;
            ApplyToForm(item);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Article", ex.Message, "OK");
        }
        finally
        {
            HeaderLoading.IsRunning = false;
            HeaderLoading.IsVisible = false;
        }
    }

    private void ApplyToForm(NewsItemDto n)
    {
        _suppressDirty = true;

        HeaderTitleLabel.Text = string.IsNullOrWhiteSpace(n.Title) ? "Untitled article" : n.Title;
        TitleEntry.Text = n.Title;
        SubtitleEntry.Text = n.Subtitle;
        SummaryEditor.Text = n.Summary;
        ContentEditor.Text = n.Content;
        TagsEntry.Text = n.Tags;
        FeaturedSwitch.IsToggled = n.IsFeatured;

        var catIdx = Array.FindIndex(Categories, c => string.Equals(c.Slug, n.Category, StringComparison.OrdinalIgnoreCase));
        CategoryPicker.SelectedIndex = catIdx < 0 ? Array.FindIndex(Categories, c => c.Slug == "general") : catIdx;

        var statusIdx = Array.FindIndex(StatusOptions, s => s.Code == n.Status);
        StatusPicker.SelectedIndex = statusIdx < 0 ? 0 : statusIdx;

        var prIdx = Array.FindIndex(PriorityOptions, p => p.Code == n.Priority);
        PriorityPicker.SelectedIndex = prIdx < 0 ? 0 : prIdx;

        var coverIdx = Array.FindIndex(CoverChoices, c => string.Equals(c.Resource, n.CoverImageUrl, StringComparison.OrdinalIgnoreCase));
        CoverPicker.SelectedIndex = coverIdx < 0 ? 0 : coverIdx;

        PublishDatePicker.Date = (n.PublishedAt ?? n.CreatedAt).ToLocalTime().Date;

        AuthorLabel.Text = string.IsNullOrWhiteSpace(n.AuthorName) ? "Neuralis Admin" : n.AuthorName;
        var created = n.CreatedAt.ToLocalTime().ToString("g", CultureInfo.InvariantCulture);
        var updated = n.UpdatedAt.HasValue
            ? n.UpdatedAt.Value.ToLocalTime().ToString("g", CultureInfo.InvariantCulture)
            : "—";
        MetadataLabel.Text = $"Created {created} · Updated {updated} · {n.Views:N0} views";

        UpdateStatusChip();
        UpdateWordCount(n.Content);

        _suppressDirty = false;
    }

    private void ApplyAuthor()
    {
        var u = AuthService.CurrentUser;
        AuthorLabel.Text = u == null
            ? "Neuralis Admin"
            : (string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName);
    }

    // =====================================================
    // Field changes
    // =====================================================
    private void OnAnyFieldChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressDirty) return;
        FooterStatus.Text = "Unsaved changes";
        FooterStatus.TextColor = Color.FromArgb("#FACC15");
    }

    private void OnContentChanged(object? sender, TextChangedEventArgs e)
    {
        OnAnyFieldChanged(sender, e);
        UpdateWordCount(e.NewTextValue);
    }

    private void UpdateWordCount(string? text)
    {
        var words = string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var minutes = Math.Max(1, words / 200);
        WordCountLabel.Text = $"{words:N0} words · ~{minutes} min read";
    }

    private void UpdateStatusChip()
    {
        var code = StatusOptions.ElementAtOrDefault(StatusPicker.SelectedIndex).Code;
        var (label, fg) = code switch
        {
            1 => ("PUBLISHED", "#4ADE80"),
            2 => ("ARCHIVED", "#9DBEC7"),
            _ => ("DRAFT", "#FACC15"),
        };
        StatusChipLabel.Text = label;
        StatusChipLabel.TextColor = Color.FromArgb(fg);
    }

    // =====================================================
    // Save / publish / delete
    // =====================================================
    private NewsEditorPayload BuildPayload(int? statusOverride = null)
    {
        var statusIdx = StatusPicker.SelectedIndex;
        var status = statusOverride ?? (statusIdx < 0 ? 0 : StatusOptions[statusIdx].Code);
        var catIdx = CategoryPicker.SelectedIndex;
        var cat = catIdx < 0 ? "general" : Categories[catIdx].Slug;
        var priorityIdx = PriorityPicker.SelectedIndex;
        var priority = priorityIdx < 0 ? 0 : PriorityOptions[priorityIdx].Code;
        var coverIdx = CoverPicker.SelectedIndex;
        var cover = coverIdx < 0 ? "" : CoverChoices[coverIdx].Resource;

        return new NewsEditorPayload
        {
            Title = (TitleEntry.Text ?? "").Trim(),
            Subtitle = (SubtitleEntry.Text ?? "").Trim(),
            Summary = (SummaryEditor.Text ?? "").Trim(),
            Content = (ContentEditor.Text ?? "").Trim(),
            Category = cat,
            Tags = (TagsEntry.Text ?? "").Trim(),
            CoverImageUrl = cover,
            Status = status,
            Priority = priority,
            IsFeatured = FeaturedSwitch.IsToggled,
            PublishedAt = status == 1
                ? DateTime.SpecifyKind(PublishDatePicker.Date, DateTimeKind.Local).ToUniversalTime()
                : null,
            IsPublished = status == 1,
        };
    }

    private bool ValidateOrAlert(NewsEditorPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Title))
        {
            DisplayAlert("Article", "Add a title to continue.", "OK");
            return false;
        }
        if (payload.Title.Length > 200)
        {
            DisplayAlert("Article", "Title is too long (200 chars max).", "OK");
            return false;
        }
        if (string.IsNullOrWhiteSpace(payload.Content))
        {
            DisplayAlert("Article", "Write some body content before saving.", "OK");
            return false;
        }
        if (payload.Content.Length > 4000)
        {
            DisplayAlert("Article", "Body is too long (4000 chars max).", "OK");
            return false;
        }
        return true;
    }

    private async void OnSaveDraftClicked(object? sender, EventArgs e) =>
        await SaveAsync(0);

    private async void OnPublishClicked(object? sender, EventArgs e) =>
        await SaveAsync(1);

    private async Task SaveAsync(int status)
    {
        var payload = BuildPayload(status);
        if (!ValidateOrAlert(payload))
            return;

        HeaderLoading.IsVisible = true;
        HeaderLoading.IsRunning = true;
        SaveDraftButton.IsEnabled = false;
        PublishButton.IsEnabled = false;
        FooterStatus.Text = "Saving…";
        FooterStatus.TextColor = Color.FromArgb("#9DBEC7");

        try
        {
            NewsItemDto? result;
            if (_loaded == null && _id == null)
                result = await _newsService.CreateAsync(payload);
            else
            {
                var targetId = _loaded?.Id ?? _id!.Value;
                result = await _newsService.UpdateAsync(targetId, payload);
            }

            if (result == null)
                throw new Exception("Server returned no article.");

            _loaded = result;
            ApplyToForm(result);
            DeleteButton.IsVisible = true;

            FooterStatus.Text = status == 1 ? "Published · visible on patient feed." : "Draft saved.";
            FooterStatus.TextColor = Color.FromArgb("#4ADE80");

            Saved?.Invoke(this, result);
        }
        catch (Exception ex)
        {
            FooterStatus.Text = ex.Message;
            FooterStatus.TextColor = Color.FromArgb("#F87171");
        }
        finally
        {
            HeaderLoading.IsRunning = false;
            HeaderLoading.IsVisible = false;
            SaveDraftButton.IsEnabled = true;
            PublishButton.IsEnabled = true;
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_loaded == null) return;
        var confirm = await DisplayAlert("Delete article",
            $"Permanently delete \"{_loaded.Title}\"? This can't be undone.",
            "Delete", "Cancel");
        if (!confirm) return;

        try
        {
            HeaderLoading.IsVisible = true;
            HeaderLoading.IsRunning = true;
            await _newsService.DeleteAsync(_loaded.Id);
            Deleted?.Invoke(this, _loaded.Id);
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Article", ex.Message, "OK");
        }
        finally
        {
            HeaderLoading.IsRunning = false;
            HeaderLoading.IsVisible = false;
        }
    }

    private async void OnBackTapped(object? sender, EventArgs e) =>
        await Navigation.PopAsync();

    // =====================================================
    // Preview
    // =====================================================
    private void OnPreviewToggleClicked(object? sender, EventArgs e)
    {
        _isPreview = !_isPreview;
        PreviewPane.IsVisible = _isPreview;
        PreviewToggle.Text = _isPreview ? "Hide preview" : "Preview";

        if (_isPreview)
            RenderPreview();
    }

    private void RenderPreview()
    {
        var title = (TitleEntry.Text ?? "").Trim();
        var subtitle = (SubtitleEntry.Text ?? "").Trim();
        var body = (ContentEditor.Text ?? "").Trim();
        var catIdx = CategoryPicker.SelectedIndex;
        var catLabel = catIdx >= 0 ? Categories[catIdx].Label.ToUpperInvariant() : "GENERAL";
        var author = AuthorLabel.Text;
        var dateText = PublishDatePicker.Date.ToString("MMM d yyyy", CultureInfo.InvariantCulture);
        var words = body.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var minutes = Math.Max(1, words / 200);

        PreviewCategoryLabel.Text = catLabel;
        PreviewMetaLabel.Text = $"{author} · {dateText} · {minutes} min read";
        PreviewTitleLabel.Text = string.IsNullOrWhiteSpace(title) ? "(Untitled)" : title;
        PreviewSubtitleLabel.Text = string.IsNullOrWhiteSpace(subtitle)
            ? (SummaryEditor.Text ?? "").Trim()
            : subtitle;

        PreviewBodyHost.Children.Clear();
        if (string.IsNullOrWhiteSpace(body))
        {
            PreviewBodyHost.Children.Add(new Label
            {
                Text = "Body is empty.",
                FontSize = 13,
                TextColor = Color.FromArgb("#6E8C96")
            });
            return;
        }

        foreach (var view in RenderMarkdownBlocks(body))
            PreviewBodyHost.Children.Add(view);
    }

    /// <summary>
    /// Lightweight Markdown-like block renderer suitable for the in-page preview:
    /// supports H2 (## ), H3 (### ), bullet lists (- ), callouts (&gt; ), and paragraphs.
    /// Same heuristics also drive the patient article view.
    /// </summary>
    private IEnumerable<View> RenderMarkdownBlocks(string source)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var paragraph = new List<string>();
        List<string>? bullets = null;

        View Heading(string text, int level)
        {
            var fontSize = level switch { 2 => 18, _ => 15 };
            return new Label
            {
                Text = text,
                FontSize = fontSize,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#E6F7FA"),
                LineBreakMode = LineBreakMode.WordWrap
            };
        }

        View Paragraph(string text) => new Label
        {
            Text = text,
            FontSize = 14,
            TextColor = Color.FromArgb("#C5DDE5"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        View Callout(string text) => new Border
        {
            BackgroundColor = Color.FromArgb("#0C3A40"),
            Stroke = Color.FromArgb("#2FE3C9"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(14, 10),
            Content = new Label
            {
                Text = text,
                FontSize = 13,
                TextColor = Color.FromArgb("#E6F7FA"),
                LineBreakMode = LineBreakMode.WordWrap
            }
        };

        View BulletList(List<string> items)
        {
            var stack = new VerticalStackLayout { Spacing = 4 };
            foreach (var item in items)
            {
                stack.Children.Add(new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label
                        {
                            Text = "•",
                            FontSize = 14,
                            TextColor = Color.FromArgb("#2FE3C9"),
                            VerticalOptions = LayoutOptions.Start
                        },
                        new Label
                        {
                            Text = item,
                            FontSize = 14,
                            TextColor = Color.FromArgb("#C5DDE5"),
                            LineBreakMode = LineBreakMode.WordWrap
                        }
                    }
                });
            }
            return stack;
        }

        var output = new List<View>();
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                if (bullets != null)
                {
                    output.Add(BulletList(bullets));
                    bullets = null;
                }
                if (paragraph.Count > 0)
                {
                    output.Add(Paragraph(string.Join(" ", paragraph)));
                    paragraph.Clear();
                }
                continue;
            }

            if (line.StartsWith("## "))
            {
                if (bullets != null) { output.Add(BulletList(bullets)); bullets = null; }
                if (paragraph.Count > 0) { output.Add(Paragraph(string.Join(" ", paragraph))); paragraph.Clear(); }
                output.Add(Heading(line[3..].Trim(), 2));
            }
            else if (line.StartsWith("### "))
            {
                if (bullets != null) { output.Add(BulletList(bullets)); bullets = null; }
                if (paragraph.Count > 0) { output.Add(Paragraph(string.Join(" ", paragraph))); paragraph.Clear(); }
                output.Add(Heading(line[4..].Trim(), 3));
            }
            else if (line.StartsWith("- "))
            {
                if (paragraph.Count > 0) { output.Add(Paragraph(string.Join(" ", paragraph))); paragraph.Clear(); }
                bullets ??= new List<string>();
                bullets.Add(line[2..].Trim());
            }
            else if (line.StartsWith("> "))
            {
                if (bullets != null) { output.Add(BulletList(bullets)); bullets = null; }
                if (paragraph.Count > 0) { output.Add(Paragraph(string.Join(" ", paragraph))); paragraph.Clear(); }
                output.Add(Callout(line[2..].Trim()));
            }
            else
            {
                if (bullets != null) { output.Add(BulletList(bullets)); bullets = null; }
                paragraph.Add(line.Trim());
            }
        }

        if (bullets != null) output.Add(BulletList(bullets));
        if (paragraph.Count > 0) output.Add(Paragraph(string.Join(" ", paragraph)));

        return output;
    }
}
