// Paycrest browser helpers (called through JS interop)
window.paycrest = {
    downloadText: function (fileName, contentType, text) {
        const blob = new Blob([text], { type: contentType });
        this._download(fileName, blob);
    },
    downloadBase64: function (fileName, contentType, base64) {
        const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
        this._download(fileName, new Blob([bytes], { type: contentType }));
    },
    _download: function (fileName, blob) {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 2000);
    },
    print: function () { window.print(); },
    toggleTheme: function () {
        const next = document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
        document.documentElement.setAttribute('data-bs-theme', next);
        try { localStorage.setItem('pc-theme', next); } catch { }
        return next;
    },
    copy: function (text) { return navigator.clipboard.writeText(text); }
};
