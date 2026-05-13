const viewers = new WeakMap();

const technicalViewState = {
    showRoof: true,
    showGuardrail: false,
    showConnections: true,
    showLadder: true,
    showReferences: false,
    showWater: true
};

// --- SISTEMA INTERNO DE MATERIALES PBR INDUSTRIALES ---
const getMaterial = (type, customColor = null) => {
    switch (type) {
        case 'steel':
            return new THREE.MeshStandardMaterial({
                color: customColor || 0x71717a,
                metalness: 0.75,
                roughness: 0.28,
                side: THREE.DoubleSide
            });
        case 'starter':
            return new THREE.MeshStandardMaterial({
                color: 0x3f3f46, // Gris oscuro pesado de cimentación/arranque
                metalness: 0.80,
                roughness: 0.35,
                side: THREE.DoubleSide
            });
        case 'galvanized':
            return new THREE.MeshStandardMaterial({
                color: 0xd4d4d8, // Galvanizado limpio
                metalness: 0.85,
                roughness: 0.20,
                side: THREE.DoubleSide
            });
        case 'safety':
            return new THREE.MeshStandardMaterial({
                color: 0xeab308, // Amarillo seguridad industrial (OSHA)
                metalness: 0.50,
                roughness: 0.30
            });
        case 'darkSteel':
            return new THREE.MeshStandardMaterial({
                color: 0x27272a, // Acero al carbono pavonado/estructural
                metalness: 0.85,
                roughness: 0.25
            });
        case 'bolt':
            return new THREE.MeshStandardMaterial({
                color: 0x18181b, // Tornillería pavonada de alta resistencia
                metalness: 0.90,
                roughness: 0.20
            });
        case 'water':
            return new THREE.MeshStandardMaterial({
                color: 0x0284c7, // Agua técnica tratada
                metalness: 0.05,
                roughness: 0.10,
                transparent: true,
                opacity: 0.35,
                side: THREE.DoubleSide
            });
        case 'foundation':
            return new THREE.MeshStandardMaterial({
                color: 0x52525b,
                metalness: 0.15,
                roughness: 0.85,
                side: THREE.DoubleSide
            });
        case 'rafter':
            return new THREE.MeshStandardMaterial({
                color: 0x991b1b, // Imprimación industrial roja para vigas
                metalness: 0.60,
                roughness: 0.35
            });
        default:
            return new THREE.MeshStandardMaterial({ color: 0x71717a });
    }
};

