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
    // 'light' | 'dark' when the user picked one, 'system' when following the OS setting.
    function stored() {
        const value = document.documentElement.getAttribute('data-theme');
        return value === 'light' || value === 'dark' ? value : 'system';
    }
    function useSystem() {
        document.documentElement.removeAttribute('data-theme');
        try {
            localStorage.removeItem('theme');
        } catch (e) { }
        paintMeta(systemMode());
        return 'system';
    }
    paintMeta(mode());
    return { mode: mode, set: set, stored: stored, useSystem: useSystem };
})();