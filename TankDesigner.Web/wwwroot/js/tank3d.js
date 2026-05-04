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

    addScaleBadge(shell, metersPerUnit, tank);
    addRoofControls(shell, container, tank);

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0xf8fafc);

    const camera = new THREE.PerspectiveCamera(42, 1, 0.01, 10000);

    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    shell.appendChild(renderer.domElement);
    addDownloadPngButton(shell, renderer);
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
    renderer.__tank3dScene = scene;
    renderer.__tank3dCamera = camera;

    bindControls(viewer);
    resize(viewer);

    const resizeObserver = new ResizeObserver(() => resize(viewer));
    resizeObserver.observe(container);

    animate(viewer);
    return viewer;
}

function addScaleBadge(shell, metersPerUnit, tank) {
    const vigas = tank.vigasTechoConico && tank.vigasTechoConico.aplica === true
        ? `<br>Vigas radiales: <strong style="color:#fca5a5">${tank.vigasTechoConico.numeroVigas || 0}</strong>`
        : "";

    const escalera = tank.escalera && tank.escalera.tipo
        ? `<br>Escalera: <strong style="color:#fde047">${tank.escalera.tipo}</strong>`
        : "";

    const scaleBadge = document.createElement("div");
    scaleBadge.innerHTML = `
        <strong>Escala automática</strong><br>
        1 unidad 3D = ${metersPerUnit.toFixed(2)} m<br>
        Techo: ${normalizarTecho(tank.techo).label}
        ${vigas}
        ${escalera}
    `;
    scaleBadge.style.position = "absolute";
    scaleBadge.style.right = "18px";
    scaleBadge.style.bottom = "18px";
    scaleBadge.style.zIndex = "5";
    scaleBadge.style.padding = "12px 14px";
    scaleBadge.style.borderRadius = "16px";
    scaleBadge.style.background = "rgba(15,23,42,0.88)";
    scaleBadge.style.color = "#ffffff";
    scaleBadge.style.font = "13px Arial";
    scaleBadge.style.lineHeight = "1.45";
    shell.appendChild(scaleBadge);
}

