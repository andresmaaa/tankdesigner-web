const viewers = new WeakMap();

const technicalViewState = {
    showRoof: true,
    showGuardrail: true,
    showConnections: true,
    showLadder: true,
    showReferences: false,
    showWater: true
};

function renderTank3D(container, tank, dotNetRef) {
    if (!container) return;

    disposeViewer(container);
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

    const viewer = createViewer(container, scale, metersPerUnit, tank, dotNetRef);
    viewers.set(container, viewer);

    buildTank(viewer, tank, rings, scale);
    fitCamera(viewer);

    viewer.renderer.render(viewer.scene, viewer.camera);
}

function disposeViewer(container) {
    const oldViewer = viewers.get(container);
    if (!oldViewer) return;

    if (oldViewer.animationId) {
        cancelAnimationFrame(oldViewer.animationId);
    }

    if (oldViewer.resizeObserver) {
        oldViewer.resizeObserver.disconnect();
    }

    oldViewer.scene.traverse(obj => {
        if (obj.geometry) obj.geometry.dispose();

        if (obj.material) {
            if (Array.isArray(obj.material)) {
                obj.material.forEach(m => m.dispose());
            } else {
                obj.material.dispose();
            }
        }
    });

    oldViewer.renderer.dispose();

    if (oldViewer.renderer.domElement && oldViewer.renderer.domElement.parentNode) {
        oldViewer.renderer.domElement.parentNode.removeChild(oldViewer.renderer.domElement);
    }

    viewers.delete(container);
}
function createViewer(container, scale, metersPerUnit, tank, dotNetRef) {
    const shell = document.createElement("div");

    shell.style.position = "relative";
    shell.style.width = "100%";
    shell.style.height = "100%";
    shell.style.minHeight = "720";
    shell.style.borderRadius = "24px";
    shell.style.overflow = "hidden";
    shell.style.background = `
        radial-gradient(circle at 52% 42%,
        rgba(255,255,255,1) 0%,
        rgba(241,245,249,0.98) 48%,
        rgba(226,232,240,0.96) 100%)
    `;

    container.appendChild(shell);

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0xf1f5f9);
    scene.fog = null;

    const camera = new THREE.PerspectiveCamera(34, 1, 0.01, 10000);

    const renderer = new THREE.WebGLRenderer({
        antialias: true,
        alpha: true,
        preserveDrawingBuffer: true
    });

    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.25;

    shell.appendChild(renderer.domElement);

    const group = new THREE.Group();
    scene.add(group);

    scene.add(new THREE.AmbientLight(0xffffff, 1.35));

    const key = new THREE.DirectionalLight(0xffffff, 2.0);
    key.position.set(36, 42, 32);
    key.castShadow = true;
    key.shadow.mapSize.width = 2048;
    key.shadow.mapSize.height = 2048;
    key.shadow.camera.near = 1;
    key.shadow.camera.far = 250;
    key.shadow.camera.left = -80;
    key.shadow.camera.right = 80;
    key.shadow.camera.top = 80;
    key.shadow.camera.bottom = -80;
    scene.add(key);

    const fill = new THREE.DirectionalLight(0xffffff, 0.95);
    fill.position.set(-32, 20, -28);
    scene.add(fill);

    const rim = new THREE.DirectionalLight(0xffffff, 0.75);
    rim.position.set(-24, 28, 44);
    scene.add(rim);

    const hemi = new THREE.HemisphereLight(0xffffff, 0x94a3b8, 0.7);
    scene.add(hemi);

    addScaleBadge(shell, metersPerUnit, tank);
    addRoofControls(shell, container, tank, dotNetRef);
    addTechnicalControls(shell, container, tank, dotNetRef);
    addTechnicalInfoOverlay(shell);
    addDownloadPngButton(shell, renderer);
    addMouseHelpPanel(shell);

    const viewer = {
        container,
        shell,
        scene,
        camera,
        renderer,
        group,
        yaw: 0.72,
        pitch: 0.24,
        distance: 72,
        target: new THREE.Vector3(0, 0, 0),
        isDragging: false,
        lastX: 0,
        lastY: 0
    };

    renderer.__tank3dScene = scene;
    renderer.__tank3dCamera = camera;

    bindControls(viewer);
    bindTechnicalHover(viewer);
    resize(viewer);

    const resizeObserver = new ResizeObserver(() => resize(viewer));
    resizeObserver.observe(container);
    viewer.resizeObserver = resizeObserver;

    animate(viewer);
    return viewer;
}

function addMouseHelpPanel(shell) {
    const panel = document.createElement("div");

    panel.innerHTML = `
        <strong>Controles de vista 3D</strong>
        <span>🖱️ Rotar: botón izquierdo + arrastrar</span>
        <span>🔍 Zoom: rueda del ratón</span>
        <span>ℹ️ Datos: pasa el cursor sobre piezas</span>
    `;

    panel.style.position = "absolute";
    panel.style.left = "24px";
    panel.style.right = "24px";
    panel.style.bottom = "18px";
    panel.style.zIndex = "12";
    panel.style.display = "flex";
    panel.style.flexWrap = "wrap";
    panel.style.justifyContent = "center";
    panel.style.gap = "16px";
    panel.style.alignItems = "center";
    panel.style.padding = "12px 16px";
    panel.style.borderRadius = "18px";
    panel.style.background = "rgba(15,23,42,0.88)";
    panel.style.border = "1px solid rgba(148,163,184,0.35)";
    panel.style.boxShadow = "0 18px 45px rgba(15,23,42,0.28)";
    panel.style.backdropFilter = "blur(14px)";
    panel.style.color = "#ffffff";
    panel.style.font = "12px Arial";
    panel.style.pointerEvents = "none";

    shell.appendChild(panel);
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
    scaleBadge.style.bottom = "94px";
    scaleBadge.style.zIndex = "5";
    scaleBadge.style.padding = "12px 14px";
    scaleBadge.style.borderRadius = "16px";
    scaleBadge.style.background = "rgba(15,23,42,0.88)";
    scaleBadge.style.color = "#ffffff";
    scaleBadge.style.font = "13px Arial";
    scaleBadge.style.lineHeight = "1.45";
    shell.appendChild(scaleBadge);
}

