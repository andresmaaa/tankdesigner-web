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

    openPreviewAndPrint: function (title, html) {
        try {
            const win = window.open("", "_blank");
            if (!win) return false;

            win.document.open();
            win.document.write(html);
            win.document.close();

            try {
                win.document.title = title || "Informe";
            } catch { }

            const lanzarImpresion = () => {
                try {
                    win.focus();
                    win.print();
                } catch { }
            };

            if (win.document.readyState === "complete") {
                setTimeout(lanzarImpresion, 250);
            } else {
                win.addEventListener("load", () => setTimeout(lanzarImpresion, 250), { once: true });
            }

            return true;
        } catch (e) {
            console.error("openPreviewAndPrint error:", e);
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

    saveFileFromBytes: async function (fileName, contentType, base64Data) {
        try {
            const byteCharacters = atob(base64Data);
            const byteNumbers = new Array(byteCharacters.length);

            for (let i = 0; i < byteCharacters.length; i++) {
                byteNumbers[i] = byteCharacters.charCodeAt(i);
            }

            const byteArray = new Uint8Array(byteNumbers);
            const blob = new Blob([byteArray], { type: contentType });

            if ('showSaveFilePicker' in window) {
                const handle = await window.showSaveFilePicker({
                    suggestedName: fileName,
                    types: [
                        {
                            description: 'Documento PDF',
                            accept: {
                                'application/pdf': ['.pdf']
                            }
                        }
                    ]
                });

                const writable = await handle.createWritable();
                await writable.write(blob);
                await writable.close();
                return { ok: true, usedPicker: true };
            }

            const url = URL.createObjectURL(blob);
            const link = document.createElement("a");
            link.href = url;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);

            setTimeout(() => URL.revokeObjectURL(url), 1000);

            return { ok: true, usedPicker: false };
        } catch (e) {
            console.error("saveFileFromBytes error:", e);

            if (e && e.name === "AbortError") {
                return { ok: false, cancelled: true, message: "Guardado cancelado por el usuario." };
            }

            return {
                ok: false,
                cancelled: false,
                message: e?.message || "No se pudo guardar el archivo."
            };
        }
    }
};