function renderTank3D(container, tank, dotNetRef) {
    if (!container) return;

    disposeViewer(container);
    container.innerHTML = "";
    container.style.minHeight = "720px";
    container.style.height = "720px";
    container.style.overflow = "visible";
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

    // Initial render
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
    shell.style.height = "720px";
    shell.style.minHeight = "720px";
    shell.style.borderRadius = "16px"; // Look SaaS más técnico y moderno
    shell.style.overflow = "hidden";
    // Fondo premium técnico limpio (SaaS industrial)
    shell.style.background = `
        radial-gradient(circle at 50% 30%,
        #ffffff 0%,
        #f1f5f9 60%,
        #e2e8f0 100%)
    `;

    container.appendChild(shell);

    const scene = new THREE.Scene();
    scene.background = null;

    // FOV cerrado (30) para aspecto técnico cercano a proyección isométrica CAD
    const camera = new THREE.PerspectiveCamera(30, 1, 0.1, 10000);

    const renderer = new THREE.WebGLRenderer({
        antialias: true,
        alpha: true,
        preserveDrawingBuffer: true,
        powerPreference: "high-performance"
    });

    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.15; // Contraste nítido sobre metales

    shell.appendChild(renderer.domElement);

    const group = new THREE.Group();
    scene.add(group);

    // --- ILUMINACIÓN CAD DE ESTUDIO INDUSTRIAL ---
    scene.add(new THREE.AmbientLight(0xffffff, 0.6));

    const keyLight = new THREE.DirectionalLight(0xfffaf0, 1.8);
    keyLight.position.set(50, 60, 50);
    keyLight.castShadow = true;
    keyLight.shadow.mapSize.width = 4096;
    keyLight.shadow.mapSize.height = 4096;
    keyLight.shadow.camera.near = 1;
    keyLight.shadow.camera.far = 300;
    const sCamSize = 60;
    keyLight.shadow.camera.left = -sCamSize;
    keyLight.shadow.camera.right = sCamSize;
    keyLight.shadow.camera.top = sCamSize;
    keyLight.shadow.camera.bottom = -sCamSize;
    keyLight.shadow.bias = -0.0005;
    keyLight.shadow.radius = 2.0;
    scene.add(keyLight);

    const fillLight = new THREE.DirectionalLight(0xe0f2fe, 0.8);
    fillLight.position.set(-50, 20, -20);
    scene.add(fillLight);

    const rimLight = new THREE.DirectionalLight(0xffffff, 1.5);
    rimLight.position.set(0, 10, -80);
    scene.add(rimLight);

    const kickLight = new THREE.DirectionalLight(0xffffff, 0.5);
    kickLight.position.set(60, -10, -30);
    scene.add(kickLight);

    // --- ENTORNO PMREM PARA REFLEJOS METÁLICOS ---
    const pmremGenerator = new THREE.PMREMGenerator(renderer);
    pmremGenerator.compileEquirectangularShader();

    const generateStudioScene = () => {
        const studioScene = new THREE.Scene();
        studioScene.background = new THREE.Color(0xf8fafc);
        for (let i = 0; i < 3; i++) {
            const lMesh = new THREE.Mesh(
                new THREE.PlaneGeometry(50, 100),
                new THREE.MeshBasicMaterial({ color: 0xffffff })
            );
            lMesh.position.set((i - 1) * 80, 60, 100);
            lMesh.lookAt(0, 0, 0);
            studioScene.add(lMesh);
        }
        return studioScene;
    };

    const studioEnv = generateStudioScene();
    scene.environment = pmremGenerator.fromScene(studioEnv).texture;
    studioEnv.traverse(child => { if (child.geometry) child.geometry.dispose(); });

    // Plano de sombra de contacto en el suelo
    const groundShadowGeo = new THREE.PlaneGeometry(200, 200);
    const groundShadowMat = new THREE.ShadowMaterial({ opacity: 0.12 });
    const groundShadow = new THREE.Mesh(groundShadowGeo, groundShadowMat);
    groundShadow.rotation.x = -Math.PI / 2;
    groundShadow.position.y = -0.05;
    groundShadow.receiveShadow = true;
    scene.add(groundShadow);

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
        inputYaw: 0.75,
        inputPitch: 0.35,
        inputDistance: 85,
        yaw: 0.75,
        pitch: 0.35,
        distance: 85,
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
        <span style="opacity:0.6;margin-right:4px;">🖱️</span>
        <strong style="margin-right:12px;">Control CAD</strong>
        <span style="color:#94a3b8;margin-right:12px;">Orbitar: Clic izq. + arrastrar</span>
        <span style="color:#94a3b8;margin-right:12px;">Zoom: Rueda</span>
        <span style="color:#94a3b8;">Inspección: Puntero sobre piezas</span>
    `;

    panel.style.position = "absolute";
    panel.style.left = "50%";
    panel.style.bottom = "16px";
    panel.style.transform = "translateX(-50%)";
    panel.style.zIndex = "12";
    panel.style.display = "flex";
    panel.style.justifyContent = "center";
    panel.style.alignItems = "center";
    panel.style.width = "auto";
    panel.style.padding = "8px 16px";
    panel.style.borderRadius = "8px";
    panel.style.background = "#0f172a";
    panel.style.border = "1px solid #334155";
    panel.style.boxShadow = "0 4px 6px -1px rgba(0,0,0,0.2)";
    panel.style.color = "#ffffff";
    panel.style.font = "normal 11px 'Inter', 'Segoe UI', sans-serif";
    panel.style.pointerEvents = "none";
    panel.style.whiteSpace = "nowrap";

    shell.appendChild(panel);
}

function addScaleBadge(shell, metersPerUnit, tank) {
    const vigas = tank.vigasTechoConico && tank.vigasTechoConico.aplica === true
        ? `<br>Vigas radiales: <strong style="color:#38bdf8">${tank.vigasTechoConico.numeroVigas || 0}</strong>`
        : "";

    const escalera = tank.escalera && tank.escalera.tipo
        ? `<br>Escalera: <strong style="color:#eab308">${tank.escalera.tipo}</strong>`
        : "";

    const scaleBadge = document.createElement("div");
    scaleBadge.innerHTML = `
        <div style="font-size:10px;text-transform:uppercase;letter-spacing:1px;color:#94a3b8;margin-bottom:4px;">Sistema Referencia</div>
        <strong>Escala Automática</strong><br>
        <span style="color:#cbd5e1">1 u. 3D = ${metersPerUnit.toFixed(2)} m</span><br>
        Techo: ${normalizarTecho(tank.techo).label}
        ${vigas}
        ${escalera}
    `;
    scaleBadge.style.position = "absolute";
    scaleBadge.style.right = "16px";
    scaleBadge.style.bottom = "16px";
    scaleBadge.style.zIndex = "5";
    scaleBadge.style.padding = "12px 14px";
    scaleBadge.style.borderRadius = "8px";
    scaleBadge.style.background = "#0f172a";
    scaleBadge.style.border = "1px solid #334155";
    scaleBadge.style.boxShadow = "0 4px 6px -1px rgba(0,0,0,0.2)";
    scaleBadge.style.color = "#ffffff";
    scaleBadge.style.font = "normal 12px/1.4 'Inter', 'Segoe UI', sans-serif";
    shell.appendChild(scaleBadge);
}

function addRoofControls(shell, container, tank, dotNetRef) {
    const roof = normalizarTecho(tank.techo);
    if (roof.type !== "cone") return;

    if (!tank.vigasTechoConico) {
        tank.vigasTechoConico = { aplica: true, numeroVigas: 16 };
    }

    const currentBeamCount = Math.max(0, Number(tank.vigasTechoConico.numeroVigas) || 0);
    const currentHubPercent = Math.round(Number(tank.vigasTechoConico.factorNucleo3D || 0.15) * 100);

    const panel = document.createElement("div");
    panel.style.position = "absolute";
    panel.style.left = "16px";
    panel.style.top = "16px";
    panel.style.zIndex = "6";
    panel.style.width = "210px";
    panel.style.padding = "14px";
    panel.style.borderRadius = "8px";
    panel.style.background = "#0f172a";
    panel.style.border = "1px solid #334155";
    panel.style.color = "#ffffff";
    panel.style.font = "normal 12px 'Inter', 'Segoe UI', sans-serif";
    panel.style.boxShadow = "0 4px 6px -1px rgba(0,0,0,0.2)";

    panel.innerHTML = `
        <div style="font-weight:bold;font-size:13px;margin-bottom:10px;color:#38bdf8;">
            ⚙️ Parámetros Cubierta
        </div>
        <label style="display:block;margin-bottom:4px;color:#94a3b8;">Número de vigas</label>
        <input data-tank3d-beams type="number" min="0" max="160" step="1" value="${currentBeamCount}" style="width:100%;box-sizing:border-box;border-radius:4px;border:1px solid #475569;background:#1e293b;color:white;padding:6px 8px;margin-bottom:10px;font-family:inherit;font-size:12px;">
        <label style="display:flex;justify-content:space-between;margin-bottom:4px;color:#94a3b8;">
            <span>Factor Núcleo</span>
            <strong data-tank3d-hub-label style="color:#38bdf8;">${currentHubPercent}%</strong>
        </label>
        <input data-tank3d-hub type="range" min="8" max="32" step="1" value="${currentHubPercent}" style="width:100%;cursor:pointer;">
    `;

    const beamsInput = panel.querySelector("[data-tank3d-beams]");
    const hubInput = panel.querySelector("[data-tank3d-hub]");
    const hubLabel = panel.querySelector("[data-tank3d-hub-label]");

    const applyChanges = () => {
        const beams = Math.max(0, Math.min(160, Math.floor(Number(beamsInput.value) || 0)));
        const hubPercent = Math.max(8, Math.min(32, Number(hubInput.value) || 15));

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
    hubInput.addEventListener("input", () => { hubLabel.textContent = `${hubInput.value}%`; });
    hubInput.addEventListener("change", applyChanges);

    shell.appendChild(panel);
}

function addTechnicalControls(shell, container, tank, dotNetRef) {
    const panel = document.createElement("div");

    panel.style.position = "absolute";
    panel.style.left = "16px";
    panel.style.bottom = "16px";
    panel.style.zIndex = "9";
    panel.style.width = "170px";
    panel.style.padding = "14px";
    panel.style.borderRadius = "8px";
    panel.style.background = "#0f172a";
    panel.style.border = "1px solid #334155";
    panel.style.color = "white";
    panel.style.font = "normal 12px 'Inter', 'Segoe UI', sans-serif";
    panel.style.boxShadow = "0 4px 6px -1px rgba(0,0,0,0.2)";

    panel.innerHTML = `
        <div style="font-weight:bold;font-size:13px;margin-bottom:10px;color:#38bdf8;">
            👁️ Capas Visibles
        </div>
        <div style="display:grid;gap:8px;">
            ${createTechnicalCheckbox("Techo", "showRoof")}
            ${createTechnicalCheckbox("Barandilla", "showGuardrail")}
            ${createTechnicalCheckbox("Conexiones", "showConnections")}
            ${createTechnicalCheckbox("Escalera", "showLadder")}
            ${createTechnicalCheckbox("Líquido", "showWater")}
            ${createTechnicalCheckbox("Referencias CAD", "showReferences")}
        </div>
    `;

    const styleTag = document.createElement('style');
    styleTag.textContent = `
        .tank3d-checkbox {
            appearance: none;
            width: 14px; height: 14px;
            border-radius: 3px;
            border: 1px solid #64748b;
            background: #1e293b;
            cursor: pointer;
            position: relative;
        }
        .tank3d-checkbox:checked {
            background: #2563eb;
            border-color: #2563eb;
        }
        .tank3d-checkbox:checked::after {
            content: '✓';
            position: absolute;
            color: white; font-size: 10px; font-weight:bold;
            top: 50%; left: 50%; transform: translate(-50%, -50%);
        }
    `;
    shell.appendChild(styleTag);

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
        <label style="display:flex;align-items:center;gap:8px;cursor:pointer;user-select:none;color:#cbd5e1;">
            <input type="checkbox" class="tank3d-checkbox" data-key="${key}" ${technicalViewState[key] ? "checked" : ""}>
            <span>${label}</span>
        </label>
    `;
}

