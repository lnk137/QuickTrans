using QuickTrans.Models;

namespace QuickTrans.Services;

public interface ITranslationProvider
{
    ProviderDescriptor Descriptor { get; }
    bool IsConfigured { get; }
    Task ValidateConfigurationAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<TranslationChunk> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken);
}
