using NovaRetail.Models;

namespace NovaRetail.Data
{
    public interface IExonerationService
    {
        Task<ExonerationValidationResult> ValidateAsync(string authorization, CancellationToken cancellationToken = default);
    }
}