function addTechnicalInfoOverlay(shell) {
    const overlay = document.createElement("div");

    overlay.id = "tank3d-tech-overlay";
    overlay.style.position = "absolute";
    overlay.style.left = "50%";
    overlay.style.top = "16px";
    overlay.style.transform = "translateX(-50%)";
    overlay.style.padding = "10px 14px";
    overlay.style.borderRadius = "8px";
    overlay.style.background = "rgba(255,255,255,0.95)";
    overlay.style.color = "#0f172a";
    overlay.style.font = "normal 12px 'Inter', 'Segoe UI', sans-serif";
    overlay.style.zIndex = "20";
    overlay.style.pointerEvents = "none";
    overlay.style.display = "none";
    overlay.style.lineHeight = "1.5";
    overlay.style.boxShadow = "0 10px 25px -5px rgba(0,0,0,0.1)";
    overlay.style.border = "1px solid #cbd5e1";

    shell.appendChild(overlay);
}

function addDownloadPngButton(shell, renderer) {
    const button = document.createElement("button");

    button.type = "button";
    button.innerHTML = `📥 Exportar Plano (PNG)`;
    button.style.position = "absolute";
    button.style.right = "16px";
    button.style.top = "16px";
    button.style.zIndex = "8";
    button.style.border = "1px solid #cbd5e1";
    button.style.borderRadius = "6px";
    button.style.padding = "8px 12px";
    button.style.background = "#ffffff";
    button.style.color = "#0f172a";
    button.style.font = "bold 12px 'Inter', 'Segoe UI', sans-serif";
    button.style.cursor = "pointer";
    button.style.boxShadow = "0 2px 4px rgba(0,0,0,0.05)";

    button.addEventListener("click", () => {
        renderer.render(renderer.__tank3dScene, renderer.__tank3dCamera);
        const link = document.createElement("a");
        link.download = `Tanque-Render-DL2.png`;
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
        const materialName = ring.material || tank.materialPrincipal || "Acero";

        const baseColor = colorForMaterial(materialName);
        const altColor = index % 2 === 0 ? baseColor : (baseColor - 0x050505);
        const shellMaterial = getMaterial('steel', altColor);

        const shellSegments = radius > 12 ? 128 : 96;
        const shellGeometry = new THREE.CylinderGeometry(radius, radius, height, shellSegments, 1, true);

        const shell = new THREE.Mesh(shellGeometry, shellMaterial);
        shell.position.y = currentY + height / 2;
        shell.castShadow = true;
        shell.receiveShadow = true;

        shell.userData = {
            tipo: `Virola Estructural ${index + 1}`,
            material: materialName,
            altura: formatTechnicalValue(ring.altura, "m"),
            espesor: formatTechnicalValue(ring.espesor, "mm"),
            diametro: formatTechnicalValue(tank.diametro, "m")
        };

        viewer.group.add(shell);

        addRingSeam(viewer.group, radius, currentY);
        addRingSeam(viewer.group, radius, currentY + height);

        currentY += height;
    });

    addBottomDisc(viewer.group, radius);

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

    addManhole(viewer.group, radius, starterHeight);
    addRoofVent(viewer.group, radius, currentY, tank.techo);

    if (technicalViewState.showReferences) {
        addReferenceGrid(viewer.group, radius, currentY);
        addVerticalReference(viewer.group, radius, currentY);
    }

    if (technicalViewState.showLadder) {
        addLadder(viewer.group, radius, currentY, tank.escalera, scale);
    }

    viewer.group.position.y = (-currentY / 2);
    viewer.modelRadius = radius;
    viewer.modelHeight = currentY;
}

function getStarterRingHeight(tank, scale) {
    const rawHeight = getStarterRingHeightMm(tank);
    return rawHeight > 50 ? (rawHeight * scale) / 1000 : rawHeight * scale;
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
    const starterRadius = radius * 1.008;
    const material = getMaterial('starter');

    const starter = new THREE.Mesh(
        new THREE.CylinderGeometry(starterRadius, starterRadius, height, 96, 1, false),
        material
    );

    starter.position.y = height / 2;
    starter.castShadow = true;
    starter.receiveShadow = true;

    starter.userData = {
        tipo: "Starter Ring (Anillo Base)",
        material: "Acero / Embebido",
        altura: `${realHeightMm} mm`,
        espesor: "Reforzado base",
        diametro: formatTechnicalValue((radius * 2), "m")
    };

    group.add(starter);
    addRingSeam(group, starterRadius, height);

    return height;
}

function formatTechnicalValue(value, suffix) {
    const n = Number(value);
    if (!Number.isFinite(n) || n <= 0) return "—";
    return `${n.toFixed(2)} ${suffix}`;
}

function addRoofGuardrail(group, radius, height, roofRaw) {
    if (normalizarTecho(roofRaw).type === "none") return;

    const material = getMaterial('safety');
    const postRadius = Math.max(radius * 0.005, 0.02);
    const railRadius = Math.max(radius * 0.004, 0.015);
    const railHeight = 1.0;
    const midRailHeight = 0.5;

    const railRadiusPosition = radius * 0.98;
    const postCount = 32;

    let prevTop = null, prevMid = null, firstTop = null, firstMid = null;

    for (let i = 0; i < postCount; i++) {
        const angle = (Math.PI * 2 * i) / postCount;
        const pt = new THREE.Vector3(Math.cos(angle) * railRadiusPosition, height, Math.sin(angle) * railRadiusPosition);
        const top = pt.clone(); top.y += railHeight;
        const mid = pt.clone(); mid.y += midRailHeight;

        addCylinderBetween(group, pt, top, postRadius, material, 6);

        if (prevTop) {
            addCylinderBetween(group, prevTop, top, railRadius, material, 6);
            addCylinderBetween(group, prevMid, mid, railRadius * 0.8, material, 6);
        } else {
            firstTop = top; firstMid = mid;
        }
        prevTop = top; prevMid = mid;
    }
    addCylinderBetween(group, prevTop, firstTop, railRadius, material, 6);
    addCylinderBetween(group, prevMid, firstMid, railRadius * 0.8, material, 6);

    addRoofAccessGate(group, radius, height, material);
}

function addRoofAccessGate(group, radius, height, material) {
    const angle = -Math.PI / 2;
    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));

    const gateWidth = 0.60;
    const gatePos = radial.clone().multiplyScalar(radius * 0.98); gatePos.y = height;

    const pLeft = gatePos.clone().add(tangent.clone().multiplyScalar(-gateWidth / 2));
    const pRight = gatePos.clone().add(tangent.clone().multiplyScalar(gateWidth / 2));

    const topL = pLeft.clone(); topL.y += 0.8;
    const topR = pRight.clone(); topR.y += 0.8;
    addCylinderBetween(group, topL, topR, 0.008, getMaterial('darkSteel'), 6);
}

