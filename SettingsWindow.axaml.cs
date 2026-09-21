using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace CustomInvoiceApp;

public partial class SettingsWindow : Window
{
    private readonly InvoiceProfile _original;
    private readonly string _dataFolder;

    // Logo state: a newly chosen file (copied into the data folder on save), or removal
    private string? _newLogoSource;
    private bool _logoRemoved;

    public InvoiceProfile? Result { get; private set; }

    public SettingsWindow() : this(new InvoiceProfile(), AppSettings.DefaultDataFolder) { }

    public SettingsWindow(InvoiceProfile profile, string dataFolder)
    {
        InitializeComponent();
        _original = profile;
        _dataFolder = dataFolder;

        var fonts = FontManager.Current.SystemFonts.Select(f => f.Name)
            .Append(InvoiceProfile.FallbackFont).Distinct().OrderBy(n => n).ToList();
        TitleFontBox.ItemsSource = fonts;
        BodyFontBox.ItemsSource = fonts;
        TitleFontBox.GotFocus += (_, _) => TitleFontBox.IsDropDownOpen = true;
        BodyFontBox.GotFocus += (_, _) => BodyFontBox.IsDropDownOpen = true;

        var p = profile;
        BusinessNameBox.Text = p.BusinessName;
        BusinessDetailsBox.Text = p.BusinessDetails;
        LogoWidthBox.Value = (decimal)p.LogoWidth;
        ShowNameWithLogoBox.IsChecked = p.ShowBusinessNameWithLogo;
        InvoiceTitleBox.Text = p.InvoiceTitle;
        TitleFontBox.Text = p.TitleFont;
        BodyFontBox.Text = p.BodyFont;
        TextColourBox.Text = p.TextColour;
        CurrencyBox.Text = p.CurrencySymbol;
        TaxNameBox.Text = p.TaxName;
        TaxRateBox.Value = p.TaxRatePercent;
        ItemLabelBox.Text = p.ItemLabel;
        UnitsLabelBox.Text = p.UnitsLabel;
        ShowUnitsBox.IsChecked = p.ShowUnitsColumn;
        QuantityLabelBox.Text = p.QuantityLabel;
        PaymentDetailsBox.Text = p.PaymentDetails;
        SignatureBox.IsChecked = p.ShowSignatureLines;
        SmallPrintBox.Text = p.SmallPrint;
        WebsiteBox.Text = p.Website;

        ShowLogo(CurrentLogoPath());
    }

    private string? CurrentLogoPath() =>
        _newLogoSource ?? (_logoRemoved ? null : _original.LogoPath(_dataFolder));

    private void ShowLogo(string? path)
    {
        LogoPreview.Source = null;
        LogoPreviewBorder.IsVisible = false;
        if (path == null || !File.Exists(path))
        {
            LogoText.Text = "No logo — the business name is shown instead";
            return;
        }

        LogoText.Text = Path.GetFileName(path);
        if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            LogoPreview.Source = new Bitmap(path);
            LogoPreviewBorder.IsVisible = true;
        }
        catch (Exception)
        {
            LogoText.Text = Path.GetFileName(path) + " (can't preview this image)";
        }
    }

    private async void ChooseLogo_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a logo",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.svg" } },
            },
        });
        if (files.Count == 0) return;
        _newLogoSource = files[0].Path.LocalPath;
        _logoRemoved = false;
        ShowLogo(_newLogoSource);
    }

    private void RemoveLogo_Click(object? sender, RoutedEventArgs e)
    {
        _newLogoSource = null;
        _logoRemoved = true;
        ShowLogo(null);
    }

    /// <summary>Profile built from the form, or null (with a message shown) if something is invalid.</summary>
    private InvoiceProfile? ReadForm()
    {
        var colour = (TextColourBox.Text ?? "").Trim();
        if (!InvoicePdf.IsValidColour(colour))
        {
            ErrorText.Text = "Text colour must look like #333333.";
            return null;
        }
        ErrorText.Text = "";

        var p = _original.Clone();
        p.BusinessName = BusinessNameBox.Text?.Trim() ?? "";
        p.BusinessDetails = BusinessDetailsBox.Text ?? "";
        p.LogoWidth = (double)(LogoWidthBox.Value ?? 200);
        p.ShowBusinessNameWithLogo = ShowNameWithLogoBox.IsChecked == true;
        p.InvoiceTitle = OrDefault(InvoiceTitleBox.Text, "INVOICE");
        p.TitleFont = OrDefault(TitleFontBox.Text, InvoiceProfile.FallbackFont);
        p.BodyFont = OrDefault(BodyFontBox.Text, InvoiceProfile.FallbackFont);
        p.TextColour = colour;
        p.CurrencySymbol = CurrencyBox.Text?.Trim() ?? "";
        p.TaxName = OrDefault(TaxNameBox.Text, "VAT");
        p.TaxRatePercent = TaxRateBox.Value ?? 0;
        p.ItemLabel = OrDefault(ItemLabelBox.Text, "Item");
        p.UnitsLabel = OrDefault(UnitsLabelBox.Text, "Cases");
        p.ShowUnitsColumn = ShowUnitsBox.IsChecked == true;
        p.QuantityLabel = OrDefault(QuantityLabelBox.Text, "Quantity");
        p.PaymentDetails = PaymentDetailsBox.Text ?? "";
        p.ShowSignatureLines = SignatureBox.IsChecked == true;
        p.SmallPrint = SmallPrintBox.Text ?? "";
        p.Website = WebsiteBox.Text?.Trim() ?? "";
        return p;
    }

    private static string OrDefault(string? text, string fallback) =>
        string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();

    private void Preview_Click(object? sender, RoutedEventArgs e)
    {
        var p = ReadForm();
        if (p == null) return;

        // Sample invoice using the unsaved settings
        var oldRate = LineItem.TaxRate;
        var oldSymbol = Money.Symbol;
        try
        {
            p.Apply();
            var sample = new Address { CustomerName = "Sample Customer Ltd", AddressText = "1 High Street\nSometown\nAB1 2CD" };
            var lines = new List<LineItem>
            {
                new() { Name = "Sample item one", Units = 1, Quantity = 6, PriceExTax = 9.99m },
                new() { Name = "Sample item two", Units = 2, Quantity = 12, PriceExTax = 14.50m },
            };
            var path = Path.Combine(Path.GetTempPath(), "Invoice-Preview.pdf");
            InvoicePdf.Generate(path, new InvoiceData("PREVIEW", DateTime.Today, sample, sample, lines), p, CurrentLogoPath());
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorText.Text = "Couldn't create the preview: " + ex.Message;
        }
        finally
        {
            LineItem.TaxRate = oldRate;
            Money.Symbol = oldSymbol;
        }
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        var p = ReadForm();
        if (p == null) return;

        try
        {
            Directory.CreateDirectory(_dataFolder);
            if (_newLogoSource != null || _logoRemoved)
            {
                // Read the new logo first, in case it's the file we're about to replace
                var logoBytes = _newLogoSource != null ? File.ReadAllBytes(_newLogoSource) : null;
                foreach (var old in Directory.GetFiles(_dataFolder, "logo.*")) File.Delete(old);
                p.LogoFile = null;
                if (logoBytes != null)
                {
                    p.LogoFile = "logo" + Path.GetExtension(_newLogoSource!).ToLowerInvariant();
                    File.WriteAllBytes(Path.Combine(_dataFolder, p.LogoFile), logoBytes);
                }
            }
            p.Save(_dataFolder);
        }
        catch (Exception ex)
        {
            ErrorText.Text = "Couldn't save settings: " + ex.Message;
            return;
        }

        Result = p;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();
}
