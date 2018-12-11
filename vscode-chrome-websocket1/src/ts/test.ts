class AsyncWebSocket
{
    private web_socket: WebSocket;

    constructor(url: string)
    {
        this.web_socket = new WebSocket(url);
    }

    public Open(): Promise<void>
    {
        let ws = this.web_socket;
        return new Promise(function (resolve, reject)
        {
            ws.onopen = (ev: Event) =>
            {
                resolve();
            };

            ws.onerror = (ev: Event) =>
            {
                reject(new Error("WebSocket open error."));
            };
        });
    }

    public SendStr(str: string): Promise<void>
    {
        let ws = this.web_socket;
        ws.send(str);

        return new Promise(function (resolve, reject)
        {
            resolve();
        });
    }

    public RecvStr(): Promise<string>
    {
        let ws = this.web_socket;
        return new Promise<string>(function (resolve, reject)
        {
            ws.onmessage = (ev: MessageEvent) =>
            {
                resolve(ev.data);
            };

            ws.onerror = (ev: Event) =>
            {
                reject(new Error("WebSocket recv error."));
            };
        });
    }

    public SendBin(data: ArrayBuffer): Promise<void>
    {
        let ws = this.web_socket;
        ws.send(data);

        return new Promise(function (resolve, reject)
        {
            resolve();
        });
    }

    public RecvBin(): Promise<ArrayBuffer>
    {
        let ws = this.web_socket;
        return new Promise<ArrayBuffer>(function (resolve, reject)
        {
            ws.onmessage = (ev: MessageEvent) =>
            {
                let b: Blob = ev.data;
                let r = new FileReader();
                r.onloadend = (ev2: ProgressEvent) =>
                {
                    resolve(r.result as ArrayBuffer);
                };
                r.onabort = () => reject(new Error("onabort"));
                r.onerror = () => reject(new Error("onabort"));
                r.readAsArrayBuffer(b);
            };

            ws.onerror = (ev: Event) =>
            {
                reject(new Error("WebSocket recv error."));
            };
        });
    }

    public Close(): Promise<void>
    {
        let ws = this.web_socket;
        ws.close();
        return new Promise(function (resolve, reject)
        {
            ws.onclose = (ev: Event) =>
            {
                resolve();
            };

            ws.onerror = (ev: Event) =>
            {
                resolve();
            };
        });
    }
}


function gen_str(len: number): string
{
    let ret = "";
    for (let i = 0; i < len; i++)
    {
        ret += String.fromCharCode(0x30 + (i % 10));
    }
    return ret;
}

async function test()
{
    let url = "ws://echo.websocket.org";

    let ws = new AsyncWebSocket(url);

    console.log("opening.");

    await ws.Open();

    console.log("opened.");

    // let long_str = gen_str(100000);
    // await ws.SendStr(long_str);
    // let long_str_ret = await ws.RecvStr();
    // console.log("RecvStr Long: " + long_str_ret);

    // for (let i = 0; i < 5; i++)
    // {
    //     let str = "Hello " + i;
    //     await ws.SendStr(str);

    //     let ret = await ws.RecvStr();

    //     console.log("RecvStr: " + ret);
    // }

    for (let i = 0; i < 5; i++)
    {
        let str = "Hello " + i;
        //await ws.SendStr(str);

        await ws.SendBin((new TextEncoder().encode(str)).buffer);

        let ret = await ws.RecvBin();

        console.log("RecvBin: " + new TextDecoder().decode(ret));
    }

    await ws.Close();

    console.log("Closed.");
}

console.log("Hello World");


test();


