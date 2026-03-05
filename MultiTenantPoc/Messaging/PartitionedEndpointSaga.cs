namespace MultiTenantPoc;

[Saga]
public class PartitionedEndpointSaga(ILogger<PartitionedEndpointSaga> logger) : Saga<PartitionedEndpointSagaData>,
    IAmStartedByMessages<StartPartitionSagaCommand>,
    IHandleMessages<PartitionSagaProbeReply>,
    IHandleTimeouts<PartitionSagaCompletionTimeout>
{
    static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(5);

    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<PartitionedEndpointSagaData> mapper)
    {
        mapper.MapSaga(saga => saga.CorrelationId)
            .ToMessage<StartPartitionSagaCommand>(message => message.CorrelationId)
            .ToMessage<PartitionSagaProbeReply>(message => message.CorrelationId);
    }

    public async Task Handle(StartPartitionSagaCommand message, IMessageHandlerContext context)
    {
        Data.CorrelationId = message.CorrelationId;
        Data.TenantId = message.TenantId;
        Data.BusinessId = message.BusinessId;
        Data.Partition = message.Partition;

        logger.LogInformation(
            "Started partition saga for CorrelationId={CorrelationId}, TenantId={TenantId}, BusinessId={BusinessId}, Partition={Partition}",
            message.CorrelationId,
            message.TenantId,
            message.BusinessId,
            message.Partition);

        await context.SendLocal(new PartitionSagaProbeCommand
        {
            CorrelationId = message.CorrelationId,
            TenantId = message.TenantId,
            BusinessId = message.BusinessId,
            Partition = message.Partition,
            Payload = message.Payload
        });
    }

    public Task Handle(PartitionSagaProbeReply message, IMessageHandlerContext context)
    {
        Data.ReplyReceived = true;
        logger.LogInformation(
            "Received partition saga reply for CorrelationId={CorrelationId}, TenantId={TenantId}, BusinessId={BusinessId}, Partition={Partition}",
            message.CorrelationId,
            message.TenantId,
            message.BusinessId,
            message.Partition);
        return RequestTimeout<PartitionSagaCompletionTimeout>(context, CompletionTimeout);
    }

    public Task Timeout(PartitionSagaCompletionTimeout state, IMessageHandlerContext context)
    {
        logger.LogInformation(
            "Completed partition saga after timeout for CorrelationId={CorrelationId}, TenantId={TenantId}, BusinessId={BusinessId}, Partition={Partition}",
            Data.CorrelationId,
            Data.TenantId,
            Data.BusinessId,
            Data.Partition);
        MarkAsComplete();
        return Task.CompletedTask;
    }
}

public sealed class PartitionedEndpointSagaData : ContainSagaData
{
    public string CorrelationId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string BusinessId { get; set; } = string.Empty;
    public int Partition { get; set; }
    public bool ReplyReceived { get; set; }
}