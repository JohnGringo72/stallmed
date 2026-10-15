// Χάρτης Παρακολούθησης Συσκευών (Leaflet, φορτώνεται στο index.html).
// Ένας χάρτης ανά elementId· markers συσκευών + διαδρομή ιστορικού.
// Τα κείμενα (ονόματα συσκευών κλπ.) μπαίνουν ως textContent -- ποτέ ως HTML.

const maps = {};

export function init(elementId, dotNetRef) {
    if (typeof L === 'undefined') return false;
    const el = document.getElementById(elementId);
    if (!el) return false;
    if (maps[elementId]) dispose(elementId);

    const map = L.map(el).setView([38.5, 23.5], 6);   // Ελλάδα
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; OpenStreetMap'
    }).addTo(map);

    maps[elementId] = {
        map,
        dotNetRef,
        markers: {},
        markerLayer: L.layerGroup().addTo(map),
        routeLayer: L.layerGroup().addTo(map),
        fitted: false
    };
    return true;
}

function popupContent(m) {
    const div = document.createElement('div');
    const title = document.createElement('strong');
    title.textContent = m.label;
    div.appendChild(title);
    (m.lines || []).forEach(line => {
        div.appendChild(document.createElement('br'));
        div.appendChild(document.createTextNode(line));
    });
    return div;
}

function icon(status) {
    // online = πράσινο, offline = γκρι, χωρίς fix/άγνωστο = πορτοκαλί, alarm = κόκκινο
    const colors = { online: '#28a745', offline: '#6c757d', nofix: '#fd7e14', alarm: '#dc3545' };
    const color = colors[status] || colors.offline;
    return L.divIcon({
        className: '',
        html: `<div style="width:16px;height:16px;border-radius:50%;background:${color};border:2px solid #fff;box-shadow:0 0 3px rgba(0,0,0,.6)"></div>`,
        iconSize: [16, 16],
        iconAnchor: [8, 8]
    });
}

// markers: [{ id, lat, lon, label, status, lines: [] }]
export function setMarkers(elementId, markers, fit) {
    const s = maps[elementId];
    if (!s) return;
    const seen = new Set();
    markers.forEach(m => {
        seen.add(m.id);
        let marker = s.markers[m.id];
        if (!marker) {
            marker = L.marker([m.lat, m.lon]).addTo(s.markerLayer);
            marker.on('click', () => {
                if (s.dotNetRef) s.dotNetRef.invokeMethodAsync('OnMarkerClickJs', m.id);
            });
            s.markers[m.id] = marker;
        }
        marker.setLatLng([m.lat, m.lon]);
        marker.setIcon(icon(m.status));
        marker.bindPopup(popupContent(m));
        marker.bindTooltip(m.label, { direction: 'top', offset: [0, -8] });
    });
    Object.keys(s.markers).forEach(id => {
        if (!seen.has(Number(id))) {
            s.markerLayer.removeLayer(s.markers[id]);
            delete s.markers[id];
        }
    });
    // Fit μόνο την πρώτη φορά (ή όταν ζητηθεί): το polling δεν πρέπει να "πηδάει" τον χάρτη.
    if ((fit || !s.fitted) && markers.length > 0) {
        const bounds = L.latLngBounds(markers.map(m => [m.lat, m.lon]));
        s.map.fitBounds(bounds, { padding: [30, 30], maxZoom: 15 });
        s.fitted = true;
    }
}

export function focus(elementId, id) {
    const s = maps[elementId];
    if (!s || !s.markers[id]) return;
    const marker = s.markers[id];
    s.map.setView(marker.getLatLng(), Math.max(s.map.getZoom(), 14));
    marker.openPopup();
}

// points: [[lat, lon], ...], stops: [{ lat, lon, label }]
export function drawRoute(elementId, points, startLabel, endLabel, stops) {
    const s = maps[elementId];
    if (!s) return;
    s.routeLayer.clearLayers();
    if (!points || points.length === 0) return;
    const line = L.polyline(points, { color: '#0d6efd', weight: 4, opacity: 0.8 }).addTo(s.routeLayer);
    const flag = (latlng, color, text) => L.circleMarker(latlng, {
        radius: 8, color: '#fff', weight: 2, fillColor: color, fillOpacity: 1
    }).bindTooltip(text).addTo(s.routeLayer);
    flag(points[0], '#28a745', startLabel);
    flag(points[points.length - 1], '#dc3545', endLabel);
    (stops || []).forEach(st => L.circleMarker([st.lat, st.lon], {
        radius: 6, color: '#fff', weight: 2, fillColor: '#ffc107', fillOpacity: 1
    }).bindTooltip(st.label).addTo(s.routeLayer));
    s.map.fitBounds(line.getBounds(), { padding: [30, 30], maxZoom: 16 });
}

export function clearRoute(elementId) {
    const s = maps[elementId];
    if (s) s.routeLayer.clearLayers();
}

// Μετά από αλλαγή καρτέλας/μεγέθους το Leaflet πρέπει να ξαναμετρήσει το div.
export function invalidate(elementId) {
    const s = maps[elementId];
    if (s) setTimeout(() => s.map.invalidateSize(), 50);
}

export function dispose(elementId) {
    const s = maps[elementId];
    if (!s) return;
    s.map.remove();
    delete maps[elementId];
}
