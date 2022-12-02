using Dapr.PluggableComponents.Components.StateStore;
using Dapr.PluggableComponents.Utilities;
using Dapr.Proto.Components.V1;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using static Dapr.Proto.Components.V1.TransactionalStateStore;

namespace Dapr.PluggableComponents.Adaptors;

public class TransactionalStateStoreAdaptor : TransactionalStateStoreBase
{
    private readonly ILogger<TransactionalStateStoreAdaptor> logger;
    private readonly IDaprPluggableComponentProvider<ITransactionalStateStore> componentProvider;

    public TransactionalStateStoreAdaptor(ILogger<TransactionalStateStoreAdaptor> logger, IDaprPluggableComponentProvider<ITransactionalStateStore> componentProvider)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.componentProvider = componentProvider ?? throw new ArgumentNullException(nameof(componentProvider));
    }

    public override async Task<TransactionalStateResponse> Transact(TransactionalStateRequest request, ServerCallContext context)
    {
        await this.GetStateStore(context.RequestHeaders).TransactAsync(
            new TransactionalStateStoreTransactRequest
            {
                Metadata = request.Metadata,
                Operations = request.Operations.Select(ToOperation).WhereNonNull().ToArray()
            },
            context.CancellationToken);

        return new TransactionalStateResponse();
    }

    private ITransactionalStateStore GetStateStore(Metadata metadata)
    {
        return this.componentProvider.GetComponent(key => metadata.Get(key));
    }

    public static TransactionalStateStoreTransactOperation? ToOperation(TransactionalStateOperation operation)
    {
        return operation.RequestCase switch
        {
            TransactionalStateOperation.RequestOneofCase.Delete => new TransactionalStateStoreTransactDeleteOperation(ToDeleteRequest(operation.Delete)),
            TransactionalStateOperation.RequestOneofCase.Set => new TransactionalStateStoreTransactSetOperation(ToSetRequest(operation.Set)),
            _ => null
        };
    }

    public static StateStoreDeleteRequest ToDeleteRequest(DeleteRequest request)
    {
        return new StateStoreDeleteRequest
        {
            ETag = request.Etag?.Value,
            Key = request.Key,
            Metadata = request.Metadata
        };
    }

    public static StateStoreSetRequest ToSetRequest(SetRequest request)
    {
        return new StateStoreSetRequest
        {
            ContentType = request.ContentType,
            ETag = request.Etag?.Value,
            Key = request.Key,
            Metadata = request.Metadata,
            Value = request.Value.Memory
        };
    }
}
