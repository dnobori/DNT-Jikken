class AsyncWebSocket
{
    private ws: WebSocket;

    constructor(url: string)
    {
        this.ws = new WebSocket(url);
    }
}


function test(): void
{
    let url = "wss://echo.websocket.org/";

    let ws = new AsyncWebSocket(url);
}

console.log("Hello World");


test();