function addCircularRail(group, radius, y, tubeRadius, material) {
    const segments = 64;
    const points = [];
    for (let i = 0; i <= segments; i++) {
        const angle = (Math.PI * 2 * i) / segments;
        points.push(new THREE.Vector3(Math.cos(angle) * radius, y, Math.sin(angle) * radius));
    }
    for (let i = 0; i < points.length - 1; i++) {
        addCylinderBetween(group, points[i], points[i + 1], tubeRadius, material, 6);
    }
}

function addRoof(group, radius, height, roofRaw, vigasTechoConico, scale) {
    const roof = normalizarTecho(roofRaw);

    if (roof.type === "none") { addOpenTop(group, radius, height); return; }
    if (roof.type === "dome") { addDomeRoof(group, radius, height); return; }
    if (roof.type === "cone") { addConeRoof(group, radius, height, vigasTechoConico, scale); return; }
    addFlatRoof(group, radius, height);
}

function addManhole(group, radius, baseHeight) {
    const angle = Math.PI * 0.18;
    const y = Math.max(baseHeight + 0.4, 0.5);
    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));

    const manholeRadius = Math.max(radius * 0.07, 0.30);
    const neckLength = 0.12;
    const flangeThickness = 0.03;

    const neckCenter = radial.clone().multiplyScalar(radius + neckLength / 2); neckCenter.y = y;
    const neckMesh = new THREE.Mesh(new THREE.CylinderGeometry(manholeRadius, manholeRadius, neckLength, 32), getMaterial('darkSteel'));
    alignToWall(neckMesh, radial, neckCenter);
    group.add(neckMesh);

    const flangeRadius = manholeRadius * 1.35;
    const flangeCenter = radial.clone().multiplyScalar(radius + neckLength); flangeCenter.y = y;
    const flangeMesh = new THREE.Mesh(new THREE.CylinderGeometry(flangeRadius, flangeRadius, flangeThickness, 32), getMaterial('galvanized'));
    alignToWall(flangeMesh, radial, flangeCenter);
    flangeMesh.castShadow = true;

    flangeMesh.userData = {
        tipo: "Manhole Paso de Hombre (API 650)",
        material: "Acero Estructural Calidad Brida",
        altura: formatTechnicalValue(y, "u.3D"),
        diametro: formatTechnicalValue(manholeRadius * 2, "u.3D"),
        espesor: "Placa Ciega"
    };
    group.add(flangeMesh);

    const bolts = 16;
    const boltCircle = (manholeRadius + flangeRadius) / 2;
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));
    const vertical = new THREE.Vector3(0, 1, 0);

    for (let i = 0; i < bolts; i++) {
        const a = (Math.PI * 2 * i) / bolts;
        const bPos = flangeCenter.clone()
            .add(tangent.clone().multiplyScalar(Math.cos(a) * boltCircle))
            .add(vertical.clone().multiplyScalar(Math.sin(a) * boltCircle))
            .add(radial.clone().multiplyScalar(flangeThickness * 0.5));

        const boltMesh = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.012, flangeThickness * 1.8, 6), getMaterial('bolt'));
        alignToWall(boltMesh, radial, bPos);
        group.add(boltMesh);
    }
}

function alignToWall(mesh, radial, pos) {
    mesh.position.copy(pos);
    mesh.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), radial.clone().normalize());
    mesh.rotateZ(Math.PI / 2);
}

function addRoofVent(group, radius, height, roofRaw) {
    if (normalizarTecho(roofRaw).type === "none") return;

    const ventGroup = new THREE.Group();
    const pipe = new THREE.Mesh(new THREE.CylinderGeometry(0.12, 0.12, 0.4, 16), getMaterial('galvanized'));
    const cap = new THREE.Mesh(new THREE.ConeGeometry(0.22, 0.12, 16), getMaterial('darkSteel'));
    cap.position.y = 0.20;
    ventGroup.add(pipe); ventGroup.add(cap);

    ventGroup.position.set(radius * 0.4, height + 0.2, -radius * 0.2);
    ventGroup.children.forEach(c => c.castShadow = true);
    group.add(ventGroup);
}

function normalizarTecho(value) {
    const text = String(value || "None").trim();
    const t = text.toUpperCase();

    if (!t || t === "—" || t.includes("NONE") || t.includes("SIN") || t.includes("ABIERTO")) return { type: "none", label: "Sin techo / abierto" };
    if (t.includes("DOME") || t.includes("DOMO") || t.includes("CUPULA")) return { type: "dome", label: "Techo Domo Geodésico" };
    if (t.includes("CONE") || t.includes("CONO")) return { type: "cone", label: "Techo Cónico" };
    return { type: "flat", label: "Techo Plano" };
}

function addOpenTop(group, radius, height) {
    const torus = new THREE.Mesh(new THREE.TorusGeometry(radius, Math.max(radius * 0.01, 0.025), 12, 96), getMaterial('darkSteel'));
    torus.rotation.x = Math.PI / 2;
    torus.position.y = height;
    group.add(torus);
}

function addFlatRoof(group, radius, height) {
    const roof = new THREE.Mesh(new THREE.CircleGeometry(radius * 1.005, 96), getMaterial('galvanized'));
    roof.rotation.x = -Math.PI / 2;
    roof.position.y = height;
    roof.castShadow = true;
    roof.userData = { tipo: "Cubierta Plana Autoportante", material: "Chapa de Acero" };
    group.add(roof);
    addOpenTop(group, radius, height);
}

function addConeRoof(group, radius, height, vigasTechoConico, scale) {
    const alturaConoReal = vigasTechoConico && Number(vigasTechoConico.alturaCono) > 0 ? Number(vigasTechoConico.alturaCono) : 0;
    const roofHeight = alturaConoReal > 0 ? alturaConoReal * scale : Math.max(radius * 0.18, 1.0);

    const coneMat = new THREE.MeshStandardMaterial({
        color: 0xe2e8f0, metalness: 0.6, roughness: 0.3,
        transparent: true, opacity: 0.90, side: THREE.DoubleSide
    });
    const cone = new THREE.Mesh(new THREE.ConeGeometry(radius * 1.005, roofHeight, 96, 1, true), coneMat);
    cone.position.y = height + roofHeight / 2;
    cone.castShadow = true;
    cone.userData = { tipo: "Cubierta Cónica Soportada", material: "Acero al Carbono", altura: formatTechnicalValue(roofHeight, "m") };
    group.add(cone);

    const numeroVigas = vigasTechoConico?.aplica === true ? (Number(vigasTechoConico.numeroVigas) || 0) : 0;

    addOpenTop(group, radius, height);
    addConeRoofPanels(group, radius, height, roofHeight);
    addConeRoofRafters(group, radius, height, roofHeight, vigasTechoConico, scale);
    addConeRoofCenterHub(group, height + roofHeight, radius, numeroVigas, vigasTechoConico, scale);
}

function addConeRoofPanels(group, radius, baseHeight, roofHeight) {
    const material = new THREE.LineBasicMaterial({ color: 0x64748b, transparent: true, opacity: 0.4 });
    [0.4, 0.75].forEach(f => {
        const r = radius * f;
        const y = baseHeight + roofHeight * (1 - f);
        const pts = new THREE.EllipseCurve(0, 0, r, r, 0, Math.PI * 2, false, 0).getPoints(64).map(p => new THREE.Vector3(p.x, y, p.y));
        group.add(new THREE.LineLoop(new THREE.BufferGeometry().setFromPoints(pts), material));
    });
}

