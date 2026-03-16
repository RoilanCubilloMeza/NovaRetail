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
                var htmlFile = BuildCacheFile("html", BuildReceiptHtml(false));
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = $"Guardar Factura #{TransactionNumber}",
                    File  = new ShareFile(htmlFile, "text/html")
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

        private string BuildReceiptHtml(bool autoPrint = true)
        {
            var rows = new StringBuilder();
            foreach (var item in Items)
            {
                rows.Append("<tr class='item-row'>")
                    .Append("<td class='desc-cell'><div class='item-name'>").Append(Esc(item.DisplayName)).Append("</div>");

                if (!string.IsNullOrWhiteSpace(item.Code) || item.HasTax)
                {
                    rows.Append("<div class='item-meta'>");

                    if (!string.IsNullOrWhiteSpace(item.Code))
                        rows.Append("<span>").Append(Esc(item.Code)).Append("</span>");

                    if (item.HasTax)
                        rows.Append("<span>").Append(Esc(item.TaxRateText)).Append("</span>");

                    rows.Append("</div>");
                }

                rows.Append("</td>")
                    .Append("<td class='num'>").Append(Esc(item.QuantityText)).Append("</td>")
                    .Append("<td class='num'>").Append(Esc(item.UnitPriceColonesText)).Append("</td>")
                    .Append("<td class='num strong'>").Append(Esc(item.LineTotalText)).Append("</td>")
                    .Append("</tr>");
            }

            var storeBlock = HasStoreInfo
                ? $"<div class='store'><div class='store-name'>{Esc(StoreName)}</div><div>{Esc(StoreAddress)}</div><div>{Esc(StorePhone)}</div></div>"
                : string.Empty;
            var discountRow = HasDiscount
                ? $"<tr><td>Descuentos</td><td>{Esc(DiscountText)}</td></tr>"
                : string.Empty;
            var exonRow = HasExoneration
                ? $"<tr><td>Exoneración</td><td>{Esc(ExonerationText)}</td></tr>"
                : string.Empty;
            var printScript = autoPrint
                ? "<script>window.onload=function(){window.print();}</script>"
                : string.Empty;

            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>")
                .AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1'>")
                .AppendLine("<title></title>")
                .AppendLine("<style>")
                .AppendLine("*{box-sizing:border-box}html,body{margin:0;padding:0}body{background:#eef3f8;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;-webkit-print-color-adjust:exact;print-color-adjust:exact}")
                .AppendLine(".wrap{padding:28px 20px}.paper{width:min(760px,100%);margin:0 auto;background:#fff;border:1px solid #dbe3ee;border-radius:22px;box-shadow:0 14px 36px rgba(15,23,42,.10);overflow:hidden}")
                .AppendLine(".hero{background:#0f172a;padding:18px 22px}.hero-table,.meta-table,.items-table,.summary-table,.payment-table{width:100%;border-collapse:collapse}.hero-title{font-size:28px;font-weight:800;color:#fff;letter-spacing:-.3px}.hero-sub{font-size:13px;color:#bfdbfe;padding-top:4px}.doc-badge{display:inline-block;background:#2563eb;color:#fff;border-radius:14px;padding:10px 14px;text-align:center;min-width:92px}.doc-badge small{display:block;font-size:10px;color:#dbeafe;margin-bottom:2px}.doc-badge strong{font-size:20px}")
                .AppendLine(".content{padding:26px}.receipt-title{text-align:center;font-size:30px;font-weight:800;margin:0 0 10px}.store{text-align:center;font-size:16px;color:#334155;line-height:1.45;margin:0 0 8px}.store-name{font-size:26px;font-weight:800;color:#0f172a}.sep{border-top:1px solid #cbd5e1;margin:14px 0}")
                .AppendLine(".meta-table td{padding:4px 0;font-size:16px;vertical-align:top}.meta-label{width:160px;font-weight:700;color:#334155}.meta-value{color:#0f172a}.meta-split{display:flex;justify-content:space-between;gap:12px}")
                .AppendLine(".items-table{table-layout:fixed}.items-head th{padding:10px 0 8px;border-bottom:2px solid #cbd5e1;font-size:15px;color:#334155;text-align:left}.items-head th:nth-child(2),.items-head th:nth-child(3),.items-head th:nth-child(4){text-align:right}.items-table th:nth-child(1){width:52%}.items-table th:nth-child(2){width:12%}.items-table th:nth-child(3){width:18%}.items-table th:nth-child(4){width:18%}.items-table td{padding:12px 0 10px;border-bottom:1px solid #eef2f7;vertical-align:top;font-size:16px}.desc-cell{padding-right:12px}.item-name{font-weight:800;line-height:1.35}.item-meta{display:flex;gap:10px;flex-wrap:wrap;color:#64748b;font-size:13px;margin-top:4px}.num{text-align:right}.strong{font-weight:800}")
                .AppendLine(".summary-table td,.payment-table td{padding:5px 0;font-size:16px}.summary-table td:last-child,.payment-table td:last-child{text-align:right;font-weight:700}.summary-total td{padding-top:10px;border-top:2px solid #cbd5e1;font-size:22px;font-weight:800}.payment-table{margin-top:2px}.policy{text-align:center;font-size:15px;line-height:1.45;color:#0f172a}.policy-sep{text-align:center;color:#94a3b8;font-size:14px;letter-spacing:1px;margin:10px 0 6px}.soon{text-align:center;font-size:20px;font-weight:800;margin-top:4px}.legal{text-align:center;font-size:12px;line-height:1.45;color:#475569;padding-top:16px}.legal strong{color:#0f172a}")
                .AppendLine("@media print{@page{size:auto;margin:0}html,body{margin:0;padding:0;background:#fff}.wrap{padding:10mm}.paper{width:100%;max-width:none;border:none;box-shadow:none;border-radius:0}.hero{padding:12px 0 14px;background:#fff;border-bottom:1px solid #cbd5e1}.hero-title{color:#0f172a;font-size:24px}.hero-sub,.doc-badge small{color:#475569}.doc-badge{background:#f8fafc;color:#0f172a;border:1px solid #cbd5e1}.content{padding:14px 0 0}.store-name{font-size:20px}.store{font-size:13px}.meta-table td,.summary-table td,.payment-table td,.items-table td{font-size:13px}.items-head th{font-size:12px}.item-name{font-size:13px}.item-meta{font-size:11px}.summary-total td{font-size:17px}.policy{font-size:12px}.soon{font-size:16px}.legal{font-size:10px;padding-top:10px}}")
                .AppendLine("</style>")
                .AppendLine(printScript)
                .AppendLine("</head><body><div class='wrap'><div class='paper'>")
                .AppendLine("<div class='hero'><table class='hero-table'><tr><td><div class='hero-title'>Recibo Duplicado</div><div class='hero-sub'>Resumen de la venta listo para imprimir, enviar o guardar.</div></td><td style='text-align:right;vertical-align:middle'><div class='doc-badge'><small>DOC</small><strong>")
                .Append(TransactionNumber)
                .AppendLine("</strong></div></td></tr></table></div>")
                .AppendLine("<div class='content'>")
                .AppendLine("<div class='receipt-title'>Recibo Duplicado</div>")
                .AppendLine(storeBlock)
                .AppendLine("<div class='sep'></div>")
                .AppendLine("<table class='meta-table'>")
                .AppendLine($"<tr><td class='meta-label'>Doc. Interno #:</td><td class='meta-value strong'>{TransactionNumber}</td></tr>")
                .AppendLine($"<tr><td class='meta-label'>Cód. Cliente:</td><td class='meta-value'>{Esc(ClientId)}</td></tr>")
                .AppendLine($"<tr><td class='meta-label'>Nombre:</td><td class='meta-value'>{Esc(ClientName)}</td></tr>")
                .AppendLine($"<tr><td class='meta-label'>Fecha/Hora:</td><td class='meta-value'>{Esc(TransactionDate)}</td></tr>")
                .AppendLine($"<tr><td class='meta-label'>Cajero:</td><td class='meta-value'><div class='meta-split'><span>{Esc(CashierName)}</span><span class='strong'>{Esc(RegisterText)}</span></div></td></tr>")
                .AppendLine("</table>")
                .AppendLine("<div class='sep'></div>")
                .AppendLine("<table class='items-table'><thead><tr class='items-head'><th>DESC/COD</th><th>CANT.</th><th>PRECIO</th><th>TOTAL</th></tr></thead><tbody>")
                .Append(rows)
                .AppendLine("</tbody></table>")
                .AppendLine("<div class='sep'></div>")
                .AppendLine("<table class='summary-table'>")
                .AppendLine($"<tr><td>Sub Total</td><td>{Esc(SubtotalText)}</td></tr>")
                .AppendLine(discountRow)
                .AppendLine(exonRow)
                .AppendLine($"<tr><td>Imp.Ventas</td><td>{Esc(TaxText)}</td></tr>")
                .AppendLine($"<tr class='summary-total'><td>Total</td><td>{Esc(TotalColonesText)}</td></tr>")
                .AppendLine("</table>")
                .AppendLine("<div class='sep'></div>")
                .AppendLine("<table class='payment-table'>")
                .AppendLine($"<tr><td>{Esc(TenderEntregadoText)}</td><td>{Esc(TotalColonesText)}</td></tr>")
                .AppendLine("<tr><td>CAMBIO</td><td>₡0.00</td></tr>")
                .AppendLine("</table>")
                .AppendLine("<div class='policy-sep'>----------------------------------------</div>")
                .AppendLine("<div class='policy'>No se cambia ropa<br>No se aceptan devoluciones sin factura<br>No se aceptan devoluciones después de 45 días</div>")
                .AppendLine("<div class='policy-sep'>----------------------------------------</div>")
                .AppendLine("<div class='soon'>¡Le Esperamos Pronto!</div>")
                .AppendLine("<div class='legal'>Documento emitido conforme lo establecido<br>en la resolución de Factura Electrónica<br>DGT-R-053-2019 del 20/06/2019<br>de la Dirección General de Tributación<br>Versión: 4.3 (Cabys)<br><strong>*** Generado por AVS Solutions ***</strong></div>")
                .AppendLine("</div></div></div></body></html>");

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
