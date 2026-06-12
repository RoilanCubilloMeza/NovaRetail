using System.Globalization;
using System.Net;
using System.Text;

namespace NovaRetail.Services;

public static class ReceiptRenderer
{
    private static readonly CultureInfo MoneyCulture = CultureInfo.GetCultureInfo("en-US");

    public static string BuildText(Receipt receipt, int width = 48)
    {
        var text = new StringBuilder();
        var line = new string('=', width);
        var dash = new string('-', width);

        text.AppendLine(Center(receipt.Company.Name, width));
        if (!string.IsNullOrWhiteSpace(receipt.Company.LegalId))
            text.AppendLine(Center($"Cedula: {receipt.Company.LegalId}", width));
        if (!string.IsNullOrWhiteSpace(receipt.Company.Address))
            AppendWrappedCentered(text, receipt.Company.Address, width);
        if (!string.IsNullOrWhiteSpace(receipt.Company.Phone))
            text.AppendLine(Center($"Tel: {receipt.Company.Phone}", width));

        text.AppendLine(line);
        text.AppendLine(Center(receipt.DocumentType, width));
        text.AppendLine($"Documento: {receipt.InvoiceNumber}");
        text.AppendLine($"Fecha:     {receipt.Date:dd/MM/yyyy HH:mm:ss}");
        text.AppendLine($"Caja: {receipt.Register}   Cajero: {receipt.Cashier}");

        if (!string.IsNullOrWhiteSpace(receipt.Customer.Name))
            text.AppendLine($"Cliente:   {receipt.Customer.Name}");
        if (!string.IsNullOrWhiteSpace(receipt.Customer.Id))
            text.AppendLine($"ID:        {receipt.Customer.Id}");

        text.AppendLine(dash);
        text.AppendLine(Columns("ARTICULO / CANTIDAD", "TOTAL", width));
        text.AppendLine(dash);

        foreach (var item in receipt.Items)
        {
            foreach (var nameLine in WrapText(ItemName(item), width))
                text.AppendLine(nameLine);

            text.AppendLine(Columns(
                $"  {Number(item.Quantity)} x {Currency(receipt, item.UnitPrice)}",
                Currency(receipt, item.LineTotal),
                width));

            if (item.Discount > 0)
                text.AppendLine(Columns("  Descuento", $"-{Currency(receipt, item.Discount)}", width));

            if (item.Tax > 0)
                text.AppendLine(Columns($"  Impuesto {Number(item.TaxRate)}%", Currency(receipt, item.Tax), width));
        }

        text.AppendLine(dash);
        text.AppendLine(Columns("SUBTOTAL:", Currency(receipt, receipt.Subtotal), width));
        if (receipt.Discount > 0)
            text.AppendLine(Columns("DESCUENTOS:", $"-{Currency(receipt, receipt.Discount)}", width));
        text.AppendLine(Columns("IMPUESTOS:", Currency(receipt, receipt.Tax), width));
        text.AppendLine(line);
        text.AppendLine(Columns("TOTAL:", Currency(receipt, receipt.Total), width));
        text.AppendLine(line);

        foreach (var payment in receipt.Payments)
            text.AppendLine(Columns($"{payment.Method}:", Currency(receipt, payment.Amount), width));
        if (receipt.Change > 0)
            text.AppendLine(Columns("CAMBIO:", Currency(receipt, receipt.Change), width));

        text.AppendLine();
        text.AppendLine($"Articulos distintos: {receipt.Items.Count}");
        text.AppendLine($"Unidades:             {Number(receipt.Items.Sum(item => item.Quantity))}");

        if (!string.IsNullOrWhiteSpace(receipt.ElectronicKey))
        {
            text.AppendLine(dash);
            text.AppendLine("Clave electronica:");
            foreach (var keyLine in WrapFixed(receipt.ElectronicKey, width))
                text.AppendLine(keyLine);
        }

        text.AppendLine(dash);
        foreach (var footerLine in receipt.FooterLines)
            AppendWrappedCentered(text, footerLine, width);
        if (!string.IsNullOrWhiteSpace(receipt.FiscalNotice))
            AppendWrappedCentered(text, receipt.FiscalNotice, width);

        return text.ToString();
    }

