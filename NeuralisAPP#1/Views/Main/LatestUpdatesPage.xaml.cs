using System.Collections.ObjectModel;
using NeuralisAPP_1.Models.News;
using NeuralisAPP_1.Services;
using NeuralisAPP_1.Views.Main.Controls;

namespace NeuralisAPP_1.Views.Main;

public partial class LatestUpdatesPage : ContentPage
{
    /// <summary>Below this width the news grid uses a single column.</summary>
    private const double MobileGridBreakpoint = 600;

    /// <summary>At or above this width the featured block uses a 60/40 side-by-side layout.</summary>
    private const double DesktopFeaturedBreakpoint = 960;

    private readonly ObservableCollection<NewsFeedDisplayItem> _gridItems = new();
    private readonly List<NewsFeedDisplayItem> _allDisplay = new();
    private readonly Button[] _filterButtons;

    private NewsCategoryFilter _filter = NewsCategoryFilter.All;
    private NewsFeedDisplayItem? _featuredItem;
    private bool _stackedFeatured;
    private bool _hasApiArticles;

    private static readonly string[] HeroImages = ["neuralis.png", "neuralis_logo.png", "neuralis_logo1.png"];

    public LatestUpdatesPage()
    {
        InitializeComponent();

        UserSidebarCoordinator.Wire(
            AppSidebar,
            UserSidebarActiveItem.LatestNews,
            onDashboardTap: () => Shell.Current.GoToAsync("//Main"),
            onBookVisitTap: async () => await Navigation.PushAsync(new BookAppointmentPage()),
            onAppointmentsTap: async () => await Navigation.PushAsync(new UserAppointmentsPage()),
            onLatestNewsTap: () => Task.CompletedTask,
            onLogoutTap: () => App.LogoutAsync(),
            closeSidebar: () => GlobalLayout.CloseSidebar());

        NewsGrid.ItemsSource = _gridItems;
        NewsGrid.EmptyView = new ContentView { HeightRequest = 0, IsVisible = false };
        _filterButtons = [FilterAll, FilterAi, FilterClinical, FilterHealth, FilterBio];
        NewsMainColumn.SizeChanged += (_, _) => ApplyResponsiveLayout();
        Loaded += (_, _) => ApplyResponsiveLayout();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        MessagingCenter.Subscribe<App>(this, "AUTH_CHANGED", __ =>
        {
            MainThread.BeginInvokeOnMainThread(UpdateAuthUi);
        });

        UpdateAuthUi();
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        MessagingCenter.Unsubscribe<App>(this, "AUTH_CHANGED");
    }

