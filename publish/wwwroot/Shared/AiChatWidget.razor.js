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
