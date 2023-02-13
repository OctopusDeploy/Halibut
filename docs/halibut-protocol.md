# Halibut Protocol data exchange

When invoking a remote method in both polling and listening mode, the same general process is followed:

 - The Server sends a `Request` message containing the method to execute and data.
 - The client executes the method and sends the result in a `Response` message.
 - The client sends a `NEXT` control message.
 - The Server semds a `Proceed` control message.
 - The above 4 steps are repeated until the connection is terminated or control `END` messages are sent.


## Listening client protocol data exchange

![Listening client protocol data exchange](images/listeningprotocoldata.png)

## Polling client protocol data exchange

![Polling client protocol data exchange](images/pollingprotocoldata.png)

## Request and Response message format

Request and Response messages are sent and received in the same format with only the data within the BSON section changing to denote the type of message.

![Request/Response message format](images/message-format.png)


### Example 

Below is an example of the Request and Response messages when making a simple RPC with Halibut.

For a RPC call made with
```
public interface ISample
{
    public string SayHello(string message, DataStream theData);
}

public class HelloMessageData
{
    public DataStream TheData { get; set; }
}

// Example call
var data = "This message is being sent to the tentacle as a data stream".ToUtf8();

var response = echo.SayHello("Alice", DataStream.FromBytes(data));
```

The resulting Request

![Example request](images/example-request.png)

The resulting response

![Example response](images/example-response.png)