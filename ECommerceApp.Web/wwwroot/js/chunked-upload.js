define("chunked-upload", [], function () {
    var MAX_ATTEMPTS = 3;
    var BASE_DELAY_MS = 500;

    var messages = {
        init: "Błąd inicjalizacji wysyłania.",
        status: "Nie udało się sprawdzić stanu przerwanego wysyłania. Kliknij „Wgraj”, aby spróbować ponownie.",
        expired: "Sesja wysyłania wygasła. Kliknij „Wgraj”, aby wysłać plik od początku.",
        resumed: "Wznowiono przerwane wysyłanie."
    };

    function storageKey(itemId) {
        return "chunkedUpload:item:" + itemId;
    }

    function loadSession(itemId) {
        try {
            var raw = window.localStorage.getItem(storageKey(itemId));
            return raw ? JSON.parse(raw) : null;
        } catch (e) {
            return null;
        }
    }

    function saveSession(itemId, sessionId, file) {
        try {
            window.localStorage.setItem(storageKey(itemId), JSON.stringify({
                sessionId: sessionId,
                fileName: file.name,
                fileSize: file.size,
                lastModified: file.lastModified
            }));
        } catch (e) {
            // Storage unavailable: the upload still works, it just cannot be resumed after a reload.
        }
    }

    function clearSession(itemId) {
        try {
            window.localStorage.removeItem(storageKey(itemId));
        } catch (e) {
            // Nothing to clear when storage is unavailable.
        }
    }

    function matchesFile(stored, file) {
        return !!stored
            && stored.fileName === file.name
            && stored.fileSize === file.size
            && stored.lastModified === file.lastModified;
    }

    function delay(ms) {
        return new Promise(function (resolve) {
            setTimeout(resolve, ms);
        });
    }

    // Retries network errors and 5xx with exponential backoff (500 ms, 1 s). 4xx and success are returned as-is.
    async function fetchWithRetry(send) {
        var lastError = null;
        for (var attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
            try {
                var response = await send();
                if (response.status < 500) {
                    return response;
                }
                lastError = new Error("HTTP " + response.status);
            } catch (e) {
                lastError = e;
            }
            if (attempt < MAX_ATTEMPTS) {
                await delay(BASE_DELAY_MS * Math.pow(2, attempt - 1));
            }
        }
        throw lastError;
    }

    function initialize(options) {
        var itemId = options.itemId;
        var onComplete = options.onComplete;
        var fileInput = document.getElementById("chunkFileInput");
        var uploadButton = document.getElementById("chunkUploadBtn");
        var errorEl = document.getElementById("chunkUploadError");
        var infoEl = document.getElementById("chunkUploadInfo");
        var progressWrapper = document.getElementById("chunkProgressWrapper");
        var progressBar = document.getElementById("chunkProgressBar");

        function antiForgeryToken() {
            var input = document.querySelector('input[name="__RequestVerificationToken"]');
            if (input) {
                return input.value;
            }
            var cookie = document.cookie.split("; ").find(function (row) {
                return row.startsWith("XSRF-TOKEN=");
            });
            return cookie ? cookie.split("=")[1] : "";
        }

        function showError(message) {
            errorEl.textContent = message;
            errorEl.classList.remove("d-none");
            progressWrapper.classList.add("d-none");
        }

        function showInfo(message) {
            infoEl.textContent = message;
            infoEl.classList.remove("d-none");
        }

        function setProgress(received, total) {
            var pct = Math.round(received / total * 100);
            progressBar.style.width = pct + "%";
            progressBar.textContent = pct + "%";
        }

        // Resolves to the session to continue, or null when the server reports the upload already completed.
        async function openSession(file) {
            var stored = loadSession(itemId);
            if (matchesFile(stored, file)) {
                var statusResponse = await fetchWithRetry(function () {
                    return fetch("/Catalog/Image/UploadStatus?sessionId=" + encodeURIComponent(stored.sessionId));
                }).catch(function () {
                    throw new Error(messages.status);
                });
                if (statusResponse.ok) {
                    var status = await statusResponse.json();
                    if (status.complete) {
                        return null;
                    }
                    return {
                        sessionId: status.sessionId,
                        chunkSize: status.chunkSize,
                        totalChunks: status.totalChunks,
                        received: status.receivedChunkIds,
                        resumed: true
                    };
                }
                if (statusResponse.status !== 404) {
                    throw new Error(messages.status);
                }
            }
            clearSession(itemId);

            var token = antiForgeryToken();
            var initResponse = await fetchWithRetry(function () {
                return fetch("/Catalog/Image/InitUpload", {
                    method: "POST",
                    headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
                    body: JSON.stringify({ fileName: file.name, fileSizeBytes: file.size, itemId: itemId })
                });
            }).catch(function () {
                throw new Error(messages.init);
            });
            if (!initResponse.ok) {
                throw new Error(messages.init);
            }
            var init = await initResponse.json();
            saveSession(itemId, init.sessionId, file);
            return {
                sessionId: init.sessionId,
                chunkSize: init.chunkSize,
                totalChunks: init.totalChunks,
                received: [],
                resumed: false
            };
        }

        async function sendChunk(file, session, chunkId) {
            var token = antiForgeryToken();
            var start = (chunkId - 1) * session.chunkSize;
            var response;
            try {
                response = await fetchWithRetry(function () {
                    var form = new FormData();
                    form.append("sessionId", session.sessionId);
                    form.append("chunkId", chunkId);
                    form.append("chunk", file.slice(start, start + session.chunkSize), file.name);
                    form.append("__RequestVerificationToken", token);
                    return fetch("/Catalog/Image/UploadChunk", { method: "POST", body: form });
                });
            } catch (e) {
                throw new Error("Nie udało się wysłać fragmentu " + chunkId + ". Kliknij „Wgraj”, aby wznowić wysyłanie.");
            }
            if (response.status === 404) {
                clearSession(itemId);
                throw new Error(messages.expired);
            }
            if (!response.ok) {
                throw new Error("Serwer odrzucił fragment " + chunkId + ". Kliknij „Wgraj”, aby wznowić wysyłanie.");
            }
            return response.json();
        }

        async function upload(file) {
            var session = await openSession(file);
            if (session === null) {
                clearSession(itemId);
                onComplete({ complete: true });
                return;
            }
            if (session.resumed) {
                showInfo(messages.resumed);
            }

            var received = new Set(session.received);
            setProgress(received.size, session.totalChunks);
            for (var chunkId = 1; chunkId <= session.totalChunks; chunkId++) {
                if (received.has(chunkId)) {
                    continue;
                }
                var result = await sendChunk(file, session, chunkId);
                setProgress(result.receivedCount, session.totalChunks);
                if (result.complete) {
                    clearSession(itemId);
                    onComplete(result);
                    return;
                }
            }
        }

        fileInput.addEventListener("change", function () {
            var file = fileInput.files[0];
            if (!file || !matchesFile(loadSession(itemId), file)) {
                clearSession(itemId);
            }
        });

        uploadButton.addEventListener("click", async function () {
            var file = fileInput.files[0];
            if (!file) {
                return;
            }

            errorEl.classList.add("d-none");
            errorEl.textContent = "";
            infoEl.classList.add("d-none");
            progressWrapper.classList.remove("d-none");
            setProgress(0, 1);
            uploadButton.disabled = true;
            try {
                await upload(file);
            } catch (e) {
                showError(e.message);
            } finally {
                uploadButton.disabled = false;
            }
        });

        // Lets browser tests wait until the click handler is attached.
        document.getElementById("chunkUploadWidget").dataset.ready = "true";
    }

    return {
        initialize: initialize
    };
});
