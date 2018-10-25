## 初期設定
コマンドプロンプトから
```
mkdir dest

npm init -y

npm install eslint eslint-config-airbnb-base eslint-plugin-import --save

npm install babel babel-core babel-cli babel-preset-env @babel/core @babel/preset-env gulp gulp-babel babel-polyfill babel-plugin-transform-runtime --save

```

## babel の設定
`gulpfile.js` を作成

```
var gulp = require('gulp');
var babel = require('gulp-babel');

gulp.task('default', function ()
{
    return gulp.src(['src/*.js', 'src/**/*.js'])
        .pipe(babel())
        .pipe(gulp.dest('dest'));
});
```

F1 -> Task Config -> `gulp`

注: うまくいかない


`.babelrc` を作成
```
{
    "presets": [
        [
            "env",
            {
                "modules": false,
                "targets": {
                    "browsers": [
                        "IE 6"
                    ]
                }
            }
        ]
    ]
}
```

手動実行方法
```
node_modules\.bin\babel src/ -d dest/ --source-maps --presets=env
```
