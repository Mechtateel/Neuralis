using Microsoft.Maui.Controls.Shapes;
using NeuralisAPP_1.Models.Auth;
using NeuralisAPP_1.Models.News;
using NeuralisAPP_1.Services;
using NeuralisAPP_1.Views.Admin.Controls;

namespace NeuralisAPP_1.Views.Admin;

public partial class AdminNewsPage : ContentPage
{
    private readonly NewsService _newsService;
    private readonly List<NewsItemDto> _all = new();
    private string _statusFilter = "all";
    private string _categoryFilter = "";
    private string _searchText = "";

    private static readonly (string Key, string Label)[] Categories =
    {
        ("",                 "All categories"),
        ("ai_research",      "AI research"),
        ("clinical_trials",  "Clinical trials"),
        ("health_tips",      "Health tips"),
        ("biotech",          "Bio-tech"),
        ("announcement",     "Announcement"),
        ("advisory",         "Advisory"),
        ("general",          "General"),
    };

    public AdminNewsPage()
    {
        InitializeComponent();

        _newsService = new NewsService(AuthService.CurrentToken);

        AdminNavigationHelper.WireSidebar(AdminSidebar, this, AdminSidebarItem.News);

        foreach (var c in Categories)
            CategoryPicker.Items.Add(c.Label);
        CategoryPicker.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        AdminSidebar.ActiveItem = AdminSidebarItem.News;
        await AdminSidebar.RefreshProfileAsync();
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        HeaderLoading.IsVisible = true;
        HeaderLoading.IsRunning = true;
        try
        {
            var list = await _newsService.GetAllForAdminAsync();
            _all.Clear();
            _all.AddRange(list);
            RenderAll();
        }
        catch (Exception ex)
        {
            await DisplayAlert("News", ex.Message, "OK");
        }
        finally
        {
            HeaderLoading.IsRunning = false;
            HeaderLoading.IsVisible = false;
        }
    }