function addRoofControls(shell, container, tank, dotNetRef) {
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

        if (dotNetRef) {
            dotNetRef.invokeMethodAsync("ActualizarConfiguracionTecho3D", beams, hubPercent / 100);
        }

        renderTank3D(container, tank, dotNetRef);
    };

    beamsInput.addEventListener("change", applyChanges);
    hubInput.addEventListener("input", () => {
        hubLabel.textContent = `${hubInput.value}%`;
    });
    hubInput.addEventListener("change", applyChanges);

    shell.appendChild(panel);
}

function addTechnicalControls(shell, container, tank, dotNetRef) {
    const panel = document.createElement("div");

    panel.style.position = "absolute";
    panel.style.left = "18px";
    panel.style.bottom = "94px";
    panel.style.zIndex = "9";
    panel.style.width = "190px";
    panel.style.padding = "14px";
    panel.style.borderRadius = "16px";
    panel.style.background = "rgba(15,23,42,0.92)";
    panel.style.color = "white";
    panel.style.font = "13px Arial";
    panel.style.boxShadow = "0 18px 45px rgba(15,23,42,0.28)";

    panel.innerHTML = `
        <div style="font-size:14px;font-weight:700;margin-bottom:12px;">
            Vista técnica 3D
        </div>

        ${createTechnicalCheckbox("Techo", "showRoof")}
        ${createTechnicalCheckbox("Barandilla", "showGuardrail")}
        ${createTechnicalCheckbox("Conexiones", "showConnections")}
        ${createTechnicalCheckbox("Escalera", "showLadder")}
        ${createTechnicalCheckbox("Agua", "showWater")}
        ${createTechnicalCheckbox("Referencias", "showReferences")}
    `;

    panel.querySelectorAll("input[type='checkbox']").forEach(input => {
        input.addEventListener("change", () => {
            technicalViewState[input.dataset.key] = input.checked;
            renderTank3D(container, tank, dotNetRef);
        });
    });

    shell.appendChild(panel);
}

function createTechnicalCheckbox(label, key) {
    return `
        <label style="display:flex;align-items:center;gap:8px;margin-bottom:8px;cursor:pointer;">
            <input type="checkbox" data-key="${key}" ${technicalViewState[key] ? "checked" : ""}>
            <span>${label}</span>
        </label>
    `;
}

