using NovaRetail.Models;
using System.Globalization;
using System.Net;
using System.Text;

namespace NovaRetail.Services;

/// <summary>
/// Genera HTML con layout de ticket térmico 80 mm (≈42 columnas monoespaciadas)
/// siguiendo el formato costarricense de Tiquete Electrónico.
/// De momento se imprime vía PDF/HTML; preparado para migración a ESC/POS.
/// </summary>
public static class TicketReceiptBuilder
{
    /// <summary>Datos necesarios para generar el ticket.</summary>
    public sealed class TicketData
    {
        // Encabezado empresa
        public string CompanyName { get; init; } = string.Empty;
        public string CedulaJuridica { get; init; } = string.Empty;
        public string StoreName { get; init; } = string.Empty;
        public string StoreAddress { get; init; } = string.Empty;
        public string StorePhone { get; init; } = string.Empty;
        public int TerminalNumber { get; init; } = 1;
        public int RegisterNumber { get; init; } = 1;

        // Tiquete Electrónico
        public string Clave50 { get; init; } = string.Empty;
        public string Consecutivo { get; init; } = string.Empty;
        public DateTime EmissionDate { get; init; } = DateTime.Now;

        // Cliente
        public string ClientName { get; init; } = string.Empty;
        public string ClientId { get; init; } = string.Empty;
        public string ClientEmail { get; init; } = string.Empty;

        // Artículos
        public IReadOnlyList<TicketLineItem> Items { get; init; } = Array.Empty<TicketLineItem>();

        // Totales
        public decimal ServiciosGravados { get; init; }
        public decimal DescuentoTotal { get; init; }
        public IReadOnlyList<TicketTaxBreakdown> TaxBreakdowns { get; init; } = Array.Empty<TicketTaxBreakdown>();
        public decimal Total { get; init; }

        // Forma de pago
        public string TenderDescription { get; init; } = string.Empty;
        public decimal TenderAmount { get; init; }
        public decimal ChangeAmount { get; init; }

        // Comprobante tipo
        public string ComprobanteTipo { get; init; } = "04";
    }

    public sealed class TicketLineItem
    {
        public int LineNumber { get; init; }
        public string Description { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal LineTotal { get; init; }

        // Descuento por línea (se muestra como -DR)
        public bool HasDiscount { get; init; }
        public string DiscountDescription { get; init; } = string.Empty;
        public decimal DiscountAmount { get; init; }
    }

    public sealed class TicketTaxBreakdown
    {
        public decimal TaxPercentage { get; init; }
        public decimal TaxAmount { get; init; }
    }

    /// <summary>
    /// Construye un <see cref="TicketData"/> a partir del estado actual del recibo.
    /// </summary>
    public static TicketData BuildFromReceipt(
        string companyName,
        string cedulaJuridica,
        string storeName,
        string storeAddress,
        string storePhone,
        int terminalNumber,
        int registerNumber,
        string clave50,
        string consecutivo,
        string clientName,
        string clientId,
        string clientEmail,
        IEnumerable<CartItemModel> cartItems,
        decimal subtotalColones,
        decimal discountColones,
        decimal totalColones,
        string tenderDescription,
        decimal tenderAmountColones,
        decimal changeColones,
        string comprobanteTipo,
        int taxSystem)
    {
        var lineItems = new List<TicketLineItem>();
        var taxGroups = new Dictionary<decimal, decimal>();
        int lineNo = 0;

        foreach (var item in cartItems)
        {
            lineNo++;
            var grossUnit = item.EffectivePriceColones;
            var grossLine = grossUnit * item.Quantity;
            var discountFactor = 1m - item.DiscountPercent / 100m;
            var netLine = Math.Round(grossLine * discountFactor, 2);
            var discountAmt = Math.Round(grossLine - netLine, 2);

            lineItems.Add(new TicketLineItem
            {
                LineNumber = lineNo,
                Description = item.DisplayName,
                Quantity = item.Quantity,
                UnitPrice = Math.Round(grossUnit, 2),
                LineTotal = Math.Round(grossLine, 2),
                HasDiscount = discountAmt > 0,
                DiscountDescription = item.HasDiscount
                    ? $"{item.DisplayName}"
                    : string.Empty,
                DiscountAmount = discountAmt
            });

            // Agrupar impuestos por porcentaje
            var taxPct = item.EffectiveTaxPercentage;
            if (taxPct > 0)
            {
                var baseForTax = taxSystem == 1
                    ? netLine / (1m + taxPct / 100m)
                    : netLine;
                var taxAmt = Math.Round(baseForTax * (taxPct / 100m), 2);

                if (taxGroups.ContainsKey(taxPct))
                    taxGroups[taxPct] += taxAmt;
                else
                    taxGroups[taxPct] = taxAmt;
            }
        }

        var breakdowns = taxGroups
            .OrderByDescending(g => g.Key)
            .Select(g => new TicketTaxBreakdown
            {
                TaxPercentage = g.Key,
                TaxAmount = Math.Round(g.Value, 2)
            })
            .ToList();

        return new TicketData
        {
            CompanyName = companyName,
            CedulaJuridica = cedulaJuridica,
            StoreName = storeName,
            StoreAddress = storeAddress,
            StorePhone = storePhone,
            TerminalNumber = terminalNumber,
            RegisterNumber = registerNumber,
            Clave50 = clave50,
            Consecutivo = consecutivo,
            EmissionDate = DateTime.Now,
            ClientName = clientName,
            ClientId = clientId,
            ClientEmail = clientEmail,
            Items = lineItems,
            ServiciosGravados = Math.Round(subtotalColones, 2),
            DescuentoTotal = Math.Round(discountColones, 2),
            TaxBreakdowns = breakdowns,
            Total = Math.Round(totalColones, 2),
            TenderDescription = tenderDescription,
            TenderAmount = Math.Round(tenderAmountColones, 2),
            ChangeAmount = Math.Round(changeColones, 2),
            ComprobanteTipo = comprobanteTipo
        };
    }

