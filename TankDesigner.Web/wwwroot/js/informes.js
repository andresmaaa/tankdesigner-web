window.tankInformes = {
    openPreview: function (title, html) {
        try {
            const win = window.open("", "_blank");
            if (!win) return false;

            win.document.open();
            win.document.write(html);
            win.document.close();

            try {
                win.document.title = title || "Informe";
            } catch { }

            return true;
        } catch (e) {
            console.error("openPreview error:", e);
            return false;
        }
    },

    downloadFileFromBytes: function (fileName, contentType, base64Data) {
        try {
            const link = document.createElement("a");
            link.href = `data:${contentType};base64,${base64Data}`;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            return true;
        } catch (e) {
            console.error("downloadFileFromBytes error:", e);
            return false;
        }
    },

    printHtmlAsPdf: function (title, html, fileName) {
        try {
            const win = window.open("", "_blank");
            if (!win) return false;

            const safeTitle = title || "Informe";
            const safeFileName = fileName || "informe";

            win.document.open();
            win.document.write(html);
            win.document.close();

            try {
                win.document.title = safeFileName.replace(".pdf", "");
            } catch { }

            const launchPrint = () => {
                setTimeout(() => {
                    try {
                        win.focus();
                        win.print();
                    } catch (e) {
                        console.error("printHtmlAsPdf print error:", e);
                    }
                }, 700);
            };

            if (win.document.readyState === "complete") {
                launchPrint();
            } else {
                win.onload = launchPrint;
            }

            return true;
        } catch (e) {
            console.error("printHtmlAsPdf error:", e);
            return false;
        }
    }
};