function addTechnicalInfoOverlay(shell) {
    const overlay = document.createElement("div");

    overlay.id = "tank3d-tech-overlay";
    overlay.style.position = "absolute";
    overlay.style.left = "50%";
    overlay.style.top = "18px";
    overlay.style.transform = "translateX(-50%)";
    overlay.style.padding = "10px 14px";
    overlay.style.borderRadius = "14px";
    overlay.style.background = "rgba(15,23,42,0.88)";
    overlay.style.color = "white";
    overlay.style.font = "13px Arial";
    overlay.style.zIndex = "20";
    overlay.style.pointerEvents = "none";
    overlay.style.display = "none";
    overlay.style.lineHeight = "1.45";

    shell.appendChild(overlay);
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

    const starterHeight = getStarterRingHeight(tank, scale);

    const starterTotalHeight = addStarterRing(viewer.group, radius, starterHeight, tank);
    currentY += starterTotalHeight;

    rings.forEach((ring, index) => {
        const height = Number(ring.altura) * scale;
        const materialName = ring.material || tank.materialPrincipal || "material";
        const color = colorForMaterial(materialName);

        const shellGeometry = new THREE.CylinderGeometry(radius, radius, height, 96, 1, true);
        const shellMaterial = new THREE.MeshStandardMaterial({
            color,
            metalness: 0.48,
            roughness: 0.38,
            side: THREE.DoubleSide
        });

        const shell = new THREE.Mesh(shellGeometry, shellMaterial);
        shell.position.y = currentY + height / 2;
        shell.castShadow = true;
        shell.receiveShadow = true;

        shell.userData = {
            tipo: `Anillo ${index + 1}`,
            material: materialName,
            altura: formatTechnicalValue(ring.altura, "m"),
            espesor: formatTechnicalValue(ring.espesor, "mm"),
            diametro: formatTechnicalValue(tank.diametro, "m")
        };

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

    addBottomDisc(viewer.group, radius * 0.98);

    if (technicalViewState.showWater) {
        addWaterLevelIfAvailable(viewer.group, radius, currentY, tank, scale);
    }

    addTopStiffener(viewer.group, radius, currentY);

    if (technicalViewState.showRoof) {
        addRoof(viewer.group, radius, currentY, tank.techo, tank.vigasTechoConico, scale);
    }

    if (technicalViewState.showGuardrail) {
        addRoofGuardrail(viewer.group, radius, currentY, tank.techo);
    }

    if (technicalViewState.showConnections) {
        addTankConnections(viewer.group, radius, currentY);
    }

    addManhole(viewer.group, radius, currentY);
    addRoofVent(viewer.group, radius, currentY, tank.techo);

    if (technicalViewState.showReferences) {
        addReferenceGrid(viewer.group, radius, currentY);
        addVerticalReference(viewer.group, radius, currentY);
    }

    if (technicalViewState.showLadder) {
        addLadder(viewer.group, radius, currentY, tank.escalera, scale);
    }

    viewer.group.position.y = -currentY / 2;
    viewer.modelRadius = radius;
    viewer.modelHeight = currentY;
}

function getStarterRingHeight(tank, scale) {
    const rawHeight =
        Number(tank?.alturaStarterRing) ||
        Number(tank?.starterRingHeight) ||
        Number(tank?.starterRingAltura) ||
        Number(tank?.starterRingAlturaMm) ||
        Number(tank?.alturaStarterRingMm) ||
        Number(tank?.resultado?.alturaStarterRing) ||
        Number(tank?.resultado?.starterRingHeight) ||
        Number(tank?.resultado?.starterRingAltura) ||
        Number(tank?.resultado?.starterRingAlturaMm) ||
        Number(tank?.resultado?.alturaStarterRingMm) ||
        540;

    return rawHeight > 50
        ? rawHeight * scale / 1000
        : rawHeight * scale;
}

function getStarterRingHeightMm(tank) {
    return Number(tank?.alturaStarterRing) ||
        Number(tank?.starterRingHeight) ||
        Number(tank?.starterRingAltura) ||
        Number(tank?.starterRingAlturaMm) ||
        Number(tank?.alturaStarterRingMm) ||
        Number(tank?.resultado?.alturaStarterRing) ||
        Number(tank?.resultado?.starterRingHeight) ||
        Number(tank?.resultado?.starterRingAltura) ||
        Number(tank?.resultado?.starterRingAlturaMm) ||
        Number(tank?.resultado?.alturaStarterRingMm) ||
        540;
}

function addStarterRing(group, radius, height, tank) {
    const realHeightMm = getStarterRingHeightMm(tank);

    const tooltipData = {
        tipo: "Starter ring / anillo de arranque",
        material: "Anillo base rellenable",
        altura: `${realHeightMm} mm`,
        espesor: "Base de mortero / concreto",
        diametro: `${((radius * 2) / 1).toFixed(3)} u.3D`
    };

    const starterGroup = new THREE.Group();

    const concreteHeight = Math.max(height * 0.65, 0.42);
    const concreteRadius = radius * 1.20;

    const concreteMaterial = new THREE.MeshStandardMaterial({
        color: 0x9f9b93,
        metalness: 0.02,
        roughness: 1
    });

    const concrete = new THREE.Mesh(
        new THREE.CylinderGeometry(concreteRadius, concreteRadius, concreteHeight, 160),
        concreteMaterial
    );

    concrete.position.y = concreteHeight / 2;
    concrete.receiveShadow = true;
    concrete.userData = tooltipData;
    starterGroup.add(concrete);

    const starterY = concreteHeight;
    const starterMaterial = new THREE.MeshStandardMaterial({
        color: 0x747541,
        metalness: 0.58,
        roughness: 0.34,
        side: THREE.DoubleSide
    });

    const starter = new THREE.Mesh(
        new THREE.CylinderGeometry(radius * 1.055, radius * 1.055, height, 160, 1, true),
        starterMaterial
    );

    starter.position.y = starterY + height / 2;
    starter.castShadow = true;
    starter.receiveShadow = true;
    starter.userData = tooltipData;
    starterGroup.add(starter);

    const darkMetal = new THREE.MeshStandardMaterial({
        color: 0x252516,
        metalness: 0.8,
        roughness: 0.2
    });

    const topRing = new THREE.Mesh(
        new THREE.TorusGeometry(radius * 1.06, Math.max(radius * 0.014, 0.045), 18, 160),
        darkMetal
    );

    topRing.rotation.x = Math.PI / 2;
    topRing.position.y = starterY + height;
    topRing.castShadow = true;
    topRing.userData = tooltipData;
    starterGroup.add(topRing);

    const bottomRing = new THREE.Mesh(
        new THREE.TorusGeometry(radius * 1.06, Math.max(radius * 0.012, 0.038), 18, 160),
        darkMetal
    );

    bottomRing.rotation.x = Math.PI / 2;
    bottomRing.position.y = starterY;
    bottomRing.userData = tooltipData;
    starterGroup.add(bottomRing);

    const plateMaterial = new THREE.MeshStandardMaterial({
        color: 0x5d5e34,
        metalness: 0.62,
        roughness: 0.34
    });

    const plateCount = 36;

    for (let i = 0; i < plateCount; i++) {
        const angle = (Math.PI * 2 * i) / plateCount;

        const plate = new THREE.Mesh(
            new THREE.BoxGeometry(
                Math.max(radius * 0.034, 0.16),
                height * 0.82,
                Math.max(radius * 0.014, 0.045)
            ),
            plateMaterial
        );

        plate.position.set(
            Math.cos(angle) * radius * 1.078,
            starterY + height * 0.43,
            Math.sin(angle) * radius * 1.078
        );

        plate.lookAt(0, starterY + height * 0.43, 0);
        plate.castShadow = true;
        plate.receiveShadow = true;
        plate.userData = tooltipData;
        starterGroup.add(plate);
    }

    const boltMaterial = new THREE.MeshStandardMaterial({
        color: 0xd9dee5,
        metalness: 0.95,
        roughness: 0.14
    });

    const boltCount = 128;

    for (let i = 0; i < boltCount; i++) {
        const angle = (Math.PI * 2 * i) / boltCount;

        const bolt = new THREE.Mesh(
            new THREE.SphereGeometry(Math.max(radius * 0.0046, 0.022), 10, 10),
            boltMaterial
        );

        bolt.position.set(
            Math.cos(angle) * radius * 1.067,
            starterY + height * 0.88,
            Math.sin(angle) * radius * 1.067
        );

        bolt.castShadow = true;
        bolt.userData = tooltipData;
        starterGroup.add(bolt);
    }

    group.add(starterGroup);

    return concreteHeight + height;
}
function formatTechnicalValue(value, suffix) {
    const n = Number(value);
    if (!Number.isFinite(n) || n <= 0) return "—";
    return `${n} ${suffix}`;
}

function addRoofGuardrail(group, radius, height, roofRaw) {
    const roof = normalizarTecho(roofRaw);
    if (roof.type === "none") return;

    const material = new THREE.MeshStandardMaterial({
        color: 0xe5e7eb,
        metalness: 0.95,
        roughness: 0.16,
        envMapIntensity: 1.4,
        side: THREE.DoubleSide
    });

    const postRadius = Math.max(radius * 0.0045, 0.022);
    const railRadius = Math.max(radius * 0.0055, 0.026);
    const railHeight = Math.max(radius * 0.095, 0.78);
    const lowerRailHeight = railHeight * 0.55;

    const railRadiusPosition = radius * 1.045;
    const postCount = Math.max(32, Math.min(72, Math.floor(radius * 5.5)));

    for (let i = 0; i < postCount; i++) {
        const angle = (Math.PI * 2 * i) / postCount;
        const x = Math.cos(angle) * railRadiusPosition;
        const z = Math.sin(angle) * railRadiusPosition;

        const bottom = new THREE.Vector3(x, height, z);
        const top = new THREE.Vector3(x, height + railHeight, z);

        addCylinderBetween(group, bottom, top, postRadius, material, 8);
    }

    addCircularRail(group, railRadiusPosition, height + railHeight, railRadius, material);
    addCircularRail(group, railRadiusPosition, height + lowerRailHeight, railRadius * 0.85, material);
    addCircularRail(group, railRadiusPosition, height + Math.max(radius * 0.018, 0.10), railRadius * 0.75, material);
    addRoofAccessGate(group, radius, height, material);
}

function addRoofAccessGate(group, radius, height, material) {
    const angle = -Math.PI / 4;
    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));

    const gateWidth = Math.max(radius * 0.18, 1.05);
    const gateHeight = Math.max(radius * 0.09, 0.72);
    const railRadius = Math.max(radius * 0.005, 0.026);

    const center = radial.clone().multiplyScalar(radius * 1.055);
    center.y = height + gateHeight * 0.55;

    const leftBottom = center.clone().add(tangent.clone().multiplyScalar(-gateWidth / 2));
    leftBottom.y = height;

    const leftTop = leftBottom.clone();
    leftTop.y = height + gateHeight;

    const rightBottom = center.clone().add(tangent.clone().multiplyScalar(gateWidth / 2));
    rightBottom.y = height;

    const rightTop = rightBottom.clone();
    rightTop.y = height + gateHeight;

    addCylinderBetween(group, leftBottom, leftTop, railRadius, material, 8);
    addCylinderBetween(group, rightBottom, rightTop, railRadius, material, 8);
    addCylinderBetween(group, leftTop, rightTop, railRadius, material, 8);

    const chain = new THREE.MeshStandardMaterial({
        color: 0xff7a18,
        metalness: 0.65,
        roughness: 0.24
    });

    addCylinderBetween(group, leftBottom.clone().lerp(leftTop, 0.55), rightBottom.clone().lerp(rightTop, 0.55), railRadius * 0.75, chain, 8);
}

