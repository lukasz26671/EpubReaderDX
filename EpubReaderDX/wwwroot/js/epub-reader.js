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
    },

    fromDataTransferJson: function (dataTransfer) {
        if (!dataTransfer || !dataTransfer.files || !dataTransfer.files.length) {
            return Promise.resolve(null);
        }
        var files = Array.prototype.slice.call(dataTransfer.files);
        var file = files.find(function (f) {
            var name = (f.name || '').toLowerCase();
            return name.endsWith('.epub')
                || f.type === 'application/epub+zip'
                || f.type === 'application/zip';
        }) || files[0];
        return window.epubReaderFile._readFile(file).then(function (obj) {
            return obj ? JSON.stringify(obj) : null;
        });
    }
};

window.epubReaderPwa = {
    _pendingLaunchFile: null,
    _launchWaiters: [],

    _setPending: function (obj) {
        if (this._launchWaiters.length) {
            var waiters = this._launchWaiters.splice(0);
            waiters.forEach(function (resolve) { resolve(obj); });
            return;
        }
        this._pendingLaunchFile = obj;
    },

    consumeLaunchFileJson: function () {
        var self = this;
        return new Promise(function (resolve) {
            if (self._pendingLaunchFile) {
                var pending = self._pendingLaunchFile;
                self._pendingLaunchFile = null;
                resolve(pending ? JSON.stringify(pending) : null);
                return;
            }
            var settled = false;
            var timer = setTimeout(function () {
                if (settled) return;
                settled = true;
                var idx = self._launchWaiters.indexOf(waiter);
                if (idx >= 0) self._launchWaiters.splice(idx, 1);
                resolve(null);
            }, 250);
            function waiter(obj) {
                if (settled) return;
                settled = true;
                clearTimeout(timer);
                resolve(obj ? JSON.stringify(obj) : null);
            }
            self._launchWaiters.push(waiter);
        });
    },

    init: function () {
        if (!('launchQueue' in window)) return;
        window.launchQueue.setConsumer(function (launchParams) {
            if (!launchParams || !launchParams.files || !launchParams.files.length) return;
            var handle = launchParams.files[0];
            Promise.resolve(handle.getFile ? handle.getFile() : handle)
                .then(function (file) { return window.epubReaderFile._readFile(file); })
                .then(function (obj) { window.epubReaderPwa._setPending(obj); })
                .catch(function (err) { console.warn('PWA launch file failed', err); });
        });
    }
};

try { window.epubReaderPwa.init(); } catch (_) { /* older browsers */ }

window.epubReaderUi = {
    _dotNetRef: null,
    _keyHandler: null,
    _clickHandler: null,
    _dragOverHandler: null,
    _dragEnterHandler: null,
    _dragLeaveHandler: null,
    _dropHandler: null,
    _dragDepth: 0,
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

    _setDropActive: function (active) {
        if (!window.epubReaderUi._dotNetRef) return;
        window.epubReaderUi._dotNetRef.invokeMethodAsync('OnDropZoneChanged', !!active);
    },

    _hasFiles: function (e) {
        var types = e.dataTransfer && e.dataTransfer.types;
        if (!types) return false;
        for (var i = 0; i < types.length; i++) {
            if (types[i] === 'Files') return true;
        }
        return false;
    },

    bind: function (dotNetRef) {
        this.unbind();
        this._dotNetRef = dotNetRef;
        this._dragDepth = 0;

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

        this._dragEnterHandler = function (e) {
            if (!window.epubReaderUi._hasFiles(e)) return;
            e.preventDefault();
            window.epubReaderUi._dragDepth += 1;
            if (window.epubReaderUi._dragDepth === 1) {
                window.epubReaderUi._setDropActive(true);
            }
        };
        this._dragOverHandler = function (e) {
            if (!window.epubReaderUi._hasFiles(e)) return;
            e.preventDefault();
            if (e.dataTransfer) e.dataTransfer.dropEffect = 'copy';
        };
        this._dragLeaveHandler = function (e) {
            if (!window.epubReaderUi._hasFiles(e) && window.epubReaderUi._dragDepth === 0) return;
            e.preventDefault();
            window.epubReaderUi._dragDepth = Math.max(0, window.epubReaderUi._dragDepth - 1);
            if (window.epubReaderUi._dragDepth === 0) {
                window.epubReaderUi._setDropActive(false);
            }
        };
        this._dropHandler = function (e) {
            if (!window.epubReaderUi._hasFiles(e)) return;
            e.preventDefault();
            e.stopPropagation();
            window.epubReaderUi._dragDepth = 0;
            window.epubReaderUi._setDropActive(false);
            if (!window.epubReaderUi._dotNetRef) return;
            window.epubReaderFile.fromDataTransferJson(e.dataTransfer).then(function (json) {
                if (!json || !window.epubReaderUi._dotNetRef) return;
                window.epubReaderUi._dotNetRef.invokeMethodAsync('OnEpubDropped', json);
            });
        };

        window.addEventListener('dragenter', this._dragEnterHandler, true);
        window.addEventListener('dragover', this._dragOverHandler, true);
        window.addEventListener('dragleave', this._dragLeaveHandler, true);
        window.addEventListener('drop', this._dropHandler, true);
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
        if (this._dragEnterHandler) {
            window.removeEventListener('dragenter', this._dragEnterHandler, true);
            this._dragEnterHandler = null;
        }
        if (this._dragOverHandler) {
            window.removeEventListener('dragover', this._dragOverHandler, true);
            this._dragOverHandler = null;
        }
        if (this._dragLeaveHandler) {
            window.removeEventListener('dragleave', this._dragLeaveHandler, true);
            this._dragLeaveHandler = null;
        }
        if (this._dropHandler) {
            window.removeEventListener('drop', this._dropHandler, true);
            this._dropHandler = null;
        }
        this._dragDepth = 0;
        if (this._dotNetRef) {
            this._dotNetRef = null;
        }
    }
};
