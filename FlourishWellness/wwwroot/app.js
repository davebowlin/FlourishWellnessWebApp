window.downloadFile = function (fileName, base64Content) {
    const link = document.createElement('a');
    link.download = fileName;
    link.href = 'data:text/csv;base64,' + base64Content;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

// Unsaved-changes guard: warn when the user tries to close/reload the tab or browser
window.flourish = window.flourish || {};

flourish._leaveHandler = null;

flourish.enableLeaveWarning = function () {
    if (flourish._leaveHandler) return; // already registered
    flourish._leaveHandler = function (e) {
        e.preventDefault();
        e.returnValue = '';
    };
    window.addEventListener('beforeunload', flourish._leaveHandler);
};

flourish.disableLeaveWarning = function () {
    if (flourish._leaveHandler) {
        window.removeEventListener('beforeunload', flourish._leaveHandler);
        flourish._leaveHandler = null;
    }
};