function addCircularRail(group, radius, y, tubeRadius, material) {
    const segments = 96;
    const points = [];

    for (let i = 0; i <= segments; i++) {
        const angle = (Math.PI * 2 * i) / segments;
        points.push(new THREE.Vector3(
            Math.cos(angle) * radius,
            y,
            Math.sin(angle) * radius
        ));
    }

    for (let i = 0; i < points.length - 1; i++) {
        addCylinderBetween(group, points[i], points[i + 1], tubeRadius, material, 8);
    }
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
    cover.userData = {
        tipo: "Manhole",
        material: "Acero",
        altura: formatTechnicalValue(y, "u.3D"),
        espesor: formatTechnicalValue(coverThickness, "u.3D"),
        diametro: formatTechnicalValue(manholeRadius * 2, "u.3D")
    };
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
    vent.userData = {
        tipo: "Ventilación de techo",
        material: "Acero",
        altura: "Techo",
        espesor: "—",
        diametro: formatTechnicalValue(ventRadius * 2, "u.3D")
    };
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
    const baseMaterial = new THREE.MeshStandardMaterial({
        color: 0xdbe3ec,
        metalness: 0.58,
        roughness: 0.34,
        side: THREE.DoubleSide
    });

    const roof = new THREE.Mesh(
        new THREE.CircleGeometry(radius * 0.985, 128),
        baseMaterial
    );

    roof.rotation.x = -Math.PI / 2;
    roof.position.y = height + radius * 0.012;
    roof.castShadow = true;
    roof.receiveShadow = true;
    group.add(roof);

    const sheetMaterial = new THREE.MeshStandardMaterial({
        color: 0xf1f5f9,
        metalness: 0.68,
        roughness: 0.26
    });

    const ribMaterial = new THREE.MeshStandardMaterial({
        color: 0x94a3b8,
        metalness: 0.72,
        roughness: 0.24
    });

    const sheetCount = Math.max(10, Math.min(22, Math.floor(radius * 1.8)));
    const sheetWidth = (radius * 2) / sheetCount;
    const ribHeight = Math.max(radius * 0.012, 0.035);
    const ribWidth = Math.max(radius * 0.010, 0.030);

    for (let i = 0; i < sheetCount; i++) {
        const x = -radius + sheetWidth * (i + 0.5);
        const halfLength = Math.sqrt(Math.max(0, radius * radius - x * x));

        if (halfLength <= 0.1) continue;

        const sheet = new THREE.Mesh(
            new THREE.BoxGeometry(sheetWidth * 0.92, ribHeight * 0.45, halfLength * 2),
            sheetMaterial
        );

        sheet.position.set(x, height + radius * 0.018, 0);
        sheet.castShadow = true;
        sheet.receiveShadow = true;
        group.add(sheet);

        [-0.28, 0.28].forEach(offset => {
            const rib = new THREE.Mesh(
                new THREE.BoxGeometry(ribWidth, ribHeight, halfLength * 2),
                ribMaterial
            );

            rib.position.set(x + sheetWidth * offset, height + radius * 0.030, 0);
            rib.castShadow = true;
            rib.receiveShadow = true;
            group.add(rib);
        });
    }

    addOpenTop(group, radius, height + radius * 0.015);
}

