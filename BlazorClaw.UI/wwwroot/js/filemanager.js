// FileManager Download Helper
window.downloadFile = function(filename, base64content) {
    const link = document.createElement('a');
    link.href = 'data:application/octet-stream;base64,' + base64content;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
