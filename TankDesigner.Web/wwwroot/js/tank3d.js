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
    addRoof(viewer.group, radius, currentY, tank.techo, tank.vigasTechoConico, scale);
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
    addConeRoofRafters(group, radius, height, roofHeight, vigasTechoConico);
    addConeRoofCenterHub(group, height + roofHeight, radius, numeroVigas);
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

function addConeRoofRafters(group, radius, baseHeight, roofHeight, vigasTechoConico) {
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

    const hubRadius = calcularRadioNucleoTecho(radius, numeroVigas);
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

function addConeRoofCenterHub(group, y, radius, numeroVigas) {
    const hubRadius = calcularRadioNucleoTecho(radius, numeroVigas);
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

function calcularRadioNucleoTecho(radius, numeroVigas) {
    const porTamanoTanque = radius * 0.16;
    const porNumeroVigas = radius * Math.min(0.30, numeroVigas * 0.006);

    return Math.max(radius * 0.14, porTamanoTanque, porNumeroVigas, 0.55);
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
        color: 0xfacc15,
        metalness: 0.62,
        roughness: 0.26
    });

    const cageMaterial = new THREE.MeshStandardMaterial({
        color: 0x475569,
        metalness: 0.58,
        roughness: 0.28
    });

    const platformMaterial = new THREE.MeshStandardMaterial({
        color: 0x64748b,
        metalness: 0.65,
        roughness: 0.3
    });

    const angle = -Math.PI / 4 + angleOffset;
    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));

    const ladderRadius = radius + Math.max(radius * 0.045, 0.28);
    const railDistance = Math.max(radius * 0.045, 0.34);
    const railRadius = Math.max(radius * 0.0065, 0.032);
    const rungRadius = Math.max(radius * 0.0055, 0.026);

    const bottomY = height * 0.015;
    const topY = height * 1.035;

    const centerBase = radial.clone().multiplyScalar(ladderRadius);

    const rail1Bottom = centerBase.clone().add(tangent.clone().multiplyScalar(-railDistance));
    rail1Bottom.y = bottomY;

    const rail1Top = centerBase.clone().add(tangent.clone().multiplyScalar(-railDistance));
    rail1Top.y = topY;

    const rail2Bottom = centerBase.clone().add(tangent.clone().multiplyScalar(railDistance));
    rail2Bottom.y = bottomY;

    const rail2Top = centerBase.clone().add(tangent.clone().multiplyScalar(railDistance));
    rail2Top.y = topY;

    addCylinderBetween(group, rail1Bottom, rail1Top, railRadius, ladderMaterial, 16);
    addCylinderBetween(group, rail2Bottom, rail2Top, railRadius, ladderMaterial, 16);

    const rungSpacing = Math.max(radius * 0.075, 0.28);
    const rungCount = Math.max(12, Math.floor((topY - bottomY) / rungSpacing));

    for (let i = 1; i < rungCount; i++) {
        const y = bottomY + ((topY - bottomY) * i) / rungCount;

        const left = centerBase.clone().add(tangent.clone().multiplyScalar(-railDistance));
        left.y = y;

        const right = centerBase.clone().add(tangent.clone().multiplyScalar(railDistance));
        right.y = y;

        addCylinderBetween(group, left, right, rungRadius, ladderMaterial, 12);
    }

    addVerticalLadderCage(group, radius, height, angle, radial, tangent, ladderRadius, centerBase, cageMaterial);
    addVerticalLadderPlatform(group, radius, height, radial, tangent, ladderRadius, platformMaterial, cageMaterial);
}

