namespace Dapr.PluggableComponents.Components.StateStore;

public interface IStateStore
{
    Task<StateStoreBulkGetResponse> BulkGetAsync(StateStoreBulkGetRequest request, CancellationToken cancellationToken = default);
    
    Task BulkSetAsync(StateStoreBulkSetRequest request, CancellationToken cancellationToken = default);

    Task<StateStoreGetResponse?> GetAsync(StateStoreGetRequest request, CancellationToken cancellationToken = default);

    Task InitAsync(StateStoreInitRequest request, CancellationToken cancellationToken = default);

    Task SetAsync(StateStoreSetRequest request, CancellationToken cancellationToken = default);
}
