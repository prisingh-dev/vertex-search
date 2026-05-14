namespace VertexSearchApi.Services.Pipeline;

/// <summary>Kicks off a step chain and returns the mutated context.</summary>
public class StepService<T> where T : class
{
    private readonly Step<T> _firstStep;

    public StepService(Step<T> firstStep) => _firstStep = firstStep;

    public T Execute(T context)
    {
        _firstStep.Handle(context);
        return context;
    }
}