function addVerticalLadderCage(group, radius, height, angle, radial, tangent, ladderRadius, centerBase, cageMaterial) {
    const cageRadius = Math.max(radius * 0.085, 0.62);
    const cageTubeRadius = Math.max(radius * 0.0045, 0.022);

    const cageStartY = Math.min(height * 0.22, 2.1);
    const cageEndY = height * 1.025;
    const cageHeight = Math.max(cageEndY - cageStartY, 0.1);

    const ringSpacing = Math.max(radius * 0.14, 0.9);
    const ringCount = Math.max(3, Math.floor(cageHeight / ringSpacing));

    for (let i = 0; i <= ringCount; i++) {
        const y = cageStartY + (cageHeight * i) / ringCount;
        addCageRing(group, centerBase, radial, tangent, cageRadius, y, cageTubeRadius, cageMaterial);
    }

    const barCount = 7;
    for (let i = 0; i < barCount; i++) {
        const localAngle = -Math.PI * 0.78 + (Math.PI * 1.56 * i) / (barCount - 1);

        const offset = radial.clone().multiplyScalar(Math.cos(localAngle) * cageRadius)
            .add(tangent.clone().multiplyScalar(Math.sin(localAngle) * cageRadius));

        const bottom = centerBase.clone().add(offset);
        bottom.y = cageStartY;

        const top = centerBase.clone().add(offset);
        top.y = cageEndY;

        addCylinderBetween(group, bottom, top, cageTubeRadius, cageMaterial, 10);
    }

    const backOffset = radial.clone().multiplyScalar(cageRadius);
    const back1 = centerBase.clone().add(backOffset);
    back1.y = cageStartY;

    const back2 = centerBase.clone().add(backOffset);
    back2.y = cageEndY;

    addCylinderBetween(group, back1, back2, cageTubeRadius, cageMaterial, 10);
}

function addCageRing(group, centerBase, radial, tangent, cageRadius, y, tubeRadius, material) {
    const points = [];
    const segments = 28;

    for (let i = 0; i <= segments; i++) {
        const a = -Math.PI * 0.82 + (Math.PI * 1.64 * i) / segments;

        const point = centerBase.clone()
            .add(radial.clone().multiplyScalar(Math.cos(a) * cageRadius))
            .add(tangent.clone().multiplyScalar(Math.sin(a) * cageRadius));

        point.y = y;
        points.push(point);
    }

    for (let i = 0; i < points.length - 1; i++) {
        addCylinderBetween(group, points[i], points[i + 1], tubeRadius, material, 8);
    }
}

function addVerticalLadderPlatform(group, radius, height, radial, tangent, ladderRadius, platformMaterial, railMaterial) {
    const platformWidth = Math.max(radius * 0.22, 1.35);
    const platformDepth = Math.max(radius * 0.16, 1.05);
    const platformThickness = Math.max(radius * 0.012, 0.06);

    const platformCenter = radial.clone().multiplyScalar(radius + platformDepth * 0.42);
    platformCenter.y = height + platformThickness * 1.5;

    const platformGeometry = new THREE.BoxGeometry(platformWidth, platformThickness, platformDepth);
    const platform = new THREE.Mesh(platformGeometry, platformMaterial);

    platform.position.copy(platformCenter);
    platform.rotation.y = -Math.atan2(radial.z, radial.x) + Math.PI / 2;
    platform.castShadow = true;
    platform.receiveShadow = true;
    group.add(platform);

    const railHeight = Math.max(radius * 0.08, 0.75);
    const railRadius = Math.max(radius * 0.005, 0.026);

    const sideA = platformCenter.clone().add(tangent.clone().multiplyScalar(-platformWidth / 2));
    const sideB = platformCenter.clone().add(tangent.clone().multiplyScalar(platformWidth / 2));
    const outerA = sideA.clone().add(radial.clone().multiplyScalar(platformDepth / 2));
    const outerB = sideB.clone().add(radial.clone().multiplyScalar(platformDepth / 2));

    [sideA, sideB, outerA, outerB].forEach(p => {
        const bottom = p.clone();
        bottom.y = height + platformThickness * 2;

        const top = p.clone();
        top.y = bottom.y + railHeight;

        addCylinderBetween(group, bottom, top, railRadius, railMaterial, 10);
    });

    const railTopA = outerA.clone();
    railTopA.y = height + platformThickness * 2 + railHeight;

    const railTopB = outerB.clone();
    railTopB.y = height + platformThickness * 2 + railHeight;

    addCylinderBetween(group, railTopA, railTopB, railRadius, railMaterial, 10);
}

