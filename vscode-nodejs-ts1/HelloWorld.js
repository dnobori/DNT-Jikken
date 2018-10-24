"use strict";
var __importStar = (this && this.__importStar) || function (mod) {
    if (mod && mod.__esModule) return mod;
    var result = {};
    if (mod != null) for (var k in mod) if (Object.hasOwnProperty.call(mod, k)) result[k] = mod[k];
    result["default"] = mod;
    return result;
};
Object.defineProperty(exports, "__esModule", { value: true });
const https = __importStar(require("https"));
class Startup {
    static main() {
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
//# sourceMappingURL=HelloWorld.js.map