    private void UpdateAuthUi()
    {
        var user = App.CurrentUser;
        AppSidebar.SetUserDisplayName(
            user == null || string.IsNullOrWhiteSpace(user.FullName)
                ? "User"
                : user.FullName);
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            await LoadAsync();
        }
        finally
        {
            RefreshHost.IsRefreshing = false;
        }
    }

    private async Task LoadAsync()
    {
        LoadingSkeleton.IsVisible = true;
        NewsContentRoot.IsVisible = false;

        try
        {
            var svc = new NewsService();
            var list = await svc.GetPublishedNewsAsync();
            var sorted = list.OrderByDescending(x => x.CreatedAt).ToList();

            _allDisplay.Clear();
            _hasApiArticles = sorted.Count > 0;

            if (_hasApiArticles)
            {
                foreach (var n in sorted)
                    _allDisplay.Add(BuildDisplayItem(n));
            }
            else
            {
                foreach (var p in CreateEditorialPlaceholders())
                    _allDisplay.Add(p);
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            await DisplayAlert("News", ex.Message, "OK");
            _allDisplay.Clear();
            _hasApiArticles = false;
            foreach (var p in CreateEditorialPlaceholders())
                _allDisplay.Add(p);
            ApplyFilter();
        }
        finally
        {
            LoadingSkeleton.IsVisible = false;
            NewsContentRoot.IsVisible = true;
            ApplyResponsiveLayout();
        }
    }

    private static NewsFeedDisplayItem BuildDisplayItem(
        NewsItemDto d,
        NewsCategoryKind? categoryOverride = null,
        bool editorialPlaceholder = false,
        bool filterHint = false)
    {
        // Prefer the server-provided slug when present, then any explicit override, then a hash-based fallback
        // so legacy rows without a category column still get rendered with one of the curated palettes.
        var cat = categoryOverride
                  ?? FromServerCategory(d.Category)
                  ?? Categorize(d);

        // Server-provided cover wins; for bundled resource names use them as-is, fall back to hash rotation.
        var img = !string.IsNullOrWhiteSpace(d.CoverImageUrl) && !d.CoverImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? d.CoverImageUrl
            : HeroImages[Math.Abs(d.Id.GetHashCode()) % HeroImages.Length];

        var cardExcerpt = !string.IsNullOrWhiteSpace(d.Summary)
            ? Excerpt(d.Summary, 160)
            : (!string.IsNullOrWhiteSpace(d.Subtitle) ? Excerpt(d.Subtitle, 160) : Excerpt(d.Content, 140));

        var featuredExcerpt = !string.IsNullOrWhiteSpace(d.Summary)
            ? Excerpt(d.Summary, 380)
            : Excerpt(d.Content, 360);

        return new NewsFeedDisplayItem
        {
            Dto = d,
            Category = cat,
            CategoryLabel = LabelFor(cat),
            CategoryIcon = IconFor(cat),
            HeroImage = img,
            ReadMinutes = EstimateReadMinutes(d.Content),
            ExcerptCard = cardExcerpt,
            ExcerptFeatured = featuredExcerpt,
            IsEditorialPlaceholder = editorialPlaceholder,
            IsFilterHint = filterHint
        };
    }

    /// <summary>Map server slug → client enum. Returns null when the slug is unknown / empty.</summary>
    private static NewsCategoryKind? FromServerCategory(string? slug) => slug?.Trim().ToLowerInvariant() switch
    {
        "ai_research" => NewsCategoryKind.AiResearch,
        "clinical_trials" => NewsCategoryKind.ClinicalTrials,
        "health_tips" => NewsCategoryKind.HealthTips,
        "biotech" => NewsCategoryKind.BioTech,
        _ => null,
    };

    private static List<NewsFeedDisplayItem> CreateEditorialPlaceholders()
    {
        var pairs = new (NewsItemDto Dto, NewsCategoryKind Cat)[]
        {
            (
                new NewsItemDto
                {
                    Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Title = "Neuralis AI achieves 99.4% accuracy in early Alzheimer's detection",
                    Content =
                        "Our multimodal models combine structured clinical data, imaging cues, and natural-language intake to surface early cognitive patterns. This release refines risk scoring with physician-in-the-loop review and is designed as decision support—not a replacement for clinical judgment.",
                    CreatedAt = new DateTime(2024, 5, 24, 14, 0, 0, DateTimeKind.Utc)
                },
                NewsCategoryKind.AiResearch
            ),
            (
                new NewsItemDto
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Title = "Synthetic heart tissue successfully integrated in recent trials",
                    Content =
                        "Researchers report improved biocompatibility using a bioengineered scaffold that encourages native cell growth. Early cohort data suggest stable hemodynamics at 90 days; larger trials are underway.",
                    CreatedAt = new DateTime(2024, 5, 22, 11, 30, 0, DateTimeKind.Utc)
                },
                NewsCategoryKind.BioTech
            ),
            (
                new NewsItemDto
                {
                    Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    Title = "Optimizing circadian rhythms with bio-feedback technology",
                    Content =
                        "Wearable light and activity signals are fused to personalize sleep–wake guidance. Pilot users reported better focus scores when adherence exceeded three weeks.",
                    CreatedAt = new DateTime(2024, 5, 20, 9, 0, 0, DateTimeKind.Utc)
                },
                NewsCategoryKind.HealthTips
            ),
            (
                new NewsItemDto
                {
                    Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    Title = "Phase II trial opens for novel neuroprotective compound",
                    Content =
                        "Sites across three regions are enrolling adults with mild cognitive impairment. Primary endpoints focus on safety and standardized cognitive battery scores at six months.",
                    CreatedAt = new DateTime(2024, 5, 18, 16, 0, 0, DateTimeKind.Utc)
                },
                NewsCategoryKind.ClinicalTrials
            ),
            (
                new NewsItemDto
                {
                    Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                    Title = "LLM-assisted triage cuts intake time without sacrificing quality",
                    Content =
                        "Clinic pilots show structured symptom summaries generated before the visit, helping teams prioritize questionnaires and labs. All outputs remain editable by clinical staff.",
                    CreatedAt = new DateTime(2024, 5, 15, 13, 15, 0, DateTimeKind.Utc)
                },
                NewsCategoryKind.AiResearch
            )
        };

        return pairs.Select(p => BuildDisplayItem(p.Dto, p.Cat, editorialPlaceholder: true)).ToList();
    }

    private static IEnumerable<NewsFeedDisplayItem> CreateFilterHintCards()
    {
        var hintBody =
            "There are no published articles in this category right now. Choose All Updates or another topic to keep browsing the Neuralis feed.";

        var a = new NewsItemDto
        {
            Id = Guid.Parse("f0f0f0f0-f0f0-f0f0-f0f0-f0f0f0f0f0f0"),
            Title = "No stories in this category yet",
            Content = hintBody,
            CreatedAt = DateTime.UtcNow
        };
        var b = new NewsItemDto
        {
            Id = Guid.Parse("f1f1f1f1-f1f1-f1f1-f1f1-f1f1f1f1f1f1"),
            Title = "Try a different filter",
            Content = hintBody + " Featured highlights and new research are added regularly.",
            CreatedAt = DateTime.UtcNow
        };

        yield return BuildDisplayItem(a, NewsCategoryKind.AiResearch, filterHint: true);
        yield return BuildDisplayItem(b, NewsCategoryKind.HealthTips, filterHint: true);
    }

    private static NewsCategoryKind Categorize(NewsItemDto d) =>
        (NewsCategoryKind)(Math.Abs(d.Id.GetHashCode()) % 4);

    private static string LabelFor(NewsCategoryKind k) => k switch
    {
        NewsCategoryKind.AiResearch => "AI RESEARCH",
        NewsCategoryKind.ClinicalTrials => "CLINICAL TRIALS",
        NewsCategoryKind.HealthTips => "HEALTH TIPS",
        NewsCategoryKind.BioTech => "BIO-TECH",
        _ => "UPDATE"
    };

    private static string IconFor(NewsCategoryKind k) => k switch
    {
        NewsCategoryKind.AiResearch => "smart_toy",
        NewsCategoryKind.ClinicalTrials => "science",
        NewsCategoryKind.HealthTips => "lightbulb",
        NewsCategoryKind.BioTech => "medical_services",
        _ => "article"
    };

    private static int EstimateReadMinutes(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 1;
        var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, words / 200);
    }

    private static string Excerpt(string? full, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(full))
            return string.Empty;
        var t = full.Trim().ReplaceLineEndings(" ");
        if (t.Length <= maxLen)
            return t;
        var cut = t[..maxLen];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > 40)
            cut = cut[..lastSpace];
        return cut.TrimEnd() + "…";
    }

    private void ApplyFilter()
    {
        MoreStoriesLabel.Text = "More stories";

        var filtered = _allDisplay.Where(MatchesFilter).OrderByDescending(x => x.Dto.CreatedAt).ToList();

        if (filtered.Count == 0 && _hasApiArticles)
        {
            _featuredItem = null;
            FeaturedCard.IsVisible = false;
            _gridItems.Clear();
            foreach (var hint in CreateFilterHintCards())
                _gridItems.Add(hint);
            MoreStoriesLabel.Text = "Explore other topics";
            MoreStoriesLabel.IsVisible = true;
            NewsGrid.IsVisible = true;
            return;
        }

        // Prefer an admin-marked featured article when present in the filtered slice.
        var serverFeatured = filtered.FirstOrDefault(x => x.Dto.IsFeatured);
        _featuredItem = serverFeatured ?? filtered.FirstOrDefault();
        _gridItems.Clear();
        foreach (var item in filtered.Where(x => !ReferenceEquals(x, _featuredItem)))
            _gridItems.Add(item);

        if (_featuredItem != null)
        {
            FeaturedCard.IsVisible = true;
            FeaturedImage.Source = ImageSource.FromFile(_featuredItem.HeroImage);
            FeaturedCategoryLabel.Text = _featuredItem.CategoryLabel;
            FeaturedTitleLabel.Text = _featuredItem.Title;
            FeaturedBodyLabel.Text = !string.IsNullOrWhiteSpace(_featuredItem.Dto.Subtitle)
                ? _featuredItem.Dto.Subtitle
                : _featuredItem.ExcerptFeatured;
            var author = string.IsNullOrWhiteSpace(_featuredItem.Dto.AuthorName)
                ? "Neuralis"
                : _featuredItem.Dto.AuthorName;
            FeaturedMetaLabel.Text = $"{author} • {_featuredItem.DateDisplay} • {_featuredItem.ReadMinutes} min read";
        }
        else
            FeaturedCard.IsVisible = false;

        MoreStoriesLabel.IsVisible = _gridItems.Count > 0;

        NewsGrid.IsVisible = _gridItems.Count > 0;
    }

    private bool MatchesFilter(NewsFeedDisplayItem x) =>
        _filter switch
        {
            NewsCategoryFilter.All => true,
            NewsCategoryFilter.AiResearch => x.Category == NewsCategoryKind.AiResearch,
            NewsCategoryFilter.ClinicalTrials => x.Category == NewsCategoryKind.ClinicalTrials,
            NewsCategoryFilter.HealthTips => x.Category == NewsCategoryKind.HealthTips,
            NewsCategoryFilter.BioTech => x.Category == NewsCategoryKind.BioTech,
            _ => true
        };

    private void ApplyResponsiveLayout()
    {
        var w = NewsMainColumn.Width;
        if (w <= 0)
            return;

        var stackedFeatured = w < DesktopFeaturedBreakpoint;
        if (stackedFeatured != _stackedFeatured)
        {
            _stackedFeatured = stackedFeatured;
            ConfigureFeaturedGrid(stackedFeatured);
        }

        var gridSpan = w < MobileGridBreakpoint ? 1 : 2;
        NewsGrid.ItemsLayout = new GridItemsLayout(gridSpan, ItemsLayoutOrientation.Vertical)
        {
            HorizontalItemSpacing = 22,
            VerticalItemSpacing = 22
        };
    }

    private void ConfigureFeaturedGrid(bool stacked)
    {
        FeaturedInnerGrid.ColumnDefinitions.Clear();
        FeaturedInnerGrid.RowDefinitions.Clear();

        if (stacked)
        {
            FeaturedInnerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            FeaturedInnerGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            FeaturedInnerGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            FeaturedInnerGrid.SetRow(FeaturedImageHost, 0);
            FeaturedInnerGrid.SetColumn(FeaturedImageHost, 0);
            FeaturedInnerGrid.SetRow(FeaturedTextColumn, 1);
            FeaturedInnerGrid.SetColumn(FeaturedTextColumn, 0);
            FeaturedImageHost.HeightRequest = 220;
        }
        else
        {
            FeaturedInnerGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(3, GridUnitType.Star)));
            FeaturedInnerGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
            FeaturedInnerGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            FeaturedInnerGrid.SetRow(FeaturedImageHost, 0);
            FeaturedInnerGrid.SetColumn(FeaturedImageHost, 0);
            FeaturedInnerGrid.SetRow(FeaturedTextColumn, 0);
            FeaturedInnerGrid.SetColumn(FeaturedTextColumn, 1);
            FeaturedImageHost.HeightRequest = 260;
        }
    }

    private void OnFilterChipClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn)
            return;

        SetActiveFilterButton(btn);

        _filter = btn switch
        {
            _ when ReferenceEquals(btn, FilterAll) => NewsCategoryFilter.All,
            _ when ReferenceEquals(btn, FilterAi) => NewsCategoryFilter.AiResearch,
            _ when ReferenceEquals(btn, FilterClinical) => NewsCategoryFilter.ClinicalTrials,
            _ when ReferenceEquals(btn, FilterHealth) => NewsCategoryFilter.HealthTips,
            _ when ReferenceEquals(btn, FilterBio) => NewsCategoryFilter.BioTech,
            _ => NewsCategoryFilter.All
        };

        ApplyFilter();
    }

    private void SetActiveFilterButton(Button active)
    {
        var inactive = Application.Current?.Resources["FilterChipStyle"] as Style;
        var activeStyle = Application.Current?.Resources["FilterChipActiveStyle"] as Style;
        foreach (var b in _filterButtons)
            b.Style = ReferenceEquals(b, active) ? activeStyle : inactive;
    }

    private async void OnFeaturedCardTapped(object? sender, EventArgs e)
    {
        if (_featuredItem != null)
            await OpenArticleAsync(_featuredItem);
    }

    private async void OnGridNewsCardTapped(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: NewsFeedDisplayItem item })
            await OpenArticleAsync(item);
    }

    private async Task OpenArticleAsync(NewsFeedDisplayItem item)
    {
        // Best-effort view tick for real (non-placeholder) articles.
        if (!item.IsEditorialPlaceholder && !item.IsFilterHint)
        {
            _ = Task.Run(async () =>
            {
                try { await new NewsService().RegisterViewAsync(item.Dto.Id); }
                catch { /* ignore */ }
            });
        }

        var accent = Application.Current?.Resources.TryGetValue("Accent", out var ac) == true && ac is Color acc
            ? acc
            : Color.FromArgb("#2FE3C9");
        var primary = Application.Current?.Resources.TryGetValue("TextPrimary", out var tp) == true && tp is Color pr
            ? pr
            : Colors.White;
        var secondary = Application.Current?.Resources.TryGetValue("TextSecondary", out var ts) == true && ts is Color se
            ? se
            : Color.FromArgb("#9DBBC3");

        var stack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16,
            Children =
            {
                new Label
                {
                    Text = item.CategoryLabel,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 12,
                    TextColor = accent
                },
                new Label
                {
                    Text = item.Title,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 22,
                    LineBreakMode = LineBreakMode.WordWrap,
                    TextColor = primary
                },
                new Label
                {
                    Text = $"{item.DateDisplay} • {item.ReadMinutes} min read",
                    FontSize = 12,
                    TextColor = secondary
                },
                new Label
                {
                    Text = string.IsNullOrWhiteSpace(item.Content) ? "—" : item.Content.Trim(),
                    FontSize = 15,
                    LineBreakMode = LineBreakMode.WordWrap,
                    TextColor = secondary
                }
            }
        };

        var page = new ContentPage
        {
            Title = "Article",
            BackgroundColor = Color.FromArgb("#071A1F"),
            Content = new ScrollView { Content = stack }
        };

        await Navigation.PushAsync(page);
    }
}
