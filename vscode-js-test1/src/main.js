
function main()
{
    'use strict';


    await_test();
}

async function await_test()
{
    console.log("start");

    for (let i = 0; i < 20; i++)
    {
        await test(100);
        console.log(i);
    }

    console.log("end");
}

function test(msec)
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

class Car
{
    constructor(name, age)
    {
        this.Name = name;
        this.Age = age;
    }

    Inc()
    {
        //throw new Error("Neko!");
        this.Age++;
    }

    static Test()
    {
        if (Car.Seq == undefined) Car.Seq = 0;
        Car.Seq++;
        return Car.Seq;
    }
}



function c1(str_value)
{
    return {
        Str: str_value,
        Hello()
        {
            console.log(`Hello World ${this.Str}`);
        },
    }
}

function rand(m, n)
{
    return m + Math.floor(Math.random() * (n - m + 1));
}

function randFace()
{
    const a = ["a", "b", "c", "d", "e"];

    return a[rand(0, a.length - 1)];
}

function test2(a, ...args)
{
    console.log(a);
    console.log(args);
}

function test1({ x, y })
{
    return x + y;
}

function sum(x = 1, y = 2)
{
    return x + y;
}






main();
