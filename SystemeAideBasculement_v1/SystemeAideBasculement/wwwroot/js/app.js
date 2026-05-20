window.autoResizeTextarea = (el) => {
    if (!el) return;

    el.style.height = "auto";       // reset
    el.style.overflow = "hidden";   // pas de scrollbar
    el.style.height = el.scrollHeight + "px"; // ajuste hauteur
};

// Global auth helper for Blazor JSInterop
// Expose a single flat function name: sabAuth_login
window.sabAuth_login = async function (username, password) {
    if (!username || !password) {
        return {
            success: false,
            displayName: null,
            role: null,
            error: "Identifiants requis."
        };
    }


    try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 20000); // timeout 8s

        const response = await fetch('/api/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, password }),
            signal: controller.signal
        });

        clearTimeout(timeoutId);
        debugger;
        let data = null;
        try {
            data = await response.json();
        } catch {
            // réponse non JSON
        }

        if (!response.ok) {
            return {
                success: false,
                displayName: null,
                role: null,
                error: data?.error ?? "Erreur d’authentification."
            };
        }

        return {
            success: true,
            displayName: data?.displayName ?? username,
            role: data?.role ?? null,
            error: null
        };
    }
    catch (err) {
        if (err.name === "AbortError") {
            return {
                success: false,
                displayName: null,
                role: null,
                error: "Timeout du serveur LDAP."
            };
        }

        return {
            success: false,
            displayName: null,
            role: null,
            error: "Impossible de joindre le serveur."
        };
    }
};

window.sabAuth_logout = async function () {
    const response = await fetch('/api/auth/logout', {
        method: 'POST'
    });

    return {
        success: response.ok
    };
};