function addHelicalStair(group, radius, height, angleOffset = 0) {
    const stepMaterial = new THREE.MeshStandardMaterial({
        color: 0xfacc15,
        metalness: 0.62,
        roughness: 0.25
    });

    const railMaterial = new THREE.MeshStandardMaterial({
        color: 0x475569,
        metalness: 0.6,
        roughness: 0.28
    });

    const stairRadius = radius + Math.max(radius * 0.07, 0.42);
    const outerRadius = stairRadius + Math.max(radius * 0.09, 0.55);
    const innerRadius = stairRadius - Math.max(radius * 0.045, 0.26);

    const turns = Math.max(1.25, height / Math.max(radius * 1.35, 1));
    const steps = Math.max(36, Math.floor(turns * 42));

    const stepWidth = Math.max(radius * 0.135, 0.75);
    const stepDepth = Math.max(radius * 0.055, 0.26);
    const stepHeight = Math.max(radius * 0.010, 0.045);

    const pathPointsOuter = [];
    const pathPointsInner = [];
    const railHeight = Math.max(radius * 0.085, 0.72);
    const railRadius = Math.max(radius * 0.0048, 0.024);
    const postRadius = Math.max(radius * 0.0055, 0.026);

    for (let i = 0; i < steps; i++) {
        const t = i / (steps - 1);
        const angle = -Math.PI / 2 + angleOffset + t * turns * Math.PI * 2;
        const y = t * height;

        const geometry = new THREE.BoxGeometry(stepWidth, stepHeight, stepDepth);
        const step = new THREE.Mesh(geometry, stepMaterial);

        step.position.set(
            Math.cos(angle) * stairRadius,
            y,
            Math.sin(angle) * stairRadius
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

        const outerTop = outerBase.clone();
        outerTop.y += railHeight;

        const innerTop = new THREE.Vector3(
            Math.cos(angle) * innerRadius,
            y + railHeight * 0.92,
            Math.sin(angle) * innerRadius
        );

        pathPointsOuter.push(outerTop);
        pathPointsInner.push(innerTop);

        if (i % 3 === 0 || i === steps - 1) {
            addCylinderBetween(group, outerBase, outerTop, postRadius, railMaterial, 10);
        }
    }

    for (let i = 0; i < pathPointsOuter.length - 1; i++) {
        addCylinderBetween(group, pathPointsOuter[i], pathPointsOuter[i + 1], railRadius, railMaterial, 10);
    }

    for (let i = 0; i < pathPointsInner.length - 1; i++) {
        addCylinderBetween(group, pathPointsInner[i], pathPointsInner[i + 1], railRadius, railMaterial, 10);
    }

    addHelicalTopPlatform(group, radius, height, angleOffset + turns * Math.PI * 2, stepMaterial, railMaterial);
}

function addHelicalTopPlatform(group, radius, height, angle, platformMaterial, railMaterial) {
    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));

    const width = Math.max(radius * 0.22, 1.35);
    const depth = Math.max(radius * 0.16, 1.0);
    const thickness = Math.max(radius * 0.012, 0.055);

    const center = radial.clone().multiplyScalar(radius + depth * 0.45);
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

    const railHeight = Math.max(radius * 0.08, 0.72);
    const railRadius = Math.max(radius * 0.005, 0.026);

    const p1 = center.clone().add(tangent.clone().multiplyScalar(-width / 2)).add(radial.clone().multiplyScalar(depth / 2));
    const p2 = center.clone().add(tangent.clone().multiplyScalar(width / 2)).add(radial.clone().multiplyScalar(depth / 2));

    const p1Bottom = p1.clone();
    p1Bottom.y = height + thickness * 2;

    const p1Top = p1.clone();
    p1Top.y = p1Bottom.y + railHeight;

    const p2Bottom = p2.clone();
    p2Bottom.y = height + thickness * 2;

    const p2Top = p2.clone();
    p2Top.y = p2Bottom.y + railHeight;

    addCylinderBetween(group, p1Bottom, p1Top, railRadius, railMaterial, 10);
    addCylinderBetween(group, p2Bottom, p2Top, railRadius, railMaterial, 10);
    addCylinderBetween(group, p1Top, p2Top, railRadius, railMaterial, 10);
}

