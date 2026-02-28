using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MultiTenantPoc;

public sealed class TransactionOutcomeDbInterceptor : DbTransactionInterceptor
{
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        TagTransactionOutcome("committed");
        base.TransactionCommitted(transaction, eventData);
    }

    public override Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        TagTransactionOutcome("committed");
        return base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        TagTransactionOutcome("rolled_back");
        base.TransactionRolledBack(transaction, eventData);
    }

    public override Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        TagTransactionOutcome("rolled_back");
        return base.TransactionRolledBackAsync(transaction, eventData, cancellationToken);
    }

    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
    {
        TagTransactionOutcome("failed");
        base.TransactionFailed(transaction, eventData);
    }

    public override Task TransactionFailedAsync(
        DbTransaction transaction,
        TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        TagTransactionOutcome("failed");
        return base.TransactionFailedAsync(transaction, eventData, cancellationToken);
    }

    static void TagTransactionOutcome(string outcome)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        activity.SetTag("db.transaction.outcome", outcome);
        activity.AddEvent(new ActivityEvent($"db.transaction.{outcome}"));
    }
}