function addConeRoofRafters(group, radius, baseHeight, roofHeight, vigasTechoConico, scale) {
    if (!vigasTechoConico || vigasTechoConico.aplica !== true) return;
    const numeroVigas = Math.max(0, Number(vigasTechoConico.numeroVigas) || 0);
    if (numeroVigas <= 0) return;

    const beamMaterial = getMaterial('rafter');
    const hubRadius = calcularRadioNucleoTecho(radius, numeroVigas, vigasTechoConico, scale);
    const rafterRadius = Math.max(radius * 0.008, 0.03);

    for (let i = 0; i < numeroVigas; i++) {
        const angle = (Math.PI * 2 * i) / numeroVigas;
        const start = new THREE.Vector3(Math.cos(angle) * hubRadius, baseHeight + roofHeight - 0.1, Math.sin(angle) * hubRadius);
        const end = new THREE.Vector3(Math.cos(angle) * radius * 0.98, baseHeight + 0.05, Math.sin(angle) * radius * 0.98);
        addCylinderBetween(group, start, end, rafterRadius, beamMaterial, 6);
    }
}

function addConeRoofCenterHub(group, topY, radius, numeroVigas, vigasTechoConico, scale) {
    if (!vigasTechoConico || vigasTechoConico.aplica !== true || numeroVigas <= 0) return;
    const hubRadius = calcularRadioNucleoTecho(radius, numeroVigas, vigasTechoConico, scale);
    const hubHeight = 0.15;

    const hub = new THREE.Mesh(new THREE.CylinderGeometry(hubRadius, hubRadius, hubHeight, 32), getMaterial('darkSteel'));
    hub.position.y = topY - hubHeight;
    group.add(hub);
}

function calcularRadioNucleoTecho(radius, numeroVigas, vigasTechoConico, scale) {
    const manual = Number(vigasTechoConico?.diametroNucleoCentralManual) || Number(vigasTechoConico?.diametroNucleo) || 0;
    if (manual > 0 && scale > 0) return (manual * scale) / 2;
    const factor = Number(vigasTechoConico?.factorNucleo3D) || 0.15;
    return Math.max(radius * factor, 0.4);
}

function addDomeRoof(group, radius, height) {
    const domeHeight = Math.max(radius * 0.38, 1.25);
    const domeMesh = new THREE.Mesh(
        new THREE.SphereGeometry(radius * 1.01, 96, 24, 0, Math.PI * 2, 0, Math.PI / 2),
        getMaterial('galvanized')
    );
    domeMesh.scale.y = domeHeight / radius;
    domeMesh.position.y = height;
    domeMesh.castShadow = true;
    domeMesh.userData = { tipo: "Cúpula Geodésica de Aluminio", material: "Aluminio Estructural" };
    group.add(domeMesh);

    addDomeRoofRibs(group, radius, height, domeHeight);
    addDomeSkirtRing(group, radius, height);
}

function addDomeSkirtRing(group, radius, height) {
    const skirt = new THREE.Mesh(new THREE.CylinderGeometry(radius * 1.015, radius * 1.015, 0.25, 96, 1, true), getMaterial('darkSteel'));
    skirt.position.y = height + 0.125;
    group.add(skirt);
}

function addDomeRoofRibs(group, radius, height, domeHeight) {
    const ribMat = getMaterial('darkSteel');
    const ribRadius = Math.max(radius * 0.005, 0.02);
    const count = 24;
    for (let i = 0; i < count; i++) {
        const a = (Math.PI * 2 * i) / count;
        const start = new THREE.Vector3(Math.cos(a) * radius, height, Math.sin(a) * radius);
        const end = new THREE.Vector3(0, height + domeHeight, 0);
        addCylinderBetween(group, start, end, ribRadius, ribMat, 6);
    }
}

function addWaterLevelIfAvailable(group, radius, height, tank, scale) {
    const rawLevel = Number(tank?.nivelAgua || tank?.alturaAgua || 0);
    if (rawLevel <= 0) return;
    const y = Math.min(rawLevel * scale, height * 0.98);
    const disc = new THREE.Mesh(new THREE.CircleGeometry(radius * 0.99, 96), getMaterial('water'));
    disc.rotation.x = -Math.PI / 2;
    disc.position.y = y;
    disc.userData = { tipo: "Nivel de líquido de diseño", altura: `${rawLevel} m` };
    group.add(disc);
}

function addTopStiffener(group, radius, height) {
    const stiff = new THREE.Mesh(new THREE.CylinderGeometry(radius * 1.015, radius * 1.015, 0.1, 96, 1, true), getMaterial('darkSteel'));
    stiff.position.y = height - 0.05;
    group.add(stiff);
}

function addRingSeam(group, radius, y) {
    const pts = new THREE.EllipseCurve(0, 0, radius * 1.001, radius * 1.001, 0, Math.PI * 2, false, 0).getPoints(96).map(p => new THREE.Vector3(p.x, y, p.y));
    group.add(new THREE.LineLoop(new THREE.BufferGeometry().setFromPoints(pts), new THREE.LineBasicMaterial({ color: 0x0f172a, opacity: 0.5, transparent: true })));
}

function addBottomDisc(group, radius) {
    const disc = new THREE.Mesh(new THREE.CircleGeometry(radius * 1.05, 96), getMaterial('foundation'));
    disc.rotation.x = -Math.PI / 2;
    disc.position.y = -0.01;
    group.add(disc);
}

function addReferenceGrid(group, radius, height) {
    const size = Math.max(radius * 3, 25);
    const grid = new THREE.GridHelper(size, 25, 0x2563eb, 0x94a3b8);
    grid.position.y = -0.02;
    group.add(grid);
}

function addVerticalReference(group, radius, height) {
    const geo = new THREE.BufferGeometry().setFromPoints([
        new THREE.Vector3(radius * 1.25, 0, 0),
        new THREE.Vector3(radius * 1.25, height, 0)
    ]);
    group.add(new THREE.Line(geo, new THREE.LineBasicMaterial({ color: 0xdc2626, linewidth: 2 })));
}

function addLadder(group, radius, height, escalera, scale) {
    const tipo = String(escalera?.tipo || "").toUpperCase();
    if (!tipo || tipo.includes("SIN") || tipo.includes("NONE")) return;

    const angleOffset = -Math.PI / 2;

    if (tipo.includes("HELICOIDAL")) {
        addHelicalStair(group, radius, height, angleOffset);
    } else {
        addVerticalLadder(group, radius, height, angleOffset, scale);
    }
}

