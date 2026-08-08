window.epubReaderPrefs = {
    get: function (key) {
        return localStorage.getItem(key);
    },
    set: function (key, value) {
        localStorage.setItem(key, value);
    }
};

window.epubReaderFile = {
    _readFile: function (file) {
        return new Promise(function (resolve) {
            if (!file) {
                resolve(null);
                return;
            }
            var reader = new FileReader();
            reader.onload = function () {
                var result = reader.result || '';
                var base64 = String(result).split(',')[1] || '';
                resolve({ base64: base64, name: file.name || 'book.epub' });
            };
            reader.onerror = function () { resolve(null); };
            reader.readAsDataURL(file);
        });
    },
    pickEpubBase64: function () {
        return window.epubReaderFile.pickEpubObject();
    },
    pickEpubBase64Json: function () {
        return window.epubReaderFile.pickEpubObject().then(function (obj) {
            return obj ? JSON.stringify(obj) : null;
        });
    },
    pickEpubObject: function () {
        return new Promise(function (resolve) {
            var input = document.createElement('input');
            input.type = 'file';
            input.accept = '.epub,application/epub+zip';
            input.onchange = function () {
                var file = input.files && input.files[0];
                window.epubReaderFile._readFile(file).then(resolve);
            };
            input.oncancel = function () { resolve(null); };
            input.click();
        });
    },
    readFileAsBase64: function (file) {
        return window.epubReaderFile._readFile(file);
    }
};

window.epubReaderUi = {
    _dotNetRef: null,
    _keyHandler: null,
    _clickHandler: null,
    _scrollTimer: null,

    createIcons: function () {
        if (window.lucide && typeof window.lucide.createIcons === 'function') {
            window.lucide.createIcons();
        }
    },

    openExternal: function (url) {
        if (!url) return;
        try {
            window.open(url, '_blank', 'noopener,noreferrer');
        } catch (_) {
            location.href = url;
        }
    },

    focusApp: function () {
        var el = document.getElementById('app-body');
        if (el) {
            el.focus({ preventScroll: true });
        }
    },

    scrollReaderTop: function () {
        var el = document.getElementById('reader-scroll');
        if (el) el.scrollTop = 0;
    },

    scrollToFragment: function (fragment) {
        if (!fragment) return false;
        var root = document.getElementById('reader-content');
        if (!root) return false;
        var target = root.querySelector('#' + CSS.escape(fragment))
            || root.querySelector('[name="' + fragment.replace(/"/g, '\\"') + '"]')
            || document.getElementById(fragment);
        if (!target) return false;
        target.scrollIntoView({ behavior: 'smooth', block: 'start' });
        return true;
    },

    scrollTocToActive: function (chapterIndex) {
        var active = document.getElementById('toc-active')
            || document.querySelector('[data-toc-index="' + chapterIndex + '"]');
        if (!active) return false;
        var scroller = document.getElementById('toc-scroll');
        if (scroller && typeof active.scrollIntoView === 'function') {
            active.scrollIntoView({ behavior: 'smooth', block: 'center' });
            return true;
        }
        return false;
    },

    getScrollRatio: function () {
        var el = document.getElementById('reader-scroll');
        if (!el) return 0;
        var max = el.scrollHeight - el.clientHeight;
        if (max <= 0) return 0;
        return el.scrollTop / max;
    },

    setScrollRatio: function (ratio) {
        var el = document.getElementById('reader-scroll');
        if (!el) return;
        var max = el.scrollHeight - el.clientHeight;
        el.scrollTop = Math.max(0, Math.min(1, ratio || 0)) * max;
    },

    pageScroll: function (direction) {
        var el = document.getElementById('reader-scroll');
        if (!el) return 'none';
        var amount = Math.max(120, el.clientHeight * 0.9);
        var before = el.scrollTop;
        if (direction > 0) {
            el.scrollBy({ top: amount, behavior: 'smooth' });
            if (before + el.clientHeight >= el.scrollHeight - 4) return 'end';
        } else {
            el.scrollBy({ top: -amount, behavior: 'smooth' });
            if (before <= 2) return 'start';
        }
        return 'scrolled';
    },

    bind: function (dotNetRef) {
        this.unbind();
        this._dotNetRef = dotNetRef;

        this._clickHandler = function (e) {
            var a = e.target && e.target.closest ? e.target.closest('a[data-epub-href]') : null;
            if (!a) return;
            e.preventDefault();
            e.stopPropagation();
            var href = a.getAttribute('data-epub-href');
            var basePath = a.getAttribute('data-epub-base') || '';
            if (window.epubReaderUi._dotNetRef) {
                window.epubReaderUi._dotNetRef.invokeMethodAsync('OnContentLinkClicked', href, basePath);
            }
        };
        document.addEventListener('click', this._clickHandler, true);

        this._keyHandler = function (e) {
            var tag = (e.target && e.target.tagName || '').toLowerCase();
            if (tag === 'input' || tag === 'textarea' || tag === 'select' || (e.target && e.target.isContentEditable)) {
                if (e.key === 'Escape' && window.epubReaderUi._dotNetRef) {
                    window.epubReaderUi._dotNetRef.invokeMethodAsync('OnHotkey', 'Escape');
                }
                return;
            }

            var key = e.key;
            var payload = key;
            if (e.ctrlKey || e.metaKey) {
                if (key === '=' || key === '+') payload = 'FontUp';
                else if (key === '-' || key === '_') payload = 'FontDown';
                else if (key.toLowerCase() === 'o') payload = 'Open';
                else if (key.toLowerCase() === 'f') payload = 'Search';
                else return;
            } else if (key === ' ') {
                payload = e.shiftKey ? 'PageUp' : 'PageDown';
                e.preventDefault();
            } else if (key === 'PageDown') {
                payload = 'PageDown';
                e.preventDefault();
            } else if (key === 'PageUp') {
                payload = 'PageUp';
                e.preventDefault();
            } else if (key === '/') {
                payload = 'Search';
                e.preventDefault();
            }

            if (!window.epubReaderUi._dotNetRef) return;
            window.epubReaderUi._dotNetRef.invokeMethodAsync('OnHotkey', payload);
        };
        window.addEventListener('keydown', this._keyHandler, true);
    },

    unbind: function () {
        if (this._clickHandler) {
            document.removeEventListener('click', this._clickHandler, true);
            this._clickHandler = null;
        }
        if (this._keyHandler) {
            window.removeEventListener('keydown', this._keyHandler, true);
            this._keyHandler = null;
        }
        if (this._dotNetRef) {
            this._dotNetRef = null;
        }
    }
};
