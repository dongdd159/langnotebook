// Interface language. The app's markup and messages are written in English; when another language is
// chosen, this script swaps visible text (and placeholder/title/aria-label/alt attributes) for the
// entries in that language's dictionary and keeps doing so as Blazor re-renders. Text without an entry,
// and anything inside an element marked translate="no", is left alone. Choosing English restores the
// original text. The choice is kept in localStorage.
window.langI18n = (function () {
    'use strict';
    var STORAGE_KEY = 'langUi';
    var LANGUAGES = ['en', 'vi', 'de'];
    var ATTRIBUTES = ['placeholder', 'title', 'aria-label', 'alt'];
    var SKIP = 'script,style,textarea,noscript,[translate="no"]';
    var MONTHS = { Jan: 1, Feb: 2, Mar: 3, Apr: 4, May: 5, Jun: 6, Jul: 7, Aug: 8, Sep: 9, Oct: 10, Nov: 11, Dec: 12 };
    var GERMAN_MONTHS = ['Jan.', 'Feb.', 'März', 'Apr.', 'Mai', 'Juni', 'Juli', 'Aug.', 'Sept.', 'Okt.', 'Nov.', 'Dez.'];

    var lang = 'en';
    try {
        var saved = localStorage.getItem(STORAGE_KEY);
        if (LANGUAGES.indexOf(saved) >= 0) lang = saved;
    } catch (e) { }

    var exact = {};
    var templates = [];
    var missing = {};
    var textState = new WeakMap();
    var attrState = new WeakMap();
    var observer = null;

    function normalise(text) {
        return text.replace(/[‘’]/g, "'").replace(/[“”]/g, '"').replace(/\s+/g, ' ').trim();
    }

    function escapeRegex(text) {
        return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    function loadDictionary() {
        exact = {};
        templates = [];
        var entries = { vi: window.langI18nVi, de: window.langI18nDe }[lang];
        if (!entries) return;
        entries.forEach(function (entry) {
            var key = normalise(entry[0]);
            if (/\{\d(?::t)?\}/.test(key)) {
                // "{0}" matches any value and passes it through. "{0:t}" also translates the value when it has
                // an entry of its own (a date such as "Today at 21:14"); user text must never use it.
                var translateValue = {};
                var pattern = key.split(/(\{\d(?::t)?\})/).map(function (part) {
                    var placeholder = /^\{(\d)(:t)?\}$/.exec(part);
                    if (!placeholder) return escapeRegex(part);
                    translateValue[placeholder[1]] = !!placeholder[2];
                    // A value never spans a " · " separator, so "A · {0} reviews" is split into parts instead.
                    return '([^\u00b7]+?)';
                }).join('');
                templates.push({ re: new RegExp('^' + pattern + '$'), out: entry[1], translateValue: translateValue,
                    size: key.replace(/\{\d(?::t)?\}/g, '').length });
            } else {
                exact[key] = entry[1];
            }
        });
        // The most specific template (most fixed text) wins.
        templates.sort(function (a, b) { return b.size - a.size; });
    }

    var nested = false;

    function lookup(core) {
        var key = normalise(core);
        if (Object.prototype.hasOwnProperty.call(exact, key)) return exact[key];
        for (var i = 0; i < templates.length; i++) {
            var template = templates[i];
            var match = template.re.exec(key);
            if (!match) continue;
            return template.out.replace(/\{(\d)\}/g, function (_, n) {
                var value = match[+n + 1];
                if (!template.translateValue[n] || nested) return value;
                nested = true;
                var inner = lookup(value);
                nested = false;
                return inner === null ? value : inner;
            });
        }
        return null;
    }

    // Vietnamese: "Nov 17" -> "17 thg 11", "Nov 17, 2026" -> "17 thg 11, 2026".
    // German:     "Nov 17" -> "17. Nov.",  "Nov 17, 2026" -> "17. Nov. 2026".
    function localiseDates(text) {
        if (lang !== 'vi' && lang !== 'de') return text;
        return text.replace(/\b(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec) (\d{1,2})\b(, (\d{4}))?/g,
            function (_, month, day, _y, year) {
                if (lang === 'de') return day + '. ' + GERMAN_MONTHS[MONTHS[month] - 1] + (year ? ' ' + year : '');
                return day + ' thg ' + MONTHS[month] + (year ? ', ' + year : '');
            });
    }

    function translate(text) {
        var lead = /^\s*/.exec(text)[0];
        var trail = /\s*$/.exec(text)[0];
        var core = text.slice(lead.length, text.length - trail.length);
        if (!core) return null;
        var out = lookup(core);
        if (out === null && core.indexOf('·') >= 0) {
            // "A · B": translate each part on its own.
            var found = false;
            out = core.split(/(\s*·\s*)/).map(function (part, index) {
                if (index % 2 === 1 || !part.trim()) return part;
                var result = lookup(part);
                if (result === null) return part;
                found = true;
                return result;
            }).join('');
            if (!found) out = null;
        }
        if (out === null) {
            var dated = localiseDates(core);
            if (dated === core) return null;
            out = dated;
        } else {
            out = localiseDates(out);
        }
        return lead + out + trail;
    }

    function noteMissing(core) {
        if (lang === 'en' || !/[A-Za-z]{3}/.test(core)) return;
        missing[normalise(core)] = true;
    }

    function processText(node) {
        var record = textState.get(node);
        var current = node.nodeValue;
        if (!record && lang === 'en') return;
        if (node.parentElement && node.parentElement.closest(SKIP)) return;
        // If the value differs from what this script last wrote, Blazor has set new text.
        var original = record && record.applied === current ? record.original : current;
        var output = original;
        if (lang !== 'en') {
            var translated = translate(original);
            if (translated === null) noteMissing(original);
            else output = translated;
        }
        if (output !== current) node.nodeValue = output;
        textState.set(node, { original: original, applied: output });
    }

    function processAttribute(element, name) {
        var states = attrState.get(element);
        if (!states) {
            if (lang === 'en') return;
            states = {};
            attrState.set(element, states);
        }
        var current = element.getAttribute(name);
        if (current === null) return;
        if (element.closest(SKIP)) return;
        var record = states[name];
        var original = record && record.applied === current ? record.original : current;
        var output = original;
        if (lang !== 'en') {
            var translated = translate(original);
            if (translated === null) noteMissing(original);
            else output = translated;
        }
        if (output !== current) element.setAttribute(name, output);
        states[name] = { original: original, applied: output };
    }

    function processElement(element) {
        for (var i = 0; i < ATTRIBUTES.length; i++) {
            if (element.hasAttribute(ATTRIBUTES[i])) processAttribute(element, ATTRIBUTES[i]);
        }
    }

    function walk(root) {
        if (root.nodeType === 3) {
            processText(root);
            return;
        }
        if (root.nodeType !== 1 && root.nodeType !== 9) return;
        if (root.nodeType === 1) processElement(root);
        var walker = document.createTreeWalker(root, 1 | 4);
        var node;
        while ((node = walker.nextNode())) {
            if (node.nodeType === 3) processText(node);
            else processElement(node);
        }
    }

    function onMutations(records) {
        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (record.type === 'childList') {
                for (var j = 0; j < record.addedNodes.length; j++) walk(record.addedNodes[j]);
            } else if (record.type === 'characterData') {
                processText(record.target);
            } else if (record.type === 'attributes') {
                processAttribute(record.target, record.attributeName);
            }
        }
    }

    function apply() {
        document.documentElement.setAttribute('lang', lang);
        walk(document.documentElement);
    }

    function start() {
        loadDictionary();
        apply();
        observer = new MutationObserver(onMutations);
        observer.observe(document.documentElement, {
            subtree: true, childList: true, characterData: true, attributes: true, attributeFilter: ATTRIBUTES
        });
    }

    function set(code) {
        if (LANGUAGES.indexOf(code) < 0) return lang;
        lang = code;
        try {
            localStorage.setItem(STORAGE_KEY, code);
        } catch (e) { }
        missing = {};
        loadDictionary();
        apply();
        return lang;
    }

    start();
    return {
        get: function () { return lang; },
        set: set,
        // Visible English text with no entry in the current dictionary. Handy for finding gaps: langI18n.missing()
        missing: function () { return Object.keys(missing).sort(); }
    };
})();
