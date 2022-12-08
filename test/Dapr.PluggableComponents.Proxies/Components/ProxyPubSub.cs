using Dapr.PluggableComponents.Components;
using Dapr.PluggableComponents.Components.PubSub;
using Dapr.PluggableComponents.Proxies.Grpc.v1;
using Dapr.PluggableComponents.Proxies.Utilities;
using Google.Protobuf;

namespace Dapr.PluggableComponents.Proxies.Components;

internal sealed class ProxyPubSub : IPubSub, IPluggableComponentFeatures, IPluggableComponentLiveness
{
    private readonly IGrpcChannelProvider grpcChannelProvider;
    private readonly ILogger<ProxyPubSub> logger;

    public ProxyPubSub(IGrpcChannelProvider grpcChannelProvider, ILogger<ProxyPubSub> logger)
    {
        this.grpcChannelProvider = grpcChannelProvider ?? throw new ArgumentNullException(nameof(grpcChannelProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region IPubSub Members

    public async Task InitAsync(Dapr.PluggableComponents.Components.MetadataRequest request, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Init request");

        var grpcRequest = new Dapr.PluggableComponents.Proxies.Grpc.v1.PubSubInitRequest
        {
            Metadata = new Dapr.PluggableComponents.Proxies.Grpc.v1.MetadataRequest()
        };

        grpcRequest.Metadata.Properties.Add(request.Properties);

        await this.GetClient().InitAsync(grpcRequest, cancellationToken: cancellationToken);
    }

    public async Task PublishAsync(PubSubPublishRequest request, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Publish request for pub-sub {0} on topic {1}", request.PubSubName, request.Topic);

        var grpcRequest = new PublishRequest
        {
            ContentType = request.ContentType ?? String.Empty,
            Data = ByteString.CopyFrom(request.Data.ToArray()),
            PubsubName = request.PubSubName,
            Topic = request.Topic
        };

        grpcRequest.Metadata.Add(request.Metadata);

        await this.GetClient().PublishAsync(
            grpcRequest,
            cancellationToken: cancellationToken);
    }

    public async Task PullMessagesAsync(IAsyncEnumerable<PubSubPullMessagesRequest> requests, IAsyncMessageWriter<PubSubPullMessagesResponse> responses, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Pull messages request");

        using var stream = this.GetClient().PullMessages(cancellationToken: cancellationToken);

        var requestReaderTask =
            async () =>
            {
                await foreach (var request in requests.WithCancellation(cancellationToken))
                {
                    this.logger.LogInformation("Received request (ID = {0})", request.AckMessageId ?? "<none>");

                    var grpcRequest = new PullMessagesRequest
                    {
                        AckError = request.AckMessageError != null ? new AckMessageError { Message = request.AckMessageError } : null,
                        AckMessageId = request.AckMessageId,
                        Topic = request.Topic != null ? new Topic { Name = request.Topic.Name } : null
                    };

                    if (grpcRequest.Topic != null)
                    {
                        grpcRequest.Topic.Metadata.Add(request.Topic?.Metadata);
                    }

                    await stream.RequestStream.WriteAsync(grpcRequest);
                }

                await stream.RequestStream.CompleteAsync();
            };

        var responseReaderTask =
            async () =>
            {
                await foreach(var response in stream.ResponseStream.AsEnumerable(cancellationToken))
                {
                    this.logger.LogInformation("Received response (ID = {0})", response.Id);

                    await responses.WriteAsync(
                        new PubSubPullMessagesResponse(response.TopicName, response.Id)
                        {
                            ContentType = response.ContentType,
                            Data = response.Data.ToArray(),
                            Metadata = response.Metadata                                
                        },
                        cancellationToken);
                }
            };

        await Task.WhenAll(requestReaderTask(), responseReaderTask());
    }

    #endregion

    #region IPluggableComponentFeatures Members

    public async Task<string[]> GetFeaturesAsync(CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Get features request");

        var response = await this.GetClient().FeaturesAsync(
            new FeaturesRequest(),
            cancellationToken: cancellationToken);

        var features = response.Features.ToArray();

        this.logger.LogInformation("Returning features: {0}", String.Join(",", features));

        return features;
    }

    #endregion

    #region IPluggableComponentLiveness Members

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("Ping request");

        await this.GetClient().PingAsync(
            new PingRequest(),
            cancellationToken: cancellationToken);
    }

    #endregion

    private PubSub.PubSubClient GetClient()
    {
        return new PubSub.PubSubClient(this.grpcChannelProvider.GetChannel());
    }
}