function addRoofControls(shell, container, tank) {
    const roof = normalizarTecho(tank.techo);
    if (roof.type !== "cone") return;

    if (!tank.vigasTechoConico) {
        tank.vigasTechoConico = { aplica: true, numeroVigas: 16 };
    }

    const currentBeamCount = Math.max(0, Number(tank.vigasTechoConico.numeroVigas) || 0);
    const currentHubPercent = Math.round(Number(tank.vigasTechoConico.factorNucleo3D || 0.16) * 100);

    const panel = document.createElement("div");
    panel.style.position = "absolute";
    panel.style.left = "18px";
    panel.style.top = "18px";
    panel.style.zIndex = "6";
    panel.style.width = "210px";
    panel.style.padding = "14px";
    panel.style.borderRadius = "16px";
    panel.style.background = "rgba(15,23,42,0.90)";
    panel.style.color = "#ffffff";
    panel.style.font = "13px Arial";
    panel.style.boxShadow = "0 18px 45px rgba(15,23,42,0.25)";

    panel.innerHTML = `
        <div style="font-weight:700;margin-bottom:10px;">Techo cónico 3D</div>
        <label style="display:block;margin-bottom:6px;color:#cbd5e1;">Número de vigas</label>
        <input data-tank3d-beams type="number" min="0" max="160" step="1" value="${currentBeamCount}" style="width:100%;box-sizing:border-box;border-radius:10px;border:1px solid #334155;background:#0f172a;color:white;padding:8px 10px;margin-bottom:10px;">
        <label style="display:flex;justify-content:space-between;gap:8px;margin-bottom:6px;color:#cbd5e1;">
            <span>Núcleo central</span>
            <strong data-tank3d-hub-label style="color:#fde68a;">${currentHubPercent}%</strong>
        </label>
        <input data-tank3d-hub type="range" min="8" max="32" step="1" value="${currentHubPercent}" style="width:100%;">
        <div style="margin-top:8px;color:#94a3b8;font-size:12px;line-height:1.35;">
            El núcleo se calcula sobre el radio del tanque y varía con el tamaño del modelo.
        </div>
    `;

    const beamsInput = panel.querySelector("[data-tank3d-beams]");
    const hubInput = panel.querySelector("[data-tank3d-hub]");
    const hubLabel = panel.querySelector("[data-tank3d-hub-label]");

    const applyChanges = () => {
        const beams = Math.max(0, Math.min(160, Math.floor(Number(beamsInput.value) || 0)));
        const hubPercent = Math.max(8, Math.min(32, Number(hubInput.value) || 16));

        tank.vigasTechoConico.aplica = beams > 0;
        tank.vigasTechoConico.numeroVigas = beams;
        tank.vigasTechoConico.factorNucleo3D = hubPercent / 100;
        hubLabel.textContent = `${hubPercent}%`;

        renderTank3D(container, tank);
    };

    beamsInput.addEventListener("change", applyChanges);
    hubInput.addEventListener("input", () => {
        hubLabel.textContent = `${hubInput.value}%`;
    });
    hubInput.addEventListener("change", applyChanges);

    shell.appendChild(panel);
}
function addDownloadPngButton(shell, renderer) {
    const button = document.createElement("button");

    button.type = "button";
    button.textContent = "Descargar PNG";

    button.style.position = "absolute";
    button.style.right = "18px";
    button.style.top = "18px";
    button.style.zIndex = "8";
    button.style.border = "0";
    button.style.borderRadius = "14px";
    button.style.padding = "10px 14px";
    button.style.background = "#2563eb";
    button.style.color = "#ffffff";
    button.style.font = "700 13px Arial";
    button.style.cursor = "pointer";
    button.style.boxShadow = "0 12px 28px rgba(37,99,235,0.32)";

    button.addEventListener("click", () => {
        renderer.render(renderer.__tank3dScene, renderer.__tank3dCamera);

        const link = document.createElement("a");
        link.download = `tank-3d-${new Date().toISOString().slice(0, 10)}.png`;
        link.href = renderer.domElement.toDataURL("image/png");
        link.click();
    });

    shell.appendChild(button);
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
    addRoof(viewer.group, radius, currentY, tank.techo, tank.vigasTechoConico, scale);
    addTankConnections(viewer.group, radius, currentY);
    addManhole(viewer.group, radius, currentY);
    addRoofVent(viewer.group, radius, currentY, tank.techo);
    addReferenceGrid(viewer.group, radius, currentY);
    addVerticalReference(viewer.group, radius, currentY);
    addLadder(viewer.group, radius, currentY, tank.escalera);

    viewer.group.position.y = -currentY / 2;
    viewer.modelRadius = radius;
    viewer.modelHeight = currentY;
}
function addRoof(group, radius, height, roofRaw, vigasTechoConico, scale) {
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
        addConeRoof(group, radius, height, vigasTechoConico, scale);
        return;
    }

    addFlatRoof(group, radius, height);
}
function addManhole(group, radius, height) {
    const angle = Math.PI * 1.82;
    const y = height * 0.22;

    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));
    const vertical = new THREE.Vector3(0, 1, 0);

    const manholeRadius = Math.max(radius * 0.105, 0.42);
    const coverThickness = Math.max(radius * 0.018, 0.06);
    const boltRadius = Math.max(radius * 0.006, 0.022);

    const materialCover = new THREE.MeshStandardMaterial({
        color: 0x94a3b8,
        metalness: 0.72,
        roughness: 0.26
    });

    const materialFrame = new THREE.MeshStandardMaterial({
        color: 0x475569,
        metalness: 0.78,
        roughness: 0.24
    });

    const center = radial.clone().multiplyScalar(radius + coverThickness * 0.7);
    center.y = y;

    const quaternion = new THREE.Quaternion();
    quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), radial.clone().normalize());

    const cover = new THREE.Mesh(
        new THREE.CylinderGeometry(manholeRadius, manholeRadius, coverThickness, 54),
        materialCover
    );

    cover.position.copy(center);
    cover.quaternion.copy(quaternion);
    cover.castShadow = true;
    cover.receiveShadow = true;
    group.add(cover);

    const frame = new THREE.Mesh(
        new THREE.TorusGeometry(manholeRadius * 1.08, Math.max(radius * 0.010, 0.035), 12, 64),
        materialFrame
    );

    frame.position.copy(center.clone().add(radial.clone().multiplyScalar(coverThickness * 0.9)));
    frame.quaternion.copy(quaternion);
    frame.castShadow = true;
    frame.receiveShadow = true;
    group.add(frame);

    const boltCount = 16;

    for (let i = 0; i < boltCount; i++) {
        const a = (Math.PI * 2 * i) / boltCount;

        const boltPos = center.clone()
            .add(tangent.clone().multiplyScalar(Math.cos(a) * manholeRadius * 0.86))
            .add(vertical.clone().multiplyScalar(Math.sin(a) * manholeRadius * 0.86))
            .add(radial.clone().multiplyScalar(coverThickness));

        const bolt = new THREE.Mesh(
            new THREE.CylinderGeometry(boltRadius, boltRadius, coverThickness * 1.35, 8),
            materialFrame
        );

        bolt.position.copy(boltPos);
        bolt.quaternion.copy(quaternion);
        bolt.castShadow = true;
        bolt.receiveShadow = true;
        group.add(bolt);
    }
}