    public static string BuildHtml(Receipt receipt, bool autoPrint = false)
    {
        var rows = new StringBuilder();
        foreach (var item in receipt.Items)
        {
            rows.Append("<tr><td>")
                .Append(Esc(ItemName(item)))
                .Append("<small>")
                .Append(Esc($"{Number(item.Quantity)} x {Currency(receipt, item.UnitPrice)}"));

            if (item.Discount > 0)
                rows.Append("<br><span class='discount'>Descuento -").Append(Esc(Currency(receipt, item.Discount))).Append("</span>");
            if (item.Tax > 0)
                rows.Append("<br>Impuesto ").Append(Esc(Number(item.TaxRate))).Append("%: ").Append(Esc(Currency(receipt, item.Tax)));

            rows.Append("</small></td><td>")
                .Append(Esc(Currency(receipt, item.LineTotal)))
                .Append("</td></tr>");
        }

        var footer = string.Join("<br>", receipt.FooterLines.Select(Esc));
        var printScript = autoPrint ? "<script>window.onload=()=>window.print();</script>" : string.Empty;

        return $$$"""
            <!doctype html>
            <html lang="es">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{{Esc(receipt.DocumentType)}}} {{{Esc(receipt.InvoiceNumber)}}}</title>
              <style>
                :root{color-scheme:light;font-family:Inter,Segoe UI,Arial,sans-serif;background:#e9eef3;color:#101820}
                *{box-sizing:border-box}body{margin:0;min-height:100vh;background:radial-gradient(circle at top,#f8fafc 0,#e9eef3 52%,#dde5ec 100%)}
                .toolbar{position:sticky;top:0;z-index:10;display:flex;align-items:center;justify-content:space-between;gap:18px;padding:14px 24px;background:#fffffff2;border-bottom:1px solid #d6dee6;box-shadow:0 4px 20px #10203012;backdrop-filter:blur(12px)}
                .toolbar h1{margin:0;font-size:17px}.toolbar p{margin:3px 0 0;color:#657483;font-size:12px}.actions{display:flex;gap:8px}.actions button{border:0;border-radius:9px;padding:10px 16px;background:#087f5b;color:#fff;font-weight:800;cursor:pointer}.actions button:hover{background:#066b4d}
                main{display:grid;place-items:start center;padding:38px 14px 72px}.receipt{position:relative;width:min(420px,100%);padding:32px 28px 38px;overflow:hidden;background:#fff;box-shadow:0 20px 42px #1f2d3a2b;font:12px/1.45 Consolas,"Courier New",monospace}
                .receipt:before,.receipt:after{position:absolute;left:0;width:100%;height:8px;content:"";background:linear-gradient(135deg,transparent 5px,#fff 0) 0 0/10px 8px repeat-x}.receipt:before{top:-1px;transform:rotate(180deg)}.receipt:after{bottom:-1px}
                .badge{display:table;margin:9px auto 0;padding:4px 9px;border:1px solid #101820;border-radius:3px;font-size:9px;font-weight:800;letter-spacing:1px}.muted{color:#596673}
                h2,p{margin:0;text-align:center}.store{margin:0;text-align:center;font:800 25px/1.1 Inter,Segoe UI,Arial,sans-serif;letter-spacing:-.8px}.rule{border-top:1px dashed #101820;margin:14px 0}
                .meta,.totals{display:grid;grid-template-columns:1fr auto;gap:5px 14px}.meta strong,.meta span:nth-child(even){text-align:right}.items{width:100%;border-collapse:collapse;table-layout:fixed}.items th,.items td{padding:8px 0;text-align:left;vertical-align:top}.items th{border-bottom:1px solid #101820;font-size:10px;letter-spacing:.6px}.items th:last-child,.items td:last-child{width:96px;text-align:right}.items td:first-child{padding-right:9px}.items small{display:block;margin-top:2px;color:#596673;font-size:10px}.discount{color:#b42318}.total-band{margin:7px -8px;padding:8px;border-block:2px solid #101820;font-size:15px;font-weight:900}.key-title{margin-top:12px;font-size:9px;font-weight:800;letter-spacing:.6px}.key{margin-top:3px;overflow-wrap:anywhere;color:#596673;font-size:9px}.footer{margin-top:15px;text-align:center;font-weight:700}.notice{margin-top:5px;text-align:center;color:#596673;font-size:10px}
                @media(max-width:560px){.toolbar{align-items:flex-start;flex-direction:column;padding:12px 15px}.actions{width:100%}.actions button{width:100%}main{padding:20px 8px 50px}.receipt{padding:28px 20px 34px}}
                @media print{@page{size:80mm auto;margin:3mm}body{background:#fff}.toolbar{display:none}main{display:block;padding:0}.receipt{width:74mm;padding:0;box-shadow:none}.receipt:before,.receipt:after{display:none}}
              </style>
              {{{printScript}}}
            </head>
            <body>
            <header class="toolbar">
              <div><h1>Vista previa del comprobante</h1><p>{{{Esc(receipt.DocumentType)}}} &middot; {{{Esc(receipt.InvoiceNumber)}}}</p></div>
              <div class="actions"><button type="button" onclick="window.print()">Imprimir vista</button></div>
            </header>
            <main><article class="receipt">
              <h1 class="store">{{{Esc(receipt.Company.Name)}}}</h1>
              <p>{{{Esc(receipt.Company.LegalId)}}}</p>
              <p class="muted">{{{Esc(receipt.Company.Address)}}}<br>{{{Esc(receipt.Company.Phone)}}}</p>
              <div class="badge">{{{Esc(receipt.DocumentType)}}}</div>
              <div class="rule"></div>
              <div class="meta">
                <span>Documento</span><strong>{{{Esc(receipt.InvoiceNumber)}}}</strong>
                <span>Fecha</span><span>{{{receipt.Date:dd/MM/yyyy HH:mm:ss}}}</span>
                <span>Caja / Cajero</span><span>{{{Esc(receipt.Register)}}} / {{{Esc(receipt.Cashier)}}}</span>
                <span>Cliente</span><span>{{{Esc(receipt.Customer.Name)}}}</span>
              </div>
              <div class="rule"></div>
              <table class="items"><thead><tr><th>ARTICULO</th><th>TOTAL</th></tr></thead><tbody>{{{rows}}}</tbody></table>
              <div class="rule"></div>
              <section class="totals">
                <span>SUBTOTAL</span><span>{{{Esc(Currency(receipt, receipt.Subtotal))}}}</span>
                <span>DESCUENTOS</span><span>-{{{Esc(Currency(receipt, receipt.Discount))}}}</span>
                <span>IMPUESTOS</span><span>{{{Esc(Currency(receipt, receipt.Tax))}}}</span>
                <strong class="total-band">TOTAL</strong><strong class="total-band">{{{Esc(Currency(receipt, receipt.Total))}}}</strong>
                <span>PAGADO</span><span>{{{Esc(Currency(receipt, receipt.Paid))}}}</span>
                <span>CAMBIO</span><span>{{{Esc(Currency(receipt, receipt.Change))}}}</span>
              </section>
              <div class="rule"></div>
              <p class="key-title">CLAVE ELECTRONICA</p>
              <p class="key">{{{Esc(receipt.ElectronicKey)}}}</p>
              <p class="footer">{{{footer}}}</p>
              <p class="notice">{{{Esc(receipt.FiscalNotice)}}}</p>
            </article></main></body></html>
            """;
    }

