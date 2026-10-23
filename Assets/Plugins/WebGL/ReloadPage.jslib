mergeInto(LibraryManager.library, {
    InstallMobileInputFix: function () {
        if (window.__krMobileInputFix) {
            return;
        }
        window.__krMobileInputFix = true;

        var head = document.head || document.getElementsByTagName("head")[0];
        var style = document.createElement("style");
        style.textContent = "input, textarea { font-size: 16px !important; }";
        head.appendChild(style);

        window.__krResetZoom = function () {
            var meta = document.querySelector('meta[name="viewport"]');
            if (!meta) {
                meta = document.createElement("meta");
                meta.setAttribute("name", "viewport");
                head.appendChild(meta);
            }
            meta.setAttribute("content", "width=device-width, initial-scale=1, minimum-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover");

            window.scrollTo(0, 0);
            document.documentElement.scrollTop = 0;
            document.body.scrollTop = 0;
        };

        window.__krResetZoom();
        document.addEventListener("focusout", function () {
            window.__krResetZoom();
        });
    },

    ResetPageZoom: function () {
        if (window.__krResetZoom) {
            window.__krResetZoom();
            return;
        }
    }
});