    private void RenderAll()
    {
        // Stats
        StatTotalLabel.Text = _all.Count.ToString("N0");
        StatPublishedLabel.Text = _all.Count(n => n.Status == 1).ToString("N0");
        StatDraftsLabel.Text = _all.Count(n => n.Status == 0).ToString("N0");
        StatArchivedLabel.Text = _all.Count(n => n.Status == 2).ToString("N0");

        // Filtered list
        ArticlesList.Children.Clear();

        var filtered = _all.AsEnumerable();
        if (_statusFilter is "published") filtered = filtered.Where(n => n.Status == 1);
        else if (_statusFilter is "draft") filtered = filtered.Where(n => n.Status == 0);
        else if (_statusFilter is "archived") filtered = filtered.Where(n => n.Status == 2);

        if (!string.IsNullOrWhiteSpace(_categoryFilter))
            filtered = filtered.Where(n => string.Equals(n.Category, _categoryFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var q = _searchText.ToLowerInvariant();
            filtered = filtered.Where(n =>
                (n.Title ?? "").ToLowerInvariant().Contains(q) ||
                (n.Subtitle ?? "").ToLowerInvariant().Contains(q) ||
                (n.Summary ?? "").ToLowerInvariant().Contains(q) ||
                (n.Tags ?? "").ToLowerInvariant().Contains(q));
        }

        var rows = filtered
            .OrderByDescending(n => n.UpdatedAt ?? n.PublishedAt ?? n.CreatedAt)
            .ToList();

        if (rows.Count == 0)
        {
            EmptyLabel.IsVisible = true;
            return;
        }
        EmptyLabel.IsVisible = false;

        foreach (var n in rows)
            ArticlesList.Children.Add(BuildRow(n));
    }

    private View BuildRow(NewsItemDto n)
    {
        var statusChip = BuildStatusChip(n.Status, n.IsFeatured);

        var actions = new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center,
            Children = { BuildEditButton(n), BuildToggleStatusButton(n), BuildArchiveButton(n), BuildDeleteButton(n) }
        };

        var meta = new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                BuildChip(CategoryLabel(n.Category), "#0E2A33", "#1F5260", "#9DBEC7"),
                BuildChip($"Author · {n.AuthorName}", "#0E2A33", "#1F5260", "#9DBEC7"),
                BuildChip(FormatRelativeTime(n.UpdatedAt ?? n.PublishedAt ?? n.CreatedAt), "#0E2A33", "#1F5260", "#9DBEC7"),
                BuildChip($"{n.Views} views", "#0E2A33", "#1F5260", "#9DBEC7")
            }
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 12,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 8,
        };

        var titleLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(n.Title) ? "(Untitled)" : n.Title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#E6F7FA"),
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        };

        var subtitleLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(n.Subtitle)
                ? (string.IsNullOrWhiteSpace(n.Summary) ? Truncate(n.Content, 140) : Truncate(n.Summary, 160))
                : n.Subtitle,
            FontSize = 12,
            TextColor = Color.FromArgb("#9DBEC7"),
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        };

        Grid.SetRow(titleLabel, 0); Grid.SetColumn(titleLabel, 0);
        Grid.SetRow(subtitleLabel, 1); Grid.SetColumn(subtitleLabel, 0);
        Grid.SetRow(meta, 2); Grid.SetColumn(meta, 0);
        Grid.SetRow(statusChip, 0); Grid.SetColumn(statusChip, 1);
        Grid.SetRowSpan(statusChip, 1);
        Grid.SetRow(actions, 0); Grid.SetColumn(actions, 2);
        Grid.SetRowSpan(actions, 3);

        grid.Children.Add(titleLabel);
        grid.Children.Add(subtitleLabel);
        grid.Children.Add(meta);
        grid.Children.Add(statusChip);
        grid.Children.Add(actions);

        var border = new Border
        {
            Style = (Style?)Resources["NewsRowStyle"],
            Content = grid
        };
        border.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OpenEditorAsync(n.Id))
        });

        return border;
    }

    private View BuildStatusChip(int status, bool isFeatured)
    {
        var (label, bg, stroke, fg) = status switch
        {
            0 => ("DRAFT", "#3A2A0C", "#7A5A0A", "#FACC15"),
            2 => ("ARCHIVED", "#1A2630", "#314657", "#9DBEC7"),
            _ => ("PUBLISHED", "#0E3A1F", "#1F6A38", "#4ADE80"),
        };

        var row = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
        row.Children.Add(BuildChip(label, bg, stroke, fg));
        if (isFeatured)
            row.Children.Add(BuildChip("FEATURED", "#0C3A40", "#2FE3C9", "#2FE3C9"));
        return row;
    }

    private Border BuildChip(string text, string bg, string stroke, string fg)
    {
        return new Border
        {
            Style = (Style?)Resources["ChipStyle"],
            BackgroundColor = Color.FromArgb(bg),
            Stroke = Color.FromArgb(stroke),
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = text,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                CharacterSpacing = 1,
                TextColor = Color.FromArgb(fg)
            }
        };
    }

    private Button BuildEditButton(NewsItemDto n)
    {
        var btn = new Button
        {
            Text = "Edit",
            Style = (Style?)Resources["GhostButton"],
            CommandParameter = n
        };
        btn.Clicked += async (_, _) => await OpenEditorAsync(n.Id);
        return btn;
    }

    private Button BuildToggleStatusButton(NewsItemDto n)
    {
        var isPublished = n.Status == 1;
        var btn = new Button
        {
            Text = isPublished ? "Unpublish" : "Publish",
            Style = (Style?)Resources["GhostButton"],
            CommandParameter = n,
        };
        if (!isPublished)
        {
            btn.BackgroundColor = Color.FromArgb("#0D3C44");
            btn.BorderColor = Color.FromArgb("#2FE3C9");
            btn.TextColor = Color.FromArgb("#2FE3C9");
        }
        btn.Clicked += async (_, _) =>
        {
            try
            {
                btn.IsEnabled = false;
                var updated = isPublished
                    ? await _newsService.UnpublishAsync(n.Id)
                    : await _newsService.PublishAsync(n.Id);
                if (updated != null)
                    ReplaceInCache(updated);
                RenderAll();
            }
            catch (Exception ex)
            {
                await DisplayAlert("News", ex.Message, "OK");
            }
            finally
            {
                btn.IsEnabled = true;
            }
        };
        return btn;
    }

    private Button BuildArchiveButton(NewsItemDto n)
    {
        var btn = new Button
        {
            Text = n.Status == 2 ? "Restore" : "Archive",
            Style = (Style?)Resources["GhostButton"],
        };
        btn.Clicked += async (_, _) =>
        {
            try
            {
                btn.IsEnabled = false;
                if (n.Status == 2)
                {
                    var unpub = await _newsService.UnpublishAsync(n.Id);
                    if (unpub != null) ReplaceInCache(unpub);
                }
                else
                {
                    var arch = await _newsService.ArchiveAsync(n.Id);
                    if (arch != null) ReplaceInCache(arch);
                }
                RenderAll();
            }
            catch (Exception ex)
            {
                await DisplayAlert("News", ex.Message, "OK");
            }
            finally
            {
                btn.IsEnabled = true;
            }
        };
        return btn;
    }

    private Button BuildDeleteButton(NewsItemDto n)
    {
        var btn = new Button
        {
            Text = "Delete",
            Style = (Style?)Resources["GhostButton"],
            BorderColor = Color.FromArgb("#7A1A1A"),
            TextColor = Color.FromArgb("#F87171"),
        };
        btn.Clicked += async (_, _) =>
        {
            var confirm = await DisplayAlert("Delete article",
                $"Permanently delete \"{n.Title}\"? This can't be undone.",
                "Delete", "Cancel");
            if (!confirm) return;

            try
            {
                btn.IsEnabled = false;
                await _newsService.DeleteAsync(n.Id);
                _all.RemoveAll(x => x.Id == n.Id);
                RenderAll();
            }
            catch (Exception ex)
            {
                await DisplayAlert("News", ex.Message, "OK");
            }
            finally
            {
                btn.IsEnabled = true;
            }
        };
        return btn;
    }

    private void ReplaceInCache(NewsItemDto updated)
    {
        var idx = _all.FindIndex(x => x.Id == updated.Id);
        if (idx >= 0)
            _all[idx] = updated;
        else
            _all.Add(updated);
    }

    private async Task OpenEditorAsync(Guid? id)
    {
        var editor = new AdminNewsEditorPage(id);
        editor.Saved += (_, item) =>
        {
            ReplaceInCache(item);
            RenderAll();
        };
        editor.Deleted += (_, deletedId) =>
        {
            _all.RemoveAll(x => x.Id == deletedId);
            RenderAll();
        };
        await Navigation.PushAsync(editor);
    }

    private async void OnRefreshClicked(object? sender, EventArgs e) => await ReloadAsync();

    private async void OnNewArticleClicked(object? sender, EventArgs e) =>
        await OpenEditorAsync(null);

    private void OnFilterTabClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        SetActiveTab(btn);

        _statusFilter = btn == TabAll ? "all"
            : btn == TabPublished ? "published"
            : btn == TabDrafts ? "draft"
            : btn == TabArchived ? "archived"
            : "all";
        RenderAll();
    }

    private void SetActiveTab(Button active)
    {
        var inactive = (Style?)Resources["TabButtonStyle"];
        var activeStyle = (Style?)Resources["TabButtonActiveStyle"];
        foreach (var b in new[] { TabAll, TabPublished, TabDrafts, TabArchived })
            b.Style = b == active ? activeStyle : inactive;
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchText = (e.NewTextValue ?? "").Trim();
        RenderAll();
    }

    private void OnCategoryChanged(object? sender, EventArgs e)
    {
        var idx = CategoryPicker.SelectedIndex;
        if (idx < 0 || idx >= Categories.Length)
            _categoryFilter = "";
        else
            _categoryFilter = Categories[idx].Key;
        RenderAll();
    }

    private static string CategoryLabel(string slug) => slug switch
    {
        "ai_research" => "AI RESEARCH",
        "clinical_trials" => "CLINICAL TRIALS",
        "health_tips" => "HEALTH TIPS",
        "biotech" => "BIO-TECH",
        "announcement" => "ANNOUNCEMENT",
        "advisory" => "ADVISORY",
        _ => "GENERAL",
    };

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return "(no body)";
        text = text.Trim().ReplaceLineEndings(" ");
        return text.Length <= max ? text : text[..max].TrimEnd() + "…";
    }

    private static string FormatRelativeTime(DateTime when)
    {
        var d = DateTime.UtcNow - when;
        if (d.TotalSeconds < 60) return "Just now";
        if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes}m ago";
        if (d.TotalHours < 24) return $"{(int)d.TotalHours}h ago";
        if (d.TotalDays < 7) return $"{(int)d.TotalDays}d ago";
        return when.ToLocalTime().ToString("dd MMM yyyy");
    }
}
