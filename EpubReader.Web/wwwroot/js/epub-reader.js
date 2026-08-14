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
    _touchStartHandler: null,
    _touchMoveHandler: null,
    _touchEndHandler: null,
    _touchCancelHandler: null,
    _dragDepth: 0,
    _scrollTimer: null,
    _touch: null,

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

    _hotkey: function (payload) {
        if (!window.epubReaderUi._dotNetRef) return;
        window.epubReaderUi._dotNetRef.invokeMethodAsync('OnHotkey', payload);
    },

    _touchIgnoreTarget: function (target) {
        if (!target || !target.closest) return true;
        return !!target.closest(
            'input, textarea, select, button, a, label, .reader-drawer, .reader-modal-card, .settings-scroll, [contenteditable="true"]'
        );
    },

    _resetTouch: function () {
        this._touch = null;
    },

    bind: function (dotNetRef) {
        this.unbind();
        this._dotNetRef = dotNetRef;
        this._dragDepth = 0;
        this._touch = null;

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

        this._touchStartHandler = function (e) {
            if (!e.touches || e.touches.length !== 1) {
                window.epubReaderUi._resetTouch();
                return;
            }
            if (window.epubReaderUi._touchIgnoreTarget(e.target)) {
                window.epubReaderUi._resetTouch();
                return;
            }
            var t = e.touches[0];
            var inToc = !!(e.target.closest && e.target.closest('.toc-panel'));
            var onBackdrop = !!(e.target.closest && e.target.closest('.toc-backdrop'));
            window.epubReaderUi._touch = {
                x: t.clientX,
                y: t.clientY,
                time: Date.now(),
                inToc: inToc,
                onBackdrop: onBackdrop,
                edge: t.clientX <= 28,
                axis: null
            };
        };

        this._touchMoveHandler = function (e) {
            var state = window.epubReaderUi._touch;
            if (!state || !e.touches || e.touches.length !== 1) return;
            var t = e.touches[0];
            var dx = t.clientX - state.x;
            var dy = t.clientY - state.y;
            if (!state.axis) {
                if (Math.abs(dx) < 10 && Math.abs(dy) < 10) return;
                state.axis = Math.abs(dx) > Math.abs(dy) * 1.15 ? 'x' : 'y';
            }
            // Only claim horizontal edge/TOC swipes so vertical reading scroll stays native.
            if (state.axis === 'x' && (state.edge || state.inToc || state.onBackdrop) && e.cancelable) {
                e.preventDefault();
            }
        };

        this._touchEndHandler = function (e) {
            var state = window.epubReaderUi._touch;
            window.epubReaderUi._resetTouch();
            if (!state || !e.changedTouches || !e.changedTouches.length) return;

            var t = e.changedTouches[0];
            var dx = t.clientX - state.x;
            var dy = t.clientY - state.y;
            var dt = Date.now() - state.time;
            var absX = Math.abs(dx);
            var absY = Math.abs(dy);

            if (dt > 900) return;
            if (absX < 56) return;
            if (absX < absY * 1.25) return;

            if (state.edge && dx > 48) {
                window.epubReaderUi._hotkey('SwipeTocOpen');
                return;
            }
            if ((state.inToc || state.onBackdrop) && dx < -48) {
                window.epubReaderUi._hotkey('SwipeTocClose');
                return;
            }
            if (state.inToc || state.onBackdrop) return;

            if (dx < 0) window.epubReaderUi._hotkey('SwipeNext');
            else window.epubReaderUi._hotkey('SwipePrev');
        };

        this._touchCancelHandler = function () {
            window.epubReaderUi._resetTouch();
        };

        var touchOpts = { capture: true, passive: false };
        document.addEventListener('touchstart', this._touchStartHandler, touchOpts);
        document.addEventListener('touchmove', this._touchMoveHandler, touchOpts);
        document.addEventListener('touchend', this._touchEndHandler, touchOpts);
        document.addEventListener('touchcancel', this._touchCancelHandler, touchOpts);
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
        var touchOpts = { capture: true };
        if (this._touchStartHandler) {
            document.removeEventListener('touchstart', this._touchStartHandler, touchOpts);
            this._touchStartHandler = null;
        }
        if (this._touchMoveHandler) {
            document.removeEventListener('touchmove', this._touchMoveHandler, touchOpts);
            this._touchMoveHandler = null;
        }
        if (this._touchEndHandler) {
            document.removeEventListener('touchend', this._touchEndHandler, touchOpts);
            this._touchEndHandler = null;
        }
        if (this._touchCancelHandler) {
            document.removeEventListener('touchcancel', this._touchCancelHandler, touchOpts);
            this._touchCancelHandler = null;
        }
        this._dragDepth = 0;
        this._touch = null;
        if (this._dotNetRef) {
            this._dotNetRef = null;
        }
    }
};

