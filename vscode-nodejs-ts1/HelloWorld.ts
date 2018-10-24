import * as https from "https";


class Startup {
    public static main(): number {
        const msg = "Hello World 15";
        console.log(msg);

        //const http = require("http");
        https.get("https://www.softether.jp/", res => {
            let body = "";
            let num = 0;
            res.on("data", data => {
                body += data;
                num++;
            });
            res.on("end", () => {
                console.log(body);

                console.log("total: " + num + " segments");
            });
        });


        return -1;
    }
}

Startup.main();

