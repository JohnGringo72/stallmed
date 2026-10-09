// Υπαγόρευση με φωνή μέσω του browser (Web Speech API).
// Δεν χρειάζεται server ούτε κλειδιά -- η αναγνώριση γίνεται από τον browser.
// Υποστήριξη: Chrome/Edge σε desktop και Android. Το Safari (iOS) δεν το έχει,
// γι' αυτό το κουμπί κρύβεται εκεί. Το μικρόφωνο απαιτεί HTTPS ή localhost.

let recognition = null;
let shouldListen = false;
let restartTimer = null;
let consecutiveErrors = 0;
// Πόσα αποτελέσματα της τρέχουσας συνεδρίας έχουν ήδη σταλεί ως τελικά.
// Το Android Chrome ξαναστέλνει ΟΛΑ τα προηγούμενα finals σε κάθε event
// (με resultIndex 0), οπότε χωρίς δικό μας μετρητή το κείμενο διπλογράφεται.
let finalizedCount = 0;
let lastFinalSent = "";
let lastFinalAt = 0;
// Ό,τι τελικό κείμενο έχει σταλεί στην τρέχουσα συνεδρία. Σε πολλά Android
// κάθε νέο "τελικό" αποτέλεσμα περιέχει ΟΛΗ τη φράση από την αρχή (όχι μόνο
// τα νέα λόγια) -- με αυτό κόβουμε το ήδη σταλμένο πρόθεμα.
let sessionText = "";

export function isSupported() {
    return !!(window.SpeechRecognition || window.webkitSpeechRecognition);
}

export function start(dotnetRef, lang) {
    if (!isSupported()) return false;
    shouldListen = true;
    consecutiveErrors = 0;
    return startInternal(dotnetRef, lang);
}

function startInternal(dotnetRef, lang) {
    const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;

    stopRecognition();

    recognition = new Recognition();
    finalizedCount = 0;   // νέα συνεδρία = νέα λίστα αποτελεσμάτων
    sessionText = "";
    recognition.lang = lang || "el-GR";
    recognition.continuous = true;
    // Τα ενδιάμεσα αποτελέσματα δείχνουν ζωντανά τι ακούγεται, ώστε ο χρήστης
    // να βλέπει ότι όντως ηχογραφεί.
    recognition.interimResults = true;
    recognition.maxAlternatives = 1;

    recognition.onresult = (event) => {
        consecutiveErrors = 0;
        let finalText = "";
        let interimText = "";
        // Όχι event.resultIndex: στο Android είναι συχνά 0 και ξαναδίνει
        // όλα τα παλιά finals. Διαβάζουμε μόνο ό,τι δεν έχουμε ήδη στείλει.
        for (let i = finalizedCount; i < event.results.length; i++) {
            const transcript = event.results[i][0].transcript;
            if (event.results[i].isFinal) {
                finalText += transcript;
                finalizedCount = i + 1;
            } else {
                interimText += transcript;
            }
        }
        finalText = finalText.trim();
        if (finalText) {
            let toSend = finalText;
            // Android: το νέο τελικό ξαναπεριέχει όλη τη φράση -- στέλνεται
            // μόνο η συνέχεια, όχι ξανά το κομμάτι που έχει ήδη γραφτεί.
            if (sessionText && finalText.startsWith(sessionText)) {
                toSend = finalText.slice(sessionText.length).trim();
                sessionText = finalText;
            } else {
                sessionText = sessionText ? sessionText + " " + finalText : finalText;
            }
            // Δεύτερη ασφάλεια: το ίδιο τελικό κείμενο μέσα σε 3" είναι
            // διπλό event του Android Chrome, όχι επανάληψη του χρήστη.
            const now = Date.now();
            if (toSend && !(toSend === lastFinalSent && now - lastFinalAt < 3000)) {
                lastFinalSent = toSend;
                lastFinalAt = now;
                dotnetRef.invokeMethodAsync("OnSpeechResult", toSend);
            }
        }
        dotnetRef.invokeMethodAsync("OnSpeechInterim", interimText.trim());
    };

    recognition.onerror = (event) => {
        const code = event.error || "unknown";
        // Αυτά σημαίνουν ότι δεν έχει νόημα να ξαναπροσπαθήσουμε.
        if (code === "not-allowed" || code === "service-not-allowed") {
            shouldListen = false;
            dotnetRef.invokeMethodAsync("OnSpeechError", code);
            return;
        }
        // Το "no-speech" είναι φυσιολογικό σε παύσεις -- συνεχίζουμε,
        // αλλά σταματάμε αν επαναλαμβάνεται συνέχεια (π.χ. χαλασμένο μικρόφωνο).
        consecutiveErrors++;
        if (consecutiveErrors >= 5) {
            shouldListen = false;
            dotnetRef.invokeMethodAsync("OnSpeechError", code);
        }
    };

    recognition.onend = () => {
        recognition = null;
        if (shouldListen) {
            // Ο Chrome κλείνει μόνος του την αναγνώριση μετά από κάθε παύση.
            // Χωρίς αυτό το restart χάνονταν οι λέξεις που λέγονται αμέσως μετά.
            restartTimer = setTimeout(() => {
                if (shouldListen) startInternal(dotnetRef, lang);
            }, 150);
        } else {
            dotnetRef.invokeMethodAsync("OnSpeechEnd");
        }
    };

    try {
        recognition.start();
        return true;
    } catch (e) {
        recognition = null;
        shouldListen = false;
        return false;
    }
}

export function stop() {
    shouldListen = false;
    if (restartTimer) {
        clearTimeout(restartTimer);
        restartTimer = null;
    }
    stopRecognition();
}

function stopRecognition() {
    if (recognition) {
        try { recognition.stop(); } catch (e) { /* αγνοείται */ }
        recognition = null;
    }
}
