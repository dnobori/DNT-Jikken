# node.js with VS Code メモ

参考 URL:
* https://code.visualstudio.com/docs/languages/typescript
* https://qiita.com/selmertsx/items/c7a17261dcf802df2422

## 初期設定
コマンドプロンプトから
```
npm init -y

npm i -DE typescript tslint

node_modules\.bin\tsc --init
node_modules\.bin\tslint --init

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
デバッグ -> 構成の追加 -> Chrome.js  
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

