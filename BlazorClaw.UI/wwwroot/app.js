function scrollToEnd(element) {
    element.scrollTop = element.scrollHeight;
}
function scrollIdToEnd(elementId) {
    var elm = document.getElementById(elementId);
    scrollToEnd(elm);
}