window.autoResizeTextarea = (el) => {
    if (!el) return;

    el.style.height = "auto";       // reset
    el.style.overflow = "hidden";   // pas de scrollbar
    el.style.height = el.scrollHeight + "px"; // ajuste hauteur
};