function addRoofVent(group, radius, height, roofRaw) {
    const roof = normalizarTecho(roofRaw);
    if (roof.type === "none") return;

    const ventRadius = Math.max(radius * 0.045, 0.22);
    const ventHeight = Math.max(radius * 0.10, 0.45);

    const material = new THREE.MeshStandardMaterial({
        color: 0x64748b,
        metalness: 0.72,
        roughness: 0.26
    });

    const vent = new THREE.Mesh(
        new THREE.CylinderGeometry(ventRadius, ventRadius, ventHeight, 32),
        material
    );

    vent.position.set(radius * 0.32, height + ventHeight * 0.65, -radius * 0.18);
    vent.castShadow = true;
    vent.receiveShadow = true;
    group.add(vent);

    const cap = new THREE.Mesh(
        new THREE.CylinderGeometry(ventRadius * 1.45, ventRadius * 1.45, ventHeight * 0.18, 32),
        material
    );

    cap.position.set(vent.position.x, vent.position.y + ventHeight * 0.55, vent.position.z);
    cap.castShadow = true;
    cap.receiveShadow = true;
    group.add(cap);
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

function addConeRoof(group, radius, height, vigasTechoConico, scale) {
    const alturaConoReal = vigasTechoConico && Number(vigasTechoConico.alturaCono) > 0
        ? Number(vigasTechoConico.alturaCono)
        : 0;

    const roofHeight = alturaConoReal > 0
        ? alturaConoReal * scale
        : Math.max(radius * 0.16, 1.2);

    const geometry = new THREE.ConeGeometry(radius * 1.01, roofHeight, 160, 1, false);
    const material = new THREE.MeshStandardMaterial({
        color: 0xe5e7eb,
        metalness: 0.52,
        roughness: 0.38,
        transparent: true,
        opacity: 0.86,
        side: THREE.DoubleSide
    });

    const cone = new THREE.Mesh(geometry, material);
    cone.position.y = height + roofHeight / 2;
    cone.castShadow = true;
    cone.receiveShadow = true;

    group.add(cone);

    const numeroVigas = vigasTechoConico && vigasTechoConico.aplica === true
        ? Number(vigasTechoConico.numeroVigas) || 0
        : 0;

    addOpenTop(group, radius, height);
    addConeRoofPanels(group, radius, height, roofHeight);
    addConeRoofRafters(group, radius, height, roofHeight, vigasTechoConico, scale);
    addConeRoofCenterHub(group, height + roofHeight, radius, numeroVigas, vigasTechoConico, scale);
}

function addConeRoofPanels(group, radius, baseHeight, roofHeight) {
    const material = new THREE.LineBasicMaterial({
        color: 0x94a3b8,
        transparent: true,
        opacity: 0.35
    });

    [0.33, 0.66].forEach(f => {
        const ringRadius = radius * f;
        const y = baseHeight + roofHeight * (1 - f);

        const curve = new THREE.EllipseCurve(0, 0, ringRadius, ringRadius, 0, Math.PI * 2, false, 0);
        const points = curve.getPoints(180).map(p => new THREE.Vector3(p.x, y, p.y));
        const geometry = new THREE.BufferGeometry().setFromPoints(points);

        group.add(new THREE.LineLoop(geometry, material));
    });
}

function addConeRoofRafters(group, radius, baseHeight, roofHeight, vigasTechoConico, scale) {
    if (!vigasTechoConico || vigasTechoConico.aplica !== true) return;

    const numeroVigas = Math.max(0, Number(vigasTechoConico.numeroVigas) || 0);
    if (numeroVigas <= 0) return;

    const beamMaterial = new THREE.MeshStandardMaterial({
        color: 0xb91c1c,
        emissive: new THREE.Color(0x450a0a),
        emissiveIntensity: 0.12,
        metalness: 0.78,
        roughness: 0.24
    });

    const hubRadius = calcularRadioNucleoTecho(radius, numeroVigas, vigasTechoConico, scale);
    const startRadius = hubRadius * 1.05;
    const endRadius = radius * 0.965;
    const beamRadius = Math.max(radius * 0.0075, 0.04);
    const offsetY = Math.max(radius * 0.016, 0.06);

    for (let i = 0; i < numeroVigas; i++) {
        const angle = (Math.PI * 2 * i) / numeroVigas;

        const x1 = Math.cos(angle) * startRadius;
        const z1 = Math.sin(angle) * startRadius;
        const y1 = baseHeight + roofHeight * (1 - startRadius / radius) + offsetY;

        const x2 = Math.cos(angle) * endRadius;
        const z2 = Math.sin(angle) * endRadius;
        const y2 = baseHeight + roofHeight * (1 - endRadius / radius) + offsetY;

        addCylinderBetween(
            group,
            new THREE.Vector3(x1, y1, z1),
            new THREE.Vector3(x2, y2, z2),
            beamRadius,
            beamMaterial,
            16
        );
    }
}

function addConeRoofCenterHub(group, y, radius, numeroVigas, vigasTechoConico, scale) {
    const hubRadius = calcularRadioNucleoTecho(radius, numeroVigas, vigasTechoConico, scale);
    const hubHeight = Math.max(radius * 0.035, 0.18);

    const geometry = new THREE.CylinderGeometry(hubRadius, hubRadius, hubHeight, 72);
    const material = new THREE.MeshStandardMaterial({
        color: 0x5f0f0f,
        emissive: new THREE.Color(0x450a0a),
        emissiveIntensity: 0.15,
        metalness: 0.82,
        roughness: 0.2
    });

    const hub = new THREE.Mesh(geometry, material);
    hub.position.y = y + hubHeight / 2;
    hub.castShadow = true;
    hub.receiveShadow = true;

    group.add(hub);
}

function calcularRadioNucleoTecho(radius, numeroVigas, vigasTechoConico, scale) {
    const manualDiameter = Number(vigasTechoConico?.diametroNucleoCentralManual)
        || Number(vigasTechoConico?.diametroNucleoTechoConicoManual)
        || Number(vigasTechoConico?.diametroNucleoCentral)
        || Number(vigasTechoConico?.diametroNucleo)
        || 0;

    if (manualDiameter > 0 && scale > 0) {
        return Math.max(manualDiameter * scale / 2, radius * 0.06, 0.35);
    }

    const factorUsuario = Number(vigasTechoConico?.factorNucleo3D) || 0;
    const factorPorTamano = radius < 6 ? 0.20 : radius < 12 ? 0.17 : 0.145;
    const factorPorVigas = Math.min(0.10, Math.max(0, numeroVigas) * 0.0025);
    const factorFinal = factorUsuario > 0 ? factorUsuario : factorPorTamano + factorPorVigas;

    return Math.max(radius * factorFinal, radius * 0.08, 0.42);
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

    group.add(new THREE.Line(
        geometry,
        new THREE.LineBasicMaterial({
            color: 0xef4444,
            transparent: true,
            opacity: 0.7
        })
    ));
}

function addLadder(group, radius, height, escalera) {
    const tipo = String(escalera?.tipo || "").toUpperCase();
    const numero = Number(escalera?.numeroEscaleras) || 0;

    if (numero <= 0) return;
    if (!tipo || tipo.includes("SIN") || tipo.includes("NONE")) return;

    const cantidad = Math.max(1, Math.floor(numero));

    for (let i = 0; i < cantidad; i++) {
        const angleOffset = (Math.PI * 2 * i) / cantidad;

        if (tipo.includes("HELICOIDAL")) {
            addHelicalStair(group, radius, height, angleOffset);
            continue;
        }

        if (tipo.includes("VERTICAL")) {
            addVerticalLadder(group, radius, height, angleOffset);
        }
    }
}

function addVerticalLadder(group, radius, height, angleOffset = 0) {
    const ladderMaterial = new THREE.MeshStandardMaterial({
        color: 0xff7a18,
        emissive: new THREE.Color(0x7c2d12),
        emissiveIntensity: 0.18,
        metalness: 0.58,
        roughness: 0.22
    });

    const cageMaterial = new THREE.MeshStandardMaterial({
        color: 0xe2e8f0,
        metalness: 0.7,
        roughness: 0.22
    });

    const platformMaterial = new THREE.MeshStandardMaterial({
        color: 0x334155,
        metalness: 0.72,
        roughness: 0.26
    });

    const angle = -Math.PI / 4 + angleOffset;
    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));

    const ladderGap = Math.max(radius * 0.004, 0.025);
    const ladderRadius = radius + ladderGap;

    const railHalfWidth = Math.max(radius * 0.036, 0.36);
    const railRadius = Math.max(radius * 0.0062, 0.032);
    const rungRadius = Math.max(radius * 0.0052, 0.027);

    const bottomY = height * 0.012;
    const topY = height * 1.04;

    const centerBase = radial.clone().multiplyScalar(ladderRadius);

    const rail1Bottom = centerBase.clone().add(tangent.clone().multiplyScalar(-railHalfWidth));
    rail1Bottom.y = bottomY;

    const rail1Top = centerBase.clone().add(tangent.clone().multiplyScalar(-railHalfWidth));
    rail1Top.y = topY;

    const rail2Bottom = centerBase.clone().add(tangent.clone().multiplyScalar(railHalfWidth));
    rail2Bottom.y = bottomY;

    const rail2Top = centerBase.clone().add(tangent.clone().multiplyScalar(railHalfWidth));
    rail2Top.y = topY;

    addCylinderBetween(group, rail1Bottom, rail1Top, railRadius, ladderMaterial, 16);
    addCylinderBetween(group, rail2Bottom, rail2Top, railRadius, ladderMaterial, 16);

    const rungSpacing = Math.max(radius * 0.048, 0.22);
    const rungCount = Math.max(16, Math.floor((topY - bottomY) / rungSpacing));

    for (let i = 1; i < rungCount; i++) {
        const y = bottomY + ((topY - bottomY) * i) / rungCount;

        const left = centerBase.clone().add(tangent.clone().multiplyScalar(-railHalfWidth));
        left.y = y;

        const right = centerBase.clone().add(tangent.clone().multiplyScalar(railHalfWidth));
        right.y = y;

        addCylinderBetween(group, left, right, rungRadius, ladderMaterial, 12);
    }

    addVerticalLadderCage(group, radius, height, radial, tangent, centerBase, cageMaterial);
    addVerticalLadderPlatform(group, radius, height, radial, tangent, platformMaterial, cageMaterial);
    addLadderTankBrackets(group, radius, height, radial, tangent, centerBase, cageMaterial);
}

