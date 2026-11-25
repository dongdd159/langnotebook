// Deployment settings, read from two JSON files in wwwroot/config:
//   app.config.example.json  committed; every default (client IDs empty)
//   app.config.json          git-ignored; this deployment's own values, optional
// A top-level section in app.config.json (cloud, ai) replaces the same section of the example. A missing or unreadable
// file is skipped, so the app still starts. The C# side reads the same files (see Service/AppConfig.cs).
//
// Everything here is delivered to the browser with the app, so it is not secret: keep out of these files anything that
// must stay private (such as an API secret). Client IDs are public by design.
window.langConfig = (function () {
    'use strict';

    var DEFAULTS_FILE = 'config/app.config.example.json';
    var LOCAL_FILE = 'config/app.config.json';
    var data = {};

    // Resolves to the parsed object, or null for a missing file, an error, or something that isn't JSON (some hosts
    // answer a missing file with the index page).
    function readJson(name) {
        return fetch(new URL(name, document.baseURI), { cache: 'no-cache' })
            .then(function (response) {
                return response.ok ? response.text() : null;
            })
            .then(function (text) {
                if (!text) return null;
                var value = JSON.parse(text);
                return value && typeof value === 'object' && !Array.isArray(value) ? value : null;
            })
            .catch(function () {
                return null;
            });
    }

    var ready = Promise.all([readJson(DEFAULTS_FILE), readJson(LOCAL_FILE)]).then(function (files) {
        data = Object.assign({}, files[0] || {}, files[1] || {});
        return data;
    });

    return {
        ready: ready,
        get: function () {
            return data;
        },
        clientId: function (provider) {
            var section = data.cloud && data.cloud[provider];
            return section && typeof section.clientId === 'string' ? section.clientId.trim() : '';
        }
    };
})();
