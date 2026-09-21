using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace CustomInvoiceApp;

public partial class MainWindow : Window
{
    public static readonly StyledProperty<bool> ShowUnitsColumnProperty =
        AvaloniaProperty.Register<MainWindow, bool>(nameof(ShowUnitsColumn), true);

    public bool ShowUnitsColumn
    {
        get => GetValue(ShowUnitsColumnProperty);
        set => SetValue(ShowUnitsColumnProperty, value);
    }

    private readonly ObservableCollection<LineItem> _lines = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private AddressStore _store = new();
    private InvoiceProfile _profile = new();

    // Saved record the current address was picked from, if any
    private string? _billingId;
    private string? _deliveryId;

    public MainWindow()
    {
        InitializeComponent();
        LinesList.ItemsSource = _lines;
        InvoiceNumberBox.Text = DateTime.Now.ToString("yyyyMMdd-HHmm");
        InvoiceDatePicker.SelectedDate = DateTime.Today;

        ReloadProfile();
        ReloadAddresses();
        AddLine();

        BillingSearch.SelectionChanged += (_, _) =>
        {
            if (BillingSearch.SelectedItem is not Address a) return;
            _billingId = a.Id;
            BillingName.Text = a.CustomerName;
            BillingAddress.Text = a.AddressText;
        };
        DeliverySearch.SelectionChanged += (_, _) =>
        {
            if (DeliverySearch.SelectedItem is not Address a) return;
            _deliveryId = a.Id;
            DeliveryName.Text = a.CustomerName;
            DeliveryAddress.Text = a.AddressText;
        };
        // Re-read the file on focus so addresses saved from another computer show up
        BillingSearch.GotFocus += (_, _) => { ReloadAddresses(); BillingSearch.IsDropDownOpen = true; };
        DeliverySearch.GotFocus += (_, _) => { ReloadAddresses(); DeliverySearch.IsDropDownOpen = true; };

        BillingName.TextChanged += (_, _) => SyncDelivery();
        BillingAddress.TextChanged += (_, _) => SyncDelivery();
        SameAsBilling.IsCheckedChanged += (_, _) =>
        {
            var same = SameAsBilling.IsChecked == true;
            DeliverySearch.IsEnabled = DeliveryName.IsEnabled = DeliveryAddress.IsEnabled = !same;
            SyncDelivery();
        };
    }

    // ---------- Invoice settings ----------

    private bool ReloadProfile()
    {
        try
        {
            _profile = InvoiceProfile.Load(_settings.DataFolder);
        }
        catch (Exception ex)
        {
            ShowStatus($"Couldn't read invoice settings from {_settings.DataFolder}: {ex.Message}");
            return false;
        }
        ApplyProfile();
        return true;
    }