function addVerticalLadderCage(group, radius, height, radial, tangent, centerBase, cageMaterial) {
    const cageRadius = Math.max(radius * 0.075, 0.58);
    const cageTubeRadius = Math.max(radius * 0.0048, 0.024);
    const cageCenter = centerBase.clone().add(radial.clone().multiplyScalar(cageRadius * 0.62));

    const cageStartY = Math.min(height * 0.18, 1.65);
    const cageEndY = height * 1.035;
    const cageHeight = Math.max(cageEndY - cageStartY, 0.1);

    const ringSpacing = Math.max(radius * 0.075, 0.58);
    const ringCount = Math.max(7, Math.floor(cageHeight / ringSpacing));

    for (let i = 0; i <= ringCount; i++) {
        const y = cageStartY + (cageHeight * i) / ringCount;
        addCageRing(group, cageCenter, radial, tangent, cageRadius, y, cageTubeRadius, cageMaterial);
    }

    const barCount = 9;

    for (let i = 0; i < barCount; i++) {
        const a = -Math.PI * 0.76 + (Math.PI * 1.52 * i) / (barCount - 1);

        const offset = radial.clone().multiplyScalar(Math.cos(a) * cageRadius)
            .add(tangent.clone().multiplyScalar(Math.sin(a) * cageRadius));

        const bottom = cageCenter.clone().add(offset);
        bottom.y = cageStartY;

        const top = cageCenter.clone().add(offset);
        top.y = cageEndY;

        addCylinderBetween(group, bottom, top, cageTubeRadius, cageMaterial, 10);
    }
}

