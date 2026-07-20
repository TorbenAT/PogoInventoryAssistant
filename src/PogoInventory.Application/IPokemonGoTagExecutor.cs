namespace PogoInventory.Application;

public interface IPokemonGoTagExecutor
{
    Task<TagExecutionResult> ExecuteAsync(
        string localPokemonId,
        string tagName,
        CancellationToken cancellationToken = default);
}
