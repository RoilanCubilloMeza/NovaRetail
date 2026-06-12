# Integracion de comprobantes RECEIPT en NovaRetail

## 1. Resumen

NovaRetail ahora genera, guarda, envia y reimprime comprobantes utilizando como
base el proyecto **Comprobante prueba v1.1**.

La integracion combina tres componentes:

1. Los procedimientos almacenados `AVS_RECEIPT_SALE_*`, que obtienen los datos
   oficiales de una venta registrada.
2. NovaAPI, que ejecuta los procedimientos, normaliza la informacion y la
   entrega a NovaRetail mediante JSON.
3. NovaRetail, que muestra el comprobante y utiliza el motor portado de
   Comprobante prueba v1.1 para generar HTML y trabajos de impresion ESC/POS.

El objetivo principal es que el comprobante no dependa solamente de la copia
local del carrito. Despues de registrar una venta, NovaRetail vuelve a consultar
la venta guardada en la base de datos y construye el comprobante con esa
informacion.

## 2. Alcance implementado

La implementacion cubre:

- Comprobantes de ventas nuevas.
- Reimpresion desde el historial de facturas.
- Lectura de encabezado comercial, fiscal, detalle e impuestos.
- Recuperacion de caja y formas de pago.
- Generacion de vista HTML termica.
- Generacion de texto para papel termico.
- Generacion de trabajo binario ESC/POS.
- Generacion de codigo QR ESC/POS.
- Envio RAW a la impresora predeterminada de Windows.
- Comando de corte de papel.
- Respaldo para ventas cuyo procedimiento de detalle no devuelve articulos.

La prueba fisica de impresion requiere una impresora termica ESC/POS instalada y
configurada en Windows. En el equipo de desarrollo actual, la impresora
predeterminada es `Microsoft Print to PDF`.

## 3. Arquitectura general

```text
Venta registrada
      |
      v
NovaRetail solicita detalle por TransactionNumber
      |
      v
GET /api/NovaRetailSales/invoice-history-detail/{transactionNumber}
      |
      v
NovaAPI ejecuta AVS_RECEIPT_SALE_*
      |
      v
DTO normalizado del comprobante
      |
      v
ReceiptViewModel
      |
      +--> Vista previa dentro de NovaRetail
      +--> ReceiptRenderer.BuildHtml(...)
      +--> ReceiptRenderer.BuildText(...)
      +--> EscPosPrinter.BuildPrintJob(...)
      +--> RawPrinterHelper.SendBytesToPrinter(...)
```

La responsabilidad se divide de esta manera:

| Capa | Responsabilidad |
| --- | --- |
| SQL Server | Mantener y devolver los datos oficiales de la venta |
| NovaAPI | Ejecutar procedimientos y normalizar resultados |
| NovaRetail | Presentar, guardar, enviar e imprimir el comprobante |
| Windows Spooler | Enviar bytes RAW a la impresora termica |

## 4. Procedimientos almacenados utilizados

La integracion comienza con los procedimientos de ventas:

### `dbo.AVS_RECEIPT_SALE_HEADER`

Parametro:

```sql
@TransactionNumber INT
```

Obtiene el encabezado general de la venta:

- Numero de transaccion.
- Fecha y hora.
- Total e impuesto total.
- Cliente.
- Cajero.
- Nombre, direccion y telefono del comercio.
- Tipo general de transaccion.

Este procedimiento puede devolver el mismo encabezado varias veces cuando una
venta posee varios registros de impuestos, porque incluye una relacion con
`TaxEntry`. La integracion utiliza solamente la primera fila.

### `dbo.AVS_RECEIPT_SALE_INTEGRAFAST_HEADER`

Parametro:

```sql
@TRNumber INT
```

Obtiene los datos fiscales y electronicos:

- Clave de 50 digitos.
- Consecutivo o clave de 20 digitos.
- Tipo de comprobante.
- Fecha fiscal.
- Moneda.
- Cliente fiscal.
- Correo.
- Codigos de formas de pago.
- Condicion de venta.

Los tipos se normalizan a codigos utilizados por NovaRetail:

| Codigo | Documento |
| --- | --- |
| `01` | Factura electronica |
| `02` | Nota de debito |
| `03` | Nota de credito |
| `04` | Tiquete electronico |
| `09` | Factura de exportacion |