window.epubReaderLocale = {
    get: function () {
        try {
            return (navigator.language || navigator.userLanguage || 'en') + '';
        } catch (e) {
            return 'en';
        }
    }
};

window.epubReaderTts = {
    _utterance: null,
    _audio: null,
    _speakResolve: null,
    _speakReject: null,
    _audioResolve: null,
    _preferNeural: false,
    _marks: [],

    getHostname: function () {
        try { return (location && location.hostname) ? String(location.hostname) : ''; }
        catch (e) { return ''; }
    },

    _normLang: function (lang) {
        return String(lang || 'en').toLowerCase().replace('_', '-');
    },

    _langPrefix: function (lang) {
        var n = this._normLang(lang);
        var i = n.indexOf('-');
        return i > 0 ? n.slice(0, i) : n;
    },

    _scoreVoice: function (voice, lang, preferNeural) {
        var vLang = this._normLang(voice.lang || '');
        var want = this._normLang(lang);
        var wantPrefix = this._langPrefix(lang);
        var score = 0;
        if (vLang === want) score += 100;
        else if (vLang.indexOf(wantPrefix + '-') === 0 || vLang === wantPrefix) score += 70;
        else if (this._langPrefix(vLang) === wantPrefix) score += 40;
        else return -1;

        var name = (voice.name || '').toLowerCase();
        if (preferNeural && (name.indexOf('neural') >= 0 || name.indexOf('online') >= 0 || name.indexOf('natural') >= 0 || name.indexOf('google') >= 0 || name.indexOf('microsoft') >= 0)) {
            score += 25;
        }
        if (voice.localService) score += 5;
        return score;
    },

    _pickVoice: function (voiceList, lang, preferNeural) {
        var voices = voiceList || [];
        var best = null;
        var bestScore = -1;
        for (var i = 0; i < voices.length; i++) {
            var s = this._scoreVoice(voices[i], lang, !!preferNeural);
            if (s > bestScore) {
                bestScore = s;
                best = voices[i];
            }
        }
        return best;
    },

    ensureVoices: function () {
        return new Promise(function (resolve) {
            if (!window.speechSynthesis) {
                resolve([]);
                return;
            }
            var voices = window.speechSynthesis.getVoices();
            if (voices && voices.length) {
                resolve(voices);
                return;
            }
            var done = false;
            var finish = function () {
                if (done) return;
                done = true;
                resolve(window.speechSynthesis.getVoices() || []);
            };
            window.speechSynthesis.onvoiceschanged = finish;
            setTimeout(finish, 500);
        });
    },

    hasVoiceFor: function (lang, preferNeural) {
        var self = this;
        return this.ensureVoices().then(function (voices) {
            return !!self._pickVoice(voices, lang, preferNeural);
        });
    },

    _normalizeWs: function (s) {
        return String(s || '')
            .replace(/\u00ad/g, '') // soft hyphen
            .replace(/\*+/g, '')
            .replace(/[\u2018\u2019\u201A\u2032]/g, "'")
            .replace(/[\u201C\u201D\u201E\u2033]/g, '"')
            .replace(/[\u2013\u2014]/g, '-')
            .replace(/\s+/g, ' ')
            .trim();
    },

    /**
     * Spoken text only: drop asterisks; keep paragraph pauses longer than sentence pauses.
     * Blank lines → long ellipsis pause; single newlines → space; periods stay natural/short.
     */
    _prepareSpeakText: function (raw) {
        return String(raw || '')
            .replace(/\*/g, '')
            .replace(/\r\n/g, '\n')
            .replace(/\r/g, '\n')
            .replace(/\n\s*\n+/g, ' … … ')
            .replace(/\n/g, ' ')
            .replace(/[ \t\f\v]+/g, ' ')
            .trim();
    },

    clearHighlight: function () {
        var root = document.getElementById('reader-content');
        var marks = root ? root.querySelectorAll('mark.tts-hl, mark.tts-hl-word') : [];
        for (var i = marks.length - 1; i >= 0; i--) {
            var m = marks[i];
            var parent = m.parentNode;
            if (!parent) continue;
            while (m.firstChild) parent.insertBefore(m.firstChild, m);
            parent.removeChild(m);
            try { parent.normalize(); } catch (e) { /* ignore */ }
        }
        var overlays = document.querySelectorAll('.tts-hl-overlay');
        for (var j = overlays.length - 1; j >= 0; j--) {
            overlays[j].parentNode && overlays[j].parentNode.removeChild(overlays[j]);
        }
        this._marks = [];
    },

    _collectTextNodes: function (root) {
        var nodes = [];
        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
            acceptNode: function (node) {
                if (!node || !node.nodeValue) return NodeFilter.FILTER_REJECT;
                if (!node.nodeValue.replace(/\s+/g, '').length) return NodeFilter.FILTER_REJECT;
                var p = node.parentElement;
                if (p && (p.closest('script, style, noscript'))) return NodeFilter.FILTER_REJECT;
                return NodeFilter.FILTER_ACCEPT;
            }
        });
        var n;
        while ((n = walker.nextNode())) nodes.push(n);
        return nodes;
    },

    _buildIndex: function (root) {
        var nodes = this._collectTextNodes(root);
        var full = '';
        var map = [];
        for (var i = 0; i < nodes.length; i++) {
            var live = nodes[i].nodeValue || '';
            var stripped = '';
            for (var c = 0; c < live.length; c++) {
                var ch = live.charAt(c);
                if (ch === '\u00ad') continue;
                stripped += ch;
            }
            var start = full.length;
            full += stripped;
            map.push({ node: nodes[i], start: start, end: full.length, raw: stripped });
        }

        var searchIn = '';
        var searchMap = [];
        for (var k = 0; k < full.length; k++) {
            var ch2 = full.charAt(k);
            if (/\s/.test(ch2)) {
                if (searchIn.length === 0 || searchIn.charAt(searchIn.length - 1) === ' ') continue;
                searchIn += ' ';
                searchMap.push(k);
            } else {
                if (ch2 === '\u2018' || ch2 === '\u2019' || ch2 === '\u201A') ch2 = "'";
                if (ch2 === '\u201C' || ch2 === '\u201D' || ch2 === '\u201E') ch2 = '"';
                if (ch2 === '\u2013' || ch2 === '\u2014') ch2 = '-';
                searchIn += ch2;
                searchMap.push(k);
            }
        }
        return { nodes: nodes, full: full, map: map, searchIn: searchIn, searchMap: searchMap };
    },

    _locate: function (root, snippet) {
        var needle = this._normalizeWs(snippet);
        if (!needle || !root) return null;
        var idxData = this._buildIndex(root);
        if (!idxData.searchIn) return null;

        var hay = idxData.searchIn;
        var hayLower = hay.toLowerCase();
        var tryNeedles = [needle];
        if (needle.length > 160) tryNeedles.push(needle.slice(0, 160));
        if (needle.length > 80) tryNeedles.push(needle.slice(0, 80));
        if (needle.length > 40) tryNeedles.push(needle.slice(0, 40));
        if (needle.length > 24) tryNeedles.push(needle.slice(0, 24));
        // first sentence-ish
        var sent = needle.match(/^.{12,120}?[.!?…。！？](?=\s|$)/);
        if (sent) tryNeedles.push(sent[0]);
        // distinctive mid-chunk window (helps when chapter HTML differs at the start)
        if (needle.length > 60) {
            var mid = Math.floor((needle.length - 40) / 2);
            tryNeedles.push(needle.slice(mid, mid + 40));
        }

        var idx = -1;
        var used = needle;
        for (var t = 0; t < tryNeedles.length; t++) {
            var n = tryNeedles[t];
            if (!n || n.length < 4) continue;
            idx = hay.indexOf(n);
            if (idx < 0) idx = hayLower.indexOf(n.toLowerCase());
            if (idx >= 0) {
                used = n;
                break;
            }
        }
        if (idx < 0) return null;

        // Expand to full sentence(s) in the live chapter text — never leave a mid-sentence cut.
        var wantLen = Math.min(Math.max(used.length, Math.min(needle.length, 520)), hay.length - idx);
        var rawStart = idxData.searchMap[idx];
        var endIdx = idx + wantLen - 1;
        if (endIdx >= idxData.searchMap.length) endIdx = idxData.searchMap.length - 1;
        var rawEnd = idxData.searchMap[endIdx] + 1;
        if (rawStart == null || rawEnd == null) return null;

        var expanded = this._expandToSentences(idxData.full, rawStart, rawEnd);
        return { rawStart: expanded.start, rawEnd: expanded.end, index: idxData };
    },

    _expandToSentences: function (full, rawStart, rawEnd) {
        var s = Math.max(0, rawStart | 0);
        var e = Math.max(s + 1, rawEnd | 0);
        var punct = /[.!?…。！？]/;

        while (s > 0) {
            var prev = full.charAt(s - 1);
            if (punct.test(prev)) break;
            if (prev === '\n' && s >= 2 && full.charAt(s - 2) === '\n') break;
            s--;
        }
        while (s < e && /\s/.test(full.charAt(s))) s++;

        // Extend forward through the end of the current sentence (and any more covered by rawEnd).
        var cover = Math.max(e, rawEnd | 0);
        while (e < full.length) {
            var ch = full.charAt(e);
            e++;
            if (punct.test(ch)) {
                if (e >= cover) break;
                // include following sentences still inside the spoken snippet
                continue;
            }
            if (ch === '\n' && e < full.length && full.charAt(e) === '\n') {
                if (e >= cover) break;
            }
            if (e > cover + 80 && punct.test(full.charAt(e - 1))) break;
        }

        // If we still don't end on punctuation, keep going to next sentence end.
        if (e <= full.length && e > 0 && !punct.test(full.charAt(e - 1))) {
            while (e < full.length) {
                var c2 = full.charAt(e);
                e++;
                if (punct.test(c2)) break;
                if (c2 === '\n' && e < full.length && full.charAt(e) === '\n') break;
            }
        }

        return { start: s, end: Math.min(e, full.length) };
    },

    _paintRawRange: function (index, rawStart, rawEnd, className) {
        if (!index || rawEnd <= rawStart) return [];
        var marks = [];
        // Work on a copy of nodes list; splitText mutates the tree.
        var items = index.map.slice();
        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            if (item.end <= rawStart || item.start >= rawEnd) continue;
            var node = item.node;
            if (!node || !node.parentNode) continue;
            if (!node.nodeValue) continue;

            var localStart = Math.max(0, rawStart - item.start);
            var localEnd = Math.min(item.raw.length, rawEnd - item.start);
            if (localEnd <= localStart) continue;

            try {
                // Account for soft hyphens stripped in index vs live nodeValue
                var live = node.nodeValue;
                // Approximate offsets on live text by scanning without soft hyphens
                var ls = 0, le = 0, seen = 0;
                for (var p = 0; p < live.length; p++) {
                    if (live.charAt(p) === '\u00ad') continue;
                    if (seen === localStart) ls = p;
                    seen++;
                    if (seen === localEnd) {
                        le = p + 1;
                        break;
                    }
                }
                if (le <= ls) {
                    ls = localStart;
                    le = localEnd;
                    if (le > live.length) le = live.length;
                }

                var target = node;
                if (ls > 0 && ls < target.nodeValue.length) {
                    target = target.splitText(ls);
                    le = le - ls;
                }
                if (le > 0 && le < target.nodeValue.length) {
                    target.splitText(le);
                }

                var mark = document.createElement('mark');
                mark.className = className || 'tts-hl';
                var parent = target.parentNode;
                parent.insertBefore(mark, target);
                mark.appendChild(target);
                marks.push(mark);
            } catch (e) {
                // continue other nodes
            }
        }
        return marks;
    },

    highlight: function (snippet) {
        this.clearHighlight();
        var root = document.getElementById('reader-content');
        if (!root) return false;
        var loc = this._locate(root, snippet);
        if (!loc) {
            var m = this._normalizeWs(snippet).match(/[^\s]{8,80}/);
            if (m) loc = this._locate(root, m[0]);
        }
        if (!loc) return false;

        var marks = this._paintRawRange(loc.index, loc.rawStart, loc.rawEnd, 'tts-hl');
        this._marks = marks;
        if (!marks.length) return false;

        try {
            marks[0].scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'nearest' });
        } catch (e) { /* ignore */ }
        return true;
    },

    _liveOffset: function (node, strippedOffset) {
        var live = node.nodeValue || '';
        var seen = 0;
        for (var p = 0; p < live.length; p++) {
            if (live.charAt(p) === '\u00ad') continue;
            if (seen === strippedOffset) return p;
            seen++;
        }
        return live.length;
    },

    highlightWord: function (chunkText, charIndex, charLength) {
        var root = document.getElementById('reader-content');
        if (!root) return false;
        var text = this._normalizeWs(chunkText);
        var start = Math.max(0, charIndex | 0);
        var len = charLength | 0;
        if (!len) {
            var rest = text.slice(start);
            var m = rest.match(/^\S+/);
            len = m ? m[0].length : Math.min(12, rest.length);
        }
        if (len <= 0) return false;
        var word = text.substr(start, len);
        if (!word.trim()) return false;

        var words = root.querySelectorAll('mark.tts-hl-word');
        for (var i = words.length - 1; i >= 0; i--) {
            var w = words[i];
            var parent = w.parentNode;
            if (!parent) continue;
            while (w.firstChild) parent.insertBefore(w.firstChild, w);
            parent.removeChild(w);
            try { parent.normalize(); } catch (e) { /* ignore */ }
        }

        var loc = this._locate(root, word);
        if (!loc) return false;
        // tight word paint
        var want = Math.min(word.length, 40);
        var endIdx = loc.index.searchIn.indexOf(this._normalizeWs(word));
        if (endIdx < 0) endIdx = loc.rawStart;
        var marks = this._paintRawRange(loc.index, loc.rawStart, loc.rawStart + Math.max(1, Math.min(word.length + 2, loc.rawEnd - loc.rawStart)), 'tts-hl-word');
        if (!marks.length) return false;
        try {
            marks[0].scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'nearest' });
        } catch (e) { /* ignore */ }
        return true;
    },

    speak: function (text, lang, rate, preferNeural) {
        return this.speakQueue([text], lang, rate, preferNeural, null);
    },

    /**
     * Gapless-ish playback: keep the next utterance queued while the current one speaks.
     * bridge (DotNetObjectReference) optional — OnChunk(index) on each utterance start.
     */
    speakQueue: function (chunks, lang, rate, preferNeural, bridge) {
        var self = this;
        this._preferNeural = !!preferNeural;
        var list = Array.isArray(chunks) ? chunks.slice() : [];
        if (!list.length) return Promise.resolve();

        return this.ensureVoices().then(function (voices) {
            return new Promise(function (resolve, reject) {
                if (!window.speechSynthesis) {
                    reject(new Error('speechSynthesis niedostępne'));
                    return;
                }

                self.stopSpeechOnly();
                self.stopAudioOnly();

                self._queueActive = true;
                self._queueResolve = resolve;
                self._queueReject = reject;
                self._queueBridge = bridge || null;
                self._queueFinished = 0;
                self._queueNext = 0;
                self._queueFailed = false;

                var voice = self._pickVoice(voices, lang, preferNeural);
                var rateClamped = Math.max(0.5, Math.min(2, Number(rate) || 1));
                var langNorm = self._normLang(lang || 'en');

                function finishOk() {
                    if (!self._queueActive && !self._queueResolve) return;
                    self._queueActive = false;
                    self._utterance = null;
                    self._queueBridge = null;
                    var r = self._queueResolve;
                    self._queueResolve = null;
                    self._queueReject = null;
                    if (r) r();
                }

                function finishErr(err) {
                    if (self._queueFailed) return;
                    self._queueFailed = true;
                    self._queueActive = false;
                    self._utterance = null;
                    self._queueBridge = null;
                    var rej = self._queueReject;
                    self._queueResolve = null;
                    self._queueReject = null;
                    if (rej) rej(err);
                }

                function enqueueOne(i) {
                    if (!self._queueActive || i >= list.length || i < self._queueNext) return;
                    self._queueNext = i + 1;

                    var raw = String(list[i] || '');
                    var spokenNorm = self._prepareSpeakText(raw);
                    if (!spokenNorm) {
                        self._queueFinished++;
                        if (self._queueFinished >= list.length) finishOk();
                        else enqueueOne(i + 1);
                        return;
                    }

                    var u = new SpeechSynthesisUtterance(spokenNorm);
                    u.lang = langNorm;
                    u.rate = rateClamped;
                    if (voice) u.voice = voice;
                    self._utterance = u;

                    u.onstart = function () {
                        try { self.highlight(raw); } catch (e1) { /* ignore */ }
                        var b = self._queueBridge;
                        if (b && b.invokeMethodAsync) {
                            try { b.invokeMethodAsync('OnChunk', i); } catch (e2) { /* ignore */ }
                        }
                        // Speculative: queue the next chunk while this one is speaking.
                        if (self._queueActive && i + 1 < list.length) {
                            enqueueOne(i + 1);
                        }
                    };

                    // Keep whole-sentence highlight stable (word boundaries were cutting mid-sentence).
                    u.onboundary = null;

                    u.onend = function () {
                        self._queueFinished++;
                        if (!self._queueActive) return;
                        if (self._queueFinished >= list.length) finishOk();
                    };

                    u.onerror = function (e) {
                        var err = (e && e.error) ? String(e.error) : 'speak error';
                        if (err === 'canceled' || err === 'interrupted' || !self._queueActive) {
                            finishOk();
                            return;
                        }
                        finishErr(new Error(err));
                    };

                    try {
                        window.speechSynthesis.speak(u);
                    } catch (errSpeak) {
                        finishErr(errSpeak || new Error('speak failed'));
                    }
                }

                enqueueOne(0);
            });
        });
    },

    pause: function () {
        if (this._audio && !this._audio.paused) {
            this._audio.pause();
            return;
        }
        if (window.speechSynthesis) {
            try { window.speechSynthesis.pause(); } catch (e) { /* ignore */ }
        }
    },

    resume: function () {
        if (this._audio && this._audio.paused) {
            this._audio.play();
            return;
        }
        if (window.speechSynthesis) {
            try { window.speechSynthesis.resume(); } catch (e) { /* ignore */ }
            // Chrome sometimes needs a second resume after pause with a queued utterance.
            var synth = window.speechSynthesis;
            setTimeout(function () {
                try { if (synth.paused) synth.resume(); } catch (e2) { /* ignore */ }
            }, 40);
        }
    },

    stopSpeechOnly: function () {
        this._queueActive = false;
        if (window.speechSynthesis) {
            try { window.speechSynthesis.cancel(); } catch (e) { /* ignore */ }
        }
        this._utterance = null;
        if (this._speakResolve) {
            this._speakResolve();
            this._speakResolve = null;
            this._speakReject = null;
        }
        if (this._queueResolve) {
            var r = this._queueResolve;
            this._queueResolve = null;
            this._queueReject = null;
            this._queueBridge = null;
            r();
        }
    },

    stopAudioOnly: function () {
        if (this._audio) {
            try {
                this._audio.pause();
                this._audio.removeAttribute('src');
                this._audio.load();
            } catch (e) { /* ignore */ }
            this._audio = null;
        }
        if (this._audioResolve) {
            this._audioResolve();
            this._audioResolve = null;
        }
    },

    stop: function () {
        this.stopSpeechOnly();
        this.stopAudioOnly();
    },

    playMp3Base64: function (base64) {
        return this.playAudioBase64('audio/mpeg', base64);
    },

    playWavBase64: function (base64) {
        return this.playAudioBase64('audio/wav', base64);
    },

    playAudioBase64: function (mime, base64) {
        var self = this;
        return new Promise(function (resolve, reject) {
            self.stopAudioOnly();
            self.stopSpeechOnly();
            if (!base64) {
                resolve();
                return;
            }
            var audio = new Audio('data:' + (mime || 'audio/mpeg') + ';base64,' + base64);
            self._audio = audio;
            self._audioResolve = resolve;
            audio.onended = function () {
                if (self._audio === audio) self._audio = null;
                self._audioResolve = null;
                resolve();
            };
            audio.onerror = function () {
                if (self._audio === audio) self._audio = null;
                self._audioResolve = null;
                reject(new Error('Odtwarzanie audio nie powiodło się'));
            };
            var p = audio.play();
            if (p && p.then) {
                p.catch(function (err) {
                    if (self._audio === audio) self._audio = null;
                    self._audioResolve = null;
                    reject(err || new Error('play blocked'));
                });
            }
        });
    }
};