function addConeRoof(group, radius, height, vigasTechoConico, scale) {
    const alturaConoReal = vigasTechoConico && Number(vigasTechoConico.alturaCono) > 0
        ? Number(vigasTechoConico.alturaCono)
        : 0;

    const roofHeight = alturaConoReal > 0
        ? alturaConoReal * scale
        : Math.max(radius * 0.16, 1.2);

    const geometry = new THREE.ConeGeometry(radius * 1.01, roofHeight, 96, 1, false);
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
    cone.userData = {
        tipo: "Techo cónico",
        material: "Acero",
        altura: alturaConoReal > 0 ? `${alturaConoReal} m` : "Auto 3D",
        espesor: "—",
        diametro: "—"
    };

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
        const points = curve.getPoints(120).map(p => new THREE.Vector3(p.x, y, p.y));
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
    const domeHeight = Math.max(radius * 0.42, 1.35);

    const geometry = new THREE.SphereGeometry(
        radius * 1.015,
        128,
        32,
        0,
        Math.PI * 2,
        0,
        Math.PI / 2
    );

    const material = new THREE.MeshStandardMaterial({
        color: 0xe5e7eb,
        metalness: 0.58,
        roughness: 0.30,
        transparent: true,
        opacity: 0.92,
        side: THREE.DoubleSide
    });

    const dome = new THREE.Mesh(geometry, material);

    dome.scale.y = domeHeight / radius;
    dome.position.y = height - radius * 0.01;
    dome.castShadow = true;
    dome.receiveShadow = true;

    dome.userData = {
        tipo: "Techo domo geodésico",
        material: "Aluminio / acero",
        altura: formatTechnicalValue(domeHeight, "u.3D"),
        espesor: "—",
        diametro: formatTechnicalValue(radius * 2, "u.3D")
    };

    group.add(dome);

    addDomeRoofRibs(group, radius * 0.99, height, domeHeight);
    addDomeSkirtRing(group, radius, height);
}
function addDomeSkirtRing(group, radius, height) {
    const material = new THREE.MeshStandardMaterial({
        color: 0xd1d5db,
        metalness: 0.72,
        roughness: 0.24
    });

    const skirtHeight = Math.max(radius * 0.09, 0.38);
    const skirtThickness = Math.max(radius * 0.016, 0.05);

    const skirt = new THREE.Mesh(
        new THREE.CylinderGeometry(radius * 1.01, radius * 1.01, skirtHeight, 128, 1, true),
        material
    );

    skirt.position.y = height + skirtHeight / 2 - radius * 0.025;
    skirt.castShadow = true;
    skirt.receiveShadow = true;

    group.add(skirt);

    addCircularRail(group, radius * 1.018, height + skirtHeight, skirtThickness, material);
    addCircularRail(group, radius * 1.018, height, skirtThickness * 0.8, material);
}
function addDomeRoofRibs(group, radius, height, domeHeight) {
    const material = new THREE.MeshStandardMaterial({
        color: 0xcbd5e1,
        metalness: 0.72,
        roughness: 0.24
    });

    const ribRadius = Math.max(radius * 0.0045, 0.020);
    const ribCount = 24;

    for (let i = 0; i < ribCount; i++) {
        const angle = (Math.PI * 2 * i) / ribCount;

        const start = new THREE.Vector3(
            Math.cos(angle) * radius * 0.96,
            height + radius * 0.015,
            Math.sin(angle) * radius * 0.96
        );

        const end = new THREE.Vector3(0, height + domeHeight, 0);

        addCylinderBetween(group, start, end, ribRadius, material, 8);
    }

    [0.35, 0.62, 0.84].forEach(f => {
        const ringRadius = radius * f;
        const y = height + domeHeight * (1 - f * 0.72);

        addCircularRail(group, ringRadius, y, ribRadius * 0.85, material);
    });
}

function addWaterLevelIfAvailable(group, radius, height, tank, scale) {
    const rawLevel = Number(tank?.nivelAgua)
        || Number(tank?.alturaAgua)
        || Number(tank?.nivelLiquido)
        || Number(tank?.alturaLiquido)
        || 0;

    if (!rawLevel || rawLevel <= 0 || !scale || scale <= 0) return;

    const y = Math.min(rawLevel * scale, height * 0.995);
    if (y <= 0) return;

    const material = new THREE.MeshStandardMaterial({
        color: 0x38bdf8,
        transparent: true,
        opacity: 0.18,
        metalness: 0.05,
        roughness: 0.12,
        side: THREE.DoubleSide
    });

    const disc = new THREE.Mesh(new THREE.CircleGeometry(radius * 0.96, 96), material);
    disc.rotation.x = -Math.PI / 2;
    disc.position.y = y;
    disc.receiveShadow = true;
    disc.userData = {
        tipo: "Nivel de agua",
        material: "Agua",
        altura: `${rawLevel} m`,
        espesor: "—",
        diametro: "—"
    };
    group.add(disc);

    const lineMaterial = new THREE.LineBasicMaterial({
        color: 0x0284c7,
        transparent: true,
        opacity: 0.82
    });

    const curve = new THREE.EllipseCurve(0, 0, radius * 0.965, radius * 0.965, 0, Math.PI * 2, false, 0);
    const points = curve.getPoints(120).map(p => new THREE.Vector3(p.x, y + 0.01, p.y));
    const geometry = new THREE.BufferGeometry().setFromPoints(points);
    group.add(new THREE.LineLoop(geometry, lineMaterial));
}

function addTopStiffener(group, radius, height) {
    const geometry = new THREE.TorusGeometry(radius * 1.015, Math.max(radius * 0.018, 0.045), 14, 120);
    const material = new THREE.MeshStandardMaterial({
        color: 0x1e293b,
        metalness: 0.72,
        roughness: 0.28
    });

    const stiffener = new THREE.Mesh(geometry, material);
    stiffener.rotation.x = Math.PI / 2;
    stiffener.position.y = height;
    stiffener.userData = {
        tipo: "Rigidizador superior",
        material: "Acero",
        altura: "Coronación",
        espesor: "—",
        diametro: "—"
    };

    group.add(stiffener);
}

