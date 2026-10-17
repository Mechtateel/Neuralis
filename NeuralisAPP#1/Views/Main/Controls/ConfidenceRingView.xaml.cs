namespace NeuralisAPP_1.Views.Main.Controls;

public partial class ConfidenceRingView : ContentView
{
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress),
        typeof(double),
        typeof(ConfidenceRingView),
        0.86d,
        propertyChanged: OnProgressChanged);

    private readonly ConfidenceRingDrawable _drawable = new();

    public ConfidenceRingView()
    {
        InitializeComponent();
        RingCanvas.Drawable = _drawable;
        _drawable.Progress = Progress;
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    private static void OnProgressChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not ConfidenceRingView view)
            return;
        view._drawable.Progress = view.Progress;
        view.RingCanvas.Invalidate();
    }
}
