mergeInto(LibraryManager.library, {
  GoogleAuth_Init: function (clientIdPtr, gameObjectNamePtr) {
    var clientId = UTF8ToString(clientIdPtr).replace(/^\s+|\s+$/g, "");
    var gameObjectName = UTF8ToString(gameObjectNamePtr);

    window._googleAuth = window._googleAuth || {};
    window._googleAuth.clientId = clientId;
    window._googleAuth.gameObjectName = gameObjectName;
    window._googleAuth.ready = false;

    function send(method, message) {
      var target = window._googleAuth.gameObjectName || gameObjectName;
      if (typeof SendMessage === "function") {
        SendMessage(target, method, message || "");
      }
    }

    var tokenKey = "KingsRescue.GoogleIdToken";
    var uxKey = "KingsRescue.GoogleUx";

    function hideOverlay() {
      var overlay = document.getElementById("google-auth-overlay");
      if (overlay) {
        overlay.style.display = "none";
      }
      setGameClicks(true);
    }

    function setGameClicks(enabled) {
      var canvas = document.querySelector("canvas");
      if (canvas) {
        canvas.style.pointerEvents = enabled ? "auto" : "none";
      }
    }

    function rememberUx(mode) {
      try {
        sessionStorage.setItem(uxKey, mode);
      } catch (err) {
        // Private mode can block storage. The click still proceeds.
      }
    }

    function storedUx() {
      try {
        return sessionStorage.getItem(uxKey) || "";
      } catch (err) {
        return "";
      }
    }

    function clearUx() {
      try {
        sessionStorage.removeItem(uxKey);
      } catch (err) {
        // Ignore.
      }
    }

    function isFullscreen() {
      try {
        if (document.fullscreenElement || document.webkitFullscreenElement) {
          return true;
        }
      } catch (err) {
        // Ignore.
      }

      // itch.io fullscreenes the game iframe from the parent page, which this
      // frame is not allowed to see. A game that fills the screen is that case.
      try {
        if (window.top !== window.self) {
          var sw = window.screen.width || 0;
          var sh = window.screen.height || 0;
          if (sw > 0 && sh > 0 && window.innerWidth / sw > 0.92 && window.innerHeight / sh > 0.92) {
            return true;
          }
        }
      } catch (err) {
        // Cross-origin parent. The screen-size check above still works.
      }

      return false;
    }

    function prefersRedirect() {
      var ua = navigator.userAgent || "";
      if (/Android|iPhone|iPad|iPod|Mobile/i.test(ua)) {
        return true;
      }
      return isFullscreen();
    }

    function leaveFullscreen() {
      var tasks = [];

      function exitDoc(doc) {
        try {
          var active = doc.fullscreenElement || doc.webkitFullscreenElement;
          if (!active) {
            return;
          }
          var exit = doc.exitFullscreen || doc.webkitExitFullscreen || doc.mozCancelFullScreen || doc.msExitFullscreen;
          if (!exit) {
            return;
          }
          var result = exit.call(doc);
          if (result && typeof result.then === "function") {
            tasks.push(result.catch(function () {}));
          }
        } catch (err) {
          // The parent page (itch.io) is cross-origin. Redirect covers that case.
        }
      }

      exitDoc(document);
      try {
        if (window.parent && window.parent !== window) {
          exitDoc(window.parent.document);
        }
      } catch (err) {
        // Cross-origin parent.
      }

      return Promise.all(tasks);
    }

    function deliverCredential(credential) {
      clearUx();
      window._googleAuth.pendingCredential = credential;
      try {
        sessionStorage.setItem(tokenKey, credential);
      } catch (err) {
        // SendMessage below is the live path. Storage only covers a reload.
      }
      send("OnGoogleIdToken", credential);
    }

    function currentOrigin() {
      try {
        return window.location.origin || "";
      } catch (err) {
        return "";
      }
    }

    function showOverlay() {
      var overlay = document.getElementById("google-auth-overlay");
      if (!overlay) {
        overlay = document.createElement("div");
        overlay.id = "google-auth-overlay";
        overlay.style.cssText =
          "position:fixed;inset:0;background:rgba(0,0,0,0.55);display:flex;align-items:center;justify-content:center;z-index:2147483000;";

        var panel = document.createElement("div");
        panel.style.cssText =
          "background:#fff;padding:24px 28px;border-radius:12px;text-align:center;font-family:Arial,sans-serif;min-width:280px;max-width:420px;";
        panel.innerHTML =
          '<p style="margin:0 0 16px;color:#202124;font-size:16px;">Sign in with Google</p>' +
          '<div id="google-signin-button" style="min-height:44px;display:flex;justify-content:center;"></div>' +
          '<p id="google-auth-note" style="margin:12px 0 0;color:#5f6368;font-size:13px;"></p>' +
          '<p id="google-auth-cancel" style="margin:16px 0 0;cursor:pointer;color:#5f6368;font-size:14px;">Cancel</p>';
        overlay.appendChild(panel);
        document.body.appendChild(overlay);

        document.getElementById("google-auth-cancel").onclick = function () {
          clearUx();
          hideOverlay();
          send("OnGoogleAuthError", "Sign-in cancelled");
        };
      } else {
        overlay.style.display = "flex";
      }

      var redirect = prefersRedirect();
      rememberUx(redirect ? "redirect" : "popup");
      var note = document.getElementById("google-auth-note");
      if (note) {
        note.textContent = redirect
          ? "The game will reload for a moment so Google can open outside full screen."
          : "";
      }

      setGameClicks(false);
      var buttonHost = document.getElementById("google-signin-button");
      if (buttonHost && window.google && google.accounts && google.accounts.id) {
        buttonHost.innerHTML = "";
        google.accounts.id.renderButton(buttonHost, {
          theme: "outline",
          size: "large",
          type: "standard",
          text: "signin_with",
          shape: "rectangular",
          width: 280,
          ux_mode: redirect ? "redirect" : "popup",
        });
      }
    }

    window._googleAuth.showOverlay = showOverlay;
    window._googleAuth.hideOverlay = hideOverlay;
    window._googleAuth.leaveFullscreen = leaveFullscreen;
    window._googleAuth.send = send;

    // The default WebGL template does not include Google's script, so load it here.
    if (!document.getElementById("google-gsi-client")) {
      var gsi = document.createElement("script");
      gsi.id = "google-gsi-client";
      gsi.src = "https://accounts.google.com/gsi/client";
      gsi.async = true;
      gsi.defer = true;
      document.head.appendChild(gsi);
    }

    function initializeGis() {
      if (!window.google || !google.accounts || !google.accounts.id) {
        return false;
      }

      if (!clientId || clientId.indexOf(".apps.googleusercontent.com") < 0) {
        window._googleAuth.initError = "Google Web client ID is missing or invalid";
        return true;
      }

      console.log("[GoogleAuth] origin=" + currentOrigin() + " client_id=" + clientId);

      // FedCM draws the saved account on the button and then drops the click in Chrome
      // and Edge. The popup path actually returns a token. Phones and full screen use
      // a redirect instead, because a popup cannot appear above full screen.
      var returning = storedUx() === "redirect";
      google.accounts.id.initialize({
        client_id: clientId,
        callback: function (response) {
          hideOverlay();
          if (response && response.credential) {
            deliverCredential(response.credential);
          } else {
            clearUx();
            send("OnGoogleAuthError", "Google did not return an ID token");
          }
        },
        auto_select: false,
        cancel_on_tap_outside: false,
        ux_mode: returning ? "redirect" : "popup",
        itp_support: true,
        use_fedcm_for_prompt: false,
        use_fedcm_for_button: false,
      });

      window._googleAuth.ready = true;
      return true;
    }

    function waitForGis(attemptsLeft) {
      if (initializeGis()) {
        return;
      }
      if (attemptsLeft <= 0) {
        window._googleAuth.initError = "Google Identity Services failed to load";
        return;
      }
      setTimeout(function () {
        waitForGis(attemptsLeft - 1);
      }, 100);
    }

    waitForGis(100);
  },

  GoogleAuth_SignIn: function () {
    var auth = window._googleAuth;
    if (!auth || !auth.ready) {
      var reason = (auth && auth.initError) || "Google Sign-In is not ready";
      if (auth && typeof auth.send === "function") {
        auth.send("OnGoogleAuthError", reason);
      }
      return;
    }

    // Do not call google.accounts.id.prompt(): One Tap falls back to GeneralOAuthFlow and shows
    // 401 invalid_client unless the OAuth client is type Web with this page's exact JavaScript origin.
    // Leave full screen first. Android and itch.io keep the Google window hidden while it is up.
    var opened = false;
    function open() {
      if (opened) {
        return;
      }
      opened = true;
      auth.showOverlay();
    }

    var pendingExit = typeof auth.leaveFullscreen === "function" ? auth.leaveFullscreen() : Promise.resolve();
    if (pendingExit && typeof pendingExit.then === "function") {
      pendingExit.then(open, open);
    }
    setTimeout(open, 400);
  },

  GoogleAuth_ConsumePending: function () {
    var token = "";
    try {
      token = sessionStorage.getItem("KingsRescue.GoogleIdToken") || "";
      if (token) {
        sessionStorage.removeItem("KingsRescue.GoogleIdToken");
      }
    } catch (err) {
      token = "";
    }

    if (!token && window._googleAuth && window._googleAuth.pendingCredential) {
      token = window._googleAuth.pendingCredential;
    }
    if (window._googleAuth) {
      window._googleAuth.pendingCredential = "";
    }

    var length = lengthBytesUTF8(token) + 1;
    var buffer = _malloc(length);
    stringToUTF8(token, buffer, length);
    return buffer;
  },

  GoogleAuth_ClearPending: function () {
    try {
      sessionStorage.removeItem("KingsRescue.GoogleIdToken");
    } catch (err) {
      // Ignore.
    }
    if (window._googleAuth) {
      window._googleAuth.pendingCredential = "";
    }
  },

  GoogleAuth_SignOut: function () {
    var auth = window._googleAuth;
    if (auth && typeof auth.hideOverlay === "function") {
      auth.hideOverlay();
    }
    try {
      if (window.google && google.accounts && google.accounts.id) {
        google.accounts.id.disableAutoSelect();
      }
    } catch (err) {
      // Ignore GIS sign-out errors.
    }
  },
});
