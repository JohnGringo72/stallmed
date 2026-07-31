// Collocated JS module του AiChatWidget (Blazor JS isolation).
// Χρειάζεται γιατί το Blazor δεν υποστηρίζει conditional preventDefault:
// Enter = αποστολή (preventDefault), Shift+Enter = νέα γραμμή (default).

export function wireInput(textarea, dotnetRef) {
    if (!textarea) return;
    textarea.addEventListener("keydown", (e) => {
        if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            dotnetRef.invokeMethodAsync("SendFromJs");
        }
    });
}

export function scrollToBottom(element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
}

// ---- Συρόμενο (floating) εικονίδιο του bot ----
// Πιάνεις το κουμπί και το σέρνεις όπου θες· η θέση αποθηκεύεται στο
// localStorage και επανέρχεται στο επόμενο φόρτωμα. Το panel ανοίγει προς
// τα πάνω όταν το κουμπί είναι χαμηλά, προς τα κάτω όταν είναι ψηλά.
export function wireDrag(root, fab) {
    if (!root || !fab) return;

    const saved = localStorage.getItem("sbtBotPos");
    if (saved) {
        try { applyPos(root, JSON.parse(saved)); } catch { }
    }

    let startX = 0, startY = 0, startRect = null, moved = false, tracking = false;

    fab.addEventListener("pointerdown", (e) => {
        tracking = true;
        moved = false;
        startX = e.clientX;
        startY = e.clientY;
        startRect = fab.getBoundingClientRect();
        fab.setPointerCapture(e.pointerId);
    });

    fab.addEventListener("pointermove", (e) => {
        if (!tracking) return;
        // Μικρές μετακινήσεις = κλικ, όχι σύρσιμο
        if (!moved && Math.abs(e.clientX - startX) < 6 && Math.abs(e.clientY - startY) < 6) return;
        moved = true;
        const cx = startRect.left + startRect.width / 2 + (e.clientX - startX);
        const cy = startRect.top + startRect.height / 2 + (e.clientY - startY);
        applyPos(root, computePos(cx, cy));
    });

    fab.addEventListener("pointerup", () => {
        tracking = false;
        if (moved) {
            const r = fab.getBoundingClientRect();
            localStorage.setItem("sbtBotPos",
                JSON.stringify(computePos(r.left + r.width / 2, r.top + r.height / 2)));
        }
    });

    // Μετά από σύρσιμο, το "κλικ" που ακολουθεί δεν πρέπει να ανοίξει το panel
    fab.addEventListener("click", (e) => {
        if (moved) {
            e.stopPropagation();
            e.preventDefault();
            moved = false;
        }
    });
}

function computePos(cx, cy) {
    const vw = window.innerWidth, vh = window.innerHeight;
    const right = Math.min(Math.max(vw - cx - 28, 8), vw - 64);
    const topMode = cy < vh / 2;
    const val = topMode
        ? Math.min(Math.max(cy - 28, 8), vh - 64)
        : Math.min(Math.max(vh - cy - 28, 8), vh - 64);
    return { right, topMode, val };
}

function applyPos(root, p) {
    root.style.right = p.right + "px";
    if (p.topMode) {
        root.style.top = p.val + "px";
        root.style.bottom = "auto";
        root.classList.add("ai-top-mode");
    } else {
        root.style.bottom = p.val + "px";
        root.style.top = "auto";
        root.classList.remove("ai-top-mode");
    }
}
