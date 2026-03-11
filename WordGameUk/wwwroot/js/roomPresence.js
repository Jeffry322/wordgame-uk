const presenceState = {
    roomId: null,
    playerId: null,
    beaconSent: false
};

function sendDisconnectBeacon() {
    if (presenceState.beaconSent || !presenceState.roomId || !presenceState.playerId) {
        return;
    }

    const payload = JSON.stringify({
        roomId: presenceState.roomId,
        playerId: presenceState.playerId
    });

    const body = new Blob([payload], {type: "application/json"});
    presenceState.beaconSent = true;
    navigator.sendBeacon("/api/multiplayer/presence/disconnect", body);
}

window.wordGameRoomPresence = {
    set(roomId, playerId) {
        presenceState.roomId = roomId || null;
        presenceState.playerId = playerId || null;
        presenceState.beaconSent = false;
    },
    clear() {
        presenceState.roomId = null;
        presenceState.playerId = null;
        presenceState.beaconSent = false;
    }
};

window.addEventListener("pagehide", sendDisconnectBeacon);
window.addEventListener("beforeunload", sendDisconnectBeacon);
