﻿using System.Linq.Expressions;

namespace FastEndpoints;

/// <summary>
/// interface for implementing a storage provider for event hub app (gRPC server)
/// </summary>
/// <typeparam name="TStorageRecord">the type of the storage record</typeparam>
public interface IEventHubStorageProvider<TStorageRecord> where TStorageRecord : IEventStorageRecord
{
    /// <summary>
    /// this method will only be called once (for each event type) on app startup. if there are any pending records on storage from a previous app run,
    /// simply return a collection of unique subscriber IDs.
    /// </summary>
    ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<TStorageRecord> parameters);

    /// <summary>
    /// store the event storage record however you please. ideally on a nosql database.
    /// </summary>
    /// <param name="e">the event storage record which contains the actual event object as well as some metadata</param>
    /// <param name="ct">cancellation token</param>
    ValueTask StoreEventAsync(TStorageRecord e, CancellationToken ct);

    /// <summary>
    /// fetch the next batch of pending event storage records that need to be processed.
    /// </summary>
    ValueTask<IEnumerable<TStorageRecord>> GetNextBatchAsync(GetPendingRecordsParams<TStorageRecord> parameters);

    /// <summary>
    /// mark the event storage record as complete by either replacing the entity on storage with the supplied instance or
    /// simply update the <see cref="IEventStorageRecord.IsComplete"/> field to true with a partial update operation.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="ct">cancellation token</param>
    ValueTask MarkEventAsCompleteAsync(TStorageRecord e, CancellationToken ct);

    /// <summary>
    /// this method will be called hourly. implement this method to remove stale records (completed or (expired and incomplete)) from storage.
    /// or instead of removing them, you can move them to some other location (dead-letter-queue maybe) or for inspection by a human.
    /// or if you'd like to retry expired events, update the <see cref="IEventStorageRecord.ExpireOn"/> field to a future date/time.
    /// </summary>
    ValueTask PurgeStaleRecordsAsync();
}

/// <summary>
/// parameters to use in finding subscriber IDs to restore
/// </summary>
/// <typeparam name="TStorageRecord">the type of event storage record</typeparam>
public struct SubscriberIDRestorationParams<TStorageRecord> where TStorageRecord : IEventStorageRecord
{
    /// <summary>
    /// the type name of the events to search for which correlates to <see cref="IEventStorageRecord.EventType"/>
    /// </summary>
    public string EventType { get; internal set; }

    /// <summary>
    /// a boolean lambda expression to match pending records.
    /// <code>
    ///     Where(e => e.EventType == eventType &amp;&amp; !e.IsComplete &amp;&amp; DateTime.UtcNow &lt;= e.ExpireOn)
    ///     Select(e => e.SubscriberID)
    /// </code>
    /// </summary>
    public Expression<Func<TStorageRecord, bool>> Match { get; internal set; }

    /// <summary>
    /// member expression to select/project the UNIQUE <see cref="IEventStorageRecord.SubscriberID"/> values.
    /// <code>
    ///     Select(e => e.SubscriberID)
    /// </code>
    /// </summary>
    public Expression<Func<TStorageRecord, string>> Projection { get; internal set; }

    /// <summary>
    /// a cancellation token
    /// </summary>
    public CancellationToken CancellationToken { get; internal set; }
}

public struct GetPendingRecordsParams<TStorageRecord> where TStorageRecord : IEventStorageRecord
{
    /// <summary>
    /// the subscriber ID to fetch the next batch of pending records for
    /// </summary>
    public string SubscriberID { get; internal set; }

    /// <summary>
    /// a boolean lambda expression to match the next batch of pending records
    /// <code>
    ///   Where(e => e.SubscriberID == subscriberID &amp;&amp; !e.IsComplete &amp;&amp; DateTime.UtcNow &lt;= e.ExpireOn)
    /// </code>
    /// </summary>
    public Expression<Func<TStorageRecord, bool>> Match { get; internal set; }

    /// <summary>
    /// the number of pending records to fetch
    /// </summary>
    public int Limit { get; internal set; }

    /// <summary>
    /// cancellation token
    /// </summary>
    public CancellationToken CancellationToken { get; internal set; }
}