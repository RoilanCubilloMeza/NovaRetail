using NovaRetail.Models;
using NovaRetail.Services;
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

        // Cambio de precio
        public bool HasOverridePrice { get; init; }
        public string PriceChangeDetailText { get; init; } = string.Empty;

        // Descuento por línea
        public bool HasDiscount { get; init; }
        public string DiscountDetailText { get; init; } = string.Empty;

        // Exoneración por línea
        public bool HasExoneration { get; init; }
        public string ExonerationDetailText { get; init; } = string.Empty;

        public bool HasAnyDetail => HasOverridePrice || HasDiscount || HasExoneration;
    }

    public sealed class ReceiptViewModel : INotifyPropertyChanged
    {
        public event Action? RequestClose;
        public ICommand CloseCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand EmailCommand { get; }
        public ICommand SaveCommand { get; }

        public string CompanyName { get; private set; } = string.Empty;
        public string CedulaJuridica { get; private set; } = string.Empty;
        public string Clave50 { get; private set; } = string.Empty;
        public string Consecutivo { get; private set; } = string.Empty;
        public string ComprobanteTipo { get; private set; } = "04";
        public string ClientEmail { get; private set; } = string.Empty;

        public bool HasFiscalData => !string.IsNullOrWhiteSpace(Clave50);
        public string DocumentTypeName => ComprobanteTipo switch
        {
            "01" => "Factura Electrónica",
            "03" => "Nota de Crédito Electrónica",
            "04" => "Tiquete Electrónico",
            "10" => "Reposición",
            _ => "Tiquete Electrónico"
        };

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
        public string TenderTotalText { get; private set; } = string.Empty;
        public string ChangeAmountText { get; private set; } = "₡0.00";
        public bool HasChange { get; private set; }
        public string TenderEntregadoText =>
            HasSecondTender && !HasChange
                ? (string.IsNullOrWhiteSpace(TenderDescription) ? "1er Pago" : $"1er Pago: {TenderDescription}")
                : (string.IsNullOrWhiteSpace(TenderDescription) ? "Entregado" : $"{TenderDescription} Entregado");

        // Segundo medio de pago
        public bool HasSecondTender { get; private set; }
        public string SecondTenderDescription { get; private set; } = string.Empty;
        public string SecondTenderAmountText { get; private set; } = string.Empty;
        public string SecondTenderEntregadoText => string.IsNullOrWhiteSpace(SecondTenderDescription)
            ? "2do Pago"
            : $"2do Pago: {SecondTenderDescription}";

        public ReceiptViewModel()
        {
            CloseCommand       = new Command(() => RequestClose?.Invoke());
            PrintCommand       = new Command(async () => await PrintAsync());
            EmailCommand       = new Command(async () => await EmailAsync());
            SaveCommand        = new Command(async () => await SaveAsync());
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
            string tenderDescription,
            decimal tenderTotalColones = 0m,
            decimal changeColones = 0m,
            string secondTenderDescription = "",
            decimal secondTenderAmountColones = 0m,
            // Ticket-specific data
            string companyName = "",
            string cedulaJuridica = "",
            string clave50 = "",
            string consecutivo = "",
            string comprobanteTipo = "04",
            string clientEmail = "",
            decimal subtotalColones = 0m,
            decimal discountColones = 0m,
            decimal totalColones = 0m,
            int taxSystem = 1)
        {
            CompanyName = companyName ?? string.Empty;
            CedulaJuridica = cedulaJuridica ?? string.Empty;
            Clave50 = clave50 ?? string.Empty;
            Consecutivo = consecutivo ?? string.Empty;
            ComprobanteTipo = comprobanteTipo ?? "04";
            ClientEmail = clientEmail ?? string.Empty;

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
                var grossUnit = item.EffectivePriceColones;
                var grossLine = grossUnit * item.Quantity;
                var discountFactor = 1m - item.DiscountPercent / 100m;
                var netUnit = Math.Round(grossUnit * discountFactor, 2);
                var netLine = Math.Round(grossLine * discountFactor, 2);

                // PRECIO y TOTAL muestran el monto neto que paga el cliente.
                // El texto descriptivo explica el precio original y el descuento aplicado.
                Items.Add(new ReceiptLineItem
                {
                    DisplayName = item.DisplayName,
                    Code = item.Code ?? string.Empty,
                    Quantity = item.Quantity,
                    TaxPercentage = item.TaxPercentage,
                    UnitPriceColonesText = $"₡{(item.HasDiscount ? netUnit : grossUnit):N2}",
                    LineTotalText = $"₡{(item.HasDiscount ? netLine : grossLine):N2}",
                    HasOverridePrice = item.HasDownwardPriceOverride,
                    PriceChangeDetailText = item.HasDownwardPriceOverride
                        ? $"Cambio precio: de ₡{item.UnitPriceColones:N2} a ₡{grossUnit:N2}"
                        : string.Empty,
                    HasDiscount = item.HasDiscount,
                    DiscountDetailText = item.HasDiscount
                        ? $"Desc. {item.DiscountPercent:0.##}%: de ₡{grossUnit:N2} a ₡{netUnit:N2}"
                        : string.Empty,
                    HasExoneration = item.HasExoneration,
                    ExonerationDetailText = item.HasExoneration
                        ? $"Exoneración: {item.ExonerationText}"
                        : string.Empty
                });
            }

            var effectiveTenderTotal = tenderTotalColones > 0m ? tenderTotalColones : 0m;
            TenderTotalText = effectiveTenderTotal > 0m ? $"₡{effectiveTenderTotal:N2}" : totalColonesText;
            ChangeAmountText = changeColones > 0m ? $"₡{changeColones:N2}" : "₡0.00";
            HasChange = changeColones > 0m;

            SubtotalText = subtotalText;
            TaxText = taxText;
            DiscountText = discountText;
            HasDiscount = hasDiscount;
            ExonerationText = exonerationText;
            HasExoneration = hasExoneration;
            TotalText = totalText;
            TotalColonesText = totalColonesText;
            TenderDescription = tenderDescription;

            HasSecondTender = secondTenderAmountColones > 0m && !string.IsNullOrWhiteSpace(secondTenderDescription);
            SecondTenderDescription = secondTenderDescription;
            SecondTenderAmountText = secondTenderAmountColones > 0m ? $"₡{secondTenderAmountColones:N2}" : string.Empty;

            OnPropertyChanged(string.Empty);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void LoadFromHistory(InvoiceHistoryEntry entry)
        {
            CompanyName    = entry.StoreName;
            CedulaJuridica = string.Empty;
            Clave50        = entry.Clave50;
            Consecutivo    = entry.Consecutivo;
            ComprobanteTipo = entry.ComprobanteTipo;
            ClientEmail    = string.Empty;

            TransactionNumber = entry.TransactionNumber;
            TransactionDate   = entry.Date.ToString("dd/MM/yyyy HH:mm");
            ClientId          = entry.ClientId;
            ClientName        = entry.ClientName;
            CashierName       = entry.CashierName;
            RegisterNumber    = entry.RegisterNumber;
            StoreName         = entry.StoreName;
            StoreAddress      = string.Empty;
            StorePhone        = string.Empty;

            Items.Clear();
            foreach (var line in entry.Lines)
            {
                Items.Add(new ReceiptLineItem
                {
                    DisplayName           = line.DisplayName,
                    Code                  = line.Code,
                    Quantity              = line.Quantity,
                    TaxPercentage         = line.TaxPercentage,
                    UnitPriceColonesText  = $"₡{line.UnitPriceColones:N2}",
                    LineTotalText         = $"₡{line.LineTotalColones:N2}",
                    HasOverridePrice      = line.HasOverridePrice,
                    PriceChangeDetailText = line.HasOverridePrice ? "Precio modificado" : string.Empty,
                    HasDiscount           = line.HasDiscount,
                    DiscountDetailText    = line.HasDiscount
                        ? $"Desc. {line.DiscountPercent:0.##}%"
                        : string.Empty,
                    HasExoneration        = line.HasExoneration,
                    ExonerationDetailText = line.HasExoneration
                        ? $"Exoneración {line.ExonerationPercent:0.##}%"
                        : string.Empty
                });
            }

            TenderTotalText  = entry.TenderTotalColones > 0
                ? $"₡{entry.TenderTotalColones:N2}"
                : $"₡{entry.TotalColones:N2}";
            ChangeAmountText = entry.ChangeColones > 0 ? $"₡{entry.ChangeColones:N2}" : "₡0.00";
            HasChange        = entry.ChangeColones > 0;

            SubtotalText    = $"₡{entry.SubtotalColones:N2}";
            TaxText         = $"₡{entry.TaxColones:N2}";
            DiscountText    = entry.DiscountColones > 0 ? $"-₡{entry.DiscountColones:N2}" : "₡0.00";
            HasDiscount     = entry.DiscountColones > 0;
            ExonerationText = entry.ExonerationColones > 0 ? $"-₡{entry.ExonerationColones:N2}" : "₡0.00";
            HasExoneration  = entry.ExonerationColones > 0;
            TotalText       = $"₡{entry.TotalColones:N2}";
            TotalColonesText = $"₡{entry.TotalColones:N2}";

            TenderDescription    = entry.TenderDescription;
            HasSecondTender      = entry.HasSecondTender;
            SecondTenderDescription   = entry.SecondTenderDescription;
            SecondTenderAmountText    = entry.SecondTenderAmountColones > 0
                ? $"₡{entry.SecondTenderAmountColones:N2}"
                : string.Empty;

            OnPropertyChanged(string.Empty);
        }



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

            if (HasFiscalData)
            {
                sb.AppendLine(Center(DocumentTypeName, W));
                sb.AppendLine($"Clave: {Clave50}");
                sb.AppendLine($"No.    {Consecutivo}");
                sb.AppendLine($"Fecha: {TransactionDate}");
                sb.AppendLine(sep);
            }

            sb.AppendLine($"{"DESC/COD",-22}{"CANT",4}  {"PRECIO",8}  {"TOTAL",8}");
            sb.AppendLine(dash);
            foreach (var item in Items)
            {
                sb.AppendLine($"{Truncate(item.DisplayName, 22),-22}{item.QuantityText,4}  {item.UnitPriceColonesText,8}  {item.LineTotalText,8}");
                if (!string.IsNullOrWhiteSpace(item.Code)) sb.AppendLine($"  {item.Code}");
                if (item.HasTax) sb.AppendLine($"  {item.TaxRateText}");
                if (item.HasOverridePrice) sb.AppendLine($"  {item.PriceChangeDetailText}");
                if (item.HasDiscount) sb.AppendLine($"  {item.DiscountDetailText}");
                if (item.HasExoneration) sb.AppendLine($"  {item.ExonerationDetailText}");
            }
            sb.AppendLine(dash);

            sb.AppendLine($"{"Subtotal",-32}{SubtotalText,10}");
            if (HasDiscount)    sb.AppendLine($"{"Descuentos",-32}{DiscountText,10}");
            if (HasExoneration) sb.AppendLine($"{"Exoneraci\u00f3n",-32}{ExonerationText,10}");
            sb.AppendLine($"{"Imp.Ventas",-32}{TaxText,10}");
            sb.AppendLine(sep);
            sb.AppendLine($"{"TOTAL",-32}{TotalColonesText,10}");
            sb.AppendLine(sep);

            sb.AppendLine($"{TenderEntregadoText,-32}{TenderTotalText,10}");
            if (HasSecondTender)
                sb.AppendLine($"{SecondTenderEntregadoText,-32}{SecondTenderAmountText,10}");
            if (HasChange)
                sb.AppendLine($"{"CAMBIO",-32}{ChangeAmountText,10}");
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
            var rowNum = 0;
            foreach (var item in Items)
            {
                rowNum++;
                rows.Append("<tr class='item-row'>")
                    .Append($"<td class='num'>{rowNum}</td>")
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

                if (item.HasOverridePrice)
                    rows.Append("<div class='item-detail detail-price'>").Append(Esc(item.PriceChangeDetailText)).Append("</div>");
                if (item.HasDiscount)
                    rows.Append("<div class='item-detail detail-disc'>").Append(Esc(item.DiscountDetailText)).Append("</div>");
                if (item.HasExoneration)
                    rows.Append("<div class='item-detail detail-exon'>").Append(Esc(item.ExonerationDetailText)).Append("</div>");

                rows.Append("</td>")
                    .Append("<td class='num'>").Append(Esc(item.QuantityText)).Append("</td>")
                    .Append("<td class='num'>").Append(Esc(item.UnitPriceColonesText)).Append("</td>")
                    .Append("<td class='num strong'>").Append(Esc(item.LineTotalText)).Append("</td>")
                    .Append("</tr>");
            }

            var discountRow = HasDiscount
                ? $"<tr class='sum-disc'><td>Descuento</td><td>- CRC</td><td>{Esc(DiscountText)}</td></tr>"
                : string.Empty;
            var exonRow = HasExoneration
                ? $"<tr class='sum-exon'><td>Exoneración</td><td></td><td>{Esc(ExonerationText)}</td></tr>"
                : string.Empty;
            var printScript = autoPrint
                ? "<script>window.onload=function(){window.print();}</script>"
                : string.Empty;

            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>")
                .AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1'>")
                .AppendLine("<title></title>")
                .AppendLine("<style>")
                .AppendLine("*{box-sizing:border-box}html,body{margin:0;padding:0}body{background:#f1f5f9;font-family:'Segoe UI',Arial,sans-serif;color:#0f172a;-webkit-print-color-adjust:exact;print-color-adjust:exact}")
                .AppendLine(".wrap{padding:24px 16px}.paper{width:min(720px,100%);margin:0 auto;background:#fff;border:1px solid #e2e8f0;border-radius:6px;box-shadow:0 8px 24px rgba(15,23,42,.08);overflow:hidden;padding:32px 36px}")
                .AppendLine(".store-header{text-align:center;margin-bottom:6px}.store-name{font-size:22px;font-weight:800;margin:0 0 2px}.store-info{font-size:13px;color:#475569;line-height:1.5}")
                .AppendLine(".sep{border:none;border-top:1px solid #cbd5e1;margin:14px 0}.sep-double{border:none;border-top:1px dashed #94a3b8;margin:14px 0}")
                .AppendLine(".doc-title{text-align:center;font-size:22px;font-weight:800;margin:18px 0 10px}")
                .AppendLine(".fiscal{font-size:13px;color:#334155;line-height:1.65;margin-bottom:4px}.fiscal-label{font-weight:700}")
                .AppendLine(".client-block{font-size:14px;line-height:1.5;margin:0 0 4px}.client-block strong{font-weight:800}")
                .AppendLine(".items-table{width:100%;border-collapse:collapse;table-layout:fixed;margin-top:4px}.items-table thead th{padding:8px 4px;border-top:2px solid #334155;border-bottom:2px solid #334155;font-size:13px;font-weight:800;text-align:left;color:#334155}.items-table thead th.num{text-align:right}.items-table thead th:nth-child(1){width:6%}.items-table thead th:nth-child(2){width:46%}.items-table thead th:nth-child(3){width:10%}.items-table thead th:nth-child(4){width:19%}.items-table thead th:nth-child(5){width:19%}")
                .AppendLine(".items-table td{padding:8px 4px;border-bottom:1px solid #f1f5f9;vertical-align:top;font-size:14px}.desc-cell{padding-right:8px}.item-name{font-weight:700;line-height:1.3}.item-meta{display:flex;gap:8px;flex-wrap:wrap;color:#64748b;font-size:12px;margin-top:2px}.num{text-align:right}.strong{font-weight:800}")
                .AppendLine(".totals-table{width:100%;border-collapse:collapse;margin-top:4px}.totals-table td{padding:4px 0;font-size:14px}.totals-table td:nth-child(2){text-align:center;width:60px}.totals-table td:last-child{text-align:right;font-weight:700;width:120px}.totals-table .total-row td{padding-top:10px;border-top:2px solid #334155;font-size:18px;font-weight:800}")
                .AppendLine(".payment-table{width:100%;border-collapse:collapse}.payment-table td{padding:4px 0;font-size:14px}.payment-table td:last-child{text-align:right;font-weight:700}.pay-change td{color:#16a34a;font-weight:800;font-size:15px}")
                .AppendLine(".item-detail{font-size:11px;font-style:italic;margin-top:2px;line-height:1.2}.detail-price{color:#b45309}.detail-disc{color:#dc2626}.detail-exon{color:#0d9488}")
                .AppendLine(".sum-disc td{color:#dc2626;font-weight:700}.sum-exon td{color:#0d9488;font-weight:700}")
                .AppendLine(".policy{text-align:center;font-size:13px;line-height:1.5;color:#334155;margin:4px 0}.policy-sep{text-align:center;color:#94a3b8;font-size:12px;letter-spacing:1px;margin:8px 0 4px}.soon{text-align:center;font-size:18px;font-weight:800;margin:6px 0 0}.legal{text-align:center;font-size:11px;line-height:1.5;color:#475569;margin-top:14px}.legal strong{color:#0f172a}.footer-gen{text-align:center;font-size:12px;color:#64748b;margin-top:18px;padding-top:10px;border-top:1px solid #e2e8f0}")
                .AppendLine("@media print{@page{size:auto;margin:12mm}html,body{margin:0;padding:0;background:#fff}.wrap{padding:0}.paper{width:100%;max-width:none;border:none;box-shadow:none;border-radius:0;padding:0}.store-name{font-size:18px}.items-table td,.totals-table td,.payment-table td{font-size:12px}.items-table thead th{font-size:11px}.doc-title{font-size:18px}.totals-table .total-row td{font-size:15px}.policy{font-size:11px}.soon{font-size:15px}.legal{font-size:9px}.footer-gen{font-size:10px}}")
                .AppendLine("</style>")
                .AppendLine(printScript)
                .AppendLine("</head><body><div class='wrap'><div class='paper'>");

            // ── 1. Encabezado empresa ──
            if (HasStoreInfo)
            {
                html.AppendLine("<div class='store-header'>")
                    .AppendLine($"<div class='store-name'>{Esc(StoreName)}</div>")
                    .AppendLine("<div class='store-info'>");
                if (!string.IsNullOrWhiteSpace(CedulaJuridica))
                    html.AppendLine($"Cédula Jurídica: {Esc(CedulaJuridica)}<br>");
                html.AppendLine($"Terminal: {RegisterNumber} Caja: {RegisterNumber:000}<br>");
                if (!string.IsNullOrWhiteSpace(StorePhone))
                    html.AppendLine($"Teléfono: {Esc(StorePhone)}<br>");
                if (!string.IsNullOrWhiteSpace(StoreAddress))
                    html.AppendLine($"Dirección: {Esc(StoreAddress)}");
                html.AppendLine("</div></div>");
            }

            // ── 2. Título del documento ──
            html.AppendLine("<hr class='sep'>")
                .AppendLine($"<div class='doc-title'>{Esc(DocumentTypeName)}</div>");

            // ── 3. Datos fiscales ──
            if (HasFiscalData)
            {
                html.AppendLine("<div class='fiscal'>")
                    .AppendLine($"<span class='fiscal-label'>Clave:</span> {Esc(Clave50)}<br>")
                    .AppendLine($"<span class='fiscal-label'>No.</span> {Esc(Consecutivo)}<br>")
                    .AppendLine($"<span class='fiscal-label'>Fecha Emisión:</span> {Esc(TransactionDate)}")
                    .AppendLine("</div>");
            }

            // ── 4. Datos del cliente ──
            html.AppendLine("<div class='client-block'>")
                .AppendLine($"<strong>Cliente:</strong> {Esc(ClientName)}<br>")
                .AppendLine($"Identificación: {Esc(ClientId)}")
                .AppendLine("</div>")
                .AppendLine("<hr class='sep'>");

            // ── 5. Tabla de artículos ──
            html.AppendLine("<table class='items-table'><thead><tr>")
                .AppendLine("<th class='num'>No.</th><th>Concepto</th><th class='num'>Cant.</th><th class='num'>Precio/U</th><th class='num'>Total</th>")
                .AppendLine("</tr></thead><tbody>")
                .Append(rows)
                .AppendLine("</tbody></table>");

            // ── 6. Totales ──
            html.AppendLine("<table class='totals-table'>")
                .AppendLine($"<tr><td>Servicios Gravados</td><td>+ CRC</td><td>{Esc(SubtotalText)}</td></tr>")
                .AppendLine(discountRow)
                .AppendLine(exonRow)
                .AppendLine("<tr><td colspan='3'><hr class='sep-double'></td></tr>")
                .AppendLine($"<tr><td>IVA (13.00%)</td><td>+ CRC</td><td>{Esc(TaxText)}</td></tr>")
                .AppendLine($"<tr class='total-row'><td>Total</td><td>+ CRC</td><td>{Esc(TotalColonesText)}</td></tr>")
                .AppendLine("</table>");

            // ── 7. Forma de pago ──
            html.AppendLine("<hr class='sep'>")
                .AppendLine("<table class='payment-table'>")
                .AppendLine($"<tr><td>{Esc(TenderEntregadoText)}</td><td>{Esc(TenderTotalText)}</td></tr>");

            if (HasSecondTender)
                html.AppendLine($"<tr><td>{Esc(SecondTenderEntregadoText)}</td><td>{Esc(SecondTenderAmountText)}</td></tr>");

            if (HasChange)
                html.AppendLine($"<tr class='pay-change'><td>CAMBIO</td><td>{Esc(ChangeAmountText)}</td></tr>");

            html.AppendLine("</table>");

            // ── 8. Políticas ──
            html.AppendLine("<div class='policy-sep'>- - - - - - - - - - - - - - - - - - - - - - - -</div>")
                .AppendLine("<div class='policy'>No se cambia ropa<br>No se aceptan devoluciones sin factura<br>No se aceptan devoluciones después de 45 días</div>")
                .AppendLine("<div class='policy-sep'>- - - - - - - - - - - - - - - - - - - - - - - -</div>")
                .AppendLine("<div class='soon'>¡Le Esperamos Pronto!</div>");

            // ── 9. Legal ──
            html.AppendLine("<div class='legal'>Documento emitido conforme lo establecido en la resolución de<br>Factura Electrónica. N° DGT-R-053-2019 del veinte de junio de<br>dosmil diecinueve de la Dirección General de Tributación.</div>");

            // ── 10. Footer ──
            html.AppendLine("<div class='footer-gen'>Comprobante v4.4 generado por NovaRetail POS</div>");

            html.AppendLine("</div></div></body></html>");

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
