const path = require('path');

module.exports = {
    mode: "development", // "production" | "development" | "none"

    entry: "./src/ts/HelloWorld.ts",

    output: {
        path: path.join(__dirname, "dist"),
        filename: "bundle.js"
    },

    module: {
        rules: [{
            test: /\.ts$/,
            use: 'ts-loader',
            include: path.join(__dirname, "./src/ts/")
        }]
    },

    resolve: {
        modules: [
            "node_modules",
        ],
        extensions: [
            '.ts',
            '.js'
        ]
    }
};