### `dbo.AVS_RECEIPT_SALE_DETAILS`

Parametro:

```sql
@TransactionNumber INT
```

Obtiene los articulos de la venta:

- Codigo del articulo.
- Descripcion extendida.
- Cantidad.
- Precio original.
- Precio aplicado.
- Subtotal.
- Descuento.
- Impuesto.
- Total.
- Tipo y porcentaje de impuesto.

Este procedimiento usa un `INNER JOIN` con `TransactionEntryExt`. Algunas ventas
validas no tienen ese registro extendido, por lo que el procedimiento puede
devolver cero filas aunque la venta si tenga articulos.

Para cubrir ese caso, NovaAPI ejecuta una consulta de respaldo sobre:

- `TransactionEntry`.
- `TransactionEntryExt`.
- `Item`.
- `TaxEntry`.
- `Tax`.

La consulta de respaldo solamente se ejecuta cuando
`AVS_RECEIPT_SALE_DETAILS` no devuelve ninguna linea.

### `dbo.AVS_RECEIPT_SALE_TAXES`

Parametro:

```sql
@TransactionNumber INT
```

Obtiene el resumen fiscal agrupado:

- Descripcion del impuesto.
- Monto total del impuesto.

NovaAPI tambien interpreta el porcentaje indicado en la descripcion, cuando
esta disponible.

## 5. Informacion complementaria

Ademas de los procedimientos, NovaAPI consulta:

### Caja o registro

Se obtiene el `RegisterID` relacionando la transaccion con `Batch`.

```text
Transaction.BatchNumber + Transaction.StoreID
                      |
                      v
                 Batch.RegisterID
```

### Formas de pago

Se consultan hasta dos registros de `TenderEntry`, junto con `Tender`, para
obtener:

- Descripcion real del pago.
- Monto del primer pago.
- Descripcion del segundo pago.
- Monto del segundo pago.

Esto evita depender solamente de los codigos fiscales de medio de pago.

## 6. Endpoint de NovaAPI

El comprobante se obtiene mediante:

```http
GET /api/NovaRetailSales/invoice-history-detail/{transactionNumber}
```

Ejemplo:

```text
http://localhost:52500/api/NovaRetailSales/invoice-history-detail/105829
```

El endpoint primero intenta cargar el comprobante usando el nuevo flujo
`LoadSaleReceipt`. Si no encuentra el encabezado, conserva la consulta anterior
del historial como respaldo.

La respuesta contiene, entre otros:

```json
{
  "ok": true,
  "entry": {
    "transactionNumber": 105829,
    "comprobanteTipo": "04",
    "clave50": "506...",
    "consecutivo": "002...",
    "registerNumber": 1,
    "storeName": "Grupo BM SP S.A",
    "storeAddress": "Daniel Flores, Perez Zeledon, 100mts del Hotel del Sur",
    "storePhone": "2101-2868",
    "currencyCode": "CRC",
    "tenderDescription": "TRANSFERENCIA CRC",
    "tenderTotalColones": 1757.15,
    "taxBreakdowns": [],
    "lines": []
  }
}
```

## 7. Flujo de una venta nueva

El flujo principal se encuentra en:

```text
NovaRetail/ViewModels/MainViewModel.Checkout.cs
```

Pasos:

1. El usuario confirma el pago.
2. NovaRetail registra la venta mediante `CreateSaleAsync`.
3. Se obtiene el `TransactionNumber`.
4. NovaRetail construye inicialmente un comprobante con la copia del carrito.
5. NovaRetail llama a `GetInvoiceHistoryDetailAsync(TransactionNumber)`.
6. NovaAPI obtiene la informacion real mediante `AVS_RECEIPT_SALE_*`.
7. Si la consulta responde correctamente y contiene articulos, NovaRetail
   reemplaza el comprobante inicial por el comprobante oficial almacenado.
8. Se muestra el popup del comprobante.

El comprobante del carrito se conserva como respaldo. Si NovaAPI no responde o
los procedimientos fallan, el usuario todavia puede ver, guardar o imprimir el
comprobante generado con los datos disponibles localmente.

## 8. Flujo de reimpresion

El historial utiliza:

```text
NovaRetail/ViewModels/InvoiceHistoryViewModel.cs
```

