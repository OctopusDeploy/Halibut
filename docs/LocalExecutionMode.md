# Local Execution Mode Design

## Overview

This document describes the design for a new execution mode in Halibut where RPC requests are executed locally on the worker node that dequeues work from the `IPendingRequestQueue`, rather than being proxied over TCP to a remote service.

## Motivation

Currently, Halibut's polling mode works as follows:
1. Client queues work in `IPendingRequestQueue` for a specific endpoint (e.g., `poll://tentacle-1`)
2. Worker (tentacle) establishes TCP connection and polls the queue
3. Worker dequeues requests and **proxies them over TCP** back to the server
4. Server executes the RPC and returns response over TCP
5. Worker receives response and applies it back to the queue

This design introduces unnecessary TCP overhead when the goal is to distribute work execution across multiple worker nodes. Instead of using the queue for work distribution and then still doing TCP RPC, we want:

1. Client queues work in `IPendingRequestQueue` for a logical worker pool (e.g., `local://worker-pool-a`)
2. Worker node registered as `local://worker-pool-a` dequeues requests
3. Worker **executes the RPC locally** on itself (no TCP)
4. Worker applies response back to the queue
5. Client receives response

This enables true distributed work execution patterns like:
- Worker pools processing background jobs
- Horizontally scaled compute nodes
- Fan-out task distribution without TCP bottlenecks

## Design

### 1. URL Scheme: `local://`

Use `local://` scheme to identify endpoints that should execute locally:
- `local://worker-pool-a` - worker pool A
- `local://worker-pool-b` - worker pool B
- `local://image-processor` - specialized image processing workers

Multiple workers can register with the same `local://` identifier to form a pool. The queue (in-memory or Redis) handles load distribution.

### 2. Service Registration and Worker Startup

Services in Halibut are registered **globally** on the runtime, not per-endpoint. The existing service registration mechanism works unchanged:

```csharp
var worker = new HalibutRuntime(serviceFactory);
worker.Services.AddSingleton<IMyService>(new MyServiceImpl());
```

To start processing work for a specific `local://` endpoint, workers use a new API:

```csharp
// Tell Halibut to start processing requests for this endpoint
await worker.PollLocalAsync("local://worker-pool-a", cancellationToken);
```

This is analogous to how TCP polling works today - services are registered globally, and the polling mechanism specifies which endpoint's queue to process.

### 3. Protocol Layer Changes (Option B)

Modify `MessageExchangeProtocol` to detect local execution mode and bypass TCP:

#### Current Flow in `ProcessReceiverInternalAsync()`:
```csharp
// Lines 225-288 in MessageExchangeProtocol.cs
async Task ProcessReceiverInternalAsync(RequestMessageWithCancellationToken? nextRequest)
{
    if (nextRequest != null)
    {
        var response = await SendAndReceiveRequest(nextRequest);  // TCP send/receive
        await pendingRequests.ApplyResponse(response, nextRequest.ActivityId);
    }
    await stream.SendNext();
}

async Task<ResponseMessage> SendAndReceiveRequest(RequestMessageWithCancellationToken request)
{
    await stream.SendAsync(request);           // Serialize and send over TCP
    return await stream.ReceiveResponseAsync(); // Deserialize from TCP
}
```

#### Proposed Flow with Local Execution:
```csharp
async Task ProcessReceiverInternalAsync(RequestMessageWithCancellationToken? nextRequest)
{
    if (nextRequest != null)
    {
        ResponseMessage response;

        if (isLocalExecutionMode)
        {
            // Execute locally using ServiceInvoker
            response = await ExecuteLocallyAsync(nextRequest);
        }
        else
        {
            // Existing TCP-based execution
            response = await SendAndReceiveRequest(nextRequest);
        }

        await pendingRequests.ApplyResponse(response, nextRequest.ActivityId);
    }

    if (!isLocalExecutionMode)
    {
        await stream.SendNext();  // Only needed for TCP mode
    }
}

async Task<ResponseMessage> ExecuteLocallyAsync(RequestMessageWithCancellationToken request)
{
    try
    {
        // Use existing ServiceInvoker to execute the method locally
        return await incomingRequestProcessor(request.RequestMessage, request.CancellationToken);
    }
    catch (Exception ex)
    {
        return ResponseMessage.FromException(request.RequestMessage, ex);
    }
}
```

