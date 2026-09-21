using System.ComponentModel;
using System.Runtime.CompilerServices;
using QuickTrans.Models;
using QuickTrans.Services;
using QuickTrans.ViewModels;

namespace QuickTrans.Tests;

public sealed class PopoverPerformanceTests
{
    [Fact]
    public async Task StreamingChunks_AreCoalescedBeforeUpdatingTheUi()
    {
        var viewModel = new PopoverViewModel(
            [new BurstProvider()],
            new SessionTranslationCache(),
            ProviderIds.OpenAi);
        var resultUpdates = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PopoverViewModel.ResultText))
                resultUpdates++;
        };

        await viewModel.SetSourceAndTranslateAsync("hello", ProviderIds.OpenAi);

        Assert.Equal(500, viewModel.ResultText.Length);
        Assert.InRange(resultUpdates, 1, 10);
    }

    private sealed class BurstProvider : ITranslationProvider
    {
        public ProviderDescriptor Descriptor { get; } = new(ProviderIds.OpenAi, "AI 翻译", true);
        public bool IsConfigured => true;

        public Task ValidateConfigurationAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async IAsyncEnumerable<TranslationChunk> TranslateAsync(
            TranslationRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            for (var index = 0; index < 500; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new TranslationChunk("x");
            }
            yield return new TranslationChunk(string.Empty, true);
        }
    }
}
