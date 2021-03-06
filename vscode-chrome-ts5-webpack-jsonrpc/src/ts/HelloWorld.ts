import "core-js/es6/promise";
import 'whatwg-fetch'

import RPC from "./rpctest";
import UUID from "uuid";


class Startup
{
    public static main(): number
    {
        let msg = "Hello World " + 51;
        console.log(msg);

        RPC.test();

        return 0;
    }

}

async function await_test()
{
    console.log("start");

    for (let i = 0; i < 5; i++)
    {
        await test(100);
        //console.log(i);

        console.log(UUID.v4());
    }

    console.log("end");
}

function test(msec: number)
{
    return new Promise(
        function (resolve, reject)
        {
            setTimeout(function ()
            {
                //throw "Hello Error";
                //reject("neko");
                resolve();
            },
                msec);
        }
    );
}


Startup.main();

