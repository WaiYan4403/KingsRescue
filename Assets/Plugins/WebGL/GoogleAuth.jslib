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

    function hideOverlay() {
      var overlay = document.getElementById("google-auth-overlay");
      if (overlay) {
        overlay.style.display = "none";
      }
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
      var origin = currentOrigin();
      if (!overlay) {
        overlay = document.createElement("div");
        overlay.id = "google-auth-overlay";
        overlay.style.cssText =
          "position:fixed;inset:0;background:rgba(0,0,0,0.55);display:flex;align-items:center;justify-content:center;z-index:9999;";

        var panel = document.createElement("div");
        panel.style.cssText =
          "background:#fff;padding:24px 28px;border-radius:12px;text-align:center;font-family:Arial,sans-serif;min-width:280px;max-width:420px;";
        panel.innerHTML =
          '<p style="margin:0 0 16px;color:#202124;font-size:16px;">Sign in with Google</p>' +
          '<div id="google-signin-button"></div>' +
          '<p id="google-auth-cancel" style="margin:16px 0 0;cursor:pointer;color:#5f6368;font-size:14px;">Cancel</p>';
        overlay.appendChild(panel);
        document.body.appendChild(overlay);

        document.getElementById("google-auth-cancel").onclick = function () {
          hideOverlay();
          send("OnGoogleAuthError", "Sign-in cancelled");
        };
      } else {
        overlay.style.display = "flex";
      }

      var buttonHost = document.getElementById("google-signin-button");
      if (buttonHost && window.google && google.accounts && google.accounts.id) {
        buttonHost.innerHTML = "";
        google.accounts.id.renderButton(buttonHost, {
          theme: "outline",
          size: "large",
          type: "standard",
          text: "signin_with",
          shape: "rectangular",
          ux_mode: "popup",
        });
      }
    }

    window._googleAuth.showOverlay = showOverlay;
    window._googleAuth.hideOverlay = hideOverlay;
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

      google.accounts.id.initialize({
        client_id: clientId,
        callback: function (response) {
          hideOverlay();
          if (response && response.credential) {
            send("OnGoogleIdToken", response.credential);
          } else {
            send("OnGoogleAuthError", "Google did not return an ID token");
          }
        },
        auto_select: false,
        cancel_on_tap_outside: true,
        ux_mode: "popup",
        itp_support: true,
        use_fedcm_for_button: true,
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
    auth.showOverlay();
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
