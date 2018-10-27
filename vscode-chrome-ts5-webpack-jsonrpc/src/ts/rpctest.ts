
export default class RpcTest
{
    public static test()
    {
        RpcTest.test1();
    }

    public static async test1()
    {
        //try
        if (true)
        {
            //let res = await fetch("https://127.0.0.1/todos/1");
            let res = await fetch("https://jsonplaceholder.typicode.com/todos/1");

            let txt = await res.text();

            console.log(txt);
        }

        if (false)
        {
            let r = new XMLHttpRequest();

            console.log("3");
            r.open("GET", "https://127.0.0.1/todos/1");
            console.log("0");
            r.send();
            console.log("1");
            r.onload = function ()
            {
                console.log("2");
            };
            r.onerror = function (ev)
            {
                console.log("4");
            }
        }
        /*catch (err)
        {
            console.log("error str: " + err);
        }*/
    }
}