function addVerticalLadder(group, radius, height, angleOffset = 0, scale) {
    const ladderMaterial = getMaterial('safety');
    const cageMaterial = getMaterial('galvanized');
    const platformMaterial = getMaterial('darkSteel');

    const angle = angleOffset;
    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));

    const standoff = 0.20;
    const centerBase = radial.clone().multiplyScalar(radius + standoff);

    const railHalfWidth = 0.25;
    const railRadius = 0.025;
    const rungRadius = 0.012;

    const bottomY = 0.30;
    const topY = height + 1.0;

    const leftBottom = centerBase.clone().add(tangent.clone().multiplyScalar(-railHalfWidth)); leftBottom.y = bottomY;
    const leftTop = centerBase.clone().add(tangent.clone().multiplyScalar(-railHalfWidth)); leftTop.y = topY;
    const rightBottom = centerBase.clone().add(tangent.clone().multiplyScalar(railHalfWidth)); rightBottom.y = bottomY;
    const rightTop = centerBase.clone().add(tangent.clone().multiplyScalar(railHalfWidth)); rightTop.y = topY;

    addCylinderBetween(group, leftBottom, leftTop, railRadius, ladderMaterial, 8);
    addCylinderBetween(group, rightBottom, rightTop, railRadius, ladderMaterial, 8);

    const rungSpacing = 0.30;
    const rungCount = Math.floor((topY - bottomY) / rungSpacing);

    for (let i = 1; i <= rungCount; i++) {
        const y = bottomY + i * rungSpacing;
        const l = centerBase.clone().add(tangent.clone().multiplyScalar(-railHalfWidth)); l.y = y;
        const r = centerBase.clone().add(tangent.clone().multiplyScalar(railHalfWidth)); r.y = y;
        addCylinderBetween(group, l, r, rungRadius, cageMaterial, 6);
    }

    addCircularLadderCage(group, radius, height, radial, tangent, centerBase, cageMaterial, scale);
    addVerticalLadderIntermediatePlatform(group, radius, height, radial, tangent, platformMaterial, cageMaterial, centerBase, railHalfWidth);
    addVerticalLadderTopPlatform(group, radius, height, radial, tangent, platformMaterial, cageMaterial, centerBase, railHalfWidth);
    addLadderTankBrackets(group, radius, height, radial, tangent, centerBase, platformMaterial);
    addVerticalLadderSideHandrails(group, radius, height, radial, tangent, centerBase, ladderMaterial);
    addSimpleRestPlatforms(group, radius, height, radial, tangent, centerBase, platformMaterial, cageMaterial, scale, railHalfWidth);
}

function addVerticalLadderIntermediatePlatform(group, radius, height, radial, tangent, platformMaterial, railMaterial, centerBase, railHalfWidth) {
    const platformY = height * 0.5;
    if (height < 6.0) return;

    const platWidth = railHalfWidth * 3.5;
    const platDepth = 0.80;
    const platPos = centerBase.clone().add(radial.clone().multiplyScalar(platDepth * 0.4));
    platPos.y = platformY;

    const mesh = new THREE.Mesh(new THREE.BoxGeometry(platWidth, 0.04, platDepth), platformMaterial);
    mesh.position.copy(platPos);
    mesh.rotation.y = angleFromRadial(radial);
    group.add(mesh);

    const ancla = radial.clone().multiplyScalar(radius); ancla.y = platformY - 0.8;
    addCylinderBetween(group, platPos, ancla, 0.02, railMaterial, 6);
}

function addCircularLadderCage(group, radius, height, radial, tangent, centerBase, material, scale) {
    const cageStartY = 2.2;
    const topY = height + 1.0;
    if (topY <= cageStartY + 0.5) return;

    const cageRadius = 0.35;
    const cageCenter = centerBase.clone().add(radial.clone().multiplyScalar(cageRadius * 0.8));
    const hoopSpacing = 1.2;
    const hoopCount = Math.floor((topY - cageStartY) / hoopSpacing);

    for (let i = 0; i <= hoopCount; i++) {
        const y = cageStartY + i * hoopSpacing;
        addCageOpenHoop(group, cageCenter, radial, tangent, cageRadius, y, 0.012, material);
    }

    const strips = 5;
    for (let i = 0; i < strips; i++) {
        const a = -Math.PI * 0.8 + (Math.PI * 1.6 * i) / (strips - 1);
        const pB = cageCenter.clone().add(radial.clone().multiplyScalar(Math.cos(a) * cageRadius)).add(tangent.clone().multiplyScalar(Math.sin(a) * cageRadius));
        const pT = pB.clone(); pB.y = cageStartY; pT.y = topY;
        addCylinderBetween(group, pB, pT, 0.01, material, 6);
    }
}

function addCageOpenHoop(group, cageCenter, radial, tangent, cageRadius, y, tubeRadius, material) {
    const segments = 16;
    const points = [];
    for (let i = 0; i <= segments; i++) {
        const a = -Math.PI * 0.8 + (Math.PI * 1.6 * i) / segments;
        const p = cageCenter.clone().add(radial.clone().multiplyScalar(Math.cos(a) * cageRadius)).add(tangent.clone().multiplyScalar(Math.sin(a) * cageRadius));
        p.y = y;
        points.push(p);
    }
    connectPath(group, points, tubeRadius, material, 6);
}

function addSimpleRestPlatforms(group, radius, height, radial, tangent, centerBase, platformMaterial, railMaterial, scale, railHalfWidth) {
    if (height > 12.0) {
        const pY = height * 0.25;
        const mesh = new THREE.Mesh(new THREE.BoxGeometry(0.8, 0.03, 0.8), platformMaterial);
        const pos = centerBase.clone().add(radial.clone().multiplyScalar(0.3)); pos.y = pY;
        mesh.position.copy(pos);
        mesh.rotation.y = angleFromRadial(radial);
        group.add(mesh);
    }
}

function addCageCircle(group, cageCenter, radial, tangent, cageRadius, y, tubeRadius, material) {
    addCageOpenHoop(group, cageCenter, radial, tangent, cageRadius, y, tubeRadius, material);
}

function addCageRing(group, cageCenter, radial, tangent, cageRadius, y, tubeRadius, material) {
    addCageOpenHoop(group, cageCenter, radial, tangent, cageRadius, y, tubeRadius, material);
}

function addVerticalLadderSideHandrails(group, radius, height, radial, tangent, centerBase, material) {
    const leftTop = centerBase.clone().add(tangent.clone().multiplyScalar(-0.25)); leftTop.y = height + 1.0;
    const rightTop = centerBase.clone().add(tangent.clone().multiplyScalar(0.25)); rightTop.y = height + 1.0;

    const roofTargetL = radial.clone().multiplyScalar(radius * 0.95).add(tangent.clone().multiplyScalar(-0.25)); roofTargetL.y = height;
    const roofTargetR = radial.clone().multiplyScalar(radius * 0.95).add(tangent.clone().multiplyScalar(0.25)); roofTargetR.y = height;

    addCylinderBetween(group, leftTop, roofTargetL, 0.02, material, 6);
    addCylinderBetween(group, rightTop, roofTargetR, 0.02, material, 6);
}

function addVerticalLadderTopPlatform(group, radius, height, radial, tangent, platformMaterial, railMaterial, centerBase, railHalfWidth) {
    const width = 0.90;
    const depth = 0.70;
    const center = centerBase.clone().add(radial.clone().multiplyScalar(-0.1));
    center.y = height;

    const platform = new THREE.Mesh(new THREE.BoxGeometry(width, 0.04, depth), platformMaterial);
    platform.position.copy(center);
    platform.rotation.y = angleFromRadial(radial);
    platform.userData = { tipo: "Plataforma de Desembarco Superior", material: "Tramex" };
    group.add(platform);

    addPlatformRails(group, center, radial, tangent, width, depth, height, 0.04, 1.0, 0.02, railMaterial, { openFront: true });
}

