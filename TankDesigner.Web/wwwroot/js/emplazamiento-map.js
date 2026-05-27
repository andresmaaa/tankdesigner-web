window.tankDesignerMaps = (() => {
    const maps = new Map();

    function hasLeaflet() {
        return typeof window.L !== "undefined";
    }

    function createFallback(containerId, message) {
        const el = document.getElementById(containerId);
        if (!el) return;
        el.innerHTML = `<div class="map-fallback"><strong>Mapa no disponible</strong><span>${message}</span></div>`;
    }

    function destroy(containerId) {
        const current = maps.get(containerId);
        if (current && current.map) {
            current.map.remove();
        }
        maps.delete(containerId);
    }

    function init(containerId, options) {
        const el = document.getElementById(containerId);
        if (!el) return false;

        destroy(containerId);

        if (!hasLeaflet()) {
            createFallback(containerId, "Puedes introducir latitud y longitud manualmente.");
            return false;
        }

        const lat = Number(options?.lat ?? 40.4168);
        const lng = Number(options?.lng ?? -3.7038);
        const zoom = Number(options?.zoom ?? 6);
        const interactive = options?.interactive !== false;

        const map = L.map(containerId, {
            zoomControl: interactive,
            dragging: interactive,
            scrollWheelZoom: interactive,
            doubleClickZoom: interactive,
            boxZoom: interactive,
            keyboard: interactive,
            tap: interactive
        }).setView([lat, lng], zoom);

        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            attribution: "&copy; OpenStreetMap"
        }).addTo(map);

        const marker = L.marker([lat, lng], {
            draggable: interactive
        }).addTo(map);

        const dotNetRef = options?.dotNetRef || null;

        function notify(position) {
            if (!dotNetRef) return;
            dotNetRef.invokeMethodAsync("ActualizarCoordenadasDesdeMapa", position.lat, position.lng);
        }

        if (interactive) {
            map.on("click", function (e) {
                marker.setLatLng(e.latlng);
                notify(e.latlng);
            });

            marker.on("dragend", function () {
                notify(marker.getLatLng());
            });
        }

        maps.set(containerId, { map, marker });

        setTimeout(() => map.invalidateSize(), 120);
        return true;
    }

    function setMarker(containerId, lat, lng, zoom) {
        const current = maps.get(containerId);
        if (!current || !current.map || !current.marker) return false;

        const point = [Number(lat), Number(lng)];
        current.marker.setLatLng(point);
        current.map.setView(point, Number(zoom ?? current.map.getZoom()));
        setTimeout(() => current.map.invalidateSize(), 80);
        return true;
    }

    function invalidate(containerId) {
        const current = maps.get(containerId);
        if (current && current.map) {
            setTimeout(() => current.map.invalidateSize(), 80);
        }
    }

    return {
        init,
        destroy,
        setMarker,
        invalidate
    };
})();
