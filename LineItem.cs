using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CustomInvoiceApp;

public class LineItem : INotifyPropertyChanged
{
    /// <summary>Tax rate as a fraction (0.20 = 20%). Set from the invoice settings.</summary>
    public static decimal TaxRate { get; set; } = 0.20m;

    private string _name = "";
    private decimal? _units = 0;
    private decimal? _quantity = 0;
    private decimal? _priceExTax = 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get => _name; set => Set(ref _name, value); }

    /// <summary>Informational grouping column, e.g. cases. Not used in pricing.</summary>
    public decimal? Units { get => _units; set => Set(ref _units, value); }

    public decimal? Quantity { get => _quantity; set => Set(ref _quantity, value); }

    /// <summary>Price per item (per quantity), excluding tax.</summary>
    public decimal? PriceExTax { get => _priceExTax; set => Set(ref _priceExTax, value); }

    // Rounded to the penny per line, so the line tax figures add up to the invoice tax total
    public decimal TotalExTax => Math.Round((PriceExTax ?? 0) * (Quantity ?? 0), 2, MidpointRounding.AwayFromZero);
    public decimal Tax => Math.Round(TotalExTax * TaxRate, 2, MidpointRounding.AwayFromZero);
    public decimal TotalIncTax => TotalExTax + Tax;
    public string TaxDisplay => Money.Format(Tax);
    public string TotalIncTaxDisplay => Money.Format(TotalIncTax);

    /// <summary>Re-announce the calculated values, e.g. after the tax rate or currency changes.</summary>
    public void RefreshTotals()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TaxDisplay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalIncTaxDisplay)));
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (name is nameof(Quantity) or nameof(PriceExTax)) RefreshTotals();
    }
}

public static class Money
{
    private static readonly System.Globalization.CultureInfo Gb = new("en-GB");

    /// <summary>Set from the invoice settings.</summary>
    public static string Symbol { get; set; } = "£";

    public static string Format(decimal amount) =>
        (amount < 0 ? "-" : "") + Symbol + Math.Abs(amount).ToString("N2", Gb);
}
