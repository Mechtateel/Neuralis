namespace NeuralisAPP_1.Views.Shared;

public partial class MainLayout : ContentView
{
    private const double MobileBreakpoint = 900;
    private const double SidebarWidth = 280;
    private const uint SidebarAnimationMs = 180;

    private bool _isMobileMode;
    private bool _isSidebarOpen;
    private bool _isSidebarAnimating;

    public static readonly BindableProperty SidebarContentProperty =
        BindableProperty.Create(
            nameof(SidebarContent),
            typeof(View),
            typeof(MainLayout),
            default(View),
            propertyChanged: OnSidebarContentChanged);

    public static readonly BindableProperty PageContentProperty =
        BindableProperty.Create(
            nameof(PageContent),
            typeof(View),
            typeof(MainLayout),
            default(View),
            propertyChanged: OnPageContentChanged);

    public View? SidebarContent
    {
        get => (View?)GetValue(SidebarContentProperty);
        set => SetValue(SidebarContentProperty, value);
    }

    public View? PageContent
    {
        get => (View?)GetValue(PageContentProperty);
        set => SetValue(PageContentProperty, value);
    }

    public MainLayout()
    {
        InitializeComponent();
        PageContentPresenter.Content = PageContent;
        SidebarContentPresenter.Content = SidebarContent;
        Loaded += (_, _) => ApplyResponsiveMode();
        SizeChanged += (_, _) => ApplyResponsiveMode();
        Unloaded += (_, _) => DeviceDisplay.MainDisplayInfoChanged -= OnMainDisplayInfoChanged;
        DeviceDisplay.MainDisplayInfoChanged += OnMainDisplayInfoChanged;
    }

    private static void OnPageContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MainLayout layout)
            layout.PageContentPresenter.Content = newValue as View;
    }

    private static void OnSidebarContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MainLayout layout)
            layout.SidebarContentPresenter.Content = newValue as View;
    }

    public bool IsSidebarOpen
    {
        get => _isSidebarOpen;
        set
        {
            if (_isSidebarOpen == value)
                return;

            _ = SetSidebarOpenAsync(value, animated: _isMobileMode);
        }
    }

    public void CloseSidebar() => _ = SetSidebarOpenAsync(false, animated: _isMobileMode);

    private void ApplyResponsiveMode()
    {
        var width = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        if (width <= 0 && Width > 0)
            width = Width;

        if (width <= 0)
            return;

        _isMobileMode = width < MobileBreakpoint;
        HamburgerHost.IsVisible = _isMobileMode;

        if (_isMobileMode)
        {
            PageContentHost.Margin = 0;
            PageContentHost.Padding = new Thickness(12);
            OverlayLayer.IsVisible = _isSidebarOpen;
            SidebarLayer.TranslationX = _isSidebarOpen ? 0 : -SidebarWidth;
        }
        else
        {
            _isSidebarOpen = false;
            OverlayLayer.IsVisible = false;
            PageContentHost.Margin = new Thickness(250, 0, 0, 0);
            PageContentHost.Padding = new Thickness(24);
            SidebarLayer.TranslationX = 0;
        }
    }

    private async void OnHamburgerClicked(object sender, EventArgs e)
    {
        await SetSidebarOpenAsync(!_isSidebarOpen, animated: true);
    }

    private async void OnOverlayTapped(object sender, EventArgs e)
    {
        await SetSidebarOpenAsync(false, animated: true);
    }

    private void OnMainDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
    {
        ApplyResponsiveMode();
    }

    private async Task SetSidebarOpenAsync(bool open, bool animated)
    {
        if (!_isMobileMode)
        {
            _isSidebarOpen = false;
            OverlayLayer.IsVisible = false;
            SidebarLayer.TranslationX = 0;
            return;
        }

        if (_isSidebarAnimating)
            return;

        _isSidebarAnimating = true;
        try
        {
            _isSidebarOpen = open;

            if (open)
            {
                OverlayLayer.IsVisible = true;
                if (animated)
                    await SidebarLayer.TranslateTo(0, 0, SidebarAnimationMs, Easing.CubicOut);
                else
                    SidebarLayer.TranslationX = 0;
            }
            else
            {
                if (animated)
                    await SidebarLayer.TranslateTo(-SidebarWidth, 0, SidebarAnimationMs, Easing.CubicIn);
                else
                    SidebarLayer.TranslationX = -SidebarWidth;

                OverlayLayer.IsVisible = false;
            }
        }
        finally
        {
            _isSidebarAnimating = false;
        }
    }
}
