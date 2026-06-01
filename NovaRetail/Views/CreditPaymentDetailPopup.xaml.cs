namespace NovaRetail.Views;

public partial class CreditPaymentDetailPopup : ContentView
{
    private const double LargePopupWidth = 1150;
    private const double LargePopupHeight = 820;
    private const double CompactPopupWidth = 1080;
    private const double CompactPopupHeight = 780;
    private const double LargePopupMinWidth = 1320;
    private const double LargePopupMinHeight = 760;

    public CreditPaymentDetailPopup()
    {
        InitializeComponent();
        SizeChanged += OnPopupSizeChanged;
    }

    private void OnPopupSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        var useLargePopup = Width >= LargePopupMinWidth && Height >= LargePopupMinHeight;
        var availableWidth = Math.Max(760, Width - 48);

        MainPopup.MaximumWidthRequest = useLargePopup ? LargePopupWidth : CompactPopupWidth;
        MainPopup.WidthRequest = Math.Min(MainPopup.MaximumWidthRequest, availableWidth);
        MainPopup.HeightRequest = useLargePopup ? LargePopupHeight : CompactPopupHeight;
    }
}