function angleFromRadial(radial) {
    return -Math.atan2(radial.z, radial.x) + Math.PI / 2;
}

function addLadderTankBrackets(group, radius, height, radial, tangent, centerBase, material) {
    const count = Math.floor(height / 2.0);
    for (let i = 1; i <= count; i++) {
        const y = i * 2.0;
        if (y > height - 0.5) break;
        const pWall = radial.clone().multiplyScalar(radius); pWall.y = y;
        const pLad = centerBase.clone(); pLad.y = y;
        addCylinderBetween(group, pWall, pLad, 0.02, material, 6);
    }
}

function addHelicalStair(group, radius, height, angleOffset = 0) {
    const innerRadius = radius * 1.01;
    const pathWidth = 0.75;
    const outerRadius = innerRadius + pathWidth;
    const midRadius = (innerRadius + outerRadius) / 2;

    const circumference = innerRadius * Math.PI * 2;
    const totalLength = height / Math.tan(THREE.MathUtils.degToRad(32));
    const turns = totalLength / circumference;
    const steps = Math.floor(turns * 45);

    const stepMat = getMaterial('galvanized');
    const railMat = getMaterial('safety');
    const suppMat = getMaterial('darkSteel');

    const outerRailPts = [];
    const innerRailPts = [];
    const outerMidPts = [];

    for (let i = 0; i < steps; i++) {
        const t = i / (steps - 1);
        const a = angleOffset - t * turns * Math.PI * 2;
        const y = t * height;

        const step = new THREE.Mesh(new THREE.BoxGeometry(pathWidth, 0.03, 0.25), stepMat);
        step.position.set(Math.cos(a) * midRadius, y, Math.sin(a) * midRadius);
        step.rotation.y = -a;
        group.add(step);

        const pOuter = new THREE.Vector3(Math.cos(a) * outerRadius, y, Math.sin(a) * outerRadius);
        const pInner = new THREE.Vector3(Math.cos(a) * innerRadius, y, Math.sin(a) * innerRadius);

        const pOuterTop = pOuter.clone(); pOuterTop.y += 1.0;
        const pOuterMid = pOuter.clone(); pOuterMid.y += 0.5;
        const pInnerTop = pInner.clone(); pInnerTop.y += 1.0;

        outerRailPts.push(pOuterTop);
        outerMidPts.push(pOuterMid);
        innerRailPts.push(pInnerTop);

        if (i % 3 === 0) {
            addCylinderBetween(group, pOuter, pOuterTop, 0.02, railMat, 6);
            addCylinderBetween(group, pInner, pInnerTop, 0.02, railMat, 6);

            const ancla = pInner.clone(); ancla.add(new THREE.Vector3(-Math.cos(a) * 0.1, -0.2, -Math.sin(a) * 0.1));
            addCylinderBetween(group, pInner, ancla, 0.025, suppMat, 6);
        }
    }

    connectPath(group, outerRailPts, 0.022, railMat, 6);
    connectPath(group, outerMidPts, 0.015, railMat, 6);
    connectPath(group, innerRailPts, 0.022, railMat, 6);

    const finalAngle = angleOffset - turns * Math.PI * 2;
    addHelicalTopPlatform(group, radius, height, finalAngle, suppMat, railMat);
}

function connectPath(group, points, radius, material, segments) {
    for (let i = 0; i < points.length - 1; i++) {
        addCylinderBetween(group, points[i], points[i + 1], radius, material, segments);
    }
}

function addTankConnections(group, radius, height) {
    addNozzle(group, radius, height, { angle: Math.PI * 0.85, y: height * 0.08, size: 0.6, label: "Drenaje" });
    addNozzle(group, radius, height, { angle: Math.PI * 1.15, y: height * 0.28, size: 1.2, label: "Salida" });
    addNozzle(group, radius, height, { angle: Math.PI * 1.45, y: height * 0.75, size: 1.0, label: "Entrada" });
    addNozzle(group, radius, height, { angle: Math.PI * 1.75, y: height * 0.90, size: 0.8, label: "Rebosadero" });
}

function addNozzle(group, radius, height, options) {
    const angle = options.angle;
    const y = options.y;
    const scale = options.size || 1.0;
    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));

    const pipeRadius = Math.max(radius * 0.025 * scale, 0.08);
    const pipeLength = 0.22;
    const flangeRadius = pipeRadius * 1.55;
    const flangeThickness = 0.028;

    const base = radial.clone().multiplyScalar(radius); base.y = y;
    const end = radial.clone().multiplyScalar(radius + pipeLength); end.y = y;
    addCylinderBetween(group, base, end, pipeRadius, getMaterial('galvanized'), 16);

    const fCenter = radial.clone().multiplyScalar(radius + pipeLength + flangeThickness / 2); fCenter.y = y;
    const flange = new THREE.Mesh(new THREE.CylinderGeometry(flangeRadius, flangeRadius, flangeThickness, 24), getMaterial('darkSteel'));
    alignToWall(flange, radial, fCenter);
    flange.castShadow = true;
    flange.userData = {
        tipo: `Nozzle de Proceso (${options.label})`,
        material: "Brida ANSI Forjada",
        altura: formatTechnicalValue(y, "u.3D"),
        diametro: formatTechnicalValue(pipeRadius * 2, "u.3D")
    };
    group.add(flange);

    const bCount = scale > 0.9 ? 8 : 4;
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));
    const vertical = new THREE.Vector3(0, 1, 0);
    const bCircle = (pipeRadius + flangeRadius) / 2;

    for (let i = 0; i < bCount; i++) {
        const a = (Math.PI * 2 * i) / bCount;
        const bPos = fCenter.clone()
            .add(tangent.clone().multiplyScalar(Math.cos(a) * bCircle))
            .add(vertical.clone().multiplyScalar(Math.sin(a) * bCircle));
        const pMesh = new THREE.Mesh(new THREE.CylinderGeometry(0.008, 0.008, flangeThickness * 1.5, 6), getMaterial('bolt'));
        alignToWall(pMesh, radial, bPos);
        group.add(pMesh);
    }
}

function addHelicalTopPlatform(group, radius, height, angle, platformMaterial, railMaterial) {
    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));

    const width = 1.0;
    const depth = 0.8;
    const center = radial.clone().multiplyScalar(radius + depth * 0.4);
    center.y = height;

    const plat = new THREE.Mesh(new THREE.BoxGeometry(width, 0.04, depth), platformMaterial);
    plat.position.copy(center);
    plat.rotation.y = angleFromRadial(radial);
    group.add(plat);

    addPlatformRails(group, center, radial, tangent, width, depth, height, 0.04, 1.0, 0.02, railMaterial);
}