function addRingSeam(group, radius, y) {
    const curve = new THREE.EllipseCurve(0, 0, radius * 1.006, radius * 1.006, 0, Math.PI * 2, false, 0);
    const points = curve.getPoints(120).map(p => new THREE.Vector3(p.x, y, p.y));
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
    const material = new THREE.MeshStandardMaterial({
        color: 0x475569,
        metalness: 0.12,
        roughness: 0.88,
        side: THREE.DoubleSide
    });

    const disc = new THREE.Mesh(
        new THREE.CircleGeometry(radius * 1.22, 160),
        material
    );

    disc.rotation.x = -Math.PI / 2;
    disc.position.y = -0.015;
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

function addLadder(group, radius, height, escalera, scale) {
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
            addVerticalLadder(group, radius, height, angleOffset, scale);
        }
    }
}

function addVerticalLadder(group, radius, height, angleOffset = 0, scale) {
    const ladderMaterial = new THREE.MeshStandardMaterial({
        color: 0xd97706,
        emissive: new THREE.Color(0x7c2d12),
        emissiveIntensity: 0.10,
        metalness: 0.62,
        roughness: 0.26
    });

    const cageMaterial = new THREE.MeshStandardMaterial({
        color: 0xcbd5e1,
        metalness: 0.78,
        roughness: 0.20,
        transparent: true,
        opacity: 0.48
    });

    const platformMaterial = new THREE.MeshStandardMaterial({
        color: 0x64748b,
        metalness: 0.72,
        roughness: 0.28
    });

    const angle = -Math.PI / 4 + angleOffset;
    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));

    const ladderRadius = radius + Math.max(radius * 0.018, 0.14);
    const centerBase = radial.clone().multiplyScalar(ladderRadius);

    const railHalfWidth = Math.max(radius * 0.030, 0.30);
    const railRadius = Math.max(radius * 0.0052, 0.025);
    const rungRadius = Math.max(radius * 0.0042, 0.020);

    const bottomY = Math.max(height * 0.010, 0.05);
    const topY = height + Math.max(radius * 0.10, 0.75);

    const leftRailBottom = centerBase.clone().add(tangent.clone().multiplyScalar(-railHalfWidth));
    leftRailBottom.y = bottomY;

    const leftRailTop = centerBase.clone().add(tangent.clone().multiplyScalar(-railHalfWidth));
    leftRailTop.y = topY;

    const rightRailBottom = centerBase.clone().add(tangent.clone().multiplyScalar(railHalfWidth));
    rightRailBottom.y = bottomY;

    const rightRailTop = centerBase.clone().add(tangent.clone().multiplyScalar(railHalfWidth));
    rightRailTop.y = topY;

    addCylinderBetween(group, leftRailBottom, leftRailTop, railRadius, ladderMaterial, 14);
    addCylinderBetween(group, rightRailBottom, rightRailTop, railRadius, ladderMaterial, 14);

    const modelRungSpacing = scale && scale > 0 ? 0.30 * scale : 0.26;
    const rungSpacing = Math.max(Math.min(modelRungSpacing, 0.38), 0.16);
    const rungCount = Math.max(14, Math.floor((topY - bottomY) / rungSpacing));

    for (let i = 1; i < rungCount; i++) {
        const y = bottomY + ((topY - bottomY) * i) / rungCount;

        const left = centerBase.clone().add(tangent.clone().multiplyScalar(-railHalfWidth));
        left.y = y;

        const right = centerBase.clone().add(tangent.clone().multiplyScalar(railHalfWidth));
        right.y = y;

        addCylinderBetween(group, left, right, rungRadius, ladderMaterial, 10);
    }

    addCircularLadderCage(group, radius, height, radial, tangent, centerBase, cageMaterial, scale);

    addVerticalLadderIntermediatePlatform(
        group,
        radius,
        height,
        radial,
        tangent,
        platformMaterial,
        cageMaterial,
        centerBase,
        railHalfWidth
    );

    addVerticalLadderTopPlatform(group, radius, height, radial, tangent, platformMaterial, cageMaterial, centerBase, railHalfWidth);
    addLadderTankBrackets(group, radius, height, radial, tangent, centerBase, cageMaterial);
}