function addCageRing(group, cageCenter, radial, tangent, cageRadius, y, tubeRadius, material) {
    const points = [];
    const segments = 42;

    for (let i = 0; i <= segments; i++) {
        const a = -Math.PI * 0.82 + (Math.PI * 1.64 * i) / segments;

        const point = cageCenter.clone()
            .add(radial.clone().multiplyScalar(Math.cos(a) * cageRadius))
            .add(tangent.clone().multiplyScalar(Math.sin(a) * cageRadius));

        point.y = y;
        points.push(point);
    }

    for (let i = 0; i < points.length - 1; i++) {
        addCylinderBetween(group, points[i], points[i + 1], tubeRadius, material, 10);
    }
}

function addVerticalLadderPlatform(group, radius, height, radial, tangent, platformMaterial, railMaterial) {
    const platformWidth = Math.max(radius * 0.22, 1.35);
    const platformDepth = Math.max(radius * 0.16, 1.05);
    const platformThickness = Math.max(radius * 0.011, 0.06);

    const center = radial.clone().multiplyScalar(radius + platformDepth * 0.40);
    center.y = height + platformThickness * 1.5;

    const platform = new THREE.Mesh(
        new THREE.BoxGeometry(platformWidth, platformThickness, platformDepth),
        platformMaterial
    );

    platform.position.copy(center);
    platform.rotation.y = -Math.atan2(radial.z, radial.x) + Math.PI / 2;
    platform.castShadow = true;
    platform.receiveShadow = true;
    group.add(platform);

    const railHeight = Math.max(radius * 0.075, 0.72);
    const railRadius = Math.max(radius * 0.0048, 0.026);

    const p1 = center.clone().add(tangent.clone().multiplyScalar(-platformWidth / 2)).add(radial.clone().multiplyScalar(platformDepth / 2));
    const p2 = center.clone().add(tangent.clone().multiplyScalar(platformWidth / 2)).add(radial.clone().multiplyScalar(platformDepth / 2));
    const p3 = center.clone().add(tangent.clone().multiplyScalar(-platformWidth / 2)).add(radial.clone().multiplyScalar(-platformDepth / 2));
    const p4 = center.clone().add(tangent.clone().multiplyScalar(platformWidth / 2)).add(radial.clone().multiplyScalar(-platformDepth / 2));

    const posts = [p1, p2, p3, p4];
    const tops = [];

    posts.forEach(p => {
        const bottom = p.clone();
        bottom.y = height + platformThickness * 2;

        const top = p.clone();
        top.y = bottom.y + railHeight;
        tops.push(top);

        addCylinderBetween(group, bottom, top, railRadius, railMaterial, 10);
    });

    addCylinderBetween(group, tops[0], tops[1], railRadius, railMaterial, 10);
    addCylinderBetween(group, tops[0], tops[2], railRadius, railMaterial, 10);
    addCylinderBetween(group, tops[1], tops[3], railRadius, railMaterial, 10);
}

