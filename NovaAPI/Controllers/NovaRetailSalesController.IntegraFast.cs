using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using NovaAPI.Models;

namespace NovaAPI.Controllers
{
    public partial class NovaRetailSalesController
    {
        /// <summary>
        /// Lee AVS_INTEGRAFAST_02 y actualiza el request con COD_SUCURSAL, CEDULA_TRIBUTARIA,
        /// COMPROBANTE_INTERNO, TIPOCAMBIO y TERMINAL_POS corregidos.
        /// Debe llamarse ANTES de EnsureClaves para que CLAVE50/CLAVE20 usen los valores correctos.
        /// </summary>
        private static void ApplyIntegraFast02Config(SqlConnection cn, NovaRetailCreateSaleRequest request)
        {
            var comprobanteTipo = request.COMPROBANTE_TIPO ?? string.Empty;
            var consecutivoCol = GetConsecutivoColumnIntegraFast02(comprobanteTipo);
            var cedulaColumn = ColumnExists(cn, "AVS_INTEGRAFAST_02", "CEDULA") ? "CEDULA" : "PROVEEDOR_SISTEMA";

            if (consecutivoCol != null)
            {
                try
                {
                    using (var incCmd = new SqlCommand(
                        $"UPDATE dbo.AVS_INTEGRAFAST_02 SET {consecutivoCol} = {consecutivoCol} + 1 " +
                        $"OUTPUT INSERTED.{consecutivoCol} AS Consecutivo, INSERTED.COD_SUCURSAL, INSERTED.{cedulaColumn} AS CedulaTributaria", cn))
                    {
                        incCmd.CommandTimeout = 30;
                        using (var rd = incCmd.ExecuteReader())
                        {
                            if (rd.Read())
                            {
                                request.COMPROBANTE_INTERNO = Convert.ToInt32(rd["Consecutivo"]).ToString();
                                var suc = rd["COD_SUCURSAL"];
                                if (suc != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(suc)))
                                    request.COD_SUCURSAL = Convert.ToString(suc);

                                var ced = rd["CedulaTributaria"];
                                if (ced != DBNull.Value && !string.IsNullOrWhiteSpace(Convert.ToString(ced)))
                                    request.CedulaTributaria = Convert.ToString(ced);
                            }
                        }
                    }
                }
                catch
                {
                    // Tabla AVS_INTEGRAFAST_02 no disponible: se mantienen los valores del request.
                }
            }

            if (string.Equals(request.CurrencyCode, "CRC", StringComparison.OrdinalIgnoreCase))
                request.TipoCambio = "1";

            if (!string.IsNullOrWhiteSpace(request.TERMINAL_POS) && int.TryParse(request.TERMINAL_POS, out var terminalPosNum))
                request.TERMINAL_POS = terminalPosNum.ToString();
        }