function addVerticalLadderIntermediatePlatform(group, radius, height, radial, tangent, platformMaterial, railMaterial, centerBase, railHalfWidth) {
    const platformY = height * 0.46;

    if (platformY <= 0.8) return;

    const width = Math.max(railHalfWidth * 4.2, 1.55);
    const depth = Math.max(railHalfWidth * 2.8, 1.05);
    const thickness = Math.max(radius * 0.008, 0.055);

    const center = centerBase.clone().add(radial.clone().multiplyScalar(depth * 0.75));
    center.y = platformY;

    const platform = new THREE.Mesh(
        new THREE.BoxGeometry(width, thickness, depth),
        platformMaterial
    );

    platform.position.copy(center);
    platform.rotation.y = -Math.atan2(radial.z, radial.x) + Math.PI / 2;
    platform.castShadow = true;
    platform.receiveShadow = true;

    platform.userData = {
        tipo: "Plataforma intermedia de escalera",
        material: "Acero galvanizado",
        altura: formatTechnicalValue(platformY, "u.3D"),
        espesor: "—",
        diametro: "—"
    };

    group.add(platform);

    const railHeight = Math.max(radius * 0.075, 0.72);
    const railRadius = Math.max(radius * 0.004, 0.020);

    addPlatformRails(
        group,
        center,
        radial,
        tangent,
        width,
        depth,
        platformY,
        thickness,
        railHeight,
        railRadius,
        railMaterial,
        {
            openBack: true,
            openFront: false
        }
    );

    const supportRadius = Math.max(radius * 0.006, 0.025);

    const leftOuter = center.clone()
        .add(tangent.clone().multiplyScalar(-width * 0.38))
        .add(radial.clone().multiplyScalar(depth * 0.35));

    const rightOuter = center.clone()
        .add(tangent.clone().multiplyScalar(width * 0.38))
        .add(radial.clone().multiplyScalar(depth * 0.35));

    const leftBottom = centerBase.clone()
        .add(tangent.clone().multiplyScalar(-width * 0.32))
        .add(radial.clone().multiplyScalar(-depth * 0.05));

    const rightBottom = centerBase.clone()
        .add(tangent.clone().multiplyScalar(width * 0.32))
        .add(radial.clone().multiplyScalar(-depth * 0.05));

    leftOuter.y = platformY - thickness;
    rightOuter.y = platformY - thickness;
    leftBottom.y = platformY - Math.max(radius * 0.22, 1.25);
    rightBottom.y = platformY - Math.max(radius * 0.22, 1.25);

    addCylinderBetween(group, leftBottom, leftOuter, supportRadius, railMaterial, 10);
    addCylinderBetween(group, rightBottom, rightOuter, supportRadius, railMaterial, 10);

    const tankBracket = radial.clone().multiplyScalar(radius * 1.002);
    tankBracket.y = platformY;

    addCylinderBetween(group, tankBracket, center, supportRadius * 0.85, railMaterial, 8);
}
function addCircularLadderCage(group, radius, height, radial, tangent, centerBase, material, scale) {
    const cageRadius = Math.max(radius * 0.068, 0.50);
    const tubeRadius = Math.max(radius * 0.0024, 0.012);
    const cageCenter = centerBase.clone().add(radial.clone().multiplyScalar(cageRadius * 0.78));

    const startY = scale && scale > 0
        ? Math.max(2.20 * scale, height * 0.10)
        : height * 0.14;

    const endY = height + Math.max(radius * 0.075, 0.55);
    if (endY <= startY) return;

    const ringSpacing = scale && scale > 0
        ? Math.max(0.52, Math.min(0.90 * scale, 1.05))
        : Math.max(radius * 0.10, 0.62);

    const ringCount = Math.max(5, Math.ceil((endY - startY) / ringSpacing));

    for (let i = 0; i <= ringCount; i++) {
        const y = startY + ((endY - startY) * i) / ringCount;
        addCageOpenHoop(group, cageCenter, radial, tangent, cageRadius, y, tubeRadius, material);
    }

    const barAngles = [
        -Math.PI * 0.68,
        -Math.PI * 0.42,
        -Math.PI * 0.22,
        Math.PI * 0.22,
        Math.PI * 0.42,
        Math.PI * 0.68
    ];

    barAngles.forEach(a => {
        const offset = radial.clone().multiplyScalar(Math.cos(a) * cageRadius)
            .add(tangent.clone().multiplyScalar(Math.sin(a) * cageRadius));

        const bottom = cageCenter.clone().add(offset);
        bottom.y = startY;

        const top = cageCenter.clone().add(offset);
        top.y = endY;

        addCylinderBetween(group, bottom, top, tubeRadius, material, 8);
    });
}

function addCageOpenHoop(group, cageCenter, radial, tangent, cageRadius, y, tubeRadius, material) {
    const segments = 18;

    const arcs = [
        [-Math.PI * 0.78, -Math.PI * 0.18],
        [Math.PI * 0.18, Math.PI * 0.78]
    ];

    arcs.forEach(([startAngle, endAngle]) => {
        const points = [];

        for (let i = 0; i <= segments; i++) {
            const a = startAngle + ((endAngle - startAngle) * i) / segments;

            const point = cageCenter.clone()
                .add(radial.clone().multiplyScalar(Math.cos(a) * cageRadius))
                .add(tangent.clone().multiplyScalar(Math.sin(a) * cageRadius));

            point.y = y;
            points.push(point);
        }

        for (let i = 0; i < points.length - 1; i++) {
            addCylinderBetween(group, points[i], points[i + 1], tubeRadius, material, 8);
        }
    });
}

function addSimpleRestPlatforms(group, radius, height, radial, tangent, centerBase, platformMaterial, railMaterial, scale, railHalfWidth) {
    return;
}

function addCageCircle(group, cageCenter, radial, tangent, cageRadius, y, tubeRadius, material) {
    addCageOpenHoop(group, cageCenter, radial, tangent, cageRadius, y, tubeRadius, material);
}

function addCageRing(group, cageCenter, radial, tangent, cageRadius, y, tubeRadius, material) {
    addCageOpenHoop(group, cageCenter, radial, tangent, cageRadius, y, tubeRadius, material);
}

function addVerticalLadderSideHandrails(group, radius, height, radial, tangent, centerBase, material) {
    return;
}

function addVerticalLadderTopPlatform(group, radius, height, radial, tangent, platformMaterial, railMaterial, centerBase, railHalfWidth) {
    const width = Math.max(railHalfWidth * 3.2, 1.10);
    const depth = Math.max(railHalfWidth * 2.10, 0.80);
    const thickness = Math.max(radius * 0.006, 0.040);

    const center = centerBase.clone().add(radial.clone().multiplyScalar(depth * 0.55));
    center.y = height + thickness * 1.5;

    const platform = new THREE.Mesh(
        new THREE.BoxGeometry(width, thickness, depth),
        platformMaterial
    );

    platform.position.copy(center);
    platform.rotation.y = -Math.atan2(radial.z, radial.x) + Math.PI / 2;
    platform.castShadow = true;
    platform.receiveShadow = true;
    group.add(platform);

    const railHeight = Math.max(radius * 0.070, 0.65);
    const railRadius = Math.max(radius * 0.0034, 0.018);

    addPlatformRails(group, center, radial, tangent, width, depth, height, thickness, railHeight, railRadius, railMaterial, {
        openBack: true,
        openFront: false
    });
}

