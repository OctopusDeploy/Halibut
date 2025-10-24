using System;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Transport.Protocol;
using Halibut.Util;

#if NET8_0_OR_GREATER
namespace Halibut.Queue.Redis.Cancellation
{
    public class RequestCancelledSender
    {
        // How long the CancelRequestMarker will sit in redis before it times out.
        // If it does timeout it won't matter since the request-sender will stop sending heart beats
        // causing the request-processor to cancel the request anyway. 
        static TimeSpan CancelRequestMarkerTTL = TimeSpan.FromMinutes(15);
        
        public static async Task TrySendCancellation(
            IHalibutRedisTransport halibutRedisTransport, 
            Uri endpoint, 
            RequestMessage request,
            ILog log)
        {
            log.Write(EventType.Diagnostic, "Attempting to send cancellation for request - Endpoint: {0}, ActivityId: {1}", endpoint, request.ActivityId);
            
            await using var cts = new CancelOnDisposeCancellationToken();
            cts.CancelAfter(TimeSpan.FromMinutes(2)); // Best efforts.
            
            try
            {
                log.Write(EventType.Diagnostic, "Publishing cancellation notification - Endpoint: {0}, ActivityId: {1}", endpoint, request.ActivityId);
                await halibutRedisTransport.PublishCancellation(endpoint, request.ActivityId, cts.Token);
                
                log.Write(EventType.Diagnostic, "Marking request as cancelled - Endpoint: {0}, ActivityId: {1}", endpoint, request.ActivityId);
                await halibutRedisTransport.MarkRequestAsCancelled(endpoint, request.ActivityId, CancelRequestMarkerTTL, cts.Token);
                
                log.Write(EventType.Diagnostic, "Successfully sent cancellation for request - Endpoint: {0}, ActivityId: {1}", endpoint, request.ActivityId);
            }
            catch (OperationCanceledException ex)
            {
                log.Write(EventType.Error, "Cancellation send operation timed out after 2 minutes - Endpoint: {0}, ActivityId: {1}, Error: {2}", endpoint, request.ActivityId, ex.Message);
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Failed to send cancellation for request - Endpoint: {0}, ActivityId: {1}, Error: {2}", endpoint, request.ActivityId, ex.Message);
            }
        }
    }
}
#endif