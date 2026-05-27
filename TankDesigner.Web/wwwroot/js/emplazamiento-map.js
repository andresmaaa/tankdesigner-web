window.tankDesignerMaps = (() => {
    const maps = new Map();
    const searchCache = new Map();

    function hasLeaflet() {
        return typeof window.L !== "undefined";
    }

    function safeText(value) {
        return (value || "").toString().trim();
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

    function buildResult(lat, lng, raw) {
        const address = raw?.address || {};
        const ciudad = address.city || address.town || address.village || address.municipality || address.county || "";
        const provincia = address.province || address.state_district || address.county || address.state || "";
        const pais = address.country || "";
        const codigoPostal = address.postcode || "";
        const direccionResumen = raw?.display_name || [ciudad, provincia, pais].filter(Boolean).join(", ");

        return {
            latitud: Number(lat),
            longitud: Number(lng),
            nombreUbicacion: [ciudad, provincia, pais].filter(Boolean).join(", "),
            ciudad,
            provincia,
            pais,
            codigoPostal,
            direccionResumen,
            fuenteDatos: raw ? "OpenStreetMap / Nominatim" : "Coordenadas manuales",
            fechaConsulta: new Date().toISOString()
        };
    }

    async function reverseGeocode(lat, lng) {
        const key = `reverse:${Number(lat).toFixed(6)},${Number(lng).toFixed(6)}`;
        if (searchCache.has(key)) return searchCache.get(key);

        try {
            const url = `https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${encodeURIComponent(lat)}&lon=${encodeURIComponent(lng)}&accept-language=es&addressdetails=1`;
            const response = await fetch(url, {
                headers: {
                    "Accept": "application/json"
                }
            });

            if (!response.ok) throw new Error(`Nominatim error ${response.status}`);

            const raw = await response.json();
            const result = buildResult(lat, lng, raw);
            searchCache.set(key, result);
            return result;
        } catch (error) {
            console.warn("No se pudo obtener la dirección de la ubicación:", error);
            return buildResult(lat, lng, null);
        }
    }

    function setPopup(marker, result) {
        if (!marker || !result) return;
        const title = safeText(result.nombreUbicacion) || "Ubicación seleccionada";
        const coords = `${Number(result.latitud).toFixed(5)}, ${Number(result.longitud).toFixed(5)}`;
        marker.bindPopup(`<strong>${title}</strong><br/><span>${coords}</span>`);
    }

    async function notify(dotNetRef, marker, latlng) {
        if (!dotNetRef || !latlng) return;
        const result = await reverseGeocode(latlng.lat, latlng.lng);
        setPopup(marker, result);
        await dotNetRef.invokeMethodAsync("ActualizarUbicacionDesdeMapa", result);
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
        setPopup(marker, buildResult(lat, lng, null));

        if (interactive) {
            map.on("click", async function (e) {
                marker.setLatLng(e.latlng);
                await notify(dotNetRef, marker, e.latlng);
            });

            marker.on("dragend", async function () {
                await notify(dotNetRef, marker, marker.getLatLng());
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
        setPopup(current.marker, buildResult(point[0], point[1], null));
        setTimeout(() => current.map.invalidateSize(), 80);
        return true;
    }

    async function searchLocation(containerId, query) {
        const text = safeText(query);
        if (!text) return null;

        const key = `search:${text.toLowerCase()}`;
        if (searchCache.has(key)) {
            const cached = searchCache.get(key);
            setMarker(containerId, cached.latitud, cached.longitud, 13);
            return cached;
        }

        try {
            const url = `https://nominatim.openstreetmap.org/search?format=jsonv2&q=${encodeURIComponent(text)}&accept-language=es&addressdetails=1&limit=1`;
            const response = await fetch(url, {
                headers: {
                    "Accept": "application/json"
                }
            });

            if (!response.ok) throw new Error(`Nominatim error ${response.status}`);

            const results = await response.json();
            if (!Array.isArray(results) || results.length === 0) return null;

            const raw = results[0];
            const result = buildResult(Number(raw.lat), Number(raw.lon), raw);
            searchCache.set(key, result);
            setMarker(containerId, result.latitud, result.longitud, 13);
            return result;
        } catch (error) {
            console.warn("No se pudo buscar la ubicación:", error);
            return null;
        }
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
        searchLocation,
        invalidate
    };
})();
