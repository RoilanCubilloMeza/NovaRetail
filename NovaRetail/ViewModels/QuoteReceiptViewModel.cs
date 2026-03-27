using NovaRetail.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

namespace NovaRetail.ViewModels
{
    /// <summary>
    /// ViewModel del comprobante de cotización.
    /// Prepara la información mostrada al usuario después de guardar una cotización
    /// o una factura en espera, incluyendo encabezado, líneas y totales.
    /// </summary>
    public sealed class QuoteReceiptViewModel : INotifyPropertyChanged
    {
        public event Action? RequestClose;
        public ICommand CloseCommand { get; }
        public ICommand PrintCommand  { get; }
        public ICommand SaveCommand   { get; }
        public ICommand EditCashierCommand { get; }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(); } }
        }

        // ── Cajero editable ─────────────────────────────────────────
        private string _cashierName = string.Empty;
        public string CashierName
        {
            get => _cashierName;
            set
            {
                if (_cashierName != value)
                {
                    _cashierName = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isEditingCashier;
        public bool IsEditingCashier
        {
            get => _isEditingCashier;
            private set
            {
                if (_isEditingCashier != value)
                {
                    _isEditingCashier = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsNotEditingCashier));
                }
            }
        }
        public bool IsNotEditingCashier => !_isEditingCashier;

        // ── Datos del encabezado ─────────────────────────────────────
        public int    OrderID           { get; private set; }
        public string OrderIDText       => $"{OrderID}";
        public string OrderDate         { get; private set; } = string.Empty;

        public string StoreName         { get; private set; } = string.Empty;
        public string StoreAddress      { get; private set; } = string.Empty;
        public string StorePhone        { get; private set; } = string.Empty;
        public bool   HasStoreInfo      => !string.IsNullOrWhiteSpace(StoreName);

        public string ClientId          { get; private set; } = string.Empty;
        public string ClientName        { get; private set; } = string.Empty;
        public bool   HasClient         => !string.IsNullOrWhiteSpace(ClientId) && ClientId != "S-00001";
        public int    RegisterNumber    { get; private set; } = 1;
        public string RegisterText      => $"Reg.POS #: {RegisterNumber}";

        // ── Ítems ────────────────────────────────────────────────────
        public ObservableCollection<ReceiptLineItem> Items { get; } = new();

        // ── Totales ──────────────────────────────────────────────────
        public string SubtotalText      { get; private set; } = string.Empty;
        public string TaxText           { get; private set; } = string.Empty;
        public string TotalText         { get; private set; } = string.Empty;
        public string TotalColonesText  { get; private set; } = string.Empty;
        public bool   HasDiscount       { get; private set; }
        public string DiscountText      { get; private set; } = string.Empty;
        public bool   HasExoneration    { get; private set; }
        public string ExonerationText   { get; private set; } = string.Empty;

        // Vence
        public string ExpirationText    { get; private set; } = string.Empty;
        public bool   HasExpiration     => !string.IsNullOrWhiteSpace(ExpirationText);

        public QuoteReceiptViewModel()
        {
            CloseCommand       = new Command(() => RequestClose?.Invoke());
            PrintCommand       = new Command(async () => await PrintAsync());
            SaveCommand        = new Command(async () => await SaveAsync());
            EditCashierCommand = new Command(() => IsEditingCashier = !IsEditingCashier);
        }

        public void Load(
            int orderID,
            DateTime? expirationDate,
            string clientId,
            string clientName,
            string cashierName,
            int registerNumber,
            string storeName,
            string storeAddress,
            string storePhone,
            IEnumerable<CartItemModel> cartItems,
            string subtotalText,
            string taxText,
            bool hasDiscount,
            string discountText,
            bool hasExoneration,
            string exonerationText,
            string totalText,
            string totalColonesText)
        {
            OrderID        = orderID;
            OrderDate      = DateTime.Now.ToString("M/d/yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
            ClientId       = string.IsNullOrWhiteSpace(clientId) ? "S-00001" : clientId;
            ClientName     = string.IsNullOrWhiteSpace(clientName) ? "CLIENTE CONTADO" : clientName;
            CashierName    = string.IsNullOrWhiteSpace(cashierName) ? "—" : cashierName;
            IsEditingCashier = false;
            RegisterNumber = registerNumber > 0 ? registerNumber : 1;
            StoreName      = storeName  ?? string.Empty;
            StoreAddress   = storeAddress ?? string.Empty;
            StorePhone     = storePhone  ?? string.Empty;
            ExpirationText = expirationDate.HasValue
                ? expirationDate.Value.ToString("dd/MM/yyyy")
                : string.Empty;

            SubtotalText     = subtotalText;
            TaxText          = taxText;
            HasDiscount      = hasDiscount;
            DiscountText     = discountText;
            HasExoneration   = hasExoneration;
            ExonerationText  = exonerationText;
            TotalText        = totalText;
            TotalColonesText = totalColonesText;

            Items.Clear();
            foreach (var item in cartItems)
            {
                var gross      = item.EffectivePriceColones;
                var discount   = 1m - item.DiscountPercent / 100m;
                var netUnit    = Math.Round(gross * discount, 2);
                var netLine    = Math.Round(gross * item.Quantity * discount, 2);

                Items.Add(new ReceiptLineItem
                {
                    DisplayName           = item.DisplayName,
                    Code                  = item.Code ?? string.Empty,
                    Quantity              = item.Quantity,
                    TaxPercentage         = item.TaxPercentage,
                    UnitPriceColonesText  = $"₡{(item.HasDiscount ? netUnit : gross):N2}",
                    LineTotalText         = $"₡{(item.HasDiscount ? netLine : gross * item.Quantity):N2}",
                    HasOverridePrice      = item.HasDownwardPriceOverride,
                    PriceChangeDetailText = item.HasDownwardPriceOverride
                        ? $"Cambio precio: de ₡{item.UnitPriceColones:N2} a ₡{gross:N2}"
                        : string.Empty,
                    HasDiscount           = item.HasDiscount,
                    DiscountDetailText    = item.HasDiscount
                        ? $"Desc. {item.DiscountPercent:0.##}%: de ₡{gross:N2} a ₡{netUnit:N2}"
                        : string.Empty,
                    HasExoneration        = item.HasExoneration,
                    ExonerationDetailText = item.HasExoneration
                        ? $"Exoneración: {item.ExonerationText}"
                        : string.Empty
                });
            }

            OnPropertyChanged(string.Empty);
        }

        // ── Print / Save ─────────────────────────────────────────────

        private async Task PrintAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var path = Path.Combine(FileSystem.CacheDirectory, $"Proforma-{OrderID}.html");
                File.WriteAllText(path, BuildHtml(), Encoding.UTF8);
                await Launcher.OpenAsync(new OpenFileRequest
                {
                    Title = $"Imprimir Proforma #{OrderID}",
                    File  = new ReadOnlyFile(path, "text/html")
                });
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    Application.Current?.MainPage?.DisplayAlert("Error al imprimir", ex.Message, "OK") ?? Task.CompletedTask);
            }
            finally { IsBusy = false; }
        }

        private async Task SaveAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var path = Path.Combine(FileSystem.CacheDirectory, $"Proforma-{OrderID}.html");
                File.WriteAllText(path, BuildHtml(false), Encoding.UTF8);
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = $"Guardar Proforma #{OrderID}",
                    File  = new ShareFile(path, "text/html")
                });
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    Application.Current?.MainPage?.DisplayAlert("Error al guardar", ex.Message, "OK") ?? Task.CompletedTask);
            }
            finally { IsBusy = false; }
        }

        // ── HTML Builder ─────────────────────────────────────────────

        private string BuildHtml(bool autoPrint = true)
        {
            var rows = new StringBuilder();
            foreach (var item in Items)
            {
                rows.Append("<tr class='item-row'>")
                    .Append("<td class='desc-cell'><div class='item-name'>").Append(Esc(item.DisplayName)).Append("</div>");

                var details = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(item.Code))
                    details.Append($"<div class='item-detail'>{Esc(item.Code)}</div>");
                if (item.HasTax)
                    details.Append($"<div class='item-detail tax-rate'>{item.TaxRateText}</div>");
                if (item.HasOverridePrice)
                    details.Append($"<div class='item-detail override'>{Esc(item.PriceChangeDetailText)}</div>");
                if (item.HasDiscount)
                    details.Append($"<div class='item-detail disc'>{Esc(item.DiscountDetailText)}</div>");
                if (item.HasExoneration)
                    details.Append($"<div class='item-detail exon'>{Esc(item.ExonerationDetailText)}</div>");
                rows.Append(details).Append("</td>")
                    .Append($"<td class='qty-cell'>{item.QuantityText}</td>")
                    .Append($"<td class='price-cell'>{Esc(item.UnitPriceColonesText)}</td>")
                    .Append($"<td class='total-cell'>{Esc(item.LineTotalText)}</td>")
                    .Append("</tr>");
            }

            var printScript = autoPrint ? "<script>window.onload=()=>window.print();</script>" : "";

            return $@"<!DOCTYPE html>
