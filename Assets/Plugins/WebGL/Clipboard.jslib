mergeInto(LibraryManager.library, {
  CopyToClipboard: function (textPtr) {
    var text = UTF8ToString(textPtr);

    // Modern async clipboard API
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(function () {
        console.log("Copied to clipboard:", text);
      }).catch(function (err) {
        console.error("Clipboard write failed:", err);
      });
    } else {
      // Fallback (older browsers)
      var textarea = document.createElement("textarea");
      textarea.value = text;
      document.body.appendChild(textarea);
      textarea.select();
      document.execCommand("copy");
      document.body.removeChild(textarea);
    }
  }
});