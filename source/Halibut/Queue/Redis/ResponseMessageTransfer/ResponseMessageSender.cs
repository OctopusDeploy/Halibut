#if NET8_0_OR_GREATER
using System;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Queue.Redis.MessageStorage;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Util;

namespace Halibut.Queue.Redis.ResponseMessageTransfer
{
    public class ResponseMessageSender
    {
        public static async Task SendResponse(
            IHalibutRedisTransport halibutRedisTransport, 
            Uri endpoint, 
            Guid activityId,
            RedisStoredMessage responseMessage,
            TimeSpan ttl,
            ILog log)
        {
            log.Write(EventType.Diagnostic, "Attempting to set response for - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
            
            await using var cts = new CancelOnDisposeCancellationToken();
            // More than ten minutes to send the response to redis, seems sus.
            cts.CancelAfter(TimeSpan.FromMinutes(10));
            
            try
            {
                log.Write(EventType.Diagnostic, "Marking response as set - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
                await halibutRedisTransport.SetResponseMessage(endpoint, activityId, responseMessage, ttl, cts.Token);
                
                log.Write(EventType.Diagnostic, "Publishing response notification - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
                await halibutRedisTransport.PublishThatResponseIsAvailable(endpoint, activityId, cts.Token);
                
                log.Write(EventType.Diagnostic, "Successfully set response - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
            }
            catch (OperationCanceledException ex)
            {
                log.Write(EventType.Error, "Set response operation timed out after 2 minutes - Endpoint: {0}, ActivityId: {1}, Error: {2}", endpoint, activityId, ex.Message);
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Failed to set response - Endpoint: {0}, ActivityId: {1}, Error: {2}", endpoint, activityId, ex.Message);
            }
        }
    }
}
#endif