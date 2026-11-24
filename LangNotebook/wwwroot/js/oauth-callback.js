// The cloud provider redirects the sign-in window here. Pass the result (authorization code or token, plus the
// "state" value) back to the app window and close. A BroadcastChannel is used as well as window.opener because some
// provider pages sever the opener link.
(function () {
    'use strict';
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
    setTimeout(function () { window.close(); }, 150);
})();