        private static void EnsureTiqueteEspera(SqlConnection cn, NovaRetailCreateSaleRequest request, int transactionNumber)
        {
            using (var existsCmd = new SqlCommand("SELECT COUNT(1) FROM dbo.AVS_INTEGRAFAST_01 WHERE TRANSACTIONNUMBER = @TransactionNumber", cn))
            {
                existsCmd.Parameters.AddWithValue("@TransactionNumber", transactionNumber.ToString());
                if (Convert.ToInt32(existsCmd.ExecuteScalar()) > 0)
                    return;
            }

            var medioPagos = ResolveIntegraFastMedioPagos(cn, request.Tenders);
            while (medioPagos.Count < 4)
                medioPagos.Add(string.Empty);

            var codSucursal = request.COD_SUCURSAL ?? string.Empty;
            var cedulaTributaria = request.CedulaTributaria ?? string.Empty;
            var comprobanteInterno = string.IsNullOrWhiteSpace(request.COMPROBANTE_INTERNO)
                ? transactionNumber.ToString()
                : request.COMPROBANTE_INTERNO;
            var tipoCambio = request.TipoCambio ?? "1";
            var terminalPos = request.TERMINAL_POS ?? string.Empty;
            var comprobanteTipo = request.COMPROBANTE_TIPO ?? string.Empty;

            try
            {
                using (var cmd = new SqlCommand("dbo.spAVS_InsertTiqueteEspera", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 120;
                    cmd.Parameters.AddWithValue("@CLAVE50", request.CLAVE50 ?? string.Empty);
                    cmd.Parameters.AddWithValue("@CLAVE20", request.CLAVE20 ?? string.Empty);
                    cmd.Parameters.AddWithValue("@TRANSACTIONNUMBER", transactionNumber.ToString());
                    cmd.Parameters.AddWithValue("@COD_SUCURSAL", codSucursal);
                    cmd.Parameters.AddWithValue("@TERMINAL_POS", terminalPos);
                    cmd.Parameters.AddWithValue("@COMPROBANTE_INTERNO", comprobanteInterno);
                    cmd.Parameters.AddWithValue("@COMPROBANTE_SITUACION", request.COMPROBANTE_SITUACION ?? string.Empty);
                    cmd.Parameters.AddWithValue("@COMPROBANTE_TIPO", comprobanteTipo);
                    cmd.Parameters.AddWithValue("@CURRENCYCODE", request.CurrencyCode ?? "CRC");
                    cmd.Parameters.AddWithValue("@CONDICIONVENTA", request.CondicionVenta ?? "01");
                    cmd.Parameters.AddWithValue("@COD_CLIENTE", request.CodCliente ?? string.Empty);
                    cmd.Parameters.AddWithValue("@NOMBRE_CLIENTE", request.NombreCliente ?? string.Empty);
                    cmd.Parameters.AddWithValue("@MEDIO_PAGO1", medioPagos[0]);
                    cmd.Parameters.AddWithValue("@MEDIO_PAGO2", medioPagos[1]);
                    cmd.Parameters.AddWithValue("@MEDIO_PAGO3", medioPagos[2]);
                    cmd.Parameters.AddWithValue("@MEDIO_PAGO4", medioPagos[3]);
                    cmd.Parameters.AddWithValue("@TIPOCAMBIO", tipoCambio);
                    cmd.Parameters.AddWithValue("@CEDULA_TRIBUTARIA", cedulaTributaria);
                    cmd.Parameters.AddWithValue("@EXONERA", request.Exonera);
                    cmd.Parameters.AddWithValue("@NC_TIPO_DOC", request.NC_TIPO_DOC ?? string.Empty);
                    cmd.Parameters.AddWithValue("@NC_REFERENCIA", request.NC_REFERENCIA ?? string.Empty);
                    cmd.Parameters.AddWithValue("@NC_REFERENCIA_FECHA", (object)request.NC_REFERENCIA_FECHA ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@NC_CODIGO", request.NC_CODIGO ?? string.Empty);
                    cmd.Parameters.AddWithValue("@NC_RAZON", request.NC_RAZON ?? string.Empty);
                    cmd.Parameters.AddWithValue("@TR_REP", request.TR_REP ?? string.Empty);
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                InsertIntegraFast01Direct(
                    cn,
                    request,
                    transactionNumber,
                    medioPagos,
                    codSucursal,
                    terminalPos,
                    comprobanteInterno,
                    tipoCambio,
                    cedulaTributaria);
            }
        }

        private static void InsertIntegraFast01Direct(
            SqlConnection cn,
            NovaRetailCreateSaleRequest request,
            int transactionNumber,
            List<string> medioPagos,
            string codSucursal,
            string terminalPos,
            string comprobanteInterno,
            string tipoCambio,
            string cedulaTributaria)
        {
            using (var cmd = new SqlCommand(@"
                INSERT INTO dbo.AVS_INTEGRAFAST_01
                    (CLAVE50, CLAVE20, TRANSACTIONNUMBER, COD_SUCURSAL, TERMINAL_POS,
                     COMPROBANTE_INTERNO, COMPROBANTE_SITUACION, COMPROBANTE_TIPO,
                     COD_MONEDA, CONDICION_VENTA, COD_CLIENTE, NOMBRE_CLIENTE,
                     MEDIO_PAGO1, MEDIO_PAGO2, MEDIO_PAGO3, MEDIO_PAGO4,
                     TIPOCAMBIO, CEDULA_TRIBUTARIA, EXONERA,
                     NC_TIPO_DOC, NC_REFERENCIA, NC_REFERENCIA_FECHA, NC_CODIGO, NC_RAZON,
                     TR_REP, FECHA_TRANSAC, ESTADO_HACIENDA)
                VALUES
                    (@CLAVE50, @CLAVE20, @TN, @COD_SUCURSAL, @TERMINAL_POS,
                     @COMPROBANTE_INTERNO, @COMPROBANTE_SITUACION, @COMPROBANTE_TIPO,
                     @CURRENCYCODE, @CONDICIONVENTA, @COD_CLIENTE, @NOMBRE_CLIENTE,
                     @MEDIO_PAGO1, @MEDIO_PAGO2, @MEDIO_PAGO3, @MEDIO_PAGO4,
                     @TIPOCAMBIO, @CEDULA_TRIBUTARIA, @EXONERA,
                     @NC_TIPO_DOC, @NC_REFERENCIA, @NC_REFERENCIA_FECHA, @NC_CODIGO, @NC_RAZON,
                     @TR_REP, GETDATE(), '00')", cn))
            {
                cmd.CommandTimeout = 60;
                cmd.Parameters.AddWithValue("@CLAVE50", request.CLAVE50 ?? string.Empty);
                cmd.Parameters.AddWithValue("@CLAVE20", request.CLAVE20 ?? string.Empty);
                cmd.Parameters.AddWithValue("@TN", transactionNumber.ToString());
                cmd.Parameters.AddWithValue("@COD_SUCURSAL", codSucursal);
                cmd.Parameters.AddWithValue("@TERMINAL_POS", terminalPos);
                cmd.Parameters.AddWithValue("@COMPROBANTE_INTERNO", comprobanteInterno);
                cmd.Parameters.AddWithValue("@COMPROBANTE_SITUACION", request.COMPROBANTE_SITUACION ?? string.Empty);
                cmd.Parameters.AddWithValue("@COMPROBANTE_TIPO", request.COMPROBANTE_TIPO ?? string.Empty);
                cmd.Parameters.AddWithValue("@CURRENCYCODE", request.CurrencyCode ?? "CRC");
                cmd.Parameters.AddWithValue("@CONDICIONVENTA", request.CondicionVenta ?? "01");
                cmd.Parameters.AddWithValue("@COD_CLIENTE", request.CodCliente ?? string.Empty);
                cmd.Parameters.AddWithValue("@NOMBRE_CLIENTE", request.NombreCliente ?? string.Empty);
                cmd.Parameters.AddWithValue("@MEDIO_PAGO1", medioPagos[0]);
                cmd.Parameters.AddWithValue("@MEDIO_PAGO2", medioPagos[1]);
                cmd.Parameters.AddWithValue("@MEDIO_PAGO3", medioPagos[2]);
                cmd.Parameters.AddWithValue("@MEDIO_PAGO4", medioPagos[3]);
                cmd.Parameters.AddWithValue("@TIPOCAMBIO", tipoCambio);
                cmd.Parameters.AddWithValue("@CEDULA_TRIBUTARIA", cedulaTributaria);
                cmd.Parameters.AddWithValue("@EXONERA", request.Exonera);
                cmd.Parameters.AddWithValue("@NC_TIPO_DOC", request.NC_TIPO_DOC ?? string.Empty);
                cmd.Parameters.AddWithValue("@NC_REFERENCIA", request.NC_REFERENCIA ?? string.Empty);
                cmd.Parameters.AddWithValue("@NC_REFERENCIA_FECHA", (object)request.NC_REFERENCIA_FECHA ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NC_CODIGO", request.NC_CODIGO ?? string.Empty);
                cmd.Parameters.AddWithValue("@NC_RAZON", request.NC_RAZON ?? string.Empty);
                cmd.Parameters.AddWithValue("@TR_REP", request.TR_REP ?? string.Empty);
                cmd.ExecuteNonQuery();
            }
        }

        private static readonly HashSet<string> AllowedConsecutivoColumns =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CN_FE", "CN_ND", "CN_NC", "CN_TE", "CN_FEX" };

        private static string GetConsecutivoColumnIntegraFast02(string comprobanteTipo)
        {
            string col;
            switch (comprobanteTipo)
            {
                case "01": col = "CN_FE"; break;
                case "02": col = "CN_ND"; break;
                case "03": col = "CN_NC"; break;
                case "04": col = "CN_TE"; break;
                case "09": col = "CN_FEX"; break;
                default: return null;
            }

            return AllowedConsecutivoColumns.Contains(col) ? col : null;
        }

        private static bool ColumnExists(SqlConnection cn, string tableName, string columnName)
        {
            using (var cmd = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_NAME = @TableName
                    AND COLUMN_NAME = @ColumnName", cn))
            {
                cmd.Parameters.AddWithValue("@TableName", tableName);
                cmd.Parameters.AddWithValue("@ColumnName", columnName);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private sealed class TenderFiscalInfo
        {
            public string Code { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        private static List<string> ResolveIntegraFastMedioPagos(SqlConnection cn, IEnumerable<NovaRetailSaleTenderDto> tenders)
        {
            var orderedTenders = (tenders ?? Enumerable.Empty<NovaRetailSaleTenderDto>())
                .OrderBy(t => t.RowNo)
                .Take(4)
                .ToList();

            var tenderInfo = LoadTenderFiscalInfo(cn, orderedTenders.Select(t => t.TenderID));
            return orderedTenders
                .Select(t =>
                {
                    tenderInfo.TryGetValue(t.TenderID, out var info);
                    return ResolveIntegraFastMedioPagoCodigo(t, info);
                })
                .ToList();
        }

        private static Dictionary<int, TenderFiscalInfo> LoadTenderFiscalInfo(SqlConnection cn, IEnumerable<int> tenderIds)
        {
            var ids = (tenderIds ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var result = new Dictionary<int, TenderFiscalInfo>();
            if (ids.Count == 0)
                return result;

            var parameterNames = ids.Select((id, index) => $"@TenderID{index}").ToList();
            var sql = $"SELECT ID, ISNULL(Code, '') AS Code, ISNULL(Description, '') AS Description FROM dbo.Tender WHERE ID IN ({string.Join(", ", parameterNames)})";

            using (var cmd = new SqlCommand(sql, cn))
            {
                for (var index = 0; index < ids.Count; index++)
                    cmd.Parameters.AddWithValue(parameterNames[index], ids[index]);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result[Convert.ToInt32(reader["ID"])] = new TenderFiscalInfo
                        {
                            Code = reader["Code"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Code"]),
                            Description = reader["Description"] == DBNull.Value ? string.Empty : Convert.ToString(reader["Description"])
                        };
                    }
                }
            }

            return result;
        }

        private static string ResolveIntegraFastMedioPagoCodigo(NovaRetailSaleTenderDto tender, TenderFiscalInfo info)
        {
            if (!string.IsNullOrWhiteSpace(tender?.MedioPagoCodigo))
                return tender.MedioPagoCodigo.Trim();

            var description = info != null && !string.IsNullOrWhiteSpace(info.Description)
                ? info.Description
                : tender?.Description;

            if (IsCreditDescription(description))
                return "99";

            var tenderCode = info != null ? ExtractIntegraFastMedioPagoFromTenderCode(info.Code) : string.Empty;
            if (!string.IsNullOrWhiteSpace(tenderCode))
                return tenderCode;

            return InferIntegraFastMedioPagoCodigo(description);
        }

        private static string ExtractIntegraFastMedioPagoFromTenderCode(string tenderCode)
        {
            var value = (tenderCode ?? string.Empty).Trim();
            if (value.Length >= 2 && char.IsDigit(value[0]) && char.IsDigit(value[1]))
                return value.Substring(0, 2);

            return string.Empty;
        }

        private static string InferIntegraFastMedioPagoCodigo(string description)
        {
            var value = (description ?? string.Empty).Trim().ToUpperInvariant();
            if (value.Contains("EFECTIVO") || value.Contains("CONTADO"))
                return "01";
            if (value.Contains("TARJETA"))
                return "02";
            if (value.Contains("TRANSFER") || value.Contains("SINPE"))
                return "04";
            if (value.Contains("CRÉDITO") || value.Contains("CREDITO"))
                return "99";

            return string.Empty;
        }

        private static bool IsCreditDescription(string description)
        {
            var value = (description ?? string.Empty).Trim().ToUpperInvariant();
            return value.Contains("CRÃ‰DITO") || value.Contains("CREDITO");
        }

        private static void EnsureIntegraFast05(SqlConnection cn, NovaRetailCreateSaleRequest request, int transactionNumber)
        {
            if (request.Items == null || request.Items.Count == 0)
                return;

            string clave50;
            using (var cmd = new SqlCommand("SELECT TOP 1 CLAVE50 FROM dbo.AVS_INTEGRAFAST_01 WHERE TRANSACTIONNUMBER = @TN", cn))
            {
                cmd.Parameters.AddWithValue("@TN", transactionNumber.ToString());
                var val = cmd.ExecuteScalar();
                clave50 = val != null && val != DBNull.Value ? Convert.ToString(val) : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(clave50))
                return;

            using (var chk = new SqlCommand("SELECT COUNT(1) FROM dbo.AVS_INTEGRAFAST_05 WHERE CLAVE50 = @CLAVE50", cn))
            {
                chk.Parameters.AddWithValue("@CLAVE50", clave50);
                try
                {
                    if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                        return;
                }
                catch
                {
                    CreateIntegraFast05Table(cn);
                }
            }

            var taxSystem = GetTaxSystem(cn);
            var numLinea = 0;
            var itemInfoMap = new Dictionary<int, (string Cabys, string Code, string Description)>();
            var itemIds = request.Items.Select(i => i.ItemID).Distinct().ToList();

            if (itemIds.Count > 0)
            {
                var parameters = itemIds.Select((id, i) => $"@id{i}").ToList();
                var idList = string.Join(",", parameters);
                try
                {
                    using (var cmd = new SqlCommand(
                        $"SELECT ID, ISNULL(CAST(SubDescription3 AS NVARCHAR(20)),'') AS Cabys, ISNULL(ItemLookupCode,'') AS Code, ISNULL(Description,'') AS Desc1 FROM dbo.Item WHERE ID IN ({idList})", cn))
                    {
                        for (var pi = 0; pi < itemIds.Count; pi++)
                            cmd.Parameters.AddWithValue($"@id{pi}", itemIds[pi]);

                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                var id = Convert.ToInt32(r["ID"]);
                                var cabys = NormalizeCabys(r["Cabys"]?.ToString());
                                var code = r["Code"]?.ToString() ?? string.Empty;
                                var desc = r["Desc1"]?.ToString() ?? string.Empty;
                                itemInfoMap[id] = (cabys, code, desc);
                            }
                        }
                    }
                }
                catch
                {
                    // La tabla Item podría variar entre ambientes.
                }
            }

            if (UseSetBasedSalePostProcessing())
            {
                BulkInsertIntegraFast05Rows(cn, request, transactionNumber, clave50, itemInfoMap);
                return;
            }

            foreach (var item in request.Items.OrderBy(i => i.RowNo))
            {
                numLinea++;
                var qty = item.Quantity <= 0 ? 1m : item.Quantity;
                var unitPrice = item.UnitPrice;
                var fullPrice = item.FullPrice ?? unitPrice;
                var montoTotal = Math.Round(fullPrice * qty, 2);
                var montoDescuento = Math.Round(item.LineDiscountAmount, 2);
                if (montoDescuento == 0m && fullPrice > unitPrice)
                    montoDescuento = Math.Round((fullPrice - unitPrice) * qty, 2);

                var subTotal = montoTotal - montoDescuento;
                var montoImpuesto = Math.Round(item.SalesTax, 2);
                var baseTaxAmount = Math.Round(item.SalesTax + Math.Max(0m, item.ExMonto), 2);
                var baseTaxRate = baseTaxAmount > 0 && subTotal > 0
                    ? Math.Round(baseTaxAmount / subTotal * 100m, 2)
                    : 0m;
                var montoLinea = subTotal + montoImpuesto;
                var hasExoneration = !string.IsNullOrWhiteSpace(item.ExNumeroDoc);

                itemInfoMap.TryGetValue(item.ItemID, out var info);
                var cabys = info.Cabys ?? string.Empty;
                var codProducto = info.Code ?? item.ItemID.ToString();
                var detalle = !string.IsNullOrWhiteSpace(item.ExtendedDescription)
                    ? item.ExtendedDescription
                    : !string.IsNullOrWhiteSpace(info.Description)
                        ? info.Description
                        : item.ItemID.ToString();

                var codTarifaIVA = baseTaxRate > 0 ? ResolveIntegraFastTaxCode(baseTaxRate) : string.Empty;
                var naturalezaDescuento = montoDescuento > 0 ? (item.LineComment ?? "Descuento comercial") : string.Empty;
                var exoneraPorcentaje = item.ExPorcentaje;

                try
                {
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO dbo.AVS_INTEGRAFAST_05
                            (CLAVE50, TRANSACTIONNUMBER, NUM_LINEA, ID_PRODUCTO, CANTIDAD, UNIDAD_MEDIDA,
                             DETALLE, PRECIO_UNITARIO, MONTO_TOTAL, MONTO_DESCUENTO, NATURALEZA_DESCUENTO,
                             SUBTOTAL, COD_IMPUESTO, COD_IMPUESTO_BASE, TARIFA_IMPUESTO, MONTO_IMPUESTO,
                             EXONERA_TIPO_DOCUMENTO, EXONERA_NUMERO_DOCUMENTO, EXONERA_INSTITUCION,
                             EXONERA_FECHA_EMISION, EXONERA_MONTO_IMPUESTO, EXONERA_PORCENTAJE_COMPRA,
                             EXONERA_TOTAL_LINEA, SyncGuid, ARTICULO, INCISO)
                        VALUES
                            (@CLAVE50, @TN, @NUM_LINEA, @ID_PRODUCTO, @CANTIDAD, @UNIDAD_MEDIDA,
                             @DETALLE, @PRECIOUNIT, @MONTO_TOTAL, @MONTO_DESCUENTO, @NATURALEZA_DESCUENTO,
                             @SUBTOTAL, @COD_IMPUESTO, @COD_IMPUESTO_BASE, @TARIFA_IMPUESTO, @MONTO_IMPUESTO,
                             @EXONERA_TIPO_DOCUMENTO, @EXONERA_NUMERO_DOCUMENTO, @EXONERA_INSTITUCION,
                             @EXONERA_FECHA_EMISION, @EXONERA_MONTO_IMPUESTO, @EXONERA_PORCENTAJE_COMPRA,
                             @EXONERA_TOTAL_LINEA, NEWID(), @ARTICULO, @INCISO)", cn))
                    {
                        cmd.Parameters.AddWithValue("@CLAVE50", clave50);
                        cmd.Parameters.AddWithValue("@TN", transactionNumber.ToString());
                        cmd.Parameters.AddWithValue("@NUM_LINEA", numLinea);
                        cmd.Parameters.AddWithValue("@ID_PRODUCTO", Truncate(item.ItemID.ToString(), 15));
                        cmd.Parameters.AddWithValue("@CANTIDAD", qty);
                        cmd.Parameters.AddWithValue("@UNIDAD_MEDIDA", "Und");
                        cmd.Parameters.AddWithValue("@DETALLE", Truncate(detalle, 160));
                        cmd.Parameters.AddWithValue("@PRECIOUNIT", Math.Round(fullPrice, 5));
                        cmd.Parameters.AddWithValue("@MONTO_TOTAL", montoTotal);
                        cmd.Parameters.AddWithValue("@MONTO_DESCUENTO", montoDescuento);
                        cmd.Parameters.AddWithValue("@NATURALEZA_DESCUENTO", Truncate(naturalezaDescuento, 80));
                        cmd.Parameters.AddWithValue("@SUBTOTAL", subTotal);
                        cmd.Parameters.AddWithValue("@COD_IMPUESTO", codTarifaIVA);
                        cmd.Parameters.AddWithValue("@COD_IMPUESTO_BASE", baseTaxRate > 0 ? (object)codTarifaIVA : DBNull.Value);
                        cmd.Parameters.AddWithValue("@TARIFA_IMPUESTO", baseTaxRate > 0 ? baseTaxRate : 0m);
                        cmd.Parameters.AddWithValue("@MONTO_IMPUESTO", montoImpuesto);
                        cmd.Parameters.AddWithValue("@EXONERA_TIPO_DOCUMENTO", hasExoneration ? (object)Truncate(item.ExTipoDoc, 2) : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EXONERA_NUMERO_DOCUMENTO", hasExoneration ? (object)Truncate(item.ExNumeroDoc, 40) : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EXONERA_INSTITUCION", hasExoneration ? (object)Truncate(item.ExInstitucion, 100) : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EXONERA_FECHA_EMISION", hasExoneration && item.ExFecha.HasValue ? (object)item.ExFecha.Value.ToString("yyyy-MM-dd") : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EXONERA_MONTO_IMPUESTO", hasExoneration ? (object)item.ExMonto : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EXONERA_PORCENTAJE_COMPRA", hasExoneration ? (object)Convert.ToInt16(Math.Round(exoneraPorcentaje, 0, MidpointRounding.AwayFromZero)) : DBNull.Value);
                        cmd.Parameters.AddWithValue("@EXONERA_TOTAL_LINEA", hasExoneration ? (object)montoLinea : DBNull.Value);
                        cmd.Parameters.AddWithValue("@ARTICULO", Truncate(codProducto, 6));
                        cmd.Parameters.AddWithValue("@INCISO", Truncate(cabys, 6));
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"No se pudo insertar AVS_INTEGRAFAST_05 para ItemID {item.ItemID}.", ex);
                }
            }
        }

        private static void BulkInsertIntegraFast05Rows(
            SqlConnection cn,
            NovaRetailCreateSaleRequest request,
            int transactionNumber,
            string clave50,
            Dictionary<int, (string Cabys, string Code, string Description)> itemInfoMap)
        {
            var rows = new DataTable();
            rows.Columns.Add("CLAVE50", typeof(string));
            rows.Columns.Add("TRANSACTIONNUMBER", typeof(string));
            rows.Columns.Add("NUM_LINEA", typeof(int));
            rows.Columns.Add("ID_PRODUCTO", typeof(string));
            rows.Columns.Add("CANTIDAD", typeof(decimal));
            rows.Columns.Add("UNIDAD_MEDIDA", typeof(string));
            rows.Columns.Add("DETALLE", typeof(string));
            rows.Columns.Add("PRECIO_UNITARIO", typeof(decimal));
            rows.Columns.Add("MONTO_TOTAL", typeof(decimal));
            rows.Columns.Add("MONTO_DESCUENTO", typeof(decimal));
            rows.Columns.Add("NATURALEZA_DESCUENTO", typeof(string));
            rows.Columns.Add("SUBTOTAL", typeof(decimal));
            rows.Columns.Add("COD_IMPUESTO", typeof(string));
            rows.Columns.Add("COD_IMPUESTO_BASE", typeof(string));
            rows.Columns.Add("TARIFA_IMPUESTO", typeof(decimal));
            rows.Columns.Add("MONTO_IMPUESTO", typeof(decimal));
            rows.Columns.Add("EXONERA_TIPO_DOCUMENTO", typeof(string));
            rows.Columns.Add("EXONERA_NUMERO_DOCUMENTO", typeof(string));
            rows.Columns.Add("EXONERA_INSTITUCION", typeof(string));
            rows.Columns.Add("EXONERA_FECHA_EMISION", typeof(string));
            rows.Columns.Add("EXONERA_MONTO_IMPUESTO", typeof(decimal));
            rows.Columns.Add("EXONERA_PORCENTAJE_COMPRA", typeof(short));
            rows.Columns.Add("EXONERA_TOTAL_LINEA", typeof(decimal));
            rows.Columns.Add("SyncGuid", typeof(Guid));
            rows.Columns.Add("ARTICULO", typeof(string));
            rows.Columns.Add("INCISO", typeof(string));

            var numLinea = 0;
            foreach (var item in request.Items.OrderBy(i => i.RowNo))
            {
                numLinea++;
                var qty = item.Quantity <= 0 ? 1m : item.Quantity;
                var unitPrice = item.UnitPrice;
                var fullPrice = item.FullPrice ?? unitPrice;
                var montoTotal = Math.Round(fullPrice * qty, 2);
                var montoDescuento = Math.Round(item.LineDiscountAmount, 2);
                if (montoDescuento == 0m && fullPrice > unitPrice)
                    montoDescuento = Math.Round((fullPrice - unitPrice) * qty, 2);

                var subTotal = montoTotal - montoDescuento;
                var montoImpuesto = Math.Round(item.SalesTax, 2);
                var baseTaxAmount = Math.Round(item.SalesTax + Math.Max(0m, item.ExMonto), 2);
                var baseTaxRate = baseTaxAmount > 0 && subTotal > 0
                    ? Math.Round(baseTaxAmount / subTotal * 100m, 2)
                    : 0m;
                var montoLinea = subTotal + montoImpuesto;
                var hasExoneration = !string.IsNullOrWhiteSpace(item.ExNumeroDoc);

                itemInfoMap.TryGetValue(item.ItemID, out var info);
                var cabys = info.Cabys ?? string.Empty;
                var codProducto = info.Code ?? item.ItemID.ToString();
                var detalle = !string.IsNullOrWhiteSpace(item.ExtendedDescription)
                    ? item.ExtendedDescription
                    : !string.IsNullOrWhiteSpace(info.Description)
                        ? info.Description
                        : item.ItemID.ToString();

                var codTarifaIVA = baseTaxRate > 0 ? ResolveIntegraFastTaxCode(baseTaxRate) : string.Empty;
                var naturalezaDescuento = montoDescuento > 0 ? (item.LineComment ?? "Descuento comercial") : string.Empty;
                var exoneraPorcentaje = item.ExPorcentaje;

                rows.Rows.Add(
                    clave50,
                    transactionNumber.ToString(),
                    numLinea,
                    Truncate(item.ItemID.ToString(), 15),
                    qty,
                    "Und",
                    Truncate(detalle, 160),
                    Math.Round(fullPrice, 5),
                    montoTotal,
                    montoDescuento,
                    Truncate(naturalezaDescuento, 80),
                    subTotal,
                    codTarifaIVA,
                    baseTaxRate > 0 ? (object)codTarifaIVA : DBNull.Value,
                    baseTaxRate > 0 ? baseTaxRate : 0m,
                    montoImpuesto,
                    hasExoneration ? (object)Truncate(item.ExTipoDoc, 2) : DBNull.Value,
                    hasExoneration ? (object)Truncate(item.ExNumeroDoc, 40) : DBNull.Value,
                    hasExoneration ? (object)Truncate(item.ExInstitucion, 100) : DBNull.Value,
                    hasExoneration && item.ExFecha.HasValue ? (object)item.ExFecha.Value.ToString("yyyy-MM-dd") : DBNull.Value,
                    hasExoneration ? (object)item.ExMonto : DBNull.Value,
                    hasExoneration ? (object)Convert.ToInt16(Math.Round(exoneraPorcentaje, 0, MidpointRounding.AwayFromZero)) : DBNull.Value,
                    hasExoneration ? (object)montoLinea : DBNull.Value,
                    Guid.NewGuid(),
                    Truncate(codProducto, 6),
                    Truncate(NormalizeCabys(cabys), 6));
            }

            if (rows.Rows.Count == 0)
                return;

            using (var bulk = new SqlBulkCopy(cn, SqlBulkCopyOptions.TableLock, null))
            {
                bulk.DestinationTableName = "dbo.AVS_INTEGRAFAST_05";
                bulk.BulkCopyTimeout = 60;
                foreach (DataColumn column in rows.Columns)
                    bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);

                bulk.WriteToServer(rows);
            }
        }

        private static string ResolveIntegraFastTaxCode(decimal taxRate)
        {
            if (taxRate >= 13m) return "08";
            if (taxRate >= 4m) return "04";
            if (taxRate >= 2m) return "07";
            if (taxRate >= 1m) return "06";
            return "01";
        }

        private static void CreateIntegraFast05Table(SqlConnection cn)
        {
            try
            {
                using (var cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AVS_INTEGRAFAST_05')
                    CREATE TABLE dbo.AVS_INTEGRAFAST_05 (
                        ID                        INT IDENTITY(1,1) PRIMARY KEY,
                        CLAVE50                   NVARCHAR(50)  NOT NULL DEFAULT '',
                        TRANSACTIONNUMBER         NVARCHAR(20)  NOT NULL DEFAULT '',
                        NUM_LINEA                 INT           NOT NULL DEFAULT 0,
                        ID_PRODUCTO               NVARCHAR(15)  NOT NULL DEFAULT '',
                        CANTIDAD                  DECIMAL(18,4) NOT NULL DEFAULT 0,
                        UNIDAD_MEDIDA             NVARCHAR(15)  NOT NULL DEFAULT 'Und',
                        DETALLE                   NVARCHAR(160) NOT NULL DEFAULT '',
                        PRECIO_UNITARIO           DECIMAL(18,5) NOT NULL DEFAULT 0,
                        MONTO_TOTAL               DECIMAL(18,2) NOT NULL DEFAULT 0,
                        MONTO_DESCUENTO           DECIMAL(18,2) NOT NULL DEFAULT 0,
                        NATURALEZA_DESCUENTO      NVARCHAR(80)  NOT NULL DEFAULT '',
                        SUBTOTAL                  DECIMAL(18,2) NOT NULL DEFAULT 0,
                        COD_IMPUESTO              NVARCHAR(2)   NOT NULL DEFAULT '',
                        COD_IMPUESTO_BASE         NVARCHAR(2)   NULL,
                        TARIFA_IMPUESTO           DECIMAL(18,5) NOT NULL DEFAULT 0,
                        MONTO_IMPUESTO            DECIMAL(18,2) NOT NULL DEFAULT 0,
                        EXONERA_TIPO_DOCUMENTO    NVARCHAR(2)   NULL,
                        EXONERA_NUMERO_DOCUMENTO  NVARCHAR(40)  NULL,
                        EXONERA_INSTITUCION       NVARCHAR(100) NULL,
                        EXONERA_FECHA_EMISION     NVARCHAR(25)  NULL,
                        EXONERA_MONTO_IMPUESTO    DECIMAL(18,2) NULL,
                        EXONERA_PORCENTAJE_COMPRA SMALLINT      NULL,
                        EXONERA_TOTAL_LINEA       DECIMAL(18,2) NULL,
                        SyncGuid                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
                        ARTICULO                  NVARCHAR(6)   NULL,
                        INCISO                    NVARCHAR(6)   NULL
                    )", cn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // Entorno sin permisos DDL.
            }
        }

        private static int GetTaxSystem(SqlConnection cn)
        {
            using (var cmd = new SqlCommand("SELECT TOP 1 TaxSystem FROM dbo.[Configuration]", cn))
            {
                var value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
            }
        }

        private static void EnsureExonerationEntries(SqlConnection cn, NovaRetailCreateSaleRequest request, int transactionNumber)
        {
            var exonerationItems = (request.Items ?? new List<NovaRetailSaleItemDto>())
                .Where(i => !string.IsNullOrWhiteSpace(i.ExNumeroDoc))
                .ToList();

            if (exonerationItems.Count == 0)
                return;

            string clave50;
            using (var cmd = new SqlCommand("SELECT TOP 1 CLAVE50 FROM dbo.AVS_INTEGRAFAST_01 WHERE TRANSACTIONNUMBER = @TN", cn))
            {
                cmd.Parameters.AddWithValue("@TN", transactionNumber.ToString());
                var val = cmd.ExecuteScalar();
                clave50 = val != null && val != DBNull.Value ? Convert.ToString(val) : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(clave50))
                return;

            foreach (var item in exonerationItems)
            {
                using (var cmd = new SqlCommand(@"
                    INSERT INTO dbo.AVS_INTEGRAFAST_01_EXONERA
                        (CLAVE50, ITEMID, EX_TARIFA_PORC, EX_TARIFA_MONTO, EX_TIPODOC, EX_NUMERODOC, EX_INSTITUCION, EX_FECHA, EX_MONTO, EX_PORCENTAJE, SyncGuid)
                    VALUES
                        (@CLAVE50, @ITEMID, @EX_TARIFA_PORC, @EX_TARIFA_MONTO, @EX_TIPODOC, @EX_NUMERODOC, @EX_INSTITUCION, @EX_FECHA, @EX_MONTO, @EX_PORCENTAJE, NEWID())", cn))
                {
                    cmd.Parameters.AddWithValue("@CLAVE50", clave50);
                    cmd.Parameters.AddWithValue("@ITEMID", item.ItemID);
                    cmd.Parameters.AddWithValue("@EX_TARIFA_PORC", item.ExPorcentaje);
                    cmd.Parameters.AddWithValue("@EX_TARIFA_MONTO", item.ExMonto);
                    cmd.Parameters.AddWithValue("@EX_TIPODOC", Truncate(item.ExTipoDoc, 2));
                    cmd.Parameters.AddWithValue("@EX_NUMERODOC", Truncate(item.ExNumeroDoc, 17));
                    cmd.Parameters.AddWithValue("@EX_INSTITUCION", Truncate(item.ExInstitucion, 100));
                    cmd.Parameters.AddWithValue("@EX_FECHA", (object)(item.ExFecha ?? DateTime.Today));
                    cmd.Parameters.AddWithValue("@EX_MONTO", item.ExMonto);
                    cmd.Parameters.AddWithValue("@EX_PORCENTAJE", item.ExPorcentaje);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            var s = value ?? string.Empty;
            return s.Length <= maxLength ? s : s.Substring(0, maxLength);
        }

        private static string NormalizeCabys(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return new string(value.Where(char.IsDigit).ToArray());
        }
    }
}
