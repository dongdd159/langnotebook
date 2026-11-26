// The cloud provider redirects the sign-in window to <app>/oauth-callback (no file extension). Hosts and the dev server
// answer an address like that with index.html, so this script, loaded by index.html, recognises the address, hands the
// result (authorization code or token, plus the "state" value) to the app window and closes. Blazor is not started in
// that window (index.html checks window.langOAuthCallback). On any other address this script does nothing.
//
// A BroadcastChannel is used as well as window.opener because some provider pages sever the opener link.
(function () {
    'use strict';
    if (!/\/oauth-callback\/?$/.test(location.pathname)) return;
    window.langOAuthCallback = true;

    function parse(text) {
        var result = {};
        text.replace(/^[?#]/, '').split('&').forEach(function (pair) {
            if (!pair) return;
            var index = pair.indexOf('=');
            var key = decodeURIComponent((index < 0 ? pair : pair.slice(0, index)).replace(/\+/g, ' '));
            var value = index < 0 ? '' : decodeURIComponent(pair.slice(index + 1).replace(/\+/g, ' '));
            result[key] = value;
        });
        return result;
    }

    var params = Object.assign(parse(location.search), parse(location.hash));
    // Remove the code or token from the address bar and history straight away.
    try {
        history.replaceState(null, '', location.pathname);
    } catch (e) { }

    var message = { type: 'lang-cloud-oauth', params: params };
    try {
        var channel = new BroadcastChannel('lang-cloud-oauth');
        channel.postMessage(message);
        channel.close();
    } catch (e) { }
    try {
        if (window.opener) window.opener.postMessage(message, location.origin);
    } catch (e) { }

    function showMessage() {
        var app = document.getElementById('app');
        if (app) app.textContent = 'Finishing sign-in… you can close this window if it stays open.';
        document.title = 'Signing in…';
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', showMessage);
    else showMessage();
    setTimeout(function () { window.close(); }, 150);
})();
