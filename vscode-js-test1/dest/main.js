"use strict";

var _createClass = function () { function defineProperties(target, props) { for (var i = 0; i < props.length; i++) { var descriptor = props[i]; descriptor.enumerable = descriptor.enumerable || false; descriptor.configurable = true; if ("value" in descriptor) descriptor.writable = true; Object.defineProperty(target, descriptor.key, descriptor); } } return function (Constructor, protoProps, staticProps) { if (protoProps) defineProperties(Constructor.prototype, protoProps); if (staticProps) defineProperties(Constructor, staticProps); return Constructor; }; }();

var await_test = function () {
    var _ref = _asyncToGenerator( /*#__PURE__*/regeneratorRuntime.mark(function _callee() {
        var i;
        return regeneratorRuntime.wrap(function _callee$(_context) {
            while (1) {
                switch (_context.prev = _context.next) {
                    case 0:
                        console.log("start");

                        i = 0;

                    case 2:
                        if (!(i < 20)) {
                            _context.next = 9;
                            break;
                        }

                        _context.next = 5;
                        return test(100);

                    case 5:
                        console.log(i);

                    case 6:
                        i++;
                        _context.next = 2;
                        break;

                    case 9:

                        console.log("end");

                    case 10:
                    case "end":
                        return _context.stop();
                }
            }
        }, _callee, this);
    }));

    return function await_test() {
        return _ref.apply(this, arguments);
    };
}();

function _classCallCheck(instance, Constructor) { if (!(instance instanceof Constructor)) { throw new TypeError("Cannot call a class as a function"); } }

function _asyncToGenerator(fn) { return function () { var gen = fn.apply(this, arguments); return new Promise(function (resolve, reject) { function step(key, arg) { try { var info = gen[key](arg); var value = info.value; } catch (error) { reject(error); return; } if (info.done) { resolve(value); } else { return Promise.resolve(value).then(function (value) { step("next", value); }, function (err) { step("throw", err); }); } } return step("next"); }); }; }

function main() {
    'use strict';

    await_test();
}

function test(msec) {
    return new Promise(function (resolve, reject) {
        setTimeout(function () {
            //throw "Hello Error";
            //reject("neko");
            resolve();
        }, msec);
    });
}

var Car = function () {
    function Car(name, age) {
        _classCallCheck(this, Car);

        this.Name = name;
        this.Age = age;
    }

    _createClass(Car, [{
        key: "Inc",
        value: function Inc() {
            //throw new Error("Neko!");
            this.Age++;
        }
    }], [{
        key: "Test",
        value: function Test() {
            if (Car.Seq == undefined) Car.Seq = 0;
            Car.Seq++;
            return Car.Seq;
        }
    }]);

    return Car;
}();

function c1(str_value) {
    return {
        Str: str_value,
        Hello: function Hello() {
            console.log("Hello World " + this.Str);
        }
    };
}

function rand(m, n) {
    return m + Math.floor(Math.random() * (n - m + 1));
}

function randFace() {
    var a = ["a", "b", "c", "d", "e"];

    return a[rand(0, a.length - 1)];
}

function test2(a) {
    console.log(a);

    for (var _len = arguments.length, args = Array(_len > 1 ? _len - 1 : 0), _key = 1; _key < _len; _key++) {
        args[_key - 1] = arguments[_key];
    }

    console.log(args);
}

function test1(_ref2) {
    var x = _ref2.x,
        y = _ref2.y;

    return x + y;
}

function sum() {
    var x = arguments.length > 0 && arguments[0] !== undefined ? arguments[0] : 1;
    var y = arguments.length > 1 && arguments[1] !== undefined ? arguments[1] : 2;

    return x + y;
}

main();
//# sourceMappingURL=main.js.map