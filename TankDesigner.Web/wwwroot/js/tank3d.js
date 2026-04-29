const viewers = new WeakMap();

function renderTank3D(container, tank) {
    if (!container) return;

    container.innerHTML = "";

    if (!window.THREE) {
        showError(container, "Three.js no está cargado. Revisa App.razor.");
        return;
    }

    if (!tank || !Array.isArray(tank.anillos) || tank.anillos.length === 0) {
        showError(container, "No hay anillos válidos para generar el modelo 3D.");
        return;
    }

    const rings = tank.anillos.filter(r => Number(r.altura) > 0);

    const realDiameter = Number(tank.diametro) || 1;
    const realHeight = Number(tank.alturaTotal) || rings.reduce((s, r) => s + Number(r.altura || 0), 0);

    const maxRealSize = Math.max(realDiameter, realHeight, 1);
    const targetModelSize = 42;
    const scale = targetModelSize / maxRealSize;
    const metersPerUnit = 1 / scale;

    const viewer = createViewer(container, scale, metersPerUnit, tank);
    viewers.set(container, viewer);

    buildTank(viewer, tank, rings, scale);
    fitCamera(viewer);

    viewer.renderer.render(viewer.scene, viewer.camera);
}

function createViewer(container, scale, metersPerUnit, tank) {
    const shell = document.createElement("div");
    shell.style.position = "relative";
    shell.style.width = "100%";
    shell.style.height = "100%";
    shell.style.minHeight = "560px";
    shell.style.borderRadius = "24px";
    shell.style.overflow = "hidden";
    container.appendChild(shell);

    const scaleBadge = document.createElement("div");
    scaleBadge.innerHTML = `
        <strong>Escala automática</strong><br>
        1 unidad 3D = ${metersPerUnit.toFixed(2)} m<br>
        Techo: ${normalizarTecho(tank.techo).label}
    `;
    scaleBadge.style.position = "absolute";
    scaleBadge.style.left = "18px";
    scaleBadge.style.bottom = "18px";
    scaleBadge.style.zIndex = "5";
    scaleBadge.style.padding = "12px 14px";
    scaleBadge.style.borderRadius = "16px";
    scaleBadge.style.background = "rgba(15,23,42,0.88)";
    scaleBadge.style.color = "#ffffff";
    scaleBadge.style.font = "13px Arial";
    scaleBadge.style.lineHeight = "1.45";
    shell.appendChild(scaleBadge);

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0xf8fafc);

    const camera = new THREE.PerspectiveCamera(42, 1, 0.01, 10000);

    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    shell.appendChild(renderer.domElement);

    const group = new THREE.Group();
    scene.add(group);

    scene.add(new THREE.AmbientLight(0xffffff, 0.76));

    const key = new THREE.DirectionalLight(0xffffff, 1.15);
    key.position.set(28, 40, 26);
    key.castShadow = true;
    scene.add(key);

    const fill = new THREE.DirectionalLight(0xffffff, 0.45);
    fill.position.set(-30, 18, -26);
    scene.add(fill);

    const viewer = {
        container,
        shell,
        scene,
        camera,
        renderer,
        group,
        yaw: 0.85,
        pitch: 0.42,
        distance: 72,
        target: new THREE.Vector3(0, 0, 0),
        isDragging: false,
        lastX: 0,
        lastY: 0
    };

    bindControls(viewer);
    resize(viewer);

    const resizeObserver = new ResizeObserver(() => resize(viewer));
    resizeObserver.observe(container);

    animate(viewer);
    return viewer;
}

function buildTank(viewer, tank, rings, scale) {
    const diameter = (Number(tank.diametro) || 1) * scale;
    const radius = diameter / 2;

    let currentY = 0;

    rings.forEach((ring) => {
        const height = Number(ring.altura) * scale;
        const materialName = ring.material || tank.materialPrincipal || "material";
        const color = colorForMaterial(materialName);

        const shellGeometry = new THREE.CylinderGeometry(radius, radius, height, 160, 1, true);
        const shellMaterial = new THREE.MeshStandardMaterial({
            color,
            metalness: 0.72,
            roughness: 0.30,
            transparent: true,
            opacity: 0.88,
            side: THREE.DoubleSide
        });

        const shell = new THREE.Mesh(shellGeometry, shellMaterial);
        shell.position.y = currentY + height / 2;
        shell.castShadow = true;
        shell.receiveShadow = true;
        viewer.group.add(shell);

        const edgeGeometry = new THREE.EdgesGeometry(shellGeometry, 18);
        const edges = new THREE.LineSegments(
            edgeGeometry,
            new THREE.LineBasicMaterial({
                color: 0x0f172a,
                transparent: true,
                opacity: 0.22
            })
        );

        edges.position.copy(shell.position);
        viewer.group.add(edges);

        addRingSeam(viewer.group, radius, currentY);
        addRingSeam(viewer.group, radius, currentY + height);

        currentY += height;
    });

    addBottomDisc(viewer.group, radius);
    addTopStiffener(viewer.group, radius, currentY);
    addRoof(viewer.group, radius, currentY, tank.techo);
    addReferenceGrid(viewer.group, radius, currentY);
    addVerticalReference(viewer.group, radius, currentY);

    viewer.group.position.y = -currentY / 2;
    viewer.modelRadius = radius;
    viewer.modelHeight = currentY;
}

