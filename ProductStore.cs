using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomInvoiceApp;

public class Product
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Last price used, per item, excluding tax.</summary>
    public decimal Price { get; set; }

    [JsonIgnore] public string PriceDisplay => Money.Format(Price);

    // The product search box matches and fills in on this
    public override string ToString() => Name;
}

/// <summary>
/// Products remembered from past invoices, kept in products.json in the data folder
/// alongside the addresses. Keyed PROD-0001, PROD-0002…
/// </summary>
public class ProductStore
{
    public const string FileName = "products.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public Dictionary<string, Product> Products { get; set; } = new();

    public static bool ExistsIn(string folder) => File.Exists(Path.Combine(folder, FileName));

    public static ProductStore Load(string folder)
    {
        var path = Path.Combine(folder, FileName);
        if (!File.Exists(path)) return new ProductStore();
        var store = JsonSerializer.Deserialize<ProductStore>(File.ReadAllText(path)) ?? new ProductStore();
        foreach (var (id, p) in store.Products) p.Id = id;
        return store;
    }

    public void Save(string folder)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, FileName), JsonSerializer.Serialize(this, JsonOptions));
    }

    /// <summary>Add a product, or update the price of the one with the same name.</summary>
    public void Upsert(string name, decimal price)
    {
        name = name.Trim();
        if (name.Length == 0) return;

        var existing = Products.Values.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Price = price;
            return;
        }

        var next = Products.Keys
            .Select(k => int.TryParse(k.AsSpan("PROD-".Length), out var n) ? n : 0)
            .DefaultIfEmpty(0).Max() + 1;
        var id = $"PROD-{next:0000}";
        Products[id] = new Product { Id = id, Name = name, Price = price };
    }
}