Pasos:

1. El usuario abre el historial.
2. Busca o selecciona una transaccion.
3. NovaRetail solicita el detalle por `TransactionNumber`.
4. NovaAPI vuelve a ejecutar el flujo RECEIPT.
5. Los datos se convierten a `InvoiceHistoryEntry`.
6. `ReceiptViewModel.LoadFromHistory(...)` construye el comprobante.
7. El usuario puede imprimir, enviar o guardar.

## 9. Motor portado de Comprobante prueba v1.1

El nucleo del proyecto compartido fue integrado en:

```text
NovaRetail/Services/ReceiptModels.cs
NovaRetail/Services/ReceiptRenderer.cs
NovaRetail/Services/EscPosPrinter.cs
NovaRetail/Services/RawPrinterHelper.cs
```

### `ReceiptModels.cs`

Define los datos independientes de la interfaz:

- `CompanyInfo`.
- `CustomerInfo`.
- `ReceiptPayment`.
- `ReceiptItem`.
- `Receipt`.

`ReceiptItem` calcula:

- Total bruto.
- Descuento.
- Total neto.
- Impuesto.
- Total de linea.

`Receipt` calcula:

- Subtotal.
- Descuento total.
- Impuesto total.
- Total.
- Pagado.
- Cambio.

Tambien acepta totales explicitos. Esto permite utilizar los totales oficiales
de la base de datos cuando estan disponibles.

### `ReceiptRenderer.cs`

Genera dos formatos:

#### Texto termico

```csharp
ReceiptRenderer.BuildText(receipt, 48);
```

El ancho predeterminado es de 48 columnas. Incluye:

- Comercio.
- Documento.
- Fecha.
- Caja y cajero.
- Cliente.
- Articulos.
- Descuentos.
- Impuestos.
- Total.
- Pagos.
- Cambio.
- Clave electronica.
- Pie del comprobante.

#### HTML termico

```csharp
ReceiptRenderer.BuildHtml(receipt);
```

El HTML incluye:

- Vista previa adaptable.
- Estilo de papel termico.
- Boton para imprimir desde navegador.
- Estilo especial de impresion para papel de 80 mm.

Guardar y enviar desde NovaRetail utilizan este HTML.

### `EscPosPrinter.cs`

Convierte el comprobante en bytes ESC/POS:

```csharp
byte[] job = EscPosPrinter.BuildPrintJob(receipt, 48);
```

El trabajo contiene:

1. Inicializacion de impresora.
2. Texto del comprobante.
3. Codigo QR.
4. Avance de papel.
5. Comando de corte.

Tambien permite enviar el trabajo por red:

```csharp
await EscPosPrinter.SendAsync(host, port, job);
```

El puerto comun para impresoras termicas de red es `9100`.

### `RawPrinterHelper.cs`

Integra con el spooler de Windows.

Obtiene la impresora predeterminada:

```csharp
string printer = RawPrinterHelper.GetDefaultPrinterName();
```

Envia bytes RAW:

```csharp
RawPrinterHelper.SendBytesToPrinter(printer, job);
```

El envio RAW es necesario para que Windows no transforme ni reacomode los
comandos ESC/POS.

## 10. Acciones del comprobante en NovaRetail

El comportamiento se encuentra en:

```text
NovaRetail/ViewModels/ReceiptViewModel.cs
```

### Imprimir

El boton **Imprimir**:

1. Obtiene la impresora predeterminada de Windows.
2. Genera el trabajo ESC/POS.
3. Envia el trabajo como RAW.
4. Muestra una confirmacion.

Importante: el envio es inmediato. Si la impresora predeterminada no es termica
o no entiende ESC/POS, la impresion puede fallar o producir contenido
incorrecto.

### Guardar

El boton **Guardar** genera el HTML termico y utiliza el servicio de guardado de
documentos de NovaRetail.

### Enviar

El boton **Enviar** adjunta el HTML termico al cliente de correo configurado en
el equipo.

## 11. Modelos extendidos

Los DTO de NovaAPI y los modelos de NovaRetail se ampliaron con:

- `StoreAddress`.
- `StorePhone`.
- `ClientEmail`.
- `CurrencyCode`.
- `TaxBreakdowns`.
- `FullPriceColones`.
- `TaxAmountColones`.