function angleFromRadial(radial) {
    return Math.atan2(radial.z, radial.x);
}

function addLadderTankBrackets(group, radius, height, radial, tangent, centerBase, material) {
    const bracketRadius = Math.max(radius * 0.0035, 0.018);
    const bracketCount = Math.max(6, Math.floor(height / Math.max(radius * 0.18, 1.0)));

    for (let i = 0; i <= bracketCount; i++) {
        const y = (height * i) / bracketCount;

        const wallPoint = radial.clone().multiplyScalar(radius * 1.002);
        wallPoint.y = y;

        const ladderPoint = centerBase.clone();
        ladderPoint.y = y;

        addCylinderBetween(group, wallPoint, ladderPoint, bracketRadius, material, 8);
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

    const turns = Math.max(0.55, height / Math.max(radius * 6.25, 1));
    const steps = Math.max(48, Math.floor(turns * 56));

    const stepWidth = Math.max(radius * 0.155, 0.90);
    const stepDepth = Math.max(radius * 0.060, 0.30);
    const stepHeight = Math.max(radius * 0.012, 0.055);

    const railHeight = Math.max(radius * 0.105, 0.95);
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
    connectPath(group, innerStringer, stringerRadius * 0.85, railMaterial, 10);

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
        label: "Drenaje"
    });

    addNozzle(group, radius, height, {
        angle: Math.PI * 1.32,
        y: height * 0.34,
        size: 1.0,
        label: "Salida"
    });

    addNozzle(group, radius, height, {
        angle: Math.PI * 1.46,
        y: height * 0.72,
        size: 0.8,
        label: "Entrada"
    });

    addNozzle(group, radius, height, {
        angle: Math.PI * 1.62,
        y: height * 0.88,
        size: 0.55,
        label: "Rebosadero"
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
    flange.userData = {
        tipo: options.label || "Boquilla",
        material: "Acero",
        altura: formatTechnicalValue(y, "u.3D"),
        espesor: "—",
        diametro: formatTechnicalValue(nozzleRadius * 2, "u.3D")
    };
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

function addPlatformRails(group, center, radial, tangent, width, depth, y, thickness, railHeight, railRadius, material, options = {}) {
    const p1 = center.clone().add(tangent.clone().multiplyScalar(-width / 2)).add(radial.clone().multiplyScalar(depth / 2));
    const p2 = center.clone().add(tangent.clone().multiplyScalar(width / 2)).add(radial.clone().multiplyScalar(depth / 2));
    const p3 = center.clone().add(tangent.clone().multiplyScalar(-width / 2)).add(radial.clone().multiplyScalar(-depth / 2));
    const p4 = center.clone().add(tangent.clone().multiplyScalar(width / 2)).add(radial.clone().multiplyScalar(-depth / 2));

    const posts = [p1, p2, p3, p4];
    const tops = [];

    posts.forEach(p => {
        const bottom = p.clone();
        bottom.y = y + thickness;

        const top = p.clone();
        top.y = bottom.y + railHeight;
        tops.push(top);

        addCylinderBetween(group, bottom, top, railRadius, material, 8);
    });

    if (!options.openFront) addCylinderBetween(group, tops[0], tops[1], railRadius, material, 8);
    if (!options.openBack) addCylinderBetween(group, tops[2], tops[3], railRadius, material, 8);

    addCylinderBetween(group, tops[0], tops[2], railRadius, material, 8);
    addCylinderBetween(group, tops[1], tops[3], railRadius, material, 8);
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

function bindTechnicalHover(viewer) {
    const raycaster = new THREE.Raycaster();
    const mouse = new THREE.Vector2();
    const overlay = viewer.shell.querySelector("#tank3d-tech-overlay");

    if (!overlay) return;

    viewer.renderer.domElement.addEventListener("mousemove", event => {
        const rect = viewer.renderer.domElement.getBoundingClientRect();

        mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
        mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

        raycaster.setFromCamera(mouse, viewer.camera);

        const intersects = raycaster.intersectObjects(viewer.group.children, true);
        const valid = intersects.find(x => x.object.userData && x.object.userData.tipo);

        if (!valid) {
            overlay.style.display = "none";
            return;
        }

        const data = valid.object.userData;

        overlay.innerHTML = `
            <strong>${data.tipo}</strong><br>
            Material: ${data.material || "—"}<br>
            Altura: ${data.altura || "—"}<br>
            Espesor: ${data.espesor || "—"}<br>
            Diámetro: ${data.diametro || "—"}
        `;

        overlay.style.display = "block";
    });

    viewer.renderer.domElement.addEventListener("mouseleave", () => {
        overlay.style.display = "none";
    });
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
    const height = Math.max(740, rect.height || 740);

    viewer.camera.aspect = width / height;
    viewer.camera.updateProjectionMatrix();
    viewer.renderer.setSize(width, height, false);
}

function animate(viewer) {
    viewer.animationId = requestAnimationFrame(() => animate(viewer));

    if (!viewer.renderer || !viewer.scene || !viewer.camera) return;

    viewer.renderer.render(viewer.scene, viewer.camera);
}
function fitCamera(viewer) {
    const height = viewer.modelHeight || 40;
    const radius = viewer.modelRadius || 20;
    const maxSize = Math.max(height, radius * 2, 1);

    viewer.distance = maxSize * 1.85;
    viewer.target.set(0, height * 0.03, 0);
    viewer.pitch = 0.22;
    viewer.yaw = 0.72;

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

    if (normalized.includes("HSLA")) return 0x6b7280;
    if (normalized.includes("S355")) return 0x52616f;
    if (normalized.includes("S275")) return 0x5f6f7f;
    if (normalized.includes("S235")) return 0x7b8794;
    if (normalized.includes("GLASS") || normalized.includes("VITR")) return 0x5f7f7a;

    return 0x667788;
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