    /// <summary>Genera HTML con layout de ticket térmico 80 mm.</summary>
    public static string BuildTicketHtml(TicketData data, bool autoPrint = true)
    {
        var sb = new StringBuilder(4096);

        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1'>");
        sb.AppendLine($"<title>Ticket #{Esc(data.Consecutivo)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(TicketCss);
        sb.AppendLine("</style>");
        if (autoPrint)
            sb.AppendLine("<script>window.onload=function(){window.print();}</script>");
        sb.AppendLine("</head><body><div class='ticket'>");

        // ── Encabezado empresa ──
        sb.AppendLine("<div class='header'>");
        sb.AppendLine($"<div class='company'>{Esc(data.CompanyName)}</div>");
        if (!string.IsNullOrWhiteSpace(data.CedulaJuridica))
            sb.AppendLine($"<div class='sub'>Cédula Jurídica: {Esc(data.CedulaJuridica)}</div>");
        sb.AppendLine($"<div class='sub'>Terminal: {data.TerminalNumber} Caja: {data.RegisterNumber:000}</div>");
        if (!string.IsNullOrWhiteSpace(data.StorePhone))
            sb.AppendLine($"<div class='sub'>Teléfono: {Esc(data.StorePhone)}</div>");
        sb.AppendLine("</div>");

        // ── Tiquete Electrónico ──
        sb.AppendLine("<div class='sep-bold'></div>");
        sb.AppendLine("<div class='te-title'>Tiquete Electrónico</div>");
        if (!string.IsNullOrWhiteSpace(data.Clave50))
            sb.AppendLine($"<div class='te-row'>Clave: {Esc(data.Clave50)}</div>");
        if (!string.IsNullOrWhiteSpace(data.Consecutivo))
            sb.AppendLine($"<div class='te-row'>No. {Esc(data.Consecutivo)}</div>");
        sb.AppendLine($"<div class='te-row'>Fecha Emisión: {data.EmissionDate:dd/MM/yyyy HH:mm}</div>");

        // ── Cliente ──
        if (!string.IsNullOrWhiteSpace(data.ClientName) && !data.ClientName.Equals("CLIENTE CONTADO", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("<div class='sep-thin'></div>");
            sb.AppendLine($"<div class='cli-name'>Cliente: {Esc(data.ClientName)}</div>");
            if (!string.IsNullOrWhiteSpace(data.ClientId))
                sb.AppendLine($"<div class='cli-row'>Identificación: {Esc(data.ClientId)}</div>");
            if (!string.IsNullOrWhiteSpace(data.ClientEmail))
                sb.AppendLine($"<div class='cli-row'>E-mail: {Esc(data.ClientEmail)}</div>");
        }

        // ── Líneas de artículos ──
        sb.AppendLine("<div class='sep-bold'></div>");
        sb.AppendLine("<table class='items'>");
        sb.AppendLine("<thead><tr><th class='no'>No.</th><th class='desc'>Concept.</th><th class='qty'>Qt. Un.</th><th class='price'>Precio/U</th><th class='total'>Total</th></tr></thead>");
        sb.AppendLine("<tbody>");
        sb.AppendLine("<tr><td colspan='5'><div class='line-under'></div></td></tr>");

        foreach (var item in data.Items)
        {
            sb.Append("<tr class='item'>");
            sb.Append($"<td class='no'>{item.LineNumber}</td>");
            sb.Append($"<td class='desc'>{Esc(TruncateDesc(item.Description, 24))}</td>");
            sb.Append($"<td class='qty'>{item.Quantity:0.00}</td>");
            sb.Append($"<td class='price'>{item.UnitPrice:N2}</td>");
            sb.Append($"<td class='total'>{item.LineTotal:N2}</td>");
            sb.AppendLine("</tr>");

            if (item.HasDiscount && item.DiscountAmount > 0)
            {
                sb.Append("<tr class='dr'>");
                sb.Append("<td></td>");
                sb.Append($"<td class='desc dr-label'>-DR {Esc(TruncateDesc(item.DiscountDescription, 20))}</td>");
                sb.Append("<td></td>");
                sb.Append($"<td class='price dr-amt'>ẟ</td>");
                sb.Append($"<td class='total dr-amt'>{item.DiscountAmount:N2}</td>");
                sb.AppendLine("</tr>");
            }
        }

        sb.AppendLine("</tbody></table>");

        // ── Totales ──
        sb.AppendLine("<div class='sep-thin'></div>");
        sb.AppendLine("<table class='totals'>");
        sb.AppendLine($"<tr><td>Servicios Gravados</td><td class='amt'>+ CRC</td><td class='val'>{data.ServiciosGravados:N2}</td></tr>");

        if (data.DescuentoTotal > 0)
            sb.AppendLine($"<tr class='disc-row'><td>Descuento</td><td class='amt'>- CRC</td><td class='val'>- {data.DescuentoTotal:N2}</td></tr>");

        sb.AppendLine("<tr><td colspan='3'><div class='sep-dash'></div></td></tr>");

        foreach (var tax in data.TaxBreakdowns)
        {
            var rateLabel = tax.TaxPercentage == Math.Floor(tax.TaxPercentage)
                ? $"{tax.TaxPercentage:0.00}"
                : $"{tax.TaxPercentage:0.00}";
            sb.AppendLine($"<tr><td>IVA ({rateLabel}%)</td><td class='amt'>+ CRC</td><td class='val'>{tax.TaxAmount:N2}</td></tr>");
        }

        sb.AppendLine("<tr><td colspan='3'><div class='sep-dash'></div></td></tr>");
        sb.AppendLine($"<tr class='total-row'><td><strong>Total</strong></td><td class='amt'>+ CRC</td><td class='val'><strong>{data.Total:N2}</strong></td></tr>");
        sb.AppendLine("</table>");

        // ── Monto en letras ──
        var totalLetras = NumberToSpanishWords(data.Total);
        sb.AppendLine($"<div class='letras'>*{Esc(totalLetras)}*</div>");

        // ── Texto legal ──
        sb.AppendLine("<div class='legal'>");
        sb.AppendLine("Documento emitido conforme lo establecido en la resolución de Factura Electrónica. ");
        sb.AppendLine("N° DGT-R-033-2019 del veinte de junio de dosmil diecinueve de la Dirección General de Tributación.");
        sb.AppendLine("</div>");

        // ── Pie ──
        sb.AppendLine("<div class='footer'>");
        sb.AppendLine("Comprobante v4.4 generado por NovaRetail POS");
        sb.AppendLine("</div>");

        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    /// <summary>Genera texto plano ESC/POS-ready (42 columnas, sin comandos binarios aún).</summary>
    public static string BuildTicketText(TicketData data)
    {
        const int W = 42;
        var sep = new string('=', W);
        var dash = new string('-', W);
        var sb = new StringBuilder(2048);

        // Encabezado
        sb.AppendLine(Center(data.CompanyName, W));
        if (!string.IsNullOrWhiteSpace(data.CedulaJuridica))
            sb.AppendLine(Center($"Cédula Jurídica: {data.CedulaJuridica}", W));
        sb.AppendLine(Center($"Terminal: {data.TerminalNumber} Caja: {data.RegisterNumber:000}", W));
        if (!string.IsNullOrWhiteSpace(data.StorePhone))
            sb.AppendLine(Center($"Teléfono: {data.StorePhone}", W));
        sb.AppendLine(sep);

        // Tiquete Electrónico
        sb.AppendLine(Center("Tiquete Electrónico", W));
        if (!string.IsNullOrWhiteSpace(data.Clave50))
            sb.AppendLine($"Clave: {data.Clave50}");
        if (!string.IsNullOrWhiteSpace(data.Consecutivo))
            sb.AppendLine($"No. {data.Consecutivo}");
        sb.AppendLine($"Fecha Emisión: {data.EmissionDate:dd/MM/yyyy HH:mm}");

        // Cliente
        if (!string.IsNullOrWhiteSpace(data.ClientName) && !data.ClientName.Equals("CLIENTE CONTADO", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine(dash);
            sb.AppendLine($"Cliente: {data.ClientName}");
            if (!string.IsNullOrWhiteSpace(data.ClientId))
                sb.AppendLine($"Identificación: {data.ClientId}");
            if (!string.IsNullOrWhiteSpace(data.ClientEmail))
                sb.AppendLine($"E-mail: {data.ClientEmail}");
        }

        // Artículos
        sb.AppendLine(sep);
        sb.AppendLine($"{"No.",-4}{"Concept.",-18}{"Qt.",5} {"Precio",8} {"Total",8}");
        sb.AppendLine(dash);

        foreach (var item in data.Items)
        {
            var desc = TruncateDesc(item.Description, 18);
            sb.AppendLine($"{item.LineNumber,-4}{desc,-18}{item.Quantity,5:0.00} {item.UnitPrice,8:N2} {item.LineTotal,8:N2}");

            if (item.HasDiscount && item.DiscountAmount > 0)
            {
                var drDesc = TruncateDesc(item.DiscountDescription, 16);
                sb.AppendLine($"    -DR {drDesc,-16}     {"ẟ",8} {item.DiscountAmount,8:N2}");
            }
        }

        // Totales
        sb.AppendLine(dash);
        sb.AppendLine($"{"Servicios Gravados",-26}+ CRC {data.ServiciosGravados,10:N2}");
        if (data.DescuentoTotal > 0)
            sb.AppendLine($"{"Descuento",-26}- CRC   - {data.DescuentoTotal,8:N2}");
        sb.AppendLine(dash);

        foreach (var tax in data.TaxBreakdowns)
            sb.AppendLine($"{"IVA (" + tax.TaxPercentage.ToString("0.00") + "%)",-26}+ CRC {tax.TaxAmount,10:N2}");

        sb.AppendLine(sep);
        sb.AppendLine($"{"Total",-26}+ CRC {data.Total,10:N2}");
        sb.AppendLine(sep);

        // Monto en letras
        sb.AppendLine($"*{NumberToSpanishWords(data.Total)}*");

        // Legal
        sb.AppendLine();
        sb.AppendLine("Documento emitido conforme lo establecido");
        sb.AppendLine("en la resolución de Factura Electrónica.");
        sb.AppendLine("N° DGT-R-033-2019 del 20/06/2019");
        sb.AppendLine("Dirección General de Tributación.");
        sb.AppendLine();
        sb.AppendLine(Center("Comprobante v4.4", W));
        sb.AppendLine(Center("generado por NovaRetail POS", W));

        return sb.ToString();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string Esc(string text) => WebUtility.HtmlEncode(text);

    private static string Center(string text, int width)
        => text.Length >= width ? text : text.PadLeft((width + text.Length) / 2).PadRight(width);

    private static string TruncateDesc(string text, int max)
        => text.Length <= max ? text : text[..max];

    /// <summary>Convierte un monto en colones a palabras en español (formato CR).</summary>
    internal static string NumberToSpanishWords(decimal amount)
    {
        var intPart = (long)Math.Floor(Math.Abs(amount));
        var cents = (int)Math.Round((Math.Abs(amount) - intPart) * 100m);

        var words = IntegerToWords(intPart);
        var result = $"{words.ToUpperInvariant()} CON {cents:00}/100 CRC";
        return result;
    }

    private static string IntegerToWords(long n)
    {
        if (n == 0) return "CERO";
        if (n < 0) return "MENOS " + IntegerToWords(-n);

        var parts = new List<string>();

        if (n / 1_000_000 > 0)
        {
            var millions = n / 1_000_000;
            parts.Add(millions == 1 ? "UN MILLÓN" : IntegerToWords(millions) + " MILLONES");
            n %= 1_000_000;
        }

        if (n / 1_000 > 0)
        {
            var thousands = n / 1_000;
            parts.Add(thousands == 1 ? "MIL" : IntegerToWords(thousands) + " MIL");
            n %= 1_000;
        }

        if (n / 100 > 0)
        {
            var hundreds = n / 100;
            if (hundreds == 1 && n % 100 == 0)
                parts.Add("CIEN");
            else
                parts.Add(HundredsWords[(int)hundreds]);
            n %= 100;
        }

        if (n > 0)
        {
            if (n < 30)
                parts.Add(UnitsWords[(int)n]);
            else
            {
                var tens = (int)(n / 10);
                var units = (int)(n % 10);
                if (units == 0)
                    parts.Add(TensWords[tens]);
                else
                    parts.Add(TensWords[tens] + " Y " + UnitsWords[units]);
            }
        }

        return string.Join(" ", parts);
    }

    private static readonly string[] UnitsWords =
    {
        "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
        "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE",
        "DIECIOCHO", "DIECINUEVE", "VEINTE", "VEINTIUNO", "VEINTIDÓS", "VEINTITRÉS",
        "VEINTICUATRO", "VEINTICINCO", "VEINTISÉIS", "VEINTISIETE", "VEINTIOCHO", "VEINTINUEVE"
    };

    private static readonly string[] TensWords =
    {
        "", "", "", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"
    };

    private static readonly string[] HundredsWords =
    {
        "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS",
        "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"
    };

    // ── CSS ──────────────────────────────────────────────────────────────

    private const string TicketCss = @"
*{box-sizing:border-box;margin:0;padding:0}
html,body{background:#eee;font-family:'Courier New',Courier,monospace;font-size:13px;color:#000}
.ticket{width:80mm;max-width:100%;margin:10px auto;background:#fff;padding:8mm 6mm;
  box-shadow:0 2px 12px rgba(0,0,0,.15);border-radius:3px}

/* Encabezado */
.header{text-align:center;margin-bottom:4mm}
.company{font-size:16px;font-weight:900;letter-spacing:0.3px}
.sub{font-size:12px;line-height:1.5}

/* Separadores */
.sep-bold{border-top:2px solid #000;margin:3mm 0}
.sep-thin{border-top:1px solid #000;margin:2mm 0}
.sep-dash{border-top:1px dashed #999;margin:1.5mm 0}
.line-under{border-bottom:1px solid #000}

/* Tiquete Electrónico */
.te-title{text-align:center;font-size:15px;font-weight:900;margin:2mm 0 1.5mm}
.te-row{font-size:11px;line-height:1.5;word-break:break-all}

/* Cliente */
.cli-name{font-weight:900;font-size:12px;margin:1mm 0}
.cli-row{font-size:11px;line-height:1.5}

/* Tabla de artículos */
.items{width:100%;border-collapse:collapse;font-size:12px;margin:1mm 0}
.items th{text-align:left;font-weight:900;padding:1mm 0;font-size:11px;border-bottom:1px solid #000}
.items th.no{width:8%}
.items th.desc{width:38%}
.items th.qty{width:14%;text-align:right}
.items th.price{width:20%;text-align:right}
.items th.total{width:20%;text-align:right}
.items td{padding:0.8mm 0;vertical-align:top}
.items td.no{text-align:left}
.items td.qty,.items td.price,.items td.total{text-align:right}

/* Línea de descuento */
.dr td{font-size:11px}
.dr-label{font-weight:900;padding-left:2mm}
.dr-amt{font-weight:900}

/* Tabla de totales */
.totals{width:100%;border-collapse:collapse;font-size:12px;margin:1mm 0}
.totals td{padding:0.8mm 0}
.totals td.amt{text-align:right;width:20%;padding-right:2mm}
.totals td.val{text-align:right;width:25%;font-weight:700}
.disc-row td{color:#000}
.total-row td{font-size:14px}

/* Monto en letras */
.letras{font-size:10px;font-weight:900;margin:3mm 0 2mm;line-height:1.4;text-align:center}

/* Legal */
.legal{font-size:9px;line-height:1.5;margin:2mm 0;text-align:center}

/* Pie */
.footer{text-align:center;font-size:9px;color:#666;margin-top:3mm;padding-top:2mm;border-top:1px solid #ccc}

@media print{
  @page{size:80mm auto;margin:0}
  html,body{background:#fff}
  .ticket{width:100%;max-width:none;margin:0;padding:4mm 3mm;box-shadow:none;border-radius:0}
}
";
}
