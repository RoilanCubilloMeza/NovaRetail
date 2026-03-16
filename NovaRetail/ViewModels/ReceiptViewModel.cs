using NovaRetail.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

namespace NovaRetail.ViewModels
{
    public sealed class ReceiptLineItem
    {
        public string DisplayName { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public decimal TaxPercentage { get; init; }
        public string UnitPriceColonesText { get; init; } = string.Empty;
        public string LineTotalText { get; init; } = string.Empty;
        public string QuantityText => $"{Quantity:0.##}";
        public string TaxRateText => TaxPercentage > 0 ? $"{TaxPercentage:0.##} %" : string.Empty;
        public bool HasTax => TaxPercentage > 0;
    }

    public sealed class ReceiptViewModel : INotifyPropertyChanged
    {
        public event Action? RequestClose;
        public ICommand CloseCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand EmailCommand { get; }
        public ICommand SaveCommand { get; }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotBusy)); } }
        }
        public bool IsNotBusy => !_isBusy;

        public int TransactionNumber { get; private set; }
        public string TransactionNumberText => $"{TransactionNumber}";
        public string TransactionDate { get; private set; } = string.Empty;

        public string StoreName { get; private set; } = string.Empty;
        public string StoreAddress { get; private set; } = string.Empty;
        public string StorePhone { get; private set; } = string.Empty;
        public bool HasStoreInfo => !string.IsNullOrWhiteSpace(StoreName);

        public string ClientId { get; private set; } = string.Empty;
        public string ClientName { get; private set; } = string.Empty;
        public string CashierName { get; private set; } = string.Empty;
        public int RegisterNumber { get; private set; } = 1;
        public string RegisterText => $"Reg.POS #: {RegisterNumber}";

        public ObservableCollection<ReceiptLineItem> Items { get; } = new();

        public string SubtotalText { get; private set; } = string.Empty;
        public string TaxText { get; private set; } = string.Empty;
        public string DiscountText { get; private set; } = string.Empty;
        public bool HasDiscount { get; private set; }
        public string ExonerationText { get; private set; } = string.Empty;
        public bool HasExoneration { get; private set; }
        public string TotalText { get; private set; } = string.Empty;
        public string TotalColonesText { get; private set; } = string.Empty;
        public string TenderDescription { get; private set; } = string.Empty;
        public string TenderEntregadoText => string.IsNullOrWhiteSpace(TenderDescription)
            ? "Entregado"
            : $"{TenderDescription} Entregado";

        public ReceiptViewModel()
        {
            CloseCommand  = new Command(() => RequestClose?.Invoke());
            PrintCommand  = new Command(async () => await PrintAsync());
            EmailCommand  = new Command(async () => await EmailAsync());
            SaveCommand   = new Command(async () => await SaveAsync());
        }

        public void Load(
            int transactionNumber,
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
            string discountText,
            bool hasDiscount,
            string exonerationText,
            bool hasExoneration,
            string totalText,
            string totalColonesText,
            string tenderDescription)
        {
            TransactionNumber = transactionNumber;
            TransactionDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            ClientId = string.IsNullOrWhiteSpace(clientId) ? "S-00001" : clientId;
            ClientName = string.IsNullOrWhiteSpace(clientName) ? "CLIENTE CONTADO" : clientName;
            CashierName = string.IsNullOrWhiteSpace(cashierName) ? "—" : cashierName;
            RegisterNumber = registerNumber > 0 ? registerNumber : 1;

            StoreName = storeName ?? string.Empty;
            StoreAddress = storeAddress ?? string.Empty;
            StorePhone = storePhone ?? string.Empty;

            Items.Clear();
            foreach (var item in cartItems)
            {
                Items.Add(new ReceiptLineItem
                {
                    DisplayName = item.DisplayName,
                    Code = item.Code ?? string.Empty,
                    Quantity = item.Quantity,
                    TaxPercentage = item.TaxPercentage,
                    UnitPriceColonesText = $"₡{item.EffectivePriceColones:N2}",
                    LineTotalText = $"₡{item.EffectivePriceColones * item.Quantity:N2}"
                });
            }

            SubtotalText = subtotalText;
            TaxText = taxText;
            DiscountText = discountText;
            HasDiscount = hasDiscount;
            ExonerationText = exonerationText;
            HasExoneration = hasExoneration;
            TotalText = totalText;
            TotalColonesText = totalColonesText;
            TenderDescription = tenderDescription;

            OnPropertyChanged(string.Empty);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ── Print / Email / Save ──────────────────────────────────────────────

        private string BuildCacheFile(string extension, string content)
        {
            var path = Path.Combine(FileSystem.CacheDirectory, $"Factura-{TransactionNumber}.{extension}");
            File.WriteAllText(path, content, Encoding.UTF8);
            return path;
        }

        private async Task PrintAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var tempFile = BuildCacheFile("html", BuildReceiptHtml());
                await Launcher.OpenAsync(new OpenFileRequest
                {
                    Title = $"Imprimir Factura #{TransactionNumber}",
                    File  = new ReadOnlyFile(tempFile, "text/html")
                });
            }
            catch (Exception ex)
            {
                await ShowAlertAsync("Error al imprimir", ex.Message);
            }
            finally { IsBusy = false; }
        }

        private async Task EmailAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var htmlFile = BuildCacheFile("html", BuildReceiptHtml());
                var msg = new EmailMessage
                {
                    Subject = $"Factura Electrónica #{TransactionNumber} - {ClientName}",
                    Body    = $"Estimado(a) {ClientName},\n\n"
                            + $"Adjunto encontrará la factura electrónica #{TransactionNumber} "
                            + $"emitida el {TransactionDate}.\n\n"
                            + $"Total: {TotalColonesText}\n\n"
                            + "Gracias por su compra.\n"
                            + (string.IsNullOrWhiteSpace(StoreName) ? string.Empty : $"\n{StoreName}"),
                };
                msg.Attachments.Add(new EmailAttachment(htmlFile, "text/html"));
                await Email.ComposeAsync(msg);
            }
            catch
            {
                await ShowAlertAsync("Correo", "No se pudo abrir el cliente de correo.\nVerifique que tiene una cuenta de correo configurada.");
            }
            finally { IsBusy = false; }
        }

        private async Task SaveAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var txtFile = BuildCacheFile("txt", BuildReceiptText());
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = $"Guardar Factura #{TransactionNumber}",
                    File  = new ShareFile(txtFile, "text/plain")
                });
            }
            catch (Exception ex)
            {
                await ShowAlertAsync("Error al guardar", ex.Message);
            }
            finally { IsBusy = false; }
        }

        private static Task ShowAlertAsync(string title, string message)
            => MainThread.InvokeOnMainThreadAsync(() =>
                Application.Current?.MainPage?.DisplayAlert(title, message, "OK") ?? Task.CompletedTask);

        // ── Text / HTML builders ─────────────────────────────────────────────

        private string BuildReceiptText()
        {
            const int W = 44;
            var sep  = new string('=', W);
            var dash = new string('-', W);
            var sb   = new StringBuilder();

            sb.AppendLine(sep);
            sb.AppendLine(Center("Recibo Duplicado", W));
            sb.AppendLine(sep);

            if (!string.IsNullOrWhiteSpace(StoreName))
            {
                sb.AppendLine(Center(StoreName, W));
                if (!string.IsNullOrWhiteSpace(StoreAddress)) sb.AppendLine(Center(StoreAddress, W));
                if (!string.IsNullOrWhiteSpace(StorePhone))   sb.AppendLine(Center(StorePhone, W));
                sb.AppendLine(sep);
            }

            sb.AppendLine($"Doc. Interno #: {TransactionNumber}");
            sb.AppendLine($"C\u00f3d. Cliente:   {ClientId}");
            sb.AppendLine($"Nombre:         {ClientName}");
            sb.AppendLine($"Fecha/Hora:     {TransactionDate}");
            sb.AppendLine($"Cajero:         {CashierName,-16}{RegisterText}");
            sb.AppendLine(sep);

            sb.AppendLine($"{"DESC/COD",-22}{"CANT",4}  {"PRECIO",8}  {"TOTAL",8}");
            sb.AppendLine(dash);
            foreach (var item in Items)
            {
                sb.AppendLine($"{Truncate(item.DisplayName, 22),-22}{item.QuantityText,4}  {item.UnitPriceColonesText,8}  {item.LineTotalText,8}");
                if (!string.IsNullOrWhiteSpace(item.Code)) sb.AppendLine($"  {item.Code}");
                if (item.HasTax) sb.AppendLine($"  {item.TaxRateText}");
            }
            sb.AppendLine(dash);

            sb.AppendLine($"{"Subtotal",-32}{SubtotalText,10}");
            if (HasDiscount)    sb.AppendLine($"{"Descuentos",-32}{DiscountText,10}");
            if (HasExoneration) sb.AppendLine($"{"Exoneraci\u00f3n",-32}{ExonerationText,10}");
            sb.AppendLine($"{"Imp.Ventas",-32}{TaxText,10}");
            sb.AppendLine(sep);
            sb.AppendLine($"{"TOTAL",-32}{TotalColonesText,10}");
            sb.AppendLine(sep);

            sb.AppendLine($"{TenderEntregadoText,-32}{TotalColonesText,10}");
            sb.AppendLine($"{"CAMBIO",-32}{"\u20a10.00",10}");
            sb.AppendLine(sep);

            sb.AppendLine();
            sb.AppendLine("  No se cambia ropa");
            sb.AppendLine("  No se aceptan devoluciones sin factura");
            sb.AppendLine("  No se aceptan devoluciones despu\u00e9s de 45 d\u00edas");
            sb.AppendLine();
            sb.AppendLine(Center("\u00a1Le Esperamos Pronto!", W));
            sb.AppendLine();
            sb.AppendLine(sep);
            sb.AppendLine("DGT-R-053-2019 del 20/06/2019");
            sb.AppendLine("Versi\u00f3n: 4.3 (Cabys)");
            sb.AppendLine(Center("*** Generado por AVS Solutions ***", W));
            sb.AppendLine(sep);

            return sb.ToString();
        }

        private string BuildReceiptHtml()
        {
            var rows = new StringBuilder();
            foreach (var item in Items)
            {
                rows.Append("<tr><td>").Append(Esc(item.DisplayName))
                    .Append("<br><small style='color:#888'>").Append(Esc(item.Code)).Append("</small></td>")
                    .Append("<td style='text-align:center'>").Append(Esc(item.QuantityText)).Append("</td>")
                    .Append("<td style='text-align:right'>").Append(Esc(item.UnitPriceColonesText)).Append("</td>")
                    .Append("<td style='text-align:right'><b>").Append(Esc(item.LineTotalText)).AppendLine("</b></td></tr>");
                if (item.HasTax)
                    rows.Append("<tr><td colspan='4'><small style='color:#888'>").Append(Esc(item.TaxRateText)).AppendLine("</small></td></tr>");
            }

            var storeBlock = HasStoreInfo
                ? $"<p class='center'><b>{Esc(StoreName)}</b><br>{Esc(StoreAddress)}<br>{Esc(StorePhone)}</p>"
                : string.Empty;
            var discountRow = HasDiscount
                ? $"<tr><td>Descuentos</td><td colspan='3' style='text-align:right;color:#e53e3e'>{Esc(DiscountText)}</td></tr>"
                : string.Empty;
            var exonRow = HasExoneration
                ? $"<tr><td>Exoneraci\u00f3n</td><td colspan='3' style='text-align:right;color:#2b6cb0'>{Esc(ExonerationText)}</td></tr>"
                : string.Empty;

            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>")
                .AppendLine($"<title>Factura #{TransactionNumber}</title>")
                .AppendLine("<style>")
                .AppendLine("body{font-family:'Courier New',monospace;font-size:11px;max-width:380px;margin:20px auto}")
                .AppendLine("h2{text-align:center;font-size:13px;margin:4px 0}")
                .AppendLine(".center{text-align:center} .sep{border-top:1px solid #000;margin:4px 0}")
                .AppendLine("table{width:100%;border-collapse:collapse} td{padding:2px 4px}")
                .AppendLine(".th{border-top:1px solid #000;border-bottom:1px solid #000;font-weight:bold}")
                .AppendLine(".total-row td{border-top:2px solid #000;font-weight:bold;font-size:14px}")
                .AppendLine("@media print{body{margin:0}}")
                .AppendLine("</style>")
                .AppendLine("<script>window.onload=function(){window.print();}</script>")
                .AppendLine("</head><body>")
                .AppendLine("<h2>Recibo Duplicado</h2>")
                .AppendLine(storeBlock)
                .AppendLine("<div class='sep'></div>")
                .AppendLine("<table>")
                .AppendLine($"<tr><td>Doc. Interno #:</td><td><b>{TransactionNumber}</b></td></tr>")
                .AppendLine($"<tr><td>C\u00f3d. Cliente:</td><td>{Esc(ClientId)}</td></tr>")
                .AppendLine($"<tr><td>Nombre:</td><td>{Esc(ClientName)}</td></tr>")
                .AppendLine($"<tr><td>Fecha/Hora:</td><td>{Esc(TransactionDate)}</td></tr>")
                .AppendLine($"<tr><td>Cajero:</td><td>{Esc(CashierName)} &nbsp;&nbsp; {Esc(RegisterText)}</td></tr>")
                .AppendLine("</table>")
                .AppendLine("<div class='sep'></div>")
                .AppendLine("<table>")
                .AppendLine("<tr class='th'><td>DESC/COD</td><td style='text-align:center'>CANT.</td><td style='text-align:right'>PRECIO</td><td style='text-align:right'>TOTAL</td></tr>")
                .Append(rows)
                .AppendLine("</table>")
                .AppendLine("<div class='sep'></div>")
                .AppendLine("<table>")
                .AppendLine($"<tr><td>Subtotal</td><td colspan='3' style='text-align:right'>{Esc(SubtotalText)}</td></tr>")
                .AppendLine(discountRow).AppendLine(exonRow)
                .AppendLine($"<tr><td>Imp.Ventas</td><td colspan='3' style='text-align:right'>{Esc(TaxText)}</td></tr>")
                .AppendLine($"<tr class='total-row'><td>TOTAL</td><td colspan='3' style='text-align:right'>{Esc(TotalColonesText)}</td></tr>")
                .AppendLine("</table>")
                .AppendLine("<div class='sep'></div>")
                .AppendLine("<table>")
                .AppendLine($"<tr><td>{Esc(TenderEntregadoText)}</td><td style='text-align:right'>{Esc(TotalColonesText)}</td></tr>")
                .AppendLine("<tr><td>CAMBIO</td><td style='text-align:right'>\u20a10.00</td></tr>")
                .AppendLine("</table>")
                .AppendLine("<div class='sep'></div>")
                .AppendLine("<p class='center' style='font-size:9px'>No se cambia ropa<br>No se aceptan devoluciones sin factura<br>No se aceptan devoluciones despu\u00e9s de 45 d\u00edas</p>")
                .AppendLine("<p class='center'><b>\u00a1Le Esperamos Pronto!</b></p>")
                .AppendLine("<div class='sep'></div>")
                .AppendLine("<p class='center' style='font-size:8px'>DGT-R-053-2019 del 20/06/2019 &middot; Versi\u00f3n 4.3 (Cabys)<br>*** Generado por AVS Solutions ***</p>")
                .AppendLine("</body></html>");

            return html.ToString();
        }

        private static string Center(string text, int width)
            => text.Length >= width ? text : text.PadLeft((width + text.Length) / 2).PadRight(width);

        private static string Truncate(string text, int max)
            => text.Length <= max ? text : text[..max];

        private static string Esc(string text)
            => WebUtility.HtmlEncode(text);
    }
}
