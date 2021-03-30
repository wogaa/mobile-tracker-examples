# Introduction
This is a Cordova sample app implement with Wogaa tracker cordova plugin

## Add Wogaa Tracker Cordova plugin
```
cordova plugin add https://github.com/wogaa/wogaa-tracker-cordova-plugin.git
```

## Start Tracker
In your deviceready event add the start tracker

### Staging
```js
window.plugins.wogaatracker.start("STAGING");
```

### Production
```js
window.plugins.wogaatracker.start("PRODUCTION");
```

## Track Screen View
In each of your screen, add the following code

```js
window.plugins.wogaatracker.trackScreenView("Screen View Name");
```