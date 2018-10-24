# node.js React with VS Code メモ

参考 URL:
* https://code.visualstudio.com/docs/nodejs/reactjs-tutorial
* https://qiita.com/IgnorantCoder/items/d26083d9f886ca66d4ae

## 初期設定
コマンドプロンプトから
```
npm init -y

npm install --save react react-dom

npm install --save-dev @types/react @types/react-dom

npm install --save-dev webpack webpack-cli

npm install --save-dev typescript ts-loader

npm install --save-dev tslint tslint-loader tslint-config-airbnb

npm install --save-dev html-webpack-plugin

npm install --save-dev webpack-serve
```

`tslint.json` ファイルを編集
```
{
    "defaultSeverity": "warn",
    "extends": [
        "tslint:recommended"
    ],
    "jsRules": {},
    "rules": {
        "comment-format": false,
        "no-consecutive-blank-lines": false,
        "no-trailing-whitespace": false,
        "no-console": false
    },
    "rulesDirectory": []
}
```

`tsconfig.json` ファイルを編集
```
"target": "es2018",
"allowJs": true,
"sourceMap": true,
"strict": true,
"esModuleInterop": true
```

## HelloWorld.ts の実験
`HelloWorld.ts` を作成

F7 - Build を実行すると、コンパイル済み js ファイルが生成される。

```
node HelloWorld.js
```
でテスト実行。

F5 でデバッグ実行。

## VS Code のビルドタスクの作成
F1 -> `build` -> 既存のビルドタスクを構成する -> テンプレート -> `tsc watch`   
を実行すると、`.vscode/tasks.json` が作成される。

以下のように修正する。
```
{
    "version": "2.0.0",
    "tasks": [
        {
            "label": "Watch and Compile TypeScript",
            "type": "typescript",
            "tsconfig": "tsconfig.json",
            "option": "watch",
            "problemMatcher": [
                "$tsc-watch"
            ],
            "group": {
                "kind": "build",
                "isDefault": true
            }
        }
    ]
}
```

## VS Code のデバッグタスクの作成
デバッグ -> 構成の追加 -> Node.js  
を実行すると、`.vscode/launch.json` が作成される。

以下のように修正する。
```
{
    "version": "0.2.0",
    "configurations": [
        {
            "type": "node",
            "request": "launch",
            "name": "Debug TypeScript",
            "program": "${file}",
            "preLaunchTask": "Watch and Compile TypeScript",
            "cwd": "${workspaceFolder}",
            "outFiles": [
                "${workspaceFolder}/**/*.js"
            ],
            "console": "integratedTerminal"
        }
    ]
}```

