window.langTheme = (function () {
    const COLORS = { light: '#efede8', dark: '#121418' };
    function systemMode() {
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
    function mode() {
        return document.documentElement.getAttribute('data-theme') || systemMode();
    }
    function paintMeta(value) {
        const meta = document.querySelector('meta[name="theme-color"]');
        if (meta) {
            meta.setAttribute('content', COLORS[value]);
        }
    }
    function set(value) {
        document.documentElement.setAttribute('data-theme', value);
        try {
            localStorage.setItem('theme', value);
        } catch (e) { }
        paintMeta(value);
        return value;
    }
    paintMeta(mode());
    return { mode: mode, set: set };
})();