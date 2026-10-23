// Reconexión del panel con el servidor (Blazor Server).
// Al volver a la pestaña después de una pausa se reintenta enseguida; si la sesión ya no existe,
// se recarga la página sola para que nunca quede "congelada".
const reconnectModal = document.getElementById("components-reconnect-modal");
reconnectModal.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);

const retryButton = document.getElementById("components-reconnect-button");
retryButton.addEventListener("click", retry);

const resumeButton = document.getElementById("components-resume-button");
resumeButton.addEventListener("click", resume);

let desconectado = false;
let reintentando = false;

// Siempre escuchando: al volver a la pestaña, si se perdió la conexión, se reintenta ya.
document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "visible" && desconectado) {
        retry();
    }
});

function handleReconnectStateChanged(event) {
    const estado = event.detail.state;
    if (estado === "show") {
        desconectado = true;
        reconnectModal.showModal();
    } else if (estado === "hide") {
        desconectado = false;
        reconnectModal.close();
    } else if (estado === "failed") {
        desconectado = true;
        // Si el usuario está mirando, reintenta sin esperar a que pulse el botón.
        if (document.visibilityState === "visible") {
            setTimeout(retry, 2000);
        }
    } else if (estado === "rejected") {
        location.reload();
    }
}

async function retry() {
    if (reintentando) return;
    reintentando = true;
    try {
        // true = reconectó · false = el servidor respondió pero la sesión ya no existe · excepción = servidor no disponible
        const successful = await Blazor.reconnect();
        if (successful) {
            desconectado = false;
            reconnectModal.close();
            return;
        }
        const resumeSuccessful = await Blazor.resumeCircuit();
        if (resumeSuccessful) {
            desconectado = false;
            reconnectModal.close();
        } else {
            location.reload();
        }
    } catch {
        // Servidor no disponible por ahora: se reintenta al volver a la pestaña o con el botón.
    } finally {
        reintentando = false;
    }
}

async function resume() {
    try {
        const successful = await Blazor.resumeCircuit();
        if (!successful) {
            location.reload();
        }
    } catch {
        reconnectModal.classList.replace("components-reconnect-paused", "components-reconnect-resume-failed");
    }
}
