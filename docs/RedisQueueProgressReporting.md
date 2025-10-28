# DataStream Upload Progress Reporting in Redis Queue

This document describes how DataStream upload progress reporting works seamlessly with the Redis Polling Request Queue (PRQ), allowing the Request Sender Node to receive real-time progress updates while the Request Processor Node performs the actual file uploads.

## Overview

The Redis Queue implementation enables transparent progress reporting for DataStream uploads without requiring changes to calling code. The system uses heartbeat messages to communicate progress information between nodes, ensuring that progress callbacks work identically whether using direct connections or the Redis PRQ.

## Key Components

### 1. HeartBeatMessage
- **Purpose**: Carries DataStream progress information between nodes
- **Structure**: Contains a `DataStreamProgress` dictionary mapping DataStream IDs to bytes uploaded
- **Creation**: Built using `HeartBeatMessage.Build(dataStreamsTransferProgress)`

### 2. RedisDataStreamTransferProgressRecorder
- **Purpose**: Tracks upload progress for individual DataStreams on the Request Processor Node
- **Functionality**: 
  - Implements `IDataStreamTransferProgress`
  - Thread-safe progress tracking using `Interlocked` operations
  - Records `CopiedSoFar` and `TotalLength` for each DataStream

### 3. HeartBeatDrivenDataStreamProgressReporter
- **Purpose**: Receives heartbeat messages and forwards progress to original callbacks on the Request Sender Node
- **Functionality**:
  - Implements `IGetNotifiedOfHeartBeats`
  - Maps DataStream IDs to original progress callbacks
  - Handles completion detection when upload reaches total length

### 4. NodeHeartBeatSender/Watcher
- **Purpose**: Manages bidirectional heartbeat communication between nodes
- **RequestSenderNode**: Sends basic heartbeats to indicate it's still alive
- **RequestProcessorNode**: Sends heartbeats containing DataStream progress information

## Progress Reporting Flow

### Phase 1: Request Preparation (Request Sender Node)
1. **Request Creation**: Client creates `RequestMessage` with DataStreams
2. **Progress Callback Setup**: Original DataStreams have progress callbacks configured
3. **Request Preparation**: 
   - `MessageSerialiserAndDataStreamStorage.PrepareRequest()` is called
   - DataStreams are switched to not report progress locally using `SwitchDataStreamsToNotReportProgress()`
   - `HeartBeatDrivenDataStreamProgressReporter` is created to handle remote progress updates
4. **Queue Operations**:
   - Request and DataStreams are stored in Redis
   - Request GUID is pushed to the processing queue
5. **Monitoring Setup**:
   - `NodeHeartBeatSender` starts sending basic heartbeats as RequestSenderNode
   - `NodeHeartBeatWatcher` subscribes to RequestProcessorNode heartbeats with progress callback

### Phase 2: Request Processing (Request Processor Node)
1. **Request Dequeue**: 
   - `DequeueAsync()` pops the next request GUID from the queue
   - `TryGetAndRemoveRequest()` atomically retrieves and removes the request data
2. **DataStream Setup**:
   - `ReadRequest()` creates `RedisDataStreamTransferProgressRecorder` for each DataStream
   - `RehydrateWithProgressReporting` wraps DataStreams with progress tracking
3. **Heartbeat Setup**:
   - `NodeHeartBeatSender` starts as RequestProcessorNode
   - Heartbeat messages are built using `HeartBeatMessage.Build(dataStreamsTransferProgress)`

### Phase 3: DataStream Upload with Progress Reporting
1. **Upload Process**:
   - DataStreams are uploaded to the target service in chunks
   - `StreamCopierWithProgress` calls `RedisDataStreamTransferProgressRecorder.Progress()` for each chunk
   - Progress is recorded thread-safely using `Interlocked.Exchange()`

2. **Heartbeat Transmission**:
   - Periodic heartbeats are sent containing current progress for all DataStreams
   - Heartbeat JSON structure: `{"DataStreamProgress": {"dataStreamId": bytesUploaded}}`
   - Messages are published to Redis channel: `{namespace}::NodeHeartBeatChannel::{endpoint}::{requestId}::RequestProcessorNode`

3. **Progress Reception**:
   - Request Sender Node receives heartbeats via Redis subscription
   - `NodeHeartBeatWatcher` deserializes `HeartBeatMessage`
   - `HeartBeatDrivenDataStreamProgressReporter.HeartBeatReceived()` is called
   - Progress is forwarded to original DataStream progress callbacks
   - Completion is detected when `copiedSoFar == totalLength`

### Phase 4: Request Completion
1. **RPC Completion**: Request Processor Node completes the service call
2. **Response Handling**: Response is stored in Redis and notification is published
3. **Cleanup**: Both nodes stop their heartbeat senders and watchers
4. **Result Return**: Response is returned to the original caller

## Technical Details

### Heartbeat Channel Structure
- **Channel Name Pattern**: `{namespace}::NodeHeartBeatChannel::{endpoint}::{requestId}::{nodeType}`
- **Node Types**: 
  - `RequestSenderNode`: Node that initiated the request
  - `RequestProcessorNode`: Node that processes the request and uploads DataStreams

### Progress Data Flow
```
DataStream Upload → RedisDataStreamTransferProgressRecorder.Progress() 
                 → HeartBeatMessage.Build() 
                 → Redis Pub/Sub Channel 
                 → NodeHeartBeatWatcher 
                 → HeartBeatDrivenDataStreamProgressReporter 
                 → Original Progress Callback
```

### Error Handling
- **Heartbeat Failures**: If heartbeats fail to send, the sender switches to "panic mode" with increased frequency
- **Deserialization Errors**: Failed heartbeat message parsing is logged but doesn't stop progress reporting
- **Node Disconnection**: Heartbeat timeouts trigger disconnection detection and request cancellation

## Benefits

1. **Transparency**: Progress reporting works identically with or without Redis PRQ
2. **No Code Changes**: Existing code using DataStream progress callbacks requires no modifications
3. **Real-time Updates**: Progress is reported as uploads happen, not just at completion
4. **Fault Tolerance**: System handles network issues and node disconnections gracefully
5. **Scalability**: Multiple nodes can process requests while maintaining progress visibility

## Integration Points

The progress reporting system integrates seamlessly with:
- Existing `IDataStreamTransferProgress` implementations
- `PercentageCompleteDataStreamTransferProgress` for percentage-based reporting
- Octopus Deploy's deployment progress tracking
- Any custom progress reporting implementations

This design ensures that the Redis Queue provides the same user experience as direct connections while enabling the scalability and reliability benefits of a distributed queue system.