function addVerticalLadder(group, radius, height, angleOffset = 0) {    const material = new THREE.MeshStandardMaterial({
        color: 0xffea00,
        emissive: new THREE.Color(0x7c2d12),
        emissiveIntensity: 0.28,
        metalness: 0.55,
        roughness: 0.22
    });

    const angle = -Math.PI / 4 + angleOffset;
    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));

    const ladderRadius = radius * 1.32;
    const railDistance = Math.max(radius * 0.06, 0.45);
    const railRadius = Math.max(radius * 0.012, 0.055);
    const rungRadius = Math.max(radius * 0.009, 0.04);

    const bottomY = height * 0.02;
    const topY = height * 1.04;

    const centerBase = radial.clone().multiplyScalar(ladderRadius);

    const rail1Bottom = centerBase.clone().add(tangent.clone().multiplyScalar(-railDistance));
    rail1Bottom.y = bottomY;

    const rail1Top = centerBase.clone().add(tangent.clone().multiplyScalar(-railDistance));
    rail1Top.y = topY;

    const rail2Bottom = centerBase.clone().add(tangent.clone().multiplyScalar(railDistance));
    rail2Bottom.y = bottomY;

    const rail2Top = centerBase.clone().add(tangent.clone().multiplyScalar(railDistance));
    rail2Top.y = topY;

    addCylinderBetween(group, rail1Bottom, rail1Top, railRadius, material, 16);
    addCylinderBetween(group, rail2Bottom, rail2Top, railRadius, material, 16);

    const rungCount = Math.max(12, Math.floor(height / Math.max(radius * 0.09, 0.32)));

    for (let i = 1; i < rungCount; i++) {
        const y = bottomY + ((topY - bottomY) * i) / rungCount;

        const left = centerBase.clone().add(tangent.clone().multiplyScalar(-railDistance));
        left.y = y;

        const right = centerBase.clone().add(tangent.clone().multiplyScalar(railDistance));
        right.y = y;

        addCylinderBetween(group, left, right, rungRadius, material, 12);
    }
}

function addHelicalStair(group, radius, height, angleOffset = 0) {
    const material = new THREE.MeshStandardMaterial({
        color: 0xffea00,
        emissive: new THREE.Color(0x7c2d12),
        emissiveIntensity: 0.22,
        metalness: 0.6,
        roughness: 0.24
    });

    const stairRadius = radius * 1.24;
    const turns = Math.max(1.2, height / Math.max(radius * 1.4, 1));
    const steps = Math.max(28, Math.floor(turns * 32));

    const stepWidth = Math.max(radius * 0.12, 0.45);
    const stepDepth = Math.max(radius * 0.06, 0.22);
    const stepHeight = Math.max(radius * 0.014, 0.055);

    for (let i = 0; i < steps; i++) {
        const t = i / (steps - 1);
        const angle = -Math.PI / 2 + angleOffset + t * turns * Math.PI * 2;
        const geometry = new THREE.BoxGeometry(stepWidth, stepHeight, stepDepth);
        const step = new THREE.Mesh(geometry, material);

        step.position.set(
            Math.cos(angle) * stairRadius,
            t * height,
            Math.sin(angle) * stairRadius
        );

        step.rotation.y = -angle;
        step.castShadow = true;
        step.receiveShadow = true;

        group.add(step);
    }   
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