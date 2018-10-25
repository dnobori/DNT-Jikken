module.exports = {
    "extends": "eslint:recommended",
    "parserOptions": {
        "ecmaVersion": 2017,
        "sourceType": "module",
        "ecmaFeatures": {
            "jsx": true
        }
    },
    "rules":
    {
        "no-console": "off",
        "no-unused-vars": "off",
        "no-empty": "off",
    },
    "env": {
        "browser": true,
        "jquery": true,
        "es6": true,
    },
    "globals": {
        "paper": true,
        "Tool": true,
        "Shape": true,
        "PointText": true,
    },
}