### 4. Detection of Local Execution Mode

Add logic to detect `local://` scheme in the connection setup:

#### In `MessageExchangeProtocol` constructor or initialization:
```csharp
private readonly bool isLocalExecutionMode;
private readonly Func<RequestMessage, CancellationToken, Task<ResponseMessage>>? incomingRequestProcessor;

public MessageExchangeProtocol(
    IMessageExchangeStream stream,
    ConnectionId connectionId,
    IPendingRequestQueue? pendingRequests,
    Func<RequestMessage, CancellationToken, Task<ResponseMessage>>? incomingRequestProcessor,
    bool isLocalExecutionMode = false)  // New parameter
{
    this.stream = stream;
    this.connectionId = connectionId;
    this.pendingRequests = pendingRequests;
    this.incomingRequestProcessor = incomingRequestProcessor;
    this.isLocalExecutionMode = isLocalExecutionMode;
}
```

#### Detection in `PollingClient` or connection factory:
```csharp
// In PollingClient.ExecutePollingLoopAsync() or similar
var serviceUri = subscription.ServiceUri;
var isLocalMode = serviceUri.Scheme == "local";

// When creating protocol:
var protocol = new MessageExchangeProtocol(
    stream,
    connectionId,
    pendingRequestQueue,
    incomingRequestProcessor: isLocalMode ? localServiceInvoker : null,
    isLocalExecutionMode: isLocalMode
);
```

### 5. Component Changes Summary

#### A. `MessageExchangeProtocol.cs`
**File:** `/source/Halibut/Transport/Protocol/MessageExchangeProtocol.cs`

Changes:
1. Add `isLocalExecutionMode` field and constructor parameter
2. Add `incomingRequestProcessor` field to invoke services locally
3. Modify `ProcessReceiverInternalAsync()` to check mode and route accordingly
4. Add new `ExecuteLocallyAsync()` method
5. Skip `SendNext()` control message in local mode
6. Keep existing `SendAndReceiveRequest()` for TCP mode

Lines affected: ~225-294

#### B. `PollingClient.cs`
**File:** `/source/Halibut/Transport/PollingClient.cs`

Changes:
1. Detect `local://` scheme in `subscription.ServiceUri`
2. Pass local execution flag to `MessageExchangeProtocol`
3. Provide `incomingRequestProcessor` (ServiceInvoker) when in local mode
4. May need to skip actual TCP connection establishment for local mode
5. Or: Use a "null" stream implementation that throws if accidentally used

Lines affected: ~60-101 (ExecutePollingLoopAsync)

#### C. Connection/Transport Layer
**Files:**
- `/source/Halibut/Transport/Protocol/SecureClient.cs` or similar
- Potentially `/source/Halibut/HalibutRuntime.cs` for routing

Changes:
1. When establishing "connection" for `local://` endpoints:
   - Don't create actual TCP socket
   - Create dummy/null stream or special local stream
   - Pass local service invoker instead
2. Service routing already exists via `HalibutRuntime.Routes`
3. May need to ensure services are registered before polling starts

#### D. Stream Handling in Local Mode

Two options:

**Option 1: Null Stream**
- Create `NullMessageExchangeStream` that throws if methods are called
- Protocol layer ensures it's never used in local mode
- Safeguard against bugs

**Option 2: No Stream**
- Make `IMessageExchangeStream` nullable in `MessageExchangeProtocol`
- Check for null before any stream operations
- Cleaner but requires more null checks

Recommendation: Option 1 for safety.

### 6. Queue Behavior