function addPlatformRails(group, center, radial, tangent, width, depth, y, thickness, railHeight, railRadius, material, options = {}) {
    const p1 = center.clone().add(tangent.clone().multiplyScalar(-width / 2)).add(radial.clone().multiplyScalar(depth / 2));
    const p2 = center.clone().add(tangent.clone().multiplyScalar(width / 2)).add(radial.clone().multiplyScalar(depth / 2));
    const p3 = center.clone().add(tangent.clone().multiplyScalar(-width / 2)).add(radial.clone().multiplyScalar(-depth / 2));
    const p4 = center.clone().add(tangent.clone().multiplyScalar(width / 2)).add(radial.clone().multiplyScalar(-depth / 2));

    const posts = [p1, p2, p3, p4];
    const tops = [];

    posts.forEach(p => {
        const bottom = p.clone(); bottom.y = y + thickness;
        const top = p.clone(); top.y = bottom.y + railHeight;
        tops.push(top);
        addCylinderBetween(group, bottom, top, railRadius, material, 6);
    });

    if (!options.openFront) addCylinderBetween(group, tops[0], tops[1], railRadius, material, 6);
    if (!options.openBack) addCylinderBetween(group, tops[2], tops[3], railRadius, material, 6);

    addCylinderBetween(group, tops[0], tops[2], railRadius, material, 6);
    addCylinderBetween(group, tops[1], tops[3], railRadius, material, 6);
}

function addCylinderBetween(group, start, end, radius, material, segments) {
    const direction = new THREE.Vector3().subVectors(end, start);
    const length = direction.length();

    if (length <= 0.001) return;

    const geometry = new THREE.CylinderGeometry(radius, radius, length, segments || 8, 1, false);
    const mesh = new THREE.Mesh(geometry, material);

    mesh.position.copy(start).add(end).multiplyScalar(0.5);
    mesh.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction.normalize());

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
        if (viewer.isDragging) return;

        const rect = viewer.renderer.domElement.getBoundingClientRect();
        mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
        mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

        raycaster.setFromCamera(mouse, viewer.camera);
        const intersects = raycaster.intersectObjects(viewer.group.children, true);
        const valid = intersects.find(x => x.object.userData && x.object.userData.tipo);

        if (!valid) {
            overlay.style.display = "none";
            viewer.renderer.domElement.style.cursor = "grab";
            return;
        }

        const data = valid.object.userData;
        viewer.renderer.domElement.style.cursor = "crosshair";

        overlay.innerHTML = `
            <div style="font-size:10px;text-transform:uppercase;letter-spacing:1px;color:#64748b;margin-bottom:4px;border-bottom:1px solid #e2e8f0;padding-bottom:2px;">Inspección Técnica</div>
            <strong style="font-size:14px;color:#0f172a;">${data.tipo}</strong><br>
            <div style="margin-top:6px;display:grid;grid-template-columns:auto auto;gap:2px 10px;color:#334155;font-size:11px;">
                ${data.material ? `<span>Material:</span><strong>${data.material}</strong>` : ''}
                ${data.altura ? `<span>Cota Z:</span><strong>${data.altura}</strong>` : ''}
                ${data.espesor ? `<span>Espesor:</span><strong>${data.espesor}</strong>` : ''}
                ${data.diametro ? `<span>Diámetro:</span><strong>${data.diametro}</strong>` : ''}
            </div>
        `;

        overlay.style.display = "block";
    });

    viewer.renderer.domElement.addEventListener("mouseleave", () => {
        overlay.style.display = "none";
        viewer.renderer.domElement.style.cursor = "grab";
    });
}

function bindControls(viewer) {
    const canvas = viewer.renderer.domElement;
    canvas.style.cursor = "grab";
    canvas.style.touchAction = "none";

    canvas.addEventListener("pointerdown", e => {
        if (e.button !== 0) return;
        viewer.isDragging = true;
        viewer.lastX = e.clientX;
        viewer.lastY = e.clientY;
        canvas.setPointerCapture(e.pointerId);
        canvas.style.cursor = "grabbing";
        const overlay = viewer.shell.querySelector("#tank3d-tech-overlay");
        if (overlay) overlay.style.display = "none";
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

        viewer.inputYaw -= dx * 0.005;
        viewer.inputPitch -= dy * 0.005;
        viewer.inputPitch = THREE.MathUtils.clamp(viewer.inputPitch, -Math.PI / 2 + 0.1, Math.PI / 2 - 0.1);
    });

    canvas.addEventListener("wheel", e => {
        e.preventDefault();
        const factor = e.deltaY > 0 ? 1.1 : 0.9;
        viewer.inputDistance = THREE.MathUtils.clamp(viewer.inputDistance * factor, 15, 300);
    }, { passive: false });
}

function resize(viewer) {
    const rect = viewer.shell.getBoundingClientRect();

    const width = Math.max(320, rect.width || 800);
    const height = Math.max(720, rect.height || 720);

    viewer.camera.aspect = width / height;
    viewer.camera.updateProjectionMatrix();

    viewer.renderer.setSize(width, height, false);
}

function animate(viewer) {
    viewer.animationId = requestAnimationFrame(() => animate(viewer));

    if (!viewer.renderer || !viewer.scene || !viewer.camera) return;

    const dampingInfo = 0.12;
    const dampingZoom = 0.15;

    viewer.yaw += (viewer.inputYaw - viewer.yaw) * dampingInfo;
    viewer.pitch += (viewer.inputPitch - viewer.pitch) * dampingInfo;
    viewer.distance += (viewer.inputDistance - viewer.distance) * dampingZoom;

    updateCamera(viewer);
    viewer.renderer.render(viewer.scene, viewer.camera);
}

function fitCamera(viewer) {
    const height = viewer.modelHeight || 40;
    const radius = viewer.modelRadius || 20;
    const maxSize = Math.max(height, radius * 2, 1);

    viewer.inputDistance = maxSize * 1.8;
    viewer.inputPitch = 0.35;
    viewer.inputYaw = Math.PI / 4;

    viewer.yaw = viewer.inputYaw;
    viewer.pitch = viewer.inputPitch;
    viewer.distance = viewer.inputDistance;

    viewer.target.set(0, 0, 0);
    updateCamera(viewer);
}

function updateCamera(viewer) {
    const x = viewer.distance * Math.cos(viewer.pitch) * Math.sin(viewer.yaw);
    const y = viewer.distance * Math.sin(viewer.pitch);
    const z = viewer.distance * Math.cos(viewer.pitch) * Math.cos(viewer.yaw);

    viewer.camera.position.set(x, y, z);
    viewer.camera.lookAt(viewer.target);

    viewer.camera.near = Math.max(0.1, viewer.distance * 0.05);
    viewer.camera.far = Math.max(1000, viewer.distance * 5);
    viewer.camera.updateProjectionMatrix();
}

function colorForMaterial(name) {
    const normalized = String(name || "").toUpperCase();
    if (normalized.includes("S355")) return 0x52525b;
    if (normalized.includes("S275")) return 0x71717a;
    if (normalized.includes("S235")) return 0xa1a1aa;
    if (normalized.includes("INOX") || normalized.includes("316")) return 0xe2e8f0;
    return 0x71717a;
}

function showError(container, message) {
    container.innerHTML = `
        <div style="
            padding:20px;
            border-radius:8px;
            background:#fef2f2;
            color:#991b1b;
            border:1px solid #fecaca;
            font-family:'Inter', 'Segoe UI', sans-serif;
            font-weight:600;
            font-size:13px;
            box-shadow:0 4px 12px rgba(0,0,0,0.05);">
            ⚠️ Error de Interfaz DL2: ${message}
        </div>
    `;
}

window.tank3d = {
    renderTank3D: renderTank3D
};