    private void ApplyProfile()
    {
        _profile.Apply();
        Title = string.IsNullOrWhiteSpace(_profile.BusinessName)
            ? "Invoice Generator"
            : $"{_profile.BusinessName} — Invoice Generator";
        ItemHeader.Text = _profile.ItemLabel;
        UnitsHeader.Text = _profile.UnitsLabel;
        QuantityHeader.Text = _profile.QuantityLabel;
        PriceHeader.Text = $"{_profile.PriceLabel} (each)";
        TaxHeader.Text = _profile.TaxName;
        TotalHeader.Text = _profile.TotalLabel;
        ShowUnitsColumn = _profile.ShowUnitsColumn;
        foreach (var line in _lines) line.RefreshTotals();
        UpdateTotal();
    }

    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        ReloadProfile(); // pick up changes saved from another computer
        var dialog = new SettingsWindow(_profile, _settings.DataFolder);
        await dialog.ShowDialog(this);
        if (dialog.Result is null) return;
        _profile = dialog.Result;
        ApplyProfile();
    }

    // ---------- Data folder ----------

    private async void ChangeFolder_Click(object? sender, RoutedEventArgs e)
    {
        var start = Directory.Exists(_settings.DataFolder)
            ? await StorageProvider.TryGetFolderFromPathAsync(_settings.DataFolder)
            : null;
        var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the folder for addresses and invoice settings",
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });
        if (picked.Count == 0) return;

        var folder = picked[0].Path.LocalPath;
        try
        {
            // New, empty folder: start it off with what we already have
            if (!AddressStore.ExistsIn(folder)) _store.Save(folder);
            if (!InvoiceProfile.ExistsIn(folder))
            {
                var copy = _profile.Clone();
                var logo = _profile.LogoPath(_settings.DataFolder);
                if (logo != null && File.Exists(logo))
                    File.Copy(logo, Path.Combine(folder, copy.LogoFile!), overwrite: true);
                else
                    copy.LogoFile = null;
                copy.Save(folder);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Couldn't use {folder}: {ex.Message}");
            return;
        }

        _settings.DataFolder = folder;
        _settings.Save();
        _billingId = _deliveryId = null;
        ReloadProfile();
        ReloadAddresses();
    }

    // ---------- Addresses ----------

    private bool ReloadAddresses()
    {
        DataFolderText.Text = _settings.DataFolder;
        try
        {
            _store = AddressStore.Load(_settings.DataFolder);
            ShowStatus(null);
        }
        catch (Exception ex)
        {
            ShowStatus($"Couldn't read saved addresses from {_settings.DataFolder}: {ex.Message}");
            return false;
        }
        RefreshAddressLists();
        return true;
    }

    private void RefreshAddressLists()
    {
        BillingSearch.ItemsSource = _store.BillingAddresses.Values.OrderBy(a => a.DisplayKey).ToList();
        DeliverySearch.ItemsSource = _store.DeliveryAddresses.Values.OrderBy(a => a.DisplayKey).ToList();
    }

    private void SyncDelivery()
    {
        if (SameAsBilling.IsChecked != true) return;
        _deliveryId = null;
        DeliveryName.Text = BillingName.Text;
        DeliveryAddress.Text = BillingAddress.Text;
    }

    private void ShowStatus(string? message)
    {
        StatusText.Text = message;
        StatusText.IsVisible = message != null;
    }

    // ---------- Line items ----------

    private void AddLine()
    {
        var line = new LineItem();
        line.PropertyChanged += (_, _) => UpdateTotal();
        _lines.Add(line);
        UpdateTotal();
    }

    private void UpdateTotal() =>
        GrandTotalText.Text = $"{_profile.TotalLabel}: " + Money.Format(_lines.Sum(l => l.TotalIncTax));

    private void AddLine_Click(object? sender, RoutedEventArgs e) => AddLine();

    private void RemoveLine_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LineItem line })
        {
            _lines.Remove(line);
            UpdateTotal();
        }
    }

    // ---------- Generate ----------

    private async void Generate_Click(object? sender, RoutedEventArgs e)
    {
        var lines = _lines.Where(l => !string.IsNullOrWhiteSpace(l.Name)).ToList();
        if (lines.Count == 0)
        {
            GrandTotalText.Text = "Add at least one line with an item name.";
            return;
        }

        var billing = new Address { CustomerName = BillingName.Text ?? "", AddressText = BillingAddress.Text ?? "" };
        var delivery = new Address { CustomerName = DeliveryName.Text ?? "", AddressText = DeliveryAddress.Text ?? "" };

        var number = string.IsNullOrWhiteSpace(InvoiceNumberBox.Text) ? "1" : InvoiceNumberBox.Text.Trim();
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save invoice",
            SuggestedFileName = $"Invoice-{number}.pdf",
            DefaultExtension = "pdf",
            FileTypeChoices = new[] { new FilePickerFileType("PDF") { Patterns = new[] { "*.pdf" } } },
        });
        if (file is null) return;

        // Remember the addresses for next time (re-read first so changes from other computers aren't lost)
        if (ReloadAddresses())
        {
            try
            {
                if (!billing.IsEmpty) _billingId = _store.UpsertBilling(billing, _billingId).Id;
                if (!delivery.IsEmpty) _deliveryId = _store.UpsertDelivery(delivery, _deliveryId).Id;
                _store.Save(_settings.DataFolder);
                RefreshAddressLists();
            }
            catch (Exception ex)
            {
                ShowStatus($"Invoice created, but addresses couldn't be saved to {_settings.DataFolder}: {ex.Message}");
            }
        }

        var path = file.Path.LocalPath;
        try
        {
            InvoicePdf.Generate(path, new InvoiceData(
                    number, InvoiceDatePicker.SelectedDate ?? DateTime.Today, billing, delivery, lines),
                _profile, _profile.LogoPath(_settings.DataFolder));
        }
        catch (Exception ex)
        {
            ShowStatus($"Couldn't create the invoice: {ex.Message}");
            return;
        }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