Estos campos permiten reconstruir correctamente el comprobante a partir de una
venta ya guardada.

Archivos principales:

```text
NovaAPI/Models/NovaRetailCreateSaleModels.cs
NovaRetail/Models/NovaRetailSaleModels.cs
NovaRetail/Models/InvoiceHistoryEntry.cs
```

## 12. Pruebas realizadas

### Compilacion

NovaAPI:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  .\NovaAPI\NovaAPI.csproj `
  /t:Compile `
  /p:Configuration=Debug `
  /nologo `
  /verbosity:minimal
```

Resultado: correcto.

NovaRetail:

```powershell
dotnet msbuild .\NovaRetail\NovaRetail.csproj `
  /t:Build `
  /p:TargetFramework=net10.0-windows10.0.19041.0 `
  /p:Configuration=Debug `
  /p:WindowsPackageType=None `
  /nologo `
  /verbosity:minimal
```

Resultado: correcto.

### Prueba automatizada del comprobante

Archivo:

```text
NovaRetail.Tests/ReceiptRendererTests.cs
```

Valida:

- Texto termico.
- HTML.
- Clave electronica.
- Inicio ESC/POS.
- Secuencia de QR.
- Comando de corte.

Comando:

```powershell
dotnet test .\NovaRetail.Tests\NovaRetail.Tests.csproj `
  --no-restore `
  --filter FullyQualifiedName~ReceiptRendererTests
```

Resultado: `1/1` aprobada.

### Suite general

```powershell
dotnet test .\NovaRetail.Tests\NovaRetail.Tests.csproj --no-restore
```

Resultado:

```text
51 aprobadas
1 fallida
52 total
```

La prueba fallida no pertenece al comprobante. La prueba de autenticacion espera
`http://localhost:52500/api/Login`, mientras la configuracion actual utiliza
`http://192.168.137.217:2135/api/Login`.

### Validacion con ventas reales

Se validaron:

#### Transaccion `105829`

- Tipo: `04`, tiquete electronico.
- Lineas: 2.
- Grupos de impuestos: 1.
- Caja: 1.
- Pago: `TRANSFERENCIA CRC`.
- Total: `1757.15`.
- Impuesto de lineas: `202.15`.
- Impuesto del resumen: `202.15`.

#### Transaccion `105838`

- Tipo: `01`, factura electronica.
- Lineas: 3.
- Grupos de impuestos: 2.
- Caja: 1.
- Pago: `TRANSFERENCIA CRC`.
- Total: `20768.01`.
- Impuesto de lineas: `39.06`.
- Impuesto del resumen: `39.06`.

En `105838`, `AVS_RECEIPT_SALE_DETAILS` devuelve cero filas. La consulta de
respaldo recupero correctamente sus 3 articulos.

### Arranque de aplicacion

El ejecutable compilado de NovaRetail fue iniciado y permanecio funcionando
durante la prueba controlada.

## 13. Como revisar manualmente

### Paso 1: iniciar NovaAPI local

Desde la raiz del repositorio:

```powershell
.\run-novaapi-clean.ps1 -Port 52500
```

### Paso 2: consultar una factura

Abrir en navegador:

```text
http://localhost:52500/api/NovaRetailSales/invoice-history-detail/105829
```

Verificar:

- `ok` es `true`.
- `clave50` contiene la clave electronica.
- `lines` contiene articulos.
- `taxBreakdowns` contiene impuestos.
- `registerNumber` contiene la caja.
- `tenderDescription` contiene la forma de pago.

### Paso 3: abrir NovaRetail

```powershell
dotnet run --project .\NovaRetail\NovaRetail.csproj `
  --framework net10.0-windows10.0.19041.0