function addLadderTankBrackets(group, radius, height, radial, tangent, centerBase, material) {
    const bracketRadius = Math.max(radius * 0.0042, 0.02);
    const bracketWidth = Math.max(radius * 0.030, 0.30);
    const bracketCount = Math.max(6, Math.floor(height / Math.max(radius * 0.18, 1.0)));

    for (let i = 0; i <= bracketCount; i++) {
        const y = (height * i) / bracketCount;
        const wallPoint = radial.clone().multiplyScalar(radius * 1.002);
        wallPoint.y = y;

        const ladderPoint = centerBase.clone().add(radial.clone().multiplyScalar(-bracketWidth * 0.25));
        ladderPoint.y = y;

        addCylinderBetween(group, wallPoint, ladderPoint, bracketRadius, material, 10);
    }
}

function addHelicalStair(group, radius, height, angleOffset = 0) {
    const stepMaterial = new THREE.MeshStandardMaterial({
        color: 0xff7a18,
        emissive: new THREE.Color(0x7c2d12),
        emissiveIntensity: 0.14,
        metalness: 0.6,
        roughness: 0.22
    });

    const railMaterial = new THREE.MeshStandardMaterial({
        color: 0xe2e8f0,
        metalness: 0.68,
        roughness: 0.23
    });

    const supportMaterial = new THREE.MeshStandardMaterial({
        color: 0x334155,
        metalness: 0.72,
        roughness: 0.24
    });

    const stairRadius = radius + Math.max(radius * 0.012, 0.08);
    const outerRadius = stairRadius + Math.max(radius * 0.090, 0.58);
    const innerRadius = stairRadius - Math.max(radius * 0.055, 0.34);
    const midRadius = (outerRadius + innerRadius) / 2;

    const turns = Math.max(1.45, height / Math.max(radius * 1.08, 1));
    const steps = Math.max(56, Math.floor(turns * 62));

    const stepWidth = Math.max(radius * 0.155, 0.90);
    const stepDepth = Math.max(radius * 0.060, 0.30);
    const stepHeight = Math.max(radius * 0.012, 0.055);

    const railHeight = Math.max(radius * 0.090, 0.82);
    const midRailHeight = railHeight * 0.52;
    const railRadius = Math.max(radius * 0.0052, 0.028);
    const postRadius = Math.max(radius * 0.0058, 0.03);
    const stringerRadius = Math.max(radius * 0.0065, 0.035);

    const outerRail = [];
    const innerRail = [];
    const outerMidRail = [];
    const lowerStringer = [];
    const innerStringer = [];

    for (let i = 0; i < steps; i++) {
        const t = i / (steps - 1);
        const angle = -Math.PI / 2 + angleOffset + t * turns * Math.PI * 2;
        const y = t * height;

        const step = new THREE.Mesh(
            new THREE.BoxGeometry(stepWidth, stepHeight, stepDepth),
            stepMaterial
        );

        step.position.set(
            Math.cos(angle) * midRadius,
            y,
            Math.sin(angle) * midRadius
        );

        step.rotation.y = -angle;
        step.castShadow = true;
        step.receiveShadow = true;
        group.add(step);

        const outerBase = new THREE.Vector3(
            Math.cos(angle) * outerRadius,
            y,
            Math.sin(angle) * outerRadius
        );

        const innerBase = new THREE.Vector3(
            Math.cos(angle) * innerRadius,
            y,
            Math.sin(angle) * innerRadius
        );

        const outerTop = outerBase.clone();
        outerTop.y += railHeight;

        const outerMid = outerBase.clone();
        outerMid.y += midRailHeight;

        const innerTop = innerBase.clone();
        innerTop.y += railHeight * 0.92;

        const stringerOuter = outerBase.clone();
        stringerOuter.y -= stepHeight * 1.2;

        const stringerInner = innerBase.clone();
        stringerInner.y -= stepHeight * 1.2;

        outerRail.push(outerTop);
        innerRail.push(innerTop);
        outerMidRail.push(outerMid);
        lowerStringer.push(stringerOuter);
        innerStringer.push(stringerInner);

        if (i % 3 === 0 || i === steps - 1) {
            addCylinderBetween(group, outerBase, outerTop, postRadius, railMaterial, 10);
            addCylinderBetween(group, innerBase, innerTop, postRadius * 0.85, railMaterial, 10);
        }

        if (i % 4 === 0) {
            const wallPoint = new THREE.Vector3(
                Math.cos(angle) * radius * 1.002,
                y,
                Math.sin(angle) * radius * 1.002
            );

            const stairPoint = new THREE.Vector3(
                Math.cos(angle) * innerRadius,
                y,
                Math.sin(angle) * innerRadius
            );

            addCylinderBetween(group, wallPoint, stairPoint, postRadius * 0.75, supportMaterial, 8);
        }
    }

    connectPath(group, outerRail, railRadius, railMaterial, 10);
    connectPath(group, innerRail, railRadius, railMaterial, 10);
    connectPath(group, outerMidRail, railRadius * 0.8, railMaterial, 8);
    connectPath(group, lowerStringer, stringerRadius, supportMaterial, 10);
    connectPath(group, innerStringer, stringerRadius * 0.85, supportMaterial, 10);

    const finalAngle = -Math.PI / 2 + angleOffset + turns * Math.PI * 2;
    addHelicalTopPlatform(group, radius, height, finalAngle, supportMaterial, railMaterial);
}

