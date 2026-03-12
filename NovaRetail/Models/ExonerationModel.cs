namespace NovaRetail.Models
{
    public class ExonerationModel
    {
        public string NumeroDocumento { get; set; } = string.Empty;
        public string Identificacion { get; set; } = string.Empty;
        public decimal PorcentajeExoneracion { get; set; }
        public int Autorizacion { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public int Ano { get; set; }
        public List<string> Cabys { get; set; } = new();
        public string TipoAutorizacion { get; set; } = string.Empty;
        public string TipoDocumentoCodigo { get; set; } = string.Empty;
        public string TipoDocumentoDescripcion { get; set; } = string.Empty;
        public string CodigoInstitucion { get; set; } = string.Empty;
        public string NombreInstitucion { get; set; } = string.Empty;
        public bool PoseeCabys { get; set; }

        public bool IsExpired => FechaVencimiento.HasValue && FechaVencimiento.Value.Date < DateTime.Today;
    }

    public class ExonerationValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public ExonerationModel? Document { get; set; }
    }

    public class CheckoutExonerationState
    {
        public bool HasExoneration { get; set; }
        public string Authorization { get; set; } = string.Empty;
        public string SummaryText { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public string ScopeText { get; set; } = string.Empty;
    }
}