No changes needed to `IPendingRequestQueue` interface or implementations:
- `PendingRequestQueueAsync` (in-memory) works as-is
- `RedisPendingRequestQueue` works as-is
- Queue doesn't care how execution happens
- Request/response correlation remains the same

### 7. Serialization

In local mode:
- **Request parameters:** Still need to be serialized when queued (supports Redis queue)
- **During execution:** Parameters deserialized from `RequestMessage.Params` by `ServiceInvoker`
- **Response:** Serialized back into `ResponseMessage`
- **No change needed:** Existing serialization paths handle this

However, there's potential for optimization:
- If using in-memory queue only, could skip serialization entirely
- Keep serialized objects in memory
- Future enhancement, not required for v1

### 8. Worker Pool Registration

Example usage:

```csharp
// Worker Node 1
var worker1 = new HalibutRuntime(serviceFactory);
worker1.Services.AddSingleton<IImageProcessor>(new ImageProcessorImpl());

// Start polling for work from the local://image-processor queue
await worker1.PollLocalAsync("local://image-processor", cancellationToken);

// Worker Node 2 (same pool) - on different machine or process
var worker2 = new HalibutRuntime(serviceFactory);
worker2.Services.AddSingleton<IImageProcessor>(new ImageProcessorImpl());

// Both workers poll the same queue, load balanced automatically
await worker2.PollLocalAsync("local://image-processor", cancellationToken);

// Client queues work
var client = new HalibutRuntime(serviceFactory);
var imageProcessor = client.CreateClient<IImageProcessor>("local://image-processor");
var result = await imageProcessor.ProcessImageAsync(imageData); // Queued, executed by worker1 or worker2
```

**Key points:**
- Services registered globally on the worker's `HalibutRuntime`
- `PollLocalAsync()` is the new API that starts processing for a specific endpoint
- No TCP connection needed - workers directly access the queue
- Multiple workers can call `PollLocalAsync()` with the same identifier to form a pool

### 9. Control Flow Differences

#### TCP Mode:
```
PollingClient
  → Establish TCP connection
  → MessageExchangeProtocol.ExchangeAsSubscriberAsync()
    → Loop:
      → DequeueAsync() from queue
      → SendAsync(request) over TCP to server
      → ReceiveResponseAsync() from TCP
      → ApplyResponse() to queue
      → SendNext() control message
```

#### Local Mode:
```
PollingClient
  → Skip TCP connection (or use null stream)
  → MessageExchangeProtocol.ExchangeAsSubscriberAsync()
    → Loop:
      → DequeueAsync() from queue
      → ExecuteLocallyAsync(request) using ServiceInvoker
      → ApplyResponse() to queue
      → (no SendNext needed)
```

### 10. Error Handling

Local execution errors:
- Caught in `ExecuteLocallyAsync()`
- Wrapped in `ResponseMessage.FromException()`
- Applied to queue like any other response
- Client receives error response normally

Same semantics as TCP mode - no special handling needed.

### 11. Cancellation

Cancellation tokens flow through:
1. Client provides `CancellationToken` to method call
2. Token stored in queue with request
3. Dequeued as `RequestMessageWithCancellationToken`
4. Passed to `ServiceInvoker.InvokeAsync()`
5. Method can check cancellation during execution

Local mode has better cancellation behavior:
- No TCP serialization delays
- Direct propagation to service method
- Faster response to cancellation

### 12. Testing Strategy

#### Unit Tests:
1. `MessageExchangeProtocol` with `isLocalExecutionMode = true`
   - Mock `incomingRequestProcessor`
   - Verify local execution path taken
   - Verify TCP methods not called

2. `PollingClient` with `local://` URI
   - Verify local mode detected
   - Verify protocol configured correctly

#### Integration Tests:
1. End-to-end with in-memory queue
   - Client queues work for `local://test`
   - Worker dequeues and executes locally
   - Client receives response

