// Deployment settings, read from wwwroot/config/app.config.json: the one configuration file. It is git-ignored and
// optional; docs/app.config.example.json is the template. A missing, unreadable or invalid file is skipped, so the app
// still starts (the cloud buttons then show "Not set up"). The C# side reads the same file for everything else in it
// (see Service/AppConfig.cs, which also holds the built-in defaults).
//
// Everything here is delivered to the browser with the app, so it is not secret: keep out of the file anything that
// must stay private (such as an API secret). Client IDs are public by design.
window.langConfig = (function () {
    'use strict';

    var CONFIG_FILE = 'config/app.config.json';
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

    var ready = readJson(CONFIG_FILE).then(function (file) {
        data = file || {};
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
