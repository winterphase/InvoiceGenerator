# Invoice Generator

A simple desktop app for creating PDF invoices. You add line items, pick or type the customer's billing and delivery addresses, and click **Generate** to get a finished PDF.

It works on **Windows** and **macOS**. You can change everything on the invoice from inside the app, including your logo, business details, fonts, currency, tax rate, column names, payment details and small print. You don't need to edit any code.

## Features

- Line items with a name, an optional grouping column (e.g. *Cases*), a quantity (e.g. *Bottles*) and a price excluding tax. The tax and the total including tax are worked out for you.
- Tax is rounded to the penny on each line, so the line figures always add up to the invoice total.
- Products are remembered too. Start typing an item name to pick a product you've used before, and its last price is filled in.
- Billing and delivery address boxes. Addresses are saved automatically and can be searched by customer name or address, and a **Delivery Address is same as Billing Address** checkbox copies the billing address across.
- A **Settings** window with a **Preview invoice** button, so you can see changes before saving them.
- Addresses and settings are saved in a folder you choose. Point several computers at the same shared folder (OneDrive, Dropbox, a network drive…) and they all use the same address list and invoice design.
- Invoices that run over one page are handled, with page numbers in the footer.

## Download and run

Download the latest version from the [**Releases**](../../releases) page. The app is a single file, and you don't need to install anything else, including .NET.

### Windows

1. Download `InvoiceGenerator.exe` and put it wherever you like, e.g. your Desktop.
2. Double-click it.
3. The first time, Windows may show **"Windows protected your PC"**, because the app isn't code-signed. Click **More info → Run anyway**.

### macOS (Apple Silicon: M1 and later)

1. Download `InvoiceGenerator`.
2. The first time, **right-click it → Open**, then click **Open** again. macOS blocks unsigned apps on a plain double-click.
3. If macOS says it can't be opened because it's not executable, open Terminal in the download folder and run:
   ```bash
   chmod +x InvoiceGenerator
   ```

## First-time setup

1. Click **⚙ Settings…** at the top of the window.
2. **Business** tab: enter your business name, choose your logo (PNG, JPG or SVG; SVG prints sharpest) and fill in your address, phone number, VAT number and so on.
3. **Look & Money** tab:
   - Set the invoice title and pick fonts from those installed on your computer.
   - Choose the currency symbol and set the tax name and rate (e.g. VAT at 20%).
   - Rename the columns if you like, or hide the grouping column.
4. **Payment & Footer** tab: add your bank details. Lines written as `Label: value` (e.g. `Sort Code: 12-34-56`) line up with a bold label. You can also add any small print, e.g. company registration details, and your website.
5. Click **Preview invoice** to check how it looks, then **Save**.

## Making an invoice

1. Check the **invoice number** and **date** at the top. The number is filled in from the current date and time, and you can change it.
2. Fill in the **Billing Address**. Start typing in the search box to pick a saved customer, or type a new one.
3. Fill in the **Delivery Address**, or tick **Delivery Address is same as Billing Address**.
4. Add your line items. Start typing an item name to see matching saved products with their last price, and pick one to fill in the name and price. Use **+ Add line** for more rows and **✕** to remove one.
5. Click **Generate**, choose where to save the PDF, and it opens automatically.

Any new or changed addresses are saved when you click **Generate**. So are the products on the invoice, with their prices; a product that's already saved gets its price updated.

## Where your data is kept

Everything is stored in the **Data folder** shown at the top of the app. By default that's `Documents/Invoice Generator`. Click **Change folder…** to use a different one.

| File | What it holds |
|---|---|
| `addresses.json` | Saved billing addresses (keys `BILL-0001`, `BILL-0002`, …) and delivery addresses (`DEL-0001`, …) |
| `products.json` | Saved products and their latest price excluding tax (keys `PROD-0001`, …) |
| `invoice-settings.json` | Everything from the Settings window |
| `logo.png` / `logo.svg` / … | A copy of your logo |

These are plain text files (apart from the logo). You can back them up or copy them to another computer, and you can edit `addresses.json` or `products.json` in any text editor to fix or delete an entry.

### Sharing between computers

1. Put the data folder somewhere every computer can reach, e.g. a OneDrive, Dropbox or Google Drive folder, or a network share.
2. On each computer, click **Change folder…** and pick that folder.

If you switch to an empty folder, your current addresses and settings are copied into it, so nothing is lost. The app re-reads the files before saving, so addresses and products added on different computers don't overwrite each other. Try to avoid two people saving at the exact same moment, though.

## Fonts

The app can use any font installed on the computer it's running on. If a computer doesn't have the chosen font, invoices created there use **Lato** instead. The PDFs themselves always contain the fonts they use, so they look right for whoever opens them.

If you use fonts from a subscription service such as Adobe Fonts, each computer that makes invoices needs those fonts activated.

## Building from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer.

Run it:

```bash
dotnet run
```

Build single-file apps for Windows and macOS. They end up in `publish/win-x64` and `publish/osx-arm64`:

```bash
for rid in win-x64 osx-arm64; do dotnet publish CustomInvoiceApp.csproj -c Release -r $rid --self-contained -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=none -o publish/$rid; done
```

On Windows (PowerShell):

```powershell
foreach ($rid in "win-x64","osx-arm64") { dotnet publish CustomInvoiceApp.csproj -c Release -r $rid --self-contained -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:DebugType=none -o publish/$rid }
```

For Intel Macs, use `osx-x64`. For Linux, use `linux-x64`.

### Project layout

| File | Purpose |
|---|---|
| `MainWindow.axaml(.cs)` | Main screen: addresses, line items, Generate |
| `SettingsWindow.axaml(.cs)` | Settings window |
| `InvoicePdf.cs` | Lays out and writes the PDF |
| `InvoiceProfile.cs` | The invoice settings and how they're saved |
| `Address.cs` | Addresses and the saved address list |
| `ProductStore.cs` | Saved products and their prices |
| `LineItem.cs` | A line on the invoice and its tax/total maths |
| `AppSettings.cs` | Per-computer setting: which data folder to use |

Built with [Avalonia UI](https://avaloniaui.net/) for the interface and [QuestPDF](https://www.questpdf.com/) for the PDFs.

## Licence

This project is released under the [MIT licence](LICENSE). You're free to use, change and share it.

It depends on third-party libraries with their own licences. In particular, QuestPDF is free under its [Community licence](https://www.questpdf.com/license/) for individuals and for businesses with under $1M USD in annual gross revenue. Larger businesses need a commercial QuestPDF licence to use this app.
