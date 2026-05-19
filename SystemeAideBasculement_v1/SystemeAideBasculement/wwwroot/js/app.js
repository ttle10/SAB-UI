window.autoResizeTextarea = (el) => {
    if (!el) return;

    el.style.height = "auto";       // reset
    el.style.overflow = "hidden";   // pas de scrollbar
    el.style.height = el.scrollHeight + "px"; // ajuste hauteur
};

// Global auth helper for Blazor JSInterop
window.sabAuth = window.sabAuth || {};

window.sabAuth.login = async function (username, password) {
    const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password })
    });

    if (!response.ok) {
        let error;
        try {
            error = await response.json();
        } catch { }
        return {
            success: false,
            displayName: null,
            groupName: null,
            error: error?.error ?? 'Erreur de connexion.'
        };
    }

    const result = await response.json();
    return {
        success: true,
        displayName: result.displayName ?? username,
        groupName: result.groupName ?? null,
        error: null
    };
};