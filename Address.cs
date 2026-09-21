using System.Text.Json;
using System.Text.Json.Serialization;

namespace CustomInvoiceApp;

public class Address
{
    public string Id { get; set; } = "";
    public string CustomerName { get; set; } = "";

    /// <summary>Address lines, one per line.</summary>
    public string AddressText { get; set; } = "";

    [JsonIgnore]
    public string[] Lines => AddressText
        .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>"Customer Name - address, on, one, line" — shown in the search dropdowns.</summary>
    [JsonIgnore]
    public string DisplayKey => $"{CustomerName} - {string.Join(", ", Lines)}";

    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(CustomerName) && Lines.Length == 0;

    public bool SameContentAs(Address other) =>
        CustomerName.Trim().Equals(other.CustomerName.Trim(), StringComparison.OrdinalIgnoreCase) &&
        Lines.SequenceEqual(other.Lines, StringComparer.OrdinalIgnoreCase);

    public override string ToString() => DisplayKey;
}

/// <summary>
/// Saved addresses, kept in a single addresses.json file in a folder chosen in the app
/// (can be a shared/synced folder so several computers use the same list).
/// Billing addresses are keyed BILL-0001, BILL-0002…; delivery addresses DEL-0001, DEL-0002…
/// </summary>
public class AddressStore
{
    public const string FileName = "addresses.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public Dictionary<string, Address> BillingAddresses { get; set; } = new();
    public Dictionary<string, Address> DeliveryAddresses { get; set; } = new();

    public static bool ExistsIn(string folder) => File.Exists(Path.Combine(folder, FileName));

    public static AddressStore Load(string folder)
    {
        var path = Path.Combine(folder, FileName);
        if (!File.Exists(path)) return new AddressStore();
        var store = JsonSerializer.Deserialize<AddressStore>(File.ReadAllText(path)) ?? new AddressStore();
        foreach (var (id, a) in store.BillingAddresses.Concat(store.DeliveryAddresses)) a.Id = id;
        return store;
    }

    public void Save(string folder)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, FileName), JsonSerializer.Serialize(this, JsonOptions));
    }

    public Address UpsertBilling(Address a, string? selectedId) => Upsert(BillingAddresses, "BILL", a, selectedId);
    public Address UpsertDelivery(Address a, string? selectedId) => Upsert(DeliveryAddresses, "DEL", a, selectedId);

    /// <summary>
    /// If the address was picked from the saved list and the customer name is unchanged,
    /// the saved record is updated. An identical saved address is reused. Otherwise a new record is added.
    /// </summary>
    private static Address Upsert(Dictionary<string, Address> dict, string prefix, Address a, string? selectedId)
    {
        if (selectedId != null && dict.TryGetValue(selectedId, out var selected) &&
            selected.CustomerName.Trim().Equals(a.CustomerName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            selected.CustomerName = a.CustomerName.Trim();
            selected.AddressText = string.Join("\n", a.Lines);
            return selected;
        }

        var existing = dict.Values.FirstOrDefault(x => x.SameContentAs(a));
        if (existing != null) return existing;

        var next = dict.Keys
            .Select(k => int.TryParse(k.AsSpan(prefix.Length + 1), out var n) ? n : 0)
            .DefaultIfEmpty(0).Max() + 1;
        var added = new Address
        {
            Id = $"{prefix}-{next:0000}",
            CustomerName = a.CustomerName.Trim(),
            AddressText = string.Join("\n", a.Lines),
        };
        dict[added.Id] = added;
        return added;
    }
}
