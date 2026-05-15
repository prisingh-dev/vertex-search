using System.Diagnostics;

namespace VertexSearchApi.Services.Pipeline;

/// <summary>
/// Abstract base for a chain-of-responsibility step with built-in latency logging.
/// Each step calls InnerHandle, logs elapsed time, then delegates to NextStep.
/// </summary>
public abstract class Step<T> where T : class
{
    protected readonly ILogger _logger;
    private Step<T>? _nextStep;

    protected Step(ILogger logger) => _logger = logger;

    public void SetNextStep(Step<T> next) => _nextStep = next;

    public async Task HandleAsync(T context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var stepName = GetType().Name;
        var sw = Stopwatch.StartNew();
        try
        {
            await InnerHandleAsync(context);
        }
        finally
        {
            sw.Stop();
            if (sw.ElapsedMilliseconds > 500)
                _logger.LogWarning("[LATENCY] {Step} took {Ms}ms (SLOW)", stepName, sw.ElapsedMilliseconds);
            else
                _logger.LogInformation("[LATENCY] {Step} took {Ms}ms", stepName, sw.ElapsedMilliseconds);
        }
        if (_nextStep is not null)
            await _nextStep.HandleAsync(context);
    }

    protected abstract Task InnerHandleAsync(T context);
}
