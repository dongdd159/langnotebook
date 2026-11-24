// Keyboard behaviour for modal dialogs: focus moves into the dialog when it opens, Tab stays inside it, and focus
// goes back to whatever opened it when it closes. Closing with Escape is handled by the Modal component.
window.langDialog = (function () {
    'use strict';
    var stack = [];
    var FOCUSABLE = 'a[href],button:not([disabled]),input:not([disabled]):not([type="hidden"]),' +
        'select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])';

    function focusable(root) {
        return Array.prototype.filter.call(root.querySelectorAll(FOCUSABLE), function (element) {
            return element.getClientRects().length > 0;
        });
    }

    function onKeyDown(event) {
        if (event.key !== 'Tab' || stack.length === 0) return;
        var top = stack[stack.length - 1].element;
        var items = focusable(top);
        if (items.length === 0) {
            event.preventDefault();
            top.focus();
            return;
        }
        var first = items[0];
        var last = items[items.length - 1];
        var active = document.activeElement;
        if (!top.contains(active)) {
            event.preventDefault();
            (event.shiftKey ? last : first).focus();
        } else if (event.shiftKey && (active === first || active === top)) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && active === last) {
            event.preventDefault();
            first.focus();
        }
    }

    return {
        open: function (element) {
            if (!element) return;
            if (stack.length === 0) document.addEventListener('keydown', onKeyDown, true);
            stack.push({ element: element, opener: document.activeElement });
            var items = focusable(element);
            // Land on the first field or button in the content, not on the close button in the header.
            var target = items.filter(function (item) { return !item.hasAttribute('data-dialog-close'); })[0] ||
                items[0] || element;
            target.focus();
        },

        close: function () {
            var entry = stack.pop();
            if (stack.length === 0) document.removeEventListener('keydown', onKeyDown, true);
            if (entry && entry.opener && document.contains(entry.opener) && typeof entry.opener.focus === 'function')
                entry.opener.focus();
        }
    };
})();
