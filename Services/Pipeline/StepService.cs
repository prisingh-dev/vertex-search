namespace VertexSearchApi.Services.Pipeline;

/// <summary>Kicks off a step chain and returns the mutated context.</summary>
public class StepService<T> where T : class
{
    private readonly Step<T> _firstStep;

    public StepService(Step<T> firstStep) => _firstStep = firstStep;

    public async Task<T> ExecuteAsync(T context)
    {
        await _firstStep.HandleAsync(context);
        return context;
    }
}
