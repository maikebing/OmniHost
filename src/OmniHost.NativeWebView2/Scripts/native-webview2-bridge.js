(function () {
    if (!window.chrome || !window.chrome.webview) return;
    var _pending = {};
    window.chrome.webview.addEventListener('message', function (e) {
        var msg;
        try {
            msg = typeof e.data === 'string' ? JSON.parse(e.data) : e.data;
        } catch {
            return;
        }
        if (!msg || typeof msg !== 'object') return;
        if (msg.type === 'response' && _pending[msg.id]) {
            var cb = _pending[msg.id];
            delete _pending[msg.id];
            if (msg.ok === false) {
                cb.reject(new Error(msg.error || 'omni invoke failed: ' + msg.id));
                return;
            }
            var result = msg.result;
            if (typeof result === 'string') {
                try { result = JSON.parse(result); } catch { }
            }
            cb.resolve(result);
        } else if (msg.type === 'event') {
            var detail;
            if (typeof msg.data === 'string') {
                try { detail = JSON.parse(msg.data); } catch { detail = msg.data; }
            } else {
                detail = msg.data;
            }
            window.dispatchEvent(new CustomEvent('omni:' + msg.name, { detail: detail }));
        }
    });
    var omniApi = {
        invoke: function (handler, data) {
            return new Promise(function (resolve, reject) {
                var id = Math.random().toString(36).slice(2) + Date.now().toString(36);
                _pending[id] = { resolve: resolve, reject: reject };
                setTimeout(function () {
                    if (_pending[id]) { delete _pending[id]; reject(new Error('omni timeout: ' + handler)); }
                }, 30000);
                window.chrome.webview.postMessage(JSON.stringify({
                    type: 'invoke', handler: handler, id: id, data: JSON.stringify(data)
                }));
            });
        },
        on: function (eventName, callback) {
            window.addEventListener('omni:' + eventName, function (e) { callback(e.detail); });
        },
        window: {
            minimize: function () { return omniApi.invoke('window.minimize'); },
            maximize: function () { return omniApi.invoke('window.maximize'); },
            close: function () { return omniApi.invoke('window.close'); },
            exit: function () { return omniApi.invoke('window.exit'); },
            startDrag: function (data) { return omniApi.invoke('window.startDrag', data); },
            showSystemMenu: function (data) { return omniApi.invoke('window.showSystemMenu', data); }
        }
    };
    globalThis.omni = omniApi;
})();