<html lang='es'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width,initial-scale=1'>
<title>Proforma #{OrderID}</title>
{printScript}
<style>
  body{{font-family:'Courier New',monospace;max-width:400px;margin:0 auto;padding:16px;font-size:12px;color:#111;}}
  h1{{text-align:center;font-size:16px;margin:4px 0;}}
  h2{{text-align:center;font-size:13px;margin:2px 0;color:#374151;}}
  .store-info{{text-align:center;margin-bottom:8px;font-size:11px;}}
  .proforma-badge{{text-align:center;border:2px solid #111;padding:4px 16px;display:inline-block;font-weight:bold;font-size:14px;margin:8px auto;}}
  .proforma-wrap{{text-align:center;margin:6px 0;}}
  .header-table{{width:100%;border-collapse:collapse;margin:8px 0;font-size:11px;}}
  .header-table td{{padding:1px 2px;}}
  .sep{{border:none;border-top:1px solid #333;margin:6px 0;}}
  table.items{{width:100%;border-collapse:collapse;font-size:11px;}}
  table.items th{{text-align:left;border-top:1px solid #333;border-bottom:1px solid #333;padding:2px;}}
  table.items th.r{{text-align:right;}}
  .item-row td{{padding:2px;vertical-align:top;}}
  .item-name{{font-weight:bold;}}
  .item-detail{{color:#555;font-size:10px;}}
  .tax-rate{{color:#2563eb;}}
  .disc{{color:#dc2626;}}
  .exon{{color:#16a34a;}}
  .override{{color:#7c3aed;}}
  .qty-cell{{text-align:right;white-space:nowrap;}}
  .price-cell{{text-align:right;white-space:nowrap;}}
  .total-cell{{text-align:right;white-space:nowrap;font-weight:bold;}}
  .totals{{width:100%;border-collapse:collapse;font-size:12px;margin-top:4px;}}
  .totals td{{padding:2px;}}
  .totals td.r{{text-align:right;}}
  .totals tr.total-row td{{font-weight:bold;border-top:1px solid #333;}}
  .footer{{text-align:center;font-size:10px;margin-top:12px;color:#555;}}
  .legal{{text-align:center;font-size:9px;color:#888;margin-top:8px;border-top:1px solid #ccc;padding-top:6px;}}
</style>
</head>
<body>
<h1>Recibo Duplicado</h1>
{(HasStoreInfo ? $"<div class='store-info'><strong>{Esc(StoreName)}</strong><br>{Esc(StoreAddress)}<br>{Esc(StorePhone)}</div>" : "")}
<hr class='sep'>
<div class='proforma-wrap'><span class='proforma-badge'>Proforma</span></div>
<hr class='sep'>
<table class='header-table'>
  <tr><td><b>Doc. Interno #:</b></td><td>{OrderID}</td></tr>
  <tr><td><b>Cédula:</b></td><td>{Esc(ClientId)}</td></tr>
  <tr><td><b>Nombre:</b></td><td>{Esc(ClientName)}</td></tr>
  <tr><td><b>Fecha/Hora:</b></td><td>{OrderDate}</td></tr>
  <tr><td><b>Cajero:</b></td><td>{Esc(CashierName)}&nbsp;&nbsp;&nbsp;{Esc(RegisterText)}</td></tr>
  {(HasExpiration ? $"<tr><td><b>Vence:</b></td><td>{Esc(ExpirationText)}</td></tr>" : "")}
</table>
<hr class='sep'>
<table class='items'>
  <thead>
    <tr>
      <th>DESC/COD</th>
      <th class='r'>CANT.</th>
      <th class='r'>PRECIO</th>
      <th class='r'>TOTAL</th>
    </tr>
  </thead>
  <tbody>{rows}</tbody>
</table>
<hr class='sep'>
<table class='totals'>
  {(HasDiscount    ? $"<tr><td>Descuentos</td><td class='r'>{Esc(DiscountText)}</td></tr>" : "")}
  {(HasExoneration ? $"<tr><td>Exoneración</td><td class='r'>{Esc(ExonerationText)}</td></tr>" : "")}
  <tr><td>Sub Total</td><td class='r'>{Esc(SubtotalText)}</td></tr>
  <tr><td>Imp.Ventas</td><td class='r'>{Esc(TaxText)}</td></tr>
  <tr class='total-row'><td>Total</td><td class='r'>{Esc(TotalColonesText)}</td></tr>
</table>
<hr class='sep'>
<table class='totals'>
  <tr><td>Deposito Pagos</td><td class='r'>₡0.00</td></tr>
  <tr><td>Total Comprado</td><td class='r'>₡0.00</td></tr>
  <tr><td>Total Pendiente</td><td class='r'>{Esc(TotalColonesText)}</td></tr>
  <tr><td>CAMBIO</td><td class='r'>₡0.00</td></tr>
  <tr><td>Saldo</td><td class='r'>{Esc(TotalColonesText)}</td></tr>
</table>
<hr class='sep'>
<div class='footer'>
  No se cambia ropa<br>
  No se aceptan devoluciones sin factura<br>
  No se aceptan devoluciones después de 45 días<br><br>
  <strong>¡Le Esperamos Pronto!</strong>
</div>
<div class='legal'>
  Documento emitido conforme lo establecido<br>
  en la resolución de Factura Electrónica<br>
  DGT-R-033-2019 del 20/06/2019<br>
  Dirección General de Tributación<br>
  Versión: 4.3 (Cabys)<br>
  *** Generado por AVS Solutions ***
</div>
</body>
</html>";
        }

        private static string Esc(string s) =>
            WebUtility.HtmlEncode(s ?? string.Empty);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