```

### Paso 4: revisar reimpresion

1. Iniciar sesion.
2. Abrir historial de facturas.
3. Buscar `105829` o `105838`.
4. Seleccionar la factura.
5. Presionar **Reimprimir**.
6. Revisar comercio, cliente, articulos, impuestos, total y pago.
7. Presionar **Guardar** para generar HTML sin imprimir fisicamente.

### Paso 5: probar impresion fisica

1. Instalar la impresora termica en Windows.
2. Confirmar que soporte ESC/POS.
3. Configurarla como impresora predeterminada.
4. Verificar el ancho de papel, actualmente 48 columnas para 80 mm.
5. Abrir un comprobante corto.
6. Presionar **Imprimir**.
7. Confirmar texto, QR, avance y corte.

## 14. Diagnostico de problemas

### El endpoint devuelve factura sin lineas

Revisar:

1. Que exista `TransactionEntry` para la transaccion.
2. Que `AVS_RECEIPT_SALE_DETAILS` funcione.
3. Que la consulta de respaldo tenga acceso a `TransactionEntry`, `Item` y
   `TaxEntry`.
4. Los registros de error de NovaAPI.

### No aparece clave electronica

Revisar:

1. Que exista el registro en `AVS_INTEGRAFAST_01`.
2. Que `AVS_RECEIPT_SALE_INTEGRAFAST_HEADER` devuelva datos.
3. Que el `TransactionNumber` coincida.

### El impuesto no coincide

Comparar:

- Suma de `lines[].taxAmountColones`.
- Suma de `taxBreakdowns[].taxAmount`.
- `taxColones`.
- `Transaction.SalesTax`.

Una diferencia puede indicar datos antiguos, redondeo o registros incompletos
en `TaxEntry`.

### El boton Imprimir falla

Revisar:

1. Que exista una impresora predeterminada.
2. Que la impresora este encendida.
3. Que el controlador permita trabajos RAW.
4. Que la impresora soporte ESC/POS.
5. Que no este seleccionado `Microsoft Print to PDF`.

### El QR no se imprime

Revisar:

1. Compatibilidad ESC/POS del modelo.
2. Compatibilidad con comandos `GS ( k`.
3. Que la clave no exceda el limite.
4. Configuracion y firmware de la impresora.

### No corta el papel

El trabajo envia:

```text
GS V 0
```

Algunas impresoras:

- No tienen cortador.
- Requieren otro comando.
- Requieren activar el cortador en el controlador.

### NovaRetail no conecta a NovaAPI local

Revisar `BaseUrls` en:

```text
NovaRetail/MauiProgram.cs
```

La configuracion debe apuntar al servidor y puerto donde se ejecuta NovaAPI.

## 15. Limitaciones y pendientes operativos

La integracion de software esta implementada y validada. Para certificarla en
produccion todavia se recomienda:

1. Instalar y configurar la impresora termica real.
2. Imprimir un tiquete corto.
3. Imprimir una factura con varios impuestos.
4. Validar caracteres especiales y tildes.
5. Confirmar ancho de papel.
6. Confirmar QR.
7. Confirmar corte.
8. Probar una venta con dos formas de pago.
9. Probar nota de credito.
10. Confirmar el texto legal requerido por el comercio.

## 16. Archivos principales de la implementacion

### NovaAPI

```text
NovaAPI/Controllers/NovaRetailSalesController.Receipts.cs
NovaAPI/Controllers/NovaRetailSalesController.InvoiceHistory.cs
NovaAPI/Models/NovaRetailCreateSaleModels.cs
NovaAPI/NovaAPI.csproj
```

### NovaRetail

```text
NovaRetail/Services/ReceiptModels.cs
NovaRetail/Services/ReceiptRenderer.cs
NovaRetail/Services/EscPosPrinter.cs
NovaRetail/Services/RawPrinterHelper.cs
NovaRetail/ViewModels/ReceiptViewModel.cs
NovaRetail/ViewModels/MainViewModel.Checkout.cs
NovaRetail/ViewModels/InvoiceHistoryViewModel.cs
NovaRetail/Models/NovaRetailSaleModels.cs
NovaRetail/Models/InvoiceHistoryEntry.cs
```

### Pruebas

```text
NovaRetail.Tests/ReceiptRendererTests.cs
NovaRetail.Tests/NovaRetail.Tests.csproj
```

## 17. Conclusion

NovaRetail utiliza ahora los procedimientos `AVS_RECEIPT_SALE_*` como fuente
principal para construir comprobantes de venta. NovaAPI normaliza los datos y
NovaRetail utiliza el motor basado en Comprobante prueba v1.1 para generar
salidas HTML y ESC/POS.

Las compilaciones, pruebas automatizadas y validaciones con ventas reales fueron
correctas. El unico paso que no puede certificarse sin hardware es la impresion
fisica en una impresora termica ESC/POS.
