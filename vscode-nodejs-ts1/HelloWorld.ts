import * as https from "https";


class Startup {
    public static main(): number {
        const msg = "Hello World 15";
        console.log(msg);

        //const http = require("http");
        https.get("https://www.softether.jp/", res => {
            let body = "";
            res.on("data", data => {
                body += data;
            });
            res.on("end", () => {
                console.log(body);
            });
        });


        return -1;
    }
}

Startup.main();

