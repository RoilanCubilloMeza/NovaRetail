using NovaRetail.Models;

namespace NovaRetail.Data;

/// <summary>Contrato para validación de exoneraciones fiscales.</summary>
public interface IExonerationService
{
    Task<ExonerationValidationResult> ValidateAsync(string authorization, CancellationToken cancellationToken = default);
}