function connectPath(group, points, radius, material, segments) {
    for (let i = 0; i < points.length - 1; i++) {
        addCylinderBetween(group, points[i], points[i + 1], radius, material, segments);
    }
}

function addTankConnections(group, radius, height) {
    addNozzle(group, radius, height, {
        angle: Math.PI * 1.18,
        y: height * 0.12,
        size: 0.75,
        label: "drain"
    });

    addNozzle(group, radius, height, {
        angle: Math.PI * 1.32,
        y: height * 0.34,
        size: 1.0,
        label: "outlet"
    });

    addNozzle(group, radius, height, {
        angle: Math.PI * 1.46,
        y: height * 0.72,
        size: 0.8,
        label: "inlet"
    });

    addNozzle(group, radius, height, {
        angle: Math.PI * 1.62,
        y: height * 0.88,
        size: 0.55,
        label: "overflow"
    });
}

function addNozzle(group, radius, height, options) {
    const angle = options.angle;
    const y = options.y;
    const size = options.size || 1;

    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));

    const nozzleLength = Math.max(radius * 0.18 * size, 0.42);
    const nozzleRadius = Math.max(radius * 0.045 * size, 0.18);

    const flangeRadius = nozzleRadius * 1.65;
    const flangeThickness = Math.max(nozzleRadius * 0.32, 0.08);
    const boltRadius = Math.max(nozzleRadius * 0.08, 0.025);

    const materialNozzle = new THREE.MeshStandardMaterial({
        color: 0xb6beca,
        metalness: 0.76,
        roughness: 0.24
    });

    const materialFlange = new THREE.MeshStandardMaterial({
        color: 0x475569,
        metalness: 0.82,
        roughness: 0.22
    });

    const materialBolt = new THREE.MeshStandardMaterial({
        color: 0x111827,
        metalness: 0.75,
        roughness: 0.25
    });

    const base = radial.clone().multiplyScalar(radius * 1.01);
    base.y = y;

    const end = radial.clone().multiplyScalar(radius + nozzleLength);
    end.y = y;

    addCylinderBetween(group, base, end, nozzleRadius, materialNozzle, 32);

    const flangeCenter = radial.clone().multiplyScalar(radius + nozzleLength + flangeThickness * 0.25);
    flangeCenter.y = y;

    const flange = new THREE.Mesh(
        new THREE.CylinderGeometry(flangeRadius, flangeRadius, flangeThickness, 48),
        materialFlange
    );

    flange.position.copy(flangeCenter);

    const quaternion = new THREE.Quaternion();
    quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), radial.clone().normalize());
    flange.quaternion.copy(quaternion);

    flange.castShadow = true;
    flange.receiveShadow = true;
    group.add(flange);

    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));
    const vertical = new THREE.Vector3(0, 1, 0);

    const boltCount = 12;

    for (let i = 0; i < boltCount; i++) {
        const a = (Math.PI * 2 * i) / boltCount;

        const boltPos = flangeCenter.clone()
            .add(tangent.clone().multiplyScalar(Math.cos(a) * flangeRadius * 0.72))
            .add(vertical.clone().multiplyScalar(Math.sin(a) * flangeRadius * 0.72));

        const bolt = new THREE.Mesh(
            new THREE.CylinderGeometry(boltRadius, boltRadius, flangeThickness * 1.25, 10),
            materialBolt
        );

        bolt.position.copy(boltPos);
        bolt.quaternion.copy(quaternion);
        bolt.castShadow = true;
        bolt.receiveShadow = true;

        group.add(bolt);
    }
}
function addHelicalTopPlatform(group, radius, height, angle, platformMaterial, railMaterial) {
    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));

    const width = Math.max(radius * 0.26, 1.55);
    const depth = Math.max(radius * 0.18, 1.15);
    const thickness = Math.max(radius * 0.012, 0.06);

    const center = radial.clone().multiplyScalar(radius + depth * 0.42);
    center.y = height + thickness;

    const platform = new THREE.Mesh(
        new THREE.BoxGeometry(width, thickness, depth),
        platformMaterial
    );

    platform.position.copy(center);
    platform.rotation.y = -Math.atan2(radial.z, radial.x) + Math.PI / 2;
    platform.castShadow = true;
    platform.receiveShadow = true;
    group.add(platform);

    const railHeight = Math.max(radius * 0.090, 0.82);
    const railRadius = Math.max(radius * 0.0052, 0.028);

    const p1 = center.clone().add(tangent.clone().multiplyScalar(-width / 2)).add(radial.clone().multiplyScalar(depth / 2));
    const p2 = center.clone().add(tangent.clone().multiplyScalar(width / 2)).add(radial.clone().multiplyScalar(depth / 2));
    const p3 = center.clone().add(tangent.clone().multiplyScalar(-width / 2)).add(radial.clone().multiplyScalar(-depth / 2));
    const p4 = center.clone().add(tangent.clone().multiplyScalar(width / 2)).add(radial.clone().multiplyScalar(-depth / 2));

    const bottoms = [p1, p2, p3, p4].map(p => {
        const b = p.clone();
        b.y = height + thickness * 2;
        return b;
    });

    const tops = bottoms.map(p => {
        const t = p.clone();
        t.y += railHeight;
        return t;
    });

    for (let i = 0; i < bottoms.length; i++) {
        addCylinderBetween(group, bottoms[i], tops[i], railRadius, railMaterial, 10);
    }

    addCylinderBetween(group, tops[0], tops[1], railRadius, railMaterial, 10);
    addCylinderBetween(group, tops[0], tops[2], railRadius, railMaterial, 10);
    addCylinderBetween(group, tops[1], tops[3], railRadius, railMaterial, 10);
}

function addCylinderBetween(group, start, end, radius, material, segments) {
    const direction = new THREE.Vector3().subVectors(end, start);
    const length = direction.length();

    if (length <= 0) return;

    const geometry = new THREE.CylinderGeometry(radius, radius, length, segments || 12, 1, false);
    const mesh = new THREE.Mesh(geometry, material);

    mesh.position.copy(start).add(end).multiplyScalar(0.5);

    const quaternion = new THREE.Quaternion();
    quaternion.setFromUnitVectors(
        new THREE.Vector3(0, 1, 0),
        direction.clone().normalize()
    );

    mesh.quaternion.copy(quaternion);
    mesh.castShadow = true;
    mesh.receiveShadow = true;

    group.add(mesh);
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
