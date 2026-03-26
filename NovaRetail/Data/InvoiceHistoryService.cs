using Newtonsoft.Json;
using NovaRetail.Models;

namespace NovaRetail.Data;

public sealed class InvoiceHistoryService : IInvoiceHistoryService
{
    private const string FileName = "invoice_history.json";
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<InvoiceHistoryEntry>? _cache;

    private static string FilePath
    {
        get
        {
            // FileSystem.AppDataDirectory puede fallar en apps no empaquetadas en Windows.
            // Usamos Environment.GetFolderPath como alternativa confiable.
            string baseDir;
            try
            {
                baseDir = FileSystem.AppDataDirectory;
            }
            catch
            {
                baseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NovaRetail");
            }
            var path = Path.Combine(baseDir, FileName);
            System.Diagnostics.Debug.WriteLine($"[InvoiceHistory] FilePath = {path}");
            return path;
        }
    }

    public async Task<IReadOnlyList<InvoiceHistoryEntry>> GetAllAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            return (await LoadAsync()).AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddAsync(InvoiceHistoryEntry entry)
    {
        System.Diagnostics.Debug.WriteLine($"[InvoiceHistory] AddAsync: #{entry.TransactionNumber} - {entry.ClientName}");
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var list = await LoadAsync();
            list.Insert(0, entry);
            await SaveAsync(list);
            _cache = list;
            System.Diagnostics.Debug.WriteLine($"[InvoiceHistory] Guardado OK. Total entradas: {list.Count}");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var list = await LoadAsync();
            list.RemoveAll(e => e.Id == id);
            await SaveAsync(list);
            _cache = list;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearAllAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            _cache = new List<InvoiceHistoryEntry>();
            await SaveAsync(_cache);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<List<InvoiceHistoryEntry>> LoadAsync()
    {
        if (_cache is not null)
            return _cache;

        if (!File.Exists(FilePath))
        {
            _cache = new List<InvoiceHistoryEntry>();
            return _cache;
        }

        try
        {
            var json = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
            _cache = JsonConvert.DeserializeObject<List<InvoiceHistoryEntry>>(json)
                     ?? new List<InvoiceHistoryEntry>();
        }
        catch
        {
            _cache = new List<InvoiceHistoryEntry>();
        }

        return _cache;
    }

    private static Task SaveAsync(List<InvoiceHistoryEntry> list)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonConvert.SerializeObject(list, Formatting.None);
        return File.WriteAllTextAsync(FilePath, json);
    }
}