function addRoof(group, radius, height, roofRaw) {
    const roof = normalizarTecho(roofRaw);

    if (roof.type === "none") {
        addOpenTop(group, radius, height);
        return;
    }

    if (roof.type === "dome") {
        addDomeRoof(group, radius, height);
        return;
    }

    if (roof.type === "cone") {
        addConeRoof(group, radius, height);
        return;
    }

    addFlatRoof(group, radius, height);
}

function normalizarTecho(value) {
    const text = String(value || "None").trim();
    const t = text.toUpperCase();

    if (!t || t === "—" || t.includes("NONE") || t.includes("SIN") || t.includes("ABIERTO")) {
        return { type: "none", label: "Sin techo / abierto" };
    }

    if (t.includes("DOME") || t.includes("DOMO") || t.includes("CUPULA") || t.includes("CÚPULA")) {
        return { type: "dome", label: text };
    }

    if (t.includes("CONE") || t.includes("CONIC") || t.includes("CÓNIC") || t.includes("CONO")) {
        return { type: "cone", label: text };
    }

    if (t.includes("FLAT") || t.includes("PLANO")) {
        return { type: "flat", label: text };
    }

    return { type: "flat", label: text };
}

function addOpenTop(group, radius, height) {
    const geometry = new THREE.TorusGeometry(radius, Math.max(radius * 0.014, 0.035), 12, 180);
    const material = new THREE.MeshStandardMaterial({
        color: 0x0f172a,
        metalness: 0.58,
        roughness: 0.28
    });

    const torus = new THREE.Mesh(geometry, material);
    torus.rotation.x = Math.PI / 2;
    torus.position.y = height;

    group.add(torus);
}

function addFlatRoof(group, radius, height) {
    const geometry = new THREE.CircleGeometry(radius * 0.985, 160);
    const material = new THREE.MeshStandardMaterial({
        color: 0xcbd5e1,
        metalness: 0.62,
        roughness: 0.34,
        side: THREE.DoubleSide
    });

    const roof = new THREE.Mesh(geometry, material);
    roof.rotation.x = -Math.PI / 2;
    roof.position.y = height + radius * 0.015;
    roof.castShadow = true;
    roof.receiveShadow = true;

    group.add(roof);
    addOpenTop(group, radius, height + radius * 0.015);
}

function addConeRoof(group, radius, height) {
    const roofHeight = Math.max(radius * 0.16, 1.2);

    const geometry = new THREE.ConeGeometry(radius * 1.01, roofHeight, 160, 1, false);
    const material = new THREE.MeshStandardMaterial({
        color: 0xcbd5e1,
        metalness: 0.66,
        roughness: 0.30,
        transparent: true,
        opacity: 0.94,
        side: THREE.DoubleSide
    });

    const cone = new THREE.Mesh(geometry, material);
    cone.position.y = height + roofHeight / 2;
    cone.castShadow = true;
    cone.receiveShadow = true;

    group.add(cone);
    addOpenTop(group, radius, height);
}

function addDomeRoof(group, radius, height) {
    const geometry = new THREE.SphereGeometry(radius * 1.01, 160, 32, 0, Math.PI * 2, 0, Math.PI / 2);
    const material = new THREE.MeshStandardMaterial({
        color: 0xcbd5e1,
        metalness: 0.62,
        roughness: 0.28,
        transparent: true,
        opacity: 0.94,
        side: THREE.DoubleSide
    });

    const dome = new THREE.Mesh(geometry, material);
    dome.position.y = height;
    dome.castShadow = true;
    dome.receiveShadow = true;

    group.add(dome);
    addOpenTop(group, radius, height);
}

function addTopStiffener(group, radius, height) {
    const geometry = new THREE.TorusGeometry(radius * 1.015, Math.max(radius * 0.018, 0.045), 14, 180);
    const material = new THREE.MeshStandardMaterial({
        color: 0x1e293b,
        metalness: 0.72,
        roughness: 0.28
    });

    const stiffener = new THREE.Mesh(geometry, material);
    stiffener.rotation.x = Math.PI / 2;
    stiffener.position.y = height;

    group.add(stiffener);
}

function addRingSeam(group, radius, y) {
    const curve = new THREE.EllipseCurve(0, 0, radius * 1.006, radius * 1.006, 0, Math.PI * 2, false, 0);
    const points = curve.getPoints(180).map(p => new THREE.Vector3(p.x, y, p.y));
    const geometry = new THREE.BufferGeometry().setFromPoints(points);

    group.add(new THREE.LineLoop(
        geometry,
        new THREE.LineBasicMaterial({
            color: 0x0f172a,
            transparent: true,
            opacity: 0.62
        })
    ));
}