    public static string QrData(Receipt receipt) =>
        string.IsNullOrWhiteSpace(receipt.ElectronicKey)
            ? $"{receipt.DocumentType}|{receipt.InvoiceNumber}|{receipt.Currency} {Money(receipt.Total)}"
            : receipt.ElectronicKey;

    private static string ItemName(ReceiptItem item) =>
        string.IsNullOrWhiteSpace(item.Code) ? item.Name : $"{item.Code} - {item.Name}";

    private static string Currency(Receipt receipt, decimal amount) => $"{receipt.Currency} {Money(amount)}";

    private static string Money(decimal amount) =>
        amount.ToString(amount == decimal.Truncate(amount) ? "N0" : "N2", MoneyCulture);

    private static string Number(decimal value) =>
        value.ToString(value == decimal.Truncate(value) ? "N0" : "N3", MoneyCulture);

    private static string Center(string value, int width)
    {
        var fitted = Fit(value, width);
        var leftPadding = Math.Max(0, (width - fitted.Length) / 2);
        return fitted.PadLeft(fitted.Length + leftPadding);
    }

    private static string Columns(string left, string right, int width)
    {
        var availableLeft = Math.Max(1, width - right.Length - 1);
        return $"{Fit(left, availableLeft).PadRight(availableLeft)} {right}";
    }

    private static string Fit(string value, int width) =>
        value.Length <= width ? value : value[..width];

    private static IEnumerable<string> WrapText(string value, int width)
    {
        var remaining = value.Trim();
        while (remaining.Length > width)
        {
            var breakAt = remaining.LastIndexOf(' ', width);
            if (breakAt <= 0)
                breakAt = width;

            yield return remaining[..breakAt].TrimEnd();
            remaining = remaining[breakAt..].TrimStart();
        }

        if (remaining.Length > 0)
            yield return remaining;
    }

    private static IEnumerable<string> WrapFixed(string value, int width)
    {
        for (var index = 0; index < value.Length; index += width)
            yield return value.Substring(index, Math.Min(width, value.Length - index));
    }

    private static void AppendWrappedCentered(StringBuilder text, string value, int width)
    {
        foreach (var line in WrapText(value, width))
            text.AppendLine(Center(line, width));
    }

    private static string Esc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
