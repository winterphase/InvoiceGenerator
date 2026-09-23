using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomInvoiceApp;

/// <summary>
/// Everything about how the invoice looks, edited in the Settings window.
/// Saved as invoice-settings.json in the data folder (with the logo alongside it),
/// so computers sharing a data folder also share the invoice design.
/// </summary>
public class InvoiceProfile
{
    public const string FileName = "invoice-settings.json";
    public const string FallbackFont = "Lato";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // Business
    public string BusinessName { get; set; } = "Your Business Name";
    public string BusinessDetails { get; set; } = "Address line 1\nTown\nPostcode\nTel: \nEmail: \nVAT Reg No: ";
    /// <summary>File name of the logo inside the data folder, or null for no logo.</summary>
    public string? LogoFile { get; set; }
    public double LogoWidth { get; set; } = 200;
    public bool ShowBusinessNameWithLogo { get; set; }

    // Look
    public string InvoiceTitle { get; set; } = "INVOICE";
    public string TitleFont { get; set; } = FallbackFont;
    public string BodyFont { get; set; } = FallbackFont;
    public string TextColour { get; set; } = "#333333";

    // Money
    public string CurrencySymbol { get; set; } = "£";
    public string TaxName { get; set; } = "VAT";
    public decimal TaxRatePercent { get; set; } = 20;

    // Line item columns
    public string ItemLabel { get; set; } = "Item";
    public string UnitsLabel { get; set; } = "Cases";
    public bool ShowUnitsColumn { get; set; } = true;
    public string QuantityLabel { get; set; } = "Bottles";

    // Bottom of the invoice
    public string PaymentDetails { get; set; } = "Bank: \nAccount No: \nSort Code: \nAccount Name: ";
    public bool ShowSignatureLines { get; set; } = true;
    public string SmallPrint { get; set; } = "";
    public string Website { get; set; } = "";

    [JsonIgnore] public string PriceLabel => $"Price ex {TaxName}";
    [JsonIgnore] public string TotalLabel => $"Total inc {TaxName}";

    public static string[] SplitLines(string? text) =>
        (text ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public string? LogoPath(string folder) => string.IsNullOrEmpty(LogoFile) ? null : Path.Combine(folder, LogoFile);

    public static bool ExistsIn(string folder) => File.Exists(Path.Combine(folder, FileName));

    public static InvoiceProfile Load(string folder)
    {
        var path = Path.Combine(folder, FileName);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<InvoiceProfile>(File.ReadAllText(path)) ?? new InvoiceProfile()
            : new InvoiceProfile();
    }

    public void Save(string folder)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, FileName), JsonSerializer.Serialize(this, JsonOptions));
    }

    public InvoiceProfile Clone() =>
        JsonSerializer.Deserialize<InvoiceProfile>(JsonSerializer.Serialize(this, JsonOptions))!;

    /// <summary>Push the money settings to the calculation code.</summary>
    public void Apply()
    {
        LineItem.TaxRate = TaxRatePercent / 100m;
        Money.Symbol = CurrencySymbol;
    }
}