function addBottomDisc(group, radius) {
    const geometry = new THREE.CircleGeometry(radius, 160);
    const material = new THREE.MeshStandardMaterial({
        color: 0xe2e8f0,
        metalness: 0.42,
        roughness: 0.38,
        side: THREE.DoubleSide
    });

    const disc = new THREE.Mesh(geometry, material);
    disc.rotation.x = -Math.PI / 2;
    disc.position.y = 0;
    disc.receiveShadow = true;
    group.add(disc);
}

function addReferenceGrid(group, radius, height) {
    const size = Math.max(radius * 3.2, height * 1.4, 20);
    const grid = new THREE.GridHelper(size, 20, 0x94a3b8, 0xcbd5e1);
    grid.position.y = -0.02;
    group.add(grid);
}

function addVerticalReference(group, radius, height) {
    const geometry = new THREE.BufferGeometry().setFromPoints([
        new THREE.Vector3(radius * 1.35, 0, 0),
        new THREE.Vector3(radius * 1.35, height, 0)
    ]);

    const material = new THREE.LineBasicMaterial({
        color: 0xef4444,
        transparent: true,
        opacity: 0.7
    });

    group.add(new THREE.Line(geometry, material));
}

function bindControls(viewer) {
    const canvas = viewer.renderer.domElement;
    canvas.style.cursor = "grab";

    canvas.addEventListener("pointerdown", e => {
        viewer.isDragging = true;
        viewer.lastX = e.clientX;
        viewer.lastY = e.clientY;
        canvas.setPointerCapture(e.pointerId);
        canvas.style.cursor = "grabbing";
    });

    canvas.addEventListener("pointerup", e => {
        viewer.isDragging = false;
        canvas.releasePointerCapture(e.pointerId);
        canvas.style.cursor = "grab";
    });

    canvas.addEventListener("pointermove", e => {
        if (!viewer.isDragging) return;

        const dx = e.clientX - viewer.lastX;
        const dy = e.clientY - viewer.lastY;

        viewer.lastX = e.clientX;
        viewer.lastY = e.clientY;

        viewer.yaw -= dx * 0.006;
        viewer.pitch -= dy * 0.006;
        viewer.pitch = Math.max(-1.1, Math.min(1.1, viewer.pitch));

        updateCamera(viewer);
    });

    canvas.addEventListener("wheel", e => {
        e.preventDefault();

        const factor = e.deltaY > 0 ? 1.08 : 0.92;
        viewer.distance = Math.max(18, Math.min(260, viewer.distance * factor));

        updateCamera(viewer);
    }, { passive: false });
}

function resize(viewer) {
    const rect = viewer.container.getBoundingClientRect();
    const width = Math.max(320, rect.width || 320);
    const height = Math.max(560, rect.height || 560);

    viewer.camera.aspect = width / height;
    viewer.camera.updateProjectionMatrix();
    viewer.renderer.setSize(width, height, false);
}

function animate(viewer) {
    requestAnimationFrame(() => animate(viewer));
    viewer.renderer.render(viewer.scene, viewer.camera);
}

function fitCamera(viewer) {
    const height = viewer.modelHeight || 40;
    const radius = viewer.modelRadius || 20;
    const maxSize = Math.max(height, radius * 2, 1);

    viewer.distance = maxSize * 2.25;
    viewer.target.set(0, 0, 0);
    updateCamera(viewer);
}

function updateCamera(viewer) {
    const x = viewer.distance * Math.cos(viewer.pitch) * Math.sin(viewer.yaw);
    const y = viewer.distance * Math.sin(viewer.pitch);
    const z = viewer.distance * Math.cos(viewer.pitch) * Math.cos(viewer.yaw);

    viewer.camera.position.set(x, y, z);
    viewer.camera.lookAt(viewer.target);
    viewer.camera.near = 0.01;
    viewer.camera.far = 10000;
    viewer.camera.updateProjectionMatrix();
}

function colorForMaterial(name) {
    const normalized = String(name || "").toUpperCase();

    if (normalized.includes("HSLA")) return 0x2563eb;
    if (normalized.includes("S355")) return 0x0f766e;
    if (normalized.includes("S275")) return 0x7c3aed;
    if (normalized.includes("S235")) return 0x64748b;
    if (normalized.includes("GLASS") || normalized.includes("VITR")) return 0x0891b2;

    return 0x2563eb;
}

function showError(container, message) {
    container.innerHTML = `
        <div style="
            padding:18px;
            border-radius:18px;
            background:#fff7ed;
            color:#9a3412;
            border:1px solid #fed7aa;
            font-weight:700;">
            ${message}
        </div>
    `;
}

window.tank3d = {
    renderTank3D: renderTank3D
};