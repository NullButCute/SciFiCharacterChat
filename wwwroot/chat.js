window.scrollFeedToBottom = (el) => {
    if (el) el.scrollTop = el.scrollHeight;
};

window.copyText = async (text) => {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
};

window.confirmAction = (msg) => confirm(msg);