2. End-to-end with Redis queue
   - Multiple workers polling same `local://pool`
   - Verify work distribution
   - Verify no crosstalk between pools

3. Error scenarios
   - Service throws exception
   - Cancellation during execution
   - Worker crashes mid-execution

4. Performance comparison
   - Measure latency: local vs TCP mode
   - Measure throughput with worker pool

### 13. Configuration and Feature Flags

Consider adding configuration options:

```csharp
public class HalibutRuntimeConfiguration
{
    /// <summary>
    /// Enable local execution mode for 'local://' URIs.
    /// Default: true
    /// </summary>
    public bool EnableLocalExecutionMode { get; set; } = true;

    /// <summary>
    /// Timeout for local method execution.
    /// Default: 5 minutes (same as TCP default)
    /// </summary>
    public TimeSpan LocalExecutionTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
```

### 14. Migration Path

This feature is additive and backward compatible:
1. Existing `poll://` endpoints work unchanged
2. New `local://` endpoints opt into local execution
3. No breaking changes to APIs
4. Can incrementally adopt in applications

### 15. Performance Considerations

**Benefits:**
- No TCP serialization overhead
- No network latency
- No SSL/TLS handshake overhead
- Lower memory (no network buffers)
- Faster cancellation propagation

**Tradeoffs:**
- Still requires serialization for queue (especially Redis)
- Can't execute across machines (by design)
- Worker must have all required services registered

**Expected Improvements:**
- Latency: 10-100x faster (no network)
- Throughput: 5-10x higher (no TCP bottleneck)
- CPU: Lower (no SSL overhead)

### 16. Security Considerations

Local mode is inherently more secure:
- No network exposure
- No certificate validation needed
- No SSL/TLS overhead
- Requests never leave the machine

However:
- Queue still needs securing (Redis authentication, etc.)
- Service authorization still applies
- Trust boundary is at the queue, not transport

### 17. Open Questions

1. **Connection management:** Should we create a fake connection for local mode, or special-case it everywhere?
   - **Recommendation:** Use null/mock stream, maintain consistent abstractions

2. **Metrics and logging:** How to differentiate local vs TCP executions in telemetry?
   - **Recommendation:** Add tags/properties to logs indicating execution mode

3. **Health checks:** How to verify workers are running and polling?
   - **Recommendation:** Leverage existing Redis heartbeat mechanism

4. **Queue selection:** Should `local://` always use Redis, or support in-memory too?
   - **Recommendation:** Support both, let application choose

5. **Backward compatibility:** What if old worker connects with `local://` before feature is implemented?
   - **Recommendation:** Fail fast with clear error message

## Implementation Plan

### Phase 1: Core Protocol Changes
1. Modify `MessageExchangeProtocol` to support local execution mode
2. Add `isLocalExecutionMode` flag and `ExecuteLocallyAsync()` method
3. Unit tests for protocol layer

### Phase 2: Transport Integration
1. Update `PollingClient` to detect `local://` scheme
2. Pass local service invoker to protocol
3. Handle stream creation for local mode

### Phase 3: Runtime Integration
1. Ensure service routing works with `local://` endpoints
2. Configuration options for local execution
3. Integration tests with in-memory queue

### Phase 4: Redis Support
1. Test local execution with Redis queue
2. Multi-worker scenarios
3. Performance benchmarks

### Phase 5: Documentation and Examples
1. Update Halibut documentation
2. Example applications showing worker pool pattern
3. Migration guide for existing polling users

## Summary

Local execution mode extends Halibut's queue-based architecture to enable true distributed work processing without TCP overhead. By modifying the protocol layer to detect `local://` URIs and execute requests locally using the existing `ServiceInvoker`, we can support worker pool patterns efficiently while maintaining backward compatibility and leveraging existing queue implementations.

Key benefits:
- 10-100x lower latency
- No TCP/SSL overhead
- True horizontal scaling via worker pools
- Queue-agnostic (works with in-memory and Redis)
- Backward compatible with existing code