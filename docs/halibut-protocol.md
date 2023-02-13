# Halibut Protocol data exchange

When invoking a remote method in both polling and listening mode, the same general process is followed:

 - The Server sends a `Request` containing the method to execute and data.
 - The client responds with a `Response`.
 - The client sends a `NEXT` control message.
 - The Server semds a `Proceed` control message.
 - The above 4 steps are repeated until the connection is terminated or control `END` messages are sent.


## Listening client protocol data exchange:

![Listening client protocol data exchange](images/listeningprotocoldata.png)