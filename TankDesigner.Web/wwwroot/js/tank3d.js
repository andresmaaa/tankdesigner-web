const viewers = new WeakMap();

const technicalViewState = {
    showRoof: true,
    showGuardrail: false,
    showConnections: true,
    showLadder: true,
    showReferences: false,
    showWater: true
};

const VISUAL_CONFIG = {
    shellSegments: 144,
    curveSegments: 180,
    shadowMapSize: 4096,
    cameraPadding: 2.55
};

function renderTank3D(container, tank, dotNetRef) {
    if (!container) return;

    disposeViewer(container);

    container.innerHTML = "";
    container.style.minHeight = "720px";
    container.style.height = "720px";
    container.style.overflow = "visible";

    if (!window.THREE) {
        showError(container, "Three.js no está cargado.");
        return;
    }

    if (!tank || !Array.isArray(tank.anillos) || tank.anillos.length === 0) {
        showError(container, "No hay anillos válidos para generar el modelo 3D.");
        return;
    }

    const rings = tank.anillos.filter(r => Number(r.altura) > 0);

    if (rings.length === 0) {
        showError(container, "No hay anillos con altura válida.");
        return;
    }

    const realDiameter = Number(tank.diametro) || 1;
    const realHeight =
        Number(tank.alturaTotal) ||
        rings.reduce((s, r) => s + Number(r.altura || 0), 0);

    const maxRealSize = Math.max(realDiameter, realHeight, 1);
    const targetModelSize = 42;
    const scale = targetModelSize / maxRealSize;
    const metersPerUnit = 1 / scale;

    const viewer = createViewer(container, scale, metersPerUnit, tank, dotNetRef);

    viewer.scale = scale;
    viewer.rings = rings;
    viewer.tank = tank;
    viewer.dotNetRef = dotNetRef;

    viewers.set(container, viewer);

    buildTank(viewer, tank, rings, scale);
    fitCamera(viewer);

    viewer.renderer.render(viewer.scene, viewer.camera);
}

function disposeViewer(container) {
    const oldViewer = viewers.get(container);
    if (!oldViewer) return;

    if (oldViewer.animationId) cancelAnimationFrame(oldViewer.animationId);
    if (oldViewer.resizeObserver) oldViewer.resizeObserver.disconnect();

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

    if (oldViewer.renderer.domElement?.parentNode) {
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
    shell.style.borderRadius = "26px";
    shell.style.overflow = "hidden";
    shell.style.background = `
        radial-gradient(circle at 50% 20%, #ffffff 0%, #f1f5f9 42%, #dbe4ee 100%)
    `;
    shell.style.boxShadow = `
        inset 0 1px 0 rgba(255,255,255,0.85),
        0 24px 70px rgba(15,23,42,0.12)
    `;

    container.appendChild(shell);

    const scene = new THREE.Scene();
    scene.background = null;

    const camera = new THREE.PerspectiveCamera(28, 1, 0.1, 10000);

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
    renderer.toneMappingExposure = 1.08;

    shell.appendChild(renderer.domElement);

    const group = new THREE.Group();
    scene.add(group);

    addLighting(scene, renderer);
    addGround(scene);

    addScaleBadge(shell, metersPerUnit, tank);
    addRoofControls(shell, container, tank, dotNetRef);
    addTechnicalControls(shell, container);
    addTechnicalInfoOverlay(shell);
    addDownloadPngButton(shell, renderer, tank);
    addMouseHelpPanel(shell);

    const viewer = {
        container,
        shell,
        scene,
        camera,
        renderer,
        group,

        inputYaw: 0.74,
        inputPitch: 0.26,
        inputDistance: 80,

        yaw: 0.74,
        pitch: 0.26,
        distance: 80,

        target: new THREE.Vector3(0, 0, 0),

        isDragging: false,
        lastX: 0,
        lastY: 0,

        modelRadius: 20,
        modelHeight: 40,
        modelOuterRadius: 24
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

function addLighting(scene, renderer) {
    scene.add(new THREE.AmbientLight(0xffffff, 0.48));

    const keyLight = new THREE.DirectionalLight(0xffffff, 1.95);
    keyLight.position.set(42, 58, 46);
    keyLight.castShadow = true;
    keyLight.shadow.mapSize.width = VISUAL_CONFIG.shadowMapSize;
    keyLight.shadow.mapSize.height = VISUAL_CONFIG.shadowMapSize;
    keyLight.shadow.camera.near = 1;
    keyLight.shadow.camera.far = 300;
    keyLight.shadow.camera.left = -90;
    keyLight.shadow.camera.right = 90;
    keyLight.shadow.camera.top = 90;
    keyLight.shadow.camera.bottom = -90;
    keyLight.shadow.bias = -0.00045;
    keyLight.shadow.radius = 3;
    scene.add(keyLight);

    const fillLight = new THREE.DirectionalLight(0xdbeafe, 0.95);
    fillLight.position.set(-55, 20, -25);
    scene.add(fillLight);

    const rimLight = new THREE.DirectionalLight(0xffffff, 1.75);
    rimLight.position.set(-35, 42, -70);
    scene.add(rimLight);

    const topLight = new THREE.DirectionalLight(0xffffff, 0.55);
    topLight.position.set(0, 90, 0);
    scene.add(topLight);

    const pmrem = new THREE.PMREMGenerator(renderer);
    const envScene = new THREE.Scene();
    envScene.background = new THREE.Color(0xf8fafc);

    const envLight = new THREE.Mesh(
        new THREE.SphereGeometry(35, 24, 24),
        new THREE.MeshBasicMaterial({ color: 0xffffff })
    );

    envLight.position.set(0, 55, 0);
    envScene.add(envLight);

    scene.environment = pmrem.fromScene(envScene).texture;
}

function addGround(scene) {
    return;
}

function buildTank(viewer, tank, rings, scale) {
    const diameter = (Number(tank.diametro) || 1) * scale;
    const radius = diameter / 2;

    let currentY = 0;

    const starterHeight = getStarterRingHeight(tank, scale);
    currentY += addStarterRing(viewer.group, radius, starterHeight, tank);

    rings.forEach((ring, index) => {
        const height = Number(ring.altura) * scale;
        const materialName = ring.material || tank.materialPrincipal || "Acero";
        const baseColor = colorForMaterial(materialName);
        const variation = index % 2 === 0 ? 0.96 : 1.04;

        const shellGeometry = new THREE.CylinderGeometry(
            radius,
            radius,
            height,
            VISUAL_CONFIG.shellSegments,
            1,
            true
        );

        const shell = new THREE.Mesh(
            shellGeometry,
            createSteelMaterial(adjustColor(baseColor, variation))
        );

        shell.position.y = currentY + height / 2;
        shell.castShadow = true;
        shell.receiveShadow = true;
        shell.frustumCulled = false;

        shell.userData = {
            tipo: `Anillo ${index + 1}`,
            material: materialName,
            altura: formatTechnicalValue(ring.altura, "m"),
            espesor: formatTechnicalValue(ring.espesor, "mm"),
            diametro: formatTechnicalValue(tank.diametro, "m")
        };

        viewer.group.add(shell);

        const edges = new THREE.LineSegments(
            new THREE.EdgesGeometry(shellGeometry, 24),
            new THREE.LineBasicMaterial({
                color: 0x020617,
                transparent: true,
                opacity: 0.08
            })
        );

        edges.position.copy(shell.position);
        edges.frustumCulled = false;
        viewer.group.add(edges);

        addRingSeam(viewer.group, radius, currentY);
        addRingSeam(viewer.group, radius, currentY + height);

        currentY += height;
    });

    addBottomDisc(viewer.group, radius * 0.99);

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
    viewer.modelOuterRadius = radius * 1.38;
}

function rebuildTank(viewer) {
    while (viewer.group.children.length > 0) {
        const obj = viewer.group.children[0];

        if (obj.geometry) obj.geometry.dispose();

        if (obj.material) {
            if (Array.isArray(obj.material)) {
                obj.material.forEach(m => m.dispose());
            } else {
                obj.material.dispose();
            }
        }

        viewer.group.remove(obj);
    }

    buildTank(viewer, viewer.tank, viewer.rings, viewer.scale);
}

function createSteelMaterial(color) {
    return new THREE.MeshStandardMaterial({
        color,
        metalness: 0.86,
        roughness: 0.24,
        envMapIntensity: 1.25,
        side: THREE.DoubleSide
    });
}

function createDarkSteelMaterial() {
    return new THREE.MeshStandardMaterial({
        color: 0x1f2937,
        metalness: 0.92,
        roughness: 0.18,
        envMapIntensity: 1.35,
        side: THREE.DoubleSide
    });
}

function createGalvanizedMaterial() {
    return new THREE.MeshStandardMaterial({
        color: 0xd7dde5,
        metalness: 0.9,
        roughness: 0.2,
        envMapIntensity: 1.35,
        side: THREE.DoubleSide
    });
}

function adjustColor(hex, factor) {
    const color = new THREE.Color(hex);
    color.r = Math.min(1, color.r * factor);
    color.g = Math.min(1, color.g * factor);
    color.b = Math.min(1, color.b * factor);
    return color;
}

function addStarterRing(group, radius, height, tank) {
    const material = new THREE.MeshStandardMaterial({
        color: 0x4b4a33,
        metalness: 0.94,
        roughness: 0.28,
        envMapIntensity: 1.35,
        side: THREE.DoubleSide
    });

    const starter = new THREE.Mesh(
        new THREE.CylinderGeometry(radius * 1.018, radius * 1.018, height, VISUAL_CONFIG.shellSegments, 1, true),
        material
    );

    starter.position.y = height / 2;
    starter.castShadow = true;
    starter.receiveShadow = true;
    starter.frustumCulled = false;

    starter.userData = {
        tipo: "Starter Ring",
        material: "Acero estructural",
        altura: `${getStarterRingHeightMm(tank)} mm`,
        espesor: "—",
        diametro: formatTechnicalValue(radius * 2, "u.3D")
    };

    group.add(starter);

    const bandMaterial = createDarkSteelMaterial();
    addTorus(group, radius * 1.024, Math.max(radius * 0.011, 0.045), height * 0.12, bandMaterial, starter.userData);
    addTorus(group, radius * 1.024, Math.max(radius * 0.011, 0.045), height * 0.88, bandMaterial, starter.userData);
    addTorus(group, radius * 1.040, Math.max(radius * 0.014, 0.055), Math.max(0.018, height * 0.18), bandMaterial, starter.userData);

    addRingSeam(group, radius * 1.022, 0);
    addRingSeam(group, radius * 1.022, height);

    return height;
}

function addTorus(group, radius, tubeRadius, y, material, userData) {
    const torus = new THREE.Mesh(
        new THREE.TorusGeometry(radius, tubeRadius, 16, VISUAL_CONFIG.curveSegments),
        material
    );

    torus.rotation.x = Math.PI / 2;
    torus.position.y = y;
    torus.castShadow = true;
    torus.receiveShadow = true;
    torus.frustumCulled = false;
    torus.userData = userData || {};

    group.add(torus);
}

function addRingSeam(group, radius, y) {
    const curve = new THREE.EllipseCurve(0, 0, radius * 1.006, radius * 1.006, 0, Math.PI * 2);
    const points = curve.getPoints(VISUAL_CONFIG.curveSegments).map(p => new THREE.Vector3(p.x, y, p.y));
    const geometry = new THREE.BufferGeometry().setFromPoints(points);

    const line = new THREE.LineLoop(
        geometry,
        new THREE.LineBasicMaterial({
            color: 0x020617,
            transparent: true,
            opacity: 0.28
        })
    );

    line.frustumCulled = false;
    group.add(line);
}

function addBottomDisc(group, radius) {
    const disc = new THREE.Mesh(
        new THREE.CircleGeometry(radius * 1.15, VISUAL_CONFIG.curveSegments),
        new THREE.MeshStandardMaterial({
            color: 0x334155,
            metalness: 0.18,
            roughness: 0.82,
            side: THREE.DoubleSide
        })
    );

    disc.rotation.x = -Math.PI / 2;
    disc.position.y = -0.018;
    disc.receiveShadow = true;
    disc.frustumCulled = false;

    group.add(disc);
}

function addTopStiffener(group, radius, height) {
    const data = {
        tipo: "Rigidizador superior",
        material: "Acero",
        altura: "Coronación",
        espesor: "—",
        diametro: "—"
    };

    addTorus(group, radius * 1.018, Math.max(radius * 0.018, 0.045), height, createDarkSteelMaterial(), data);
}

function addWaterLevelIfAvailable(group, radius, height, tank, scale) {
    const rawLevel =
        Number(tank?.nivelAgua) ||
        Number(tank?.alturaAgua) ||
        Number(tank?.nivelLiquido) ||
        Number(tank?.alturaLiquido) ||
        0;

    if (!rawLevel || rawLevel <= 0 || !scale || scale <= 0) return;

    const y = Math.min(rawLevel * scale, height * 0.995);

    const material = new THREE.MeshPhysicalMaterial({
        color: 0x38bdf8,
        transparent: true,
        opacity: 0.26,
        metalness: 0,
        roughness: 0.03,
        transmission: 0.15,
        side: THREE.DoubleSide
    });

    const disc = new THREE.Mesh(
        new THREE.CircleGeometry(radius * 0.96, VISUAL_CONFIG.curveSegments),
        material
    );

    disc.rotation.x = -Math.PI / 2;
    disc.position.y = y;
    disc.receiveShadow = true;
    disc.frustumCulled = false;

    disc.userData = {
        tipo: "Nivel de agua",
        material: "Agua",
        altura: `${rawLevel} m`,
        espesor: "—",
        diametro: "—"
    };

    group.add(disc);

    addRingSeam(group, radius * 0.965, y + 0.01);
}

function addRoofControls(shell, container, tank, dotNetRef) {
    const roof = normalizarTecho(tank.techo);

    if (roof.type !== "cone") return;

    if (!tank.vigasTechoConico) {
        tank.vigasTechoConico = {};
    }

    const config = tank.vigasTechoConico;

    const currentBeamCount = Math.max(0, Number(config.numeroVigas) || 0);

    const currentHubDiameter =
        Number(config.diametroNucleoCentralManual) ||
        Number(config.diametroNucleoTechoConicoManual) ||
        Number(config.diametroNucleoCentral) ||
        Number(config.diametroNucleo) ||
        1.5;

    const currentConeHeight = Number(config.alturaCono) || 1.0;

    const panel = document.createElement("div");

    panel.style.position = "absolute";
    panel.style.right = "24px";
    panel.style.top = "72px";
    panel.style.zIndex = "10";
    panel.style.width = "230px";
    panel.style.padding = "16px";
    panel.style.borderRadius = "18px";
    panel.style.background = "rgba(15,23,42,0.90)";
    panel.style.border = "1px solid rgba(255,255,255,0.10)";
    panel.style.boxShadow = "0 18px 45px rgba(15,23,42,0.28)";
    panel.style.backdropFilter = "blur(14px)";
    panel.style.color = "#ffffff";
    panel.style.font = "13px 'Segoe UI', Arial, sans-serif";

    panel.innerHTML = `
        <div style="font-weight:800;font-size:14px;margin-bottom:14px;">
            Ajustes del techo cónico
        </div>

        <label style="display:block;color:#cbd5e1;margin-bottom:6px;">
            Número de vigas
        </label>

        <div style="display:grid;grid-template-columns:34px 1fr 34px;gap:6px;margin-bottom:14px;">
            <button type="button" data-action="beams-minus" style="${roofButtonStyle()}">−</button>
            <input data-field="beams" type="number" min="0" max="160" step="1" value="${currentBeamCount}" style="${roofInputStyle()}">
            <button type="button" data-action="beams-plus" style="${roofButtonStyle()}">+</button>
        </div>

        <label style="display:block;color:#cbd5e1;margin-bottom:6px;">
            Diámetro del centro
        </label>

        <div style="display:grid;grid-template-columns:34px 1fr 34px;gap:6px;margin-bottom:14px;">
            <button type="button" data-action="hub-minus" style="${roofButtonStyle()}">−</button>
            <input data-field="hub" type="number" min="0.30" max="20" step="0.10" value="${currentHubDiameter.toFixed(2)}" style="${roofInputStyle()}">
            <button type="button" data-action="hub-plus" style="${roofButtonStyle()}">+</button>
        </div>

        <label style="display:block;color:#cbd5e1;margin-bottom:6px;">
            Altura del cono
        </label>

        <input data-field="height" type="number" min="0.10" max="20" step="0.10" value="${currentConeHeight.toFixed(2)}" style="${roofInputStyle()}">

        <div data-status style="
            margin-top:14px;
            padding-top:12px;
            border-top:1px solid rgba(255,255,255,0.10);
            color:#22c55e;
            font-weight:700;">
            ✓ Guardado
        </div>

        <div style="margin-top:6px;color:#94a3b8;font-size:12px;line-height:1.35;">
            Estos valores se mantienen al actualizar la vista.
        </div>
    `;

    const beamsInput = panel.querySelector('[data-field="beams"]');
    const hubInput = panel.querySelector('[data-field="hub"]');
    const heightInput = panel.querySelector('[data-field="height"]');
    const status = panel.querySelector("[data-status]");

    const saveAndRebuild = () => {
        const beams = Math.max(0, Math.min(160, Math.floor(Number(beamsInput.value) || 0)));
        const hubDiameter = Math.max(0.30, Math.min(20, Number(hubInput.value) || 1.5));
        const coneHeight = Math.max(0.10, Math.min(20, Number(heightInput.value) || 1.0));

        beamsInput.value = beams;
        hubInput.value = hubDiameter.toFixed(2);
        heightInput.value = coneHeight.toFixed(2);

        config.aplica = beams > 0;
        config.numeroVigas = beams;
        config.diametroNucleoCentralManual = hubDiameter;
        config.diametroNucleoTechoConicoManual = hubDiameter;
        config.alturaCono = coneHeight;

        status.textContent = "✓ Guardado";

        if (dotNetRef) {
            dotNetRef.invokeMethodAsync(
                "ActualizarConfiguracionTecho3D",
                beams,
                hubDiameter,
                coneHeight
            ).catch(() => { });
        }

        const viewer = viewers.get(container);

        if (viewer) {
            rebuildTank(viewer);
        }
    };

    panel.querySelector('[data-action="beams-minus"]').addEventListener("click", () => {
        beamsInput.value = Math.max(0, Number(beamsInput.value) - 1);
        saveAndRebuild();
    });

    panel.querySelector('[data-action="beams-plus"]').addEventListener("click", () => {
        beamsInput.value = Math.min(160, Number(beamsInput.value) + 1);
        saveAndRebuild();
    });

    panel.querySelector('[data-action="hub-minus"]').addEventListener("click", () => {
        hubInput.value = Math.max(0.30, Number(hubInput.value) - 0.10).toFixed(2);
        saveAndRebuild();
    });

    panel.querySelector('[data-action="hub-plus"]').addEventListener("click", () => {
        hubInput.value = Math.min(20, Number(hubInput.value) + 0.10).toFixed(2);
        saveAndRebuild();
    });

    beamsInput.addEventListener("change", saveAndRebuild);
    hubInput.addEventListener("change", saveAndRebuild);
    heightInput.addEventListener("change", saveAndRebuild);

    shell.appendChild(panel);
}

function roofButtonStyle() {
    return `
        border:1px solid rgba(255,255,255,0.12);
        background:rgba(30,41,59,0.95);
        color:#ffffff;
        border-radius:8px;
        font-size:20px;
        line-height:1;
        cursor:pointer;
    `;
}

function roofInputStyle() {
    return `
        width:100%;
        box-sizing:border-box;
        border:1px solid rgba(255,255,255,0.12);
        background:rgba(15,23,42,0.75);
        color:#ffffff;
        border-radius:8px;
        padding:8px 10px;
        font:600 13px 'Segoe UI', Arial, sans-serif;
        text-align:center;
    `;
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

function addOpenTop(group, radius, height) {
    addTorus(
        group,
        radius,
        Math.max(radius * 0.014, 0.035),
        height,
        createDarkSteelMaterial(),
        {
            tipo: "Borde superior abierto",
            material: "Acero",
            altura: "Coronación",
            espesor: "—",
            diametro: "—"
        }
    );
}

function addFlatRoof(group, radius, height) {
    const roof = new THREE.Mesh(
        new THREE.CircleGeometry(radius * 0.995, VISUAL_CONFIG.curveSegments),
        new THREE.MeshStandardMaterial({
            color: 0xdbe3ec,
            metalness: 0.72,
            roughness: 0.26,
            envMapIntensity: 1.18,
            side: THREE.DoubleSide
        })
    );

    roof.rotation.x = -Math.PI / 2;
    roof.position.y = height + radius * 0.012;
    roof.castShadow = true;
    roof.receiveShadow = true;
    roof.frustumCulled = false;

    roof.userData = {
        tipo: "Techo plano",
        material: "Acero",
        altura: "Coronación",
        espesor: "—",
        diametro: "—"
    };

    group.add(roof);

    const ribMaterial = createGalvanizedMaterial();
    const sheetCount = Math.max(10, Math.min(24, Math.floor(radius * 1.9)));
    const sheetWidth = (radius * 2) / sheetCount;
    const ribHeight = Math.max(radius * 0.012, 0.035);
    const ribWidth = Math.max(radius * 0.009, 0.028);

    for (let i = 0; i < sheetCount; i++) {
        const x = -radius + sheetWidth * (i + 0.5);
        const halfLength = Math.sqrt(Math.max(0, radius * radius - x * x));
        if (halfLength <= 0.1) continue;

        [-0.3, 0.3].forEach(offset => {
            const rib = new THREE.Mesh(
                new THREE.BoxGeometry(ribWidth, ribHeight, halfLength * 2),
                ribMaterial
            );

            rib.position.set(x + sheetWidth * offset, height + radius * 0.03, 0);
            rib.castShadow = true;
            rib.receiveShadow = true;
            rib.frustumCulled = false;
            group.add(rib);
        });
    }

    addOpenTop(group, radius, height + radius * 0.015);
}

function addConeRoof(group, radius, height, vigasTechoConico, scale) {
    const alturaConoReal =
        vigasTechoConico && Number(vigasTechoConico.alturaCono) > 0
            ? Number(vigasTechoConico.alturaCono)
            : 0;

    const roofHeight =
        alturaConoReal > 0
            ? alturaConoReal * scale
            : Math.max(radius * 0.17, 1.25);

    const cone = new THREE.Mesh(
        new THREE.ConeGeometry(radius * 1.012, roofHeight, VISUAL_CONFIG.curveSegments, 1, false),
        new THREE.MeshStandardMaterial({
            color: 0xe5e7eb,
            metalness: 0.76,
            roughness: 0.25,
            transparent: true,
            opacity: 0.94,
            side: THREE.DoubleSide,
            envMapIntensity: 1.16
        })
    );

    cone.position.y = height + roofHeight / 2;
    cone.castShadow = true;
    cone.receiveShadow = true;
    cone.frustumCulled = false;

    cone.userData = {
        tipo: "Techo cónico",
        material: "Acero",
        altura: alturaConoReal > 0 ? `${alturaConoReal} m` : "Auto 3D",
        espesor: "—",
        diametro: "—"
    };

    group.add(cone);

    addOpenTop(group, radius, height);
    addConeRoofPanels(group, radius, height, roofHeight);
    addConeRoofRafters(group, radius, height, roofHeight, vigasTechoConico, scale);

    const numeroVigas =
        vigasTechoConico && vigasTechoConico.aplica === true
            ? Number(vigasTechoConico.numeroVigas) || 0
            : 0;

    addConeRoofCenterHub(group, height + roofHeight, radius, numeroVigas, vigasTechoConico, scale);
}

function addConeRoofPanels(group, radius, baseHeight, roofHeight) {
    const material = new THREE.LineBasicMaterial({
        color: 0x475569,
        transparent: true,
        opacity: 0.34
    });

    [0.35, 0.68].forEach(f => {
        const ringRadius = radius * f;
        const y = baseHeight + roofHeight * (1 - f);
        const curve = new THREE.EllipseCurve(0, 0, ringRadius, ringRadius, 0, Math.PI * 2);
        const points = curve.getPoints(VISUAL_CONFIG.curveSegments).map(p => new THREE.Vector3(p.x, y, p.y));
        const line = new THREE.LineLoop(new THREE.BufferGeometry().setFromPoints(points), material);
        line.frustumCulled = false;
        group.add(line);
    });
}

function addConeRoofRafters(group, radius, baseHeight, roofHeight, vigasTechoConico, scale) {
    if (!vigasTechoConico || vigasTechoConico.aplica !== true) return;

    const numeroVigas = Math.max(0, Number(vigasTechoConico.numeroVigas) || 0);
    if (numeroVigas <= 0) return;

    const beamMaterial = new THREE.MeshStandardMaterial({
        color: 0x7f1d1d,
        metalness: 0.9,
        roughness: 0.18,
        envMapIntensity: 1.2
    });

    const hubRadius = calcularRadioNucleoTecho(radius, numeroVigas, vigasTechoConico, scale);
    const startRadius = hubRadius * 1.06;
    const endRadius = radius * 0.97;
    const beamRadius = Math.max(radius * 0.007, 0.035);
    const offsetY = Math.max(radius * 0.017, 0.06);

    for (let i = 0; i < numeroVigas; i++) {
        const angle = (Math.PI * 2 * i) / numeroVigas;

        const p1 = new THREE.Vector3(
            Math.cos(angle) * startRadius,
            baseHeight + roofHeight * (1 - startRadius / radius) + offsetY,
            Math.sin(angle) * startRadius
        );

        const p2 = new THREE.Vector3(
            Math.cos(angle) * endRadius,
            baseHeight + roofHeight * (1 - endRadius / radius) + offsetY,
            Math.sin(angle) * endRadius
        );

        addCylinderBetween(group, p1, p2, beamRadius, beamMaterial, 16);
    }
}

function addConeRoofCenterHub(group, y, radius, numeroVigas, vigasTechoConico, scale) {
    const hubRadius = calcularRadioNucleoTecho(radius, numeroVigas, vigasTechoConico, scale);
    const hubHeight = Math.max(radius * 0.035, 0.18);

    const hub = new THREE.Mesh(
        new THREE.CylinderGeometry(hubRadius, hubRadius, hubHeight, 96),
        new THREE.MeshStandardMaterial({
            color: 0x450a0a,
            metalness: 0.92,
            roughness: 0.16
        })
    );

    hub.position.y = y + hubHeight / 2;
    hub.castShadow = true;
    hub.receiveShadow = true;
    hub.frustumCulled = false;

    hub.userData = {
        tipo: "Núcleo central de techo",
        material: "Acero",
        altura: "Techo",
        espesor: "—",
        diametro: formatTechnicalValue(hubRadius * 2, "u.3D")
    };

    group.add(hub);
}

function addDomeRoof(group, radius, height) {
    const domeHeight = Math.max(radius * 0.42, 1.35);

    const dome = new THREE.Mesh(
        new THREE.SphereGeometry(radius * 1.015, VISUAL_CONFIG.curveSegments, 48, 0, Math.PI * 2, 0, Math.PI / 2),
        new THREE.MeshStandardMaterial({
            color: 0xe5e7eb,
            metalness: 0.82,
            roughness: 0.2,
            transparent: true,
            opacity: 0.94,
            side: THREE.DoubleSide,
            envMapIntensity: 1.12
        })
    );

    dome.scale.y = domeHeight / radius;
    dome.position.y = height - radius * 0.01;
    dome.castShadow = true;
    dome.receiveShadow = true;
    dome.frustumCulled = false;

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
    const material = createGalvanizedMaterial();
    const skirtHeight = Math.max(radius * 0.09, 0.38);
    const skirtThickness = Math.max(radius * 0.016, 0.05);

    const skirt = new THREE.Mesh(
        new THREE.CylinderGeometry(radius * 1.01, radius * 1.01, skirtHeight, VISUAL_CONFIG.curveSegments, 1, true),
        material
    );

    skirt.position.y = height + skirtHeight / 2 - radius * 0.025;
    skirt.castShadow = true;
    skirt.receiveShadow = true;
    skirt.frustumCulled = false;

    group.add(skirt);

    addCircularRail(group, radius * 1.018, height + skirtHeight, skirtThickness, material);
    addCircularRail(group, radius * 1.018, height, skirtThickness * 0.8, material);
}

function addDomeRoofRibs(group, radius, height, domeHeight) {
    const material = createGalvanizedMaterial();
    const ribRadius = Math.max(radius * 0.0045, 0.02);
    const ribCount = 24;

    for (let i = 0; i < ribCount; i++) {
        const angle = (Math.PI * 2 * i) / ribCount;

        addCylinderBetween(
            group,
            new THREE.Vector3(Math.cos(angle) * radius * 0.96, height + radius * 0.015, Math.sin(angle) * radius * 0.96),
            new THREE.Vector3(0, height + domeHeight, 0),
            ribRadius,
            material,
            12
        );
    }

    [0.35, 0.62, 0.84].forEach(f => {
        addCircularRail(group, radius * f, height + domeHeight * (1 - f * 0.72), ribRadius * 0.85, material);
    });
}

function addRoofGuardrail(group, radius, height, roofRaw) {
    const roof = normalizarTecho(roofRaw);
    if (roof.type === "none") return;

    const material = createGalvanizedMaterial();
    const postRadius = Math.max(radius * 0.0038, 0.02);
    const railRadius = Math.max(radius * 0.0048, 0.024);
    const railHeight = Math.max(radius * 0.085, 0.72);
    const lowerRailHeight = railHeight * 0.55;
    const railRadiusPosition = radius * 1.045;
    const postCount = Math.max(28, Math.min(72, Math.floor(radius * 5.4)));

    for (let i = 0; i < postCount; i++) {
        const angle = (Math.PI * 2 * i) / postCount;
        const bottom = new THREE.Vector3(Math.cos(angle) * railRadiusPosition, height, Math.sin(angle) * railRadiusPosition);
        const top = bottom.clone();
        top.y = height + railHeight;

        addCylinderBetween(group, bottom, top, postRadius, material, 10);
    }

    addCircularRail(group, railRadiusPosition, height + railHeight, railRadius, material);
    addCircularRail(group, railRadiusPosition, height + lowerRailHeight, railRadius * 0.82, material);
}

function addTankConnections(group, radius, height) {
    addNozzle(group, radius, height, { angle: Math.PI * 1.18, y: height * 0.13, size: 0.7, label: "Drenaje" });
    addNozzle(group, radius, height, { angle: Math.PI * 1.32, y: height * 0.36, size: 0.9, label: "Salida" });
    addNozzle(group, radius, height, { angle: Math.PI * 1.46, y: height * 0.68, size: 0.78, label: "Entrada" });
    addNozzle(group, radius, height, { angle: Math.PI * 1.62, y: height * 0.84, size: 0.52, label: "Rebosadero" });
}

function addNozzle(group, radius, height, options) {
    const angle = options.angle;
    const y = options.y;
    const size = options.size || 1;

    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
    const nozzleLength = Math.max(radius * 0.055 * size, 0.18);
    const nozzleRadius = Math.max(radius * 0.026 * size, 0.105);
    const flangeRadius = nozzleRadius * 1.55;
    const flangeThickness = Math.max(nozzleRadius * 0.25, 0.045);
    const boltRadius = Math.max(nozzleRadius * 0.055, 0.014);

    const materialNozzle = new THREE.MeshStandardMaterial({
        color: 0xb6beca,
        metalness: 0.84,
        roughness: 0.2,
        envMapIntensity: 1.15
    });

    const materialFlange = new THREE.MeshStandardMaterial({
        color: 0x475569,
        metalness: 0.92,
        roughness: 0.16,
        envMapIntensity: 1.2
    });

    const materialBolt = createDarkSteelMaterial();

    const base = radial.clone().multiplyScalar(radius * 1.006);
    base.y = y;

    const end = radial.clone().multiplyScalar(radius + nozzleLength);
    end.y = y;

    addCylinderBetween(group, base, end, nozzleRadius, materialNozzle, 32);

    const flangeCenter = radial.clone().multiplyScalar(radius + nozzleLength + flangeThickness * 0.2);
    flangeCenter.y = y;

    const flange = new THREE.Mesh(
        new THREE.CylinderGeometry(flangeRadius, flangeRadius, flangeThickness, 56),
        materialFlange
    );

    flange.position.copy(flangeCenter);

    const quaternion = new THREE.Quaternion();
    quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), radial.clone().normalize());

    flange.quaternion.copy(quaternion);
    flange.castShadow = true;
    flange.receiveShadow = true;
    flange.frustumCulled = false;

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
    const boltCount = 8;

    for (let i = 0; i < boltCount; i++) {
        const a = (Math.PI * 2 * i) / boltCount;

        const boltPos = flangeCenter.clone()
            .add(tangent.clone().multiplyScalar(Math.cos(a) * flangeRadius * 0.72))
            .add(vertical.clone().multiplyScalar(Math.sin(a) * flangeRadius * 0.72))
            .add(radial.clone().multiplyScalar(flangeThickness * 0.75));

        const bolt = new THREE.Mesh(
            new THREE.CylinderGeometry(boltRadius, boltRadius, flangeThickness * 1.15, 10),
            materialBolt
        );

        bolt.position.copy(boltPos);
        bolt.quaternion.copy(quaternion);
        bolt.castShadow = true;
        bolt.receiveShadow = true;
        bolt.frustumCulled = false;

        group.add(bolt);
    }
}

function addManhole(group, radius, height) {
    const angle = Math.PI * 1.82;
    const y = height * 0.28;
    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));
    const vertical = new THREE.Vector3(0, 1, 0);

    const plateRadius = Math.max(radius * 0.1, 0.42);
    const plateThickness = Math.max(radius * 0.012, 0.045);
    const flangeRadius = plateRadius * 1.12;

    const center = radial.clone().multiplyScalar(radius + plateThickness * 0.4);
    center.y = y;

    const quaternion = new THREE.Quaternion();
    quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), radial.clone().normalize());

    const flange = new THREE.Mesh(
        new THREE.CylinderGeometry(flangeRadius, flangeRadius, plateThickness * 1.2, 56),
        createGalvanizedMaterial()
    );

    flange.position.copy(center);
    flange.quaternion.copy(quaternion);
    flange.castShadow = true;
    flange.receiveShadow = true;
    flange.frustumCulled = false;
    flange.userData = {
        tipo: "Manhole",
        material: "Acero",
        altura: formatTechnicalValue(y, "u.3D"),
        espesor: "—",
        diametro: formatTechnicalValue(flangeRadius * 2, "u.3D")
    };

    group.add(flange);

    const plate = new THREE.Mesh(
        new THREE.CylinderGeometry(plateRadius, plateRadius, plateThickness, 56),
        new THREE.MeshStandardMaterial({
            color: 0xcbd5e1,
            metalness: 0.92,
            roughness: 0.16
        })
    );

    plate.position.copy(center.clone().add(radial.clone().multiplyScalar(plateThickness * 0.5)));
    plate.quaternion.copy(quaternion);
    plate.castShadow = true;
    plate.receiveShadow = true;
    plate.frustumCulled = false;
    plate.userData = flange.userData;

    group.add(plate);

    const boltMaterial = createDarkSteelMaterial();

    for (let i = 0; i < 12; i++) {
        const a = (Math.PI * 2 * i) / 12;

        const pos = center.clone()
            .add(tangent.clone().multiplyScalar(Math.cos(a) * flangeRadius * 0.72))
            .add(vertical.clone().multiplyScalar(Math.sin(a) * flangeRadius * 0.72))
            .add(radial.clone().multiplyScalar(plateThickness * 0.85));

        const bolt = new THREE.Mesh(
            new THREE.CylinderGeometry(0.018, 0.018, 0.04, 10),
            boltMaterial
        );

        bolt.position.copy(pos);
        bolt.quaternion.copy(quaternion);
        bolt.frustumCulled = false;
        group.add(bolt);
    }
}

function addRoofVent(group, radius, height, roofRaw) {
    const roof = normalizarTecho(roofRaw);
    if (roof.type === "none") return;

    const ventRadius = Math.max(radius * 0.04, 0.2);
    const ventHeight = Math.max(radius * 0.09, 0.42);
    const material = createGalvanizedMaterial();

    const vent = new THREE.Mesh(
        new THREE.CylinderGeometry(ventRadius, ventRadius, ventHeight, 40),
        material
    );

    vent.position.set(radius * 0.32, height + ventHeight * 0.65, -radius * 0.18);
    vent.castShadow = true;
    vent.receiveShadow = true;
    vent.frustumCulled = false;

    vent.userData = {
        tipo: "Ventilación de techo",
        material: "Acero",
        altura: "Techo",
        espesor: "—",
        diametro: formatTechnicalValue(ventRadius * 2, "u.3D")
    };

    group.add(vent);

    const cap = new THREE.Mesh(
        new THREE.CylinderGeometry(ventRadius * 1.38, ventRadius * 1.38, ventHeight * 0.16, 40),
        material
    );

    cap.position.set(vent.position.x, vent.position.y + ventHeight * 0.55, vent.position.z);
    cap.castShadow = true;
    cap.receiveShadow = true;
    cap.frustumCulled = false;

    group.add(cap);
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
        } else if (tipo.includes("VERTICAL")) {
            addVerticalLadder(group, radius, height, angleOffset, scale);
        }
    }
}

function addVerticalLadder(group, radius, height, angleOffset = 0, scale) {
    const ladderMaterial = new THREE.MeshStandardMaterial({
        color: 0xd97706,
        metalness: 0.82,
        roughness: 0.2,
        side: THREE.DoubleSide
    });

    const cageMaterial = createGalvanizedMaterial();
    const platformMaterial = new THREE.MeshStandardMaterial({
        color: 0x64748b,
        metalness: 0.82,
        roughness: 0.2,
        side: THREE.DoubleSide
    });

    const angle = -Math.PI / 4 + angleOffset;
    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));

    const ladderRadius = radius + Math.max(radius * 0.028, 0.24);
    const centerBase = radial.clone().multiplyScalar(ladderRadius);

    const railHalfWidth = Math.max(radius * 0.032, 0.32);
    const railRadius = Math.max(radius * 0.0054, 0.026);
    const rungRadius = Math.max(radius * 0.0043, 0.021);

    const bottomY = Math.max(height * 0.01, 0.05);
    const topY = height + Math.max(radius * 0.1, 0.75);

    const leftRailBottom = centerBase.clone().add(tangent.clone().multiplyScalar(-railHalfWidth));
    const leftRailTop = centerBase.clone().add(tangent.clone().multiplyScalar(-railHalfWidth));
    const rightRailBottom = centerBase.clone().add(tangent.clone().multiplyScalar(railHalfWidth));
    const rightRailTop = centerBase.clone().add(tangent.clone().multiplyScalar(railHalfWidth));

    leftRailBottom.y = bottomY;
    leftRailTop.y = topY;
    rightRailBottom.y = bottomY;
    rightRailTop.y = topY;

    addCylinderBetween(group, leftRailBottom, leftRailTop, railRadius, ladderMaterial, 16);
    addCylinderBetween(group, rightRailBottom, rightRailTop, railRadius, ladderMaterial, 16);

    const modelRungSpacing = scale && scale > 0 ? 0.3 * scale : 0.26;
    const rungSpacing = Math.max(Math.min(modelRungSpacing, 0.38), 0.16);
    const rungCount = Math.max(14, Math.floor((topY - bottomY) / rungSpacing));

    for (let i = 1; i < rungCount; i++) {
        const y = bottomY + ((topY - bottomY) * i) / rungCount;

        const left = centerBase.clone().add(tangent.clone().multiplyScalar(-railHalfWidth));
        const right = centerBase.clone().add(tangent.clone().multiplyScalar(railHalfWidth));

        left.y = y;
        right.y = y;

        addCylinderBetween(group, left, right, rungRadius, ladderMaterial, 12);
    }

    addCircularLadderCage(group, radius, height, radial, tangent, centerBase, cageMaterial, scale);
    addVerticalLadderIntermediatePlatform(group, radius, height, radial, tangent, platformMaterial, cageMaterial, centerBase, railHalfWidth);
    addVerticalLadderTopPlatform(group, radius, height, radial, tangent, platformMaterial, cageMaterial, centerBase, railHalfWidth);
    addLadderTankBrackets(group, radius, height, radial, centerBase, cageMaterial);
}

function addCircularLadderCage(group, radius, height, radial, tangent, centerBase, material, scale) {
    const cageRadius = Math.max(radius * 0.07, 0.52);
    const tubeRadius = Math.max(radius * 0.0025, 0.012);
    const cageCenter = centerBase.clone().add(radial.clone().multiplyScalar(cageRadius * 0.78));

    const startY = scale && scale > 0
        ? Math.max(2.2 * scale, height * 0.1)
        : height * 0.14;

    const endY = height + Math.max(radius * 0.075, 0.55);

    if (endY <= startY) return;

    const ringSpacing = scale && scale > 0
        ? Math.max(0.52, Math.min(0.9 * scale, 1.05))
        : Math.max(radius * 0.1, 0.62);

    const ringCount = Math.max(5, Math.ceil((endY - startY) / ringSpacing));

    for (let i = 0; i <= ringCount; i++) {
        const y = startY + ((endY - startY) * i) / ringCount;
        addCageOpenHoop(group, cageCenter, radial, tangent, cageRadius, y, tubeRadius, material);
    }

    [-0.68, -0.42, -0.22, 0.22, 0.42, 0.68].forEach(v => {
        const a = Math.PI * v;
        const offset = radial.clone().multiplyScalar(Math.cos(a) * cageRadius)
            .add(tangent.clone().multiplyScalar(Math.sin(a) * cageRadius));

        const bottom = cageCenter.clone().add(offset);
        const top = cageCenter.clone().add(offset);

        bottom.y = startY;
        top.y = endY;

        addCylinderBetween(group, bottom, top, tubeRadius, material, 8);
    });
}

function addCageOpenHoop(group, cageCenter, radial, tangent, cageRadius, y, tubeRadius, material) {
    const segments = 26;
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

function addVerticalLadderIntermediatePlatform(group, radius, height, radial, tangent, platformMaterial, railMaterial, centerBase, railHalfWidth) {
    const platformY = height * 0.46;
    if (platformY <= 0.8) return;

    const width = Math.max(railHalfWidth * 4.2, 1.55);
    const depth = Math.max(railHalfWidth * 2.8, 1.05);
    const thickness = Math.max(radius * 0.008, 0.055);

    const center = centerBase.clone().add(radial.clone().multiplyScalar(depth * 0.75));
    center.y = platformY;

    const platform = new THREE.Mesh(new THREE.BoxGeometry(width, thickness, depth), platformMaterial);
    platform.position.copy(center);
    platform.rotation.y = -Math.atan2(radial.z, radial.x) + Math.PI / 2;
    platform.castShadow = true;
    platform.receiveShadow = true;
    platform.frustumCulled = false;

    platform.userData = {
        tipo: "Plataforma intermedia de escalera",
        material: "Acero galvanizado",
        altura: formatTechnicalValue(platformY, "u.3D"),
        espesor: "—",
        diametro: "—"
    };

    group.add(platform);

    const supportRadius = Math.max(radius * 0.006, 0.025);

    const leftOuter = center.clone().add(tangent.clone().multiplyScalar(-width * 0.38)).add(radial.clone().multiplyScalar(depth * 0.35));
    const rightOuter = center.clone().add(tangent.clone().multiplyScalar(width * 0.38)).add(radial.clone().multiplyScalar(depth * 0.35));
    const leftBottom = centerBase.clone().add(tangent.clone().multiplyScalar(-width * 0.32)).add(radial.clone().multiplyScalar(-depth * 0.05));
    const rightBottom = centerBase.clone().add(tangent.clone().multiplyScalar(width * 0.32)).add(radial.clone().multiplyScalar(-depth * 0.05));

    leftOuter.y = platformY - thickness;
    rightOuter.y = platformY - thickness;
    leftBottom.y = platformY - Math.max(radius * 0.22, 1.25);
    rightBottom.y = platformY - Math.max(radius * 0.22, 1.25);

    addCylinderBetween(group, leftBottom, leftOuter, supportRadius, railMaterial, 12);
    addCylinderBetween(group, rightBottom, rightOuter, supportRadius, railMaterial, 12);
}

function addVerticalLadderTopPlatform(group, radius, height, radial, tangent, platformMaterial, railMaterial, centerBase, railHalfWidth) {
    const width = Math.max(railHalfWidth * 3.2, 1.1);
    const depth = Math.max(railHalfWidth * 2.1, 0.8);
    const thickness = Math.max(radius * 0.006, 0.04);

    const center = centerBase.clone().add(radial.clone().multiplyScalar(depth * 0.55));
    center.y = height + thickness * 1.5;

    const platform = new THREE.Mesh(new THREE.BoxGeometry(width, thickness, depth), platformMaterial);
    platform.position.copy(center);
    platform.rotation.y = -Math.atan2(radial.z, radial.x) + Math.PI / 2;
    platform.castShadow = true;
    platform.receiveShadow = true;
    platform.frustumCulled = false;

    platform.userData = {
        tipo: "Plataforma superior de escalera",
        material: "Acero galvanizado",
        altura: formatTechnicalValue(center.y, "u.3D"),
        espesor: "—",
        diametro: "—"
    };

    group.add(platform);

    addPlatformRails(
        group,
        center,
        radial,
        tangent,
        width,
        depth,
        height,
        thickness,
        Math.max(radius * 0.07, 0.65),
        Math.max(radius * 0.0034, 0.018),
        railMaterial,
        { openBack: true, openFront: false }
    );
}

function addLadderTankBrackets(group, radius, height, radial, centerBase, material) {
    const bracketRadius = Math.max(radius * 0.0035, 0.018);
    const bracketCount = Math.max(6, Math.floor(height / Math.max(radius * 0.18, 1)));

    for (let i = 0; i <= bracketCount; i++) {
        const y = (height * i) / bracketCount;

        const wallPoint = radial.clone().multiplyScalar(radius * 1.002);
        wallPoint.y = y;

        const ladderPoint = centerBase.clone();
        ladderPoint.y = y;

        addCylinderBetween(group, wallPoint, ladderPoint, bracketRadius, material, 12);
    }
}

function addHelicalStair(group, radius, height, angleOffset = 0) {
    const stepMaterial = new THREE.MeshStandardMaterial({
        color: 0xd97706,
        metalness: 0.82,
        roughness: 0.2
    });

    const railMaterial = createGalvanizedMaterial();
    const supportMaterial = createDarkSteelMaterial();

    const stairRadius = radius + Math.max(radius * 0.02, 0.12);
    const outerRadius = stairRadius + Math.max(radius * 0.095, 0.62);
    const innerRadius = stairRadius - Math.max(radius * 0.052, 0.34);
    const midRadius = (outerRadius + innerRadius) / 2;

    const turns = Math.max(0.65, height / Math.max(radius * 6.25, 1));
    const steps = Math.max(48, Math.floor(turns * 58));

    const stepWidth = Math.max(radius * 0.155, 0.9);
    const stepDepth = Math.max(radius * 0.06, 0.3);
    const stepHeight = Math.max(radius * 0.012, 0.055);

    const railHeight = Math.max(radius * 0.105, 0.95);
    const railRadius = Math.max(radius * 0.0052, 0.028);
    const postRadius = Math.max(radius * 0.0058, 0.03);
    const stringerRadius = Math.max(radius * 0.0065, 0.035);

    const outerRail = [];
    const innerRail = [];
    const midRail = [];
    const outerStringer = [];
    const innerStringer = [];

    for (let i = 0; i < steps; i++) {
        const t = i / (steps - 1);
        const angle = -Math.PI / 2 + angleOffset + t * turns * Math.PI * 2;
        const y = t * height;

        const step = new THREE.Mesh(new THREE.BoxGeometry(stepWidth, stepHeight, stepDepth), stepMaterial);
        step.position.set(Math.cos(angle) * midRadius, y, Math.sin(angle) * midRadius);
        step.rotation.y = -angle;
        step.castShadow = true;
        step.receiveShadow = true;
        step.frustumCulled = false;

        step.userData = {
            tipo: "Escalera helicoidal",
            material: "Acero galvanizado",
            altura: formatTechnicalValue(y, "u.3D"),
            espesor: "—",
            diametro: "—"
        };

        group.add(step);

        const outerBase = new THREE.Vector3(Math.cos(angle) * outerRadius, y, Math.sin(angle) * outerRadius);
        const innerBase = new THREE.Vector3(Math.cos(angle) * innerRadius, y, Math.sin(angle) * innerRadius);

        const outerTop = outerBase.clone();
        outerTop.y += railHeight;

        const innerTop = innerBase.clone();
        innerTop.y += railHeight * 0.92;

        const midTop = outerBase.clone();
        midTop.y += railHeight * 0.52;

        const outerLow = outerBase.clone();
        outerLow.y -= stepHeight * 1.2;

        const innerLow = innerBase.clone();
        innerLow.y -= stepHeight * 1.2;

        outerRail.push(outerTop);
        innerRail.push(innerTop);
        midRail.push(midTop);
        outerStringer.push(outerLow);
        innerStringer.push(innerLow);

        if (i % 3 === 0 || i === steps - 1) {
            addCylinderBetween(group, outerBase, outerTop, postRadius, railMaterial, 12);
            addCylinderBetween(group, innerBase, innerTop, postRadius * 0.85, railMaterial, 12);
        }

        if (i % 4 === 0) {
            const wallPoint = new THREE.Vector3(Math.cos(angle) * radius * 1.002, y, Math.sin(angle) * radius * 1.002);
            const stairPoint = new THREE.Vector3(Math.cos(angle) * innerRadius, y, Math.sin(angle) * innerRadius);
            addCylinderBetween(group, wallPoint, stairPoint, postRadius * 0.75, supportMaterial, 10);
        }
    }

    connectPath(group, outerRail, railRadius, railMaterial, 12);
    connectPath(group, innerRail, railRadius, railMaterial, 12);
    connectPath(group, midRail, railRadius * 0.8, railMaterial, 10);
    connectPath(group, outerStringer, stringerRadius, supportMaterial, 12);
    connectPath(group, innerStringer, stringerRadius * 0.85, railMaterial, 12);

    const finalAngle = -Math.PI / 2 + angleOffset + turns * Math.PI * 2;
    addHelicalTopPlatform(group, radius, height, finalAngle, supportMaterial, railMaterial);
}

function addHelicalTopPlatform(group, radius, height, angle, platformMaterial, railMaterial) {
    const radial = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
    const tangent = new THREE.Vector3(-Math.sin(angle), 0, Math.cos(angle));

    const width = Math.max(radius * 0.26, 1.55);
    const depth = Math.max(radius * 0.18, 1.15);
    const thickness = Math.max(radius * 0.012, 0.06);

    const center = radial.clone().multiplyScalar(radius + depth * 0.42);
    center.y = height + thickness;

    const platform = new THREE.Mesh(new THREE.BoxGeometry(width, thickness, depth), platformMaterial);
    platform.position.copy(center);
    platform.rotation.y = -Math.atan2(radial.z, radial.x) + Math.PI / 2;
    platform.castShadow = true;
    platform.receiveShadow = true;
    platform.frustumCulled = false;
    group.add(platform);

    addPlatformRails(
        group,
        center,
        radial,
        tangent,
        width,
        depth,
        height,
        thickness,
        Math.max(radius * 0.09, 0.82),
        Math.max(radius * 0.0052, 0.028),
        railMaterial,
        { openBack: true, openFront: false }
    );
}

function connectPath(group, points, radius, material, segments) {
    for (let i = 0; i < points.length - 1; i++) {
        addCylinderBetween(group, points[i], points[i + 1], radius, material, segments);
    }
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

        addCylinderBetween(group, bottom, top, railRadius, material, 12);
    });

    if (!options.openFront) addCylinderBetween(group, tops[0], tops[1], railRadius, material, 12);
    if (!options.openBack) addCylinderBetween(group, tops[2], tops[3], railRadius, material, 12);

    addCylinderBetween(group, tops[0], tops[2], railRadius, material, 12);
    addCylinderBetween(group, tops[1], tops[3], railRadius, material, 12);
}

function addCircularRail(group, radius, y, tubeRadius, material) {
    const points = [];

    for (let i = 0; i <= VISUAL_CONFIG.curveSegments; i++) {
        const angle = (Math.PI * 2 * i) / VISUAL_CONFIG.curveSegments;
        points.push(new THREE.Vector3(Math.cos(angle) * radius, y, Math.sin(angle) * radius));
    }

    for (let i = 0; i < points.length - 1; i++) {
        addCylinderBetween(group, points[i], points[i + 1], tubeRadius, material, 10);
    }
}

function addCylinderBetween(group, start, end, radius, material, segments) {
    const direction = new THREE.Vector3().subVectors(end, start);
    const length = direction.length();

    if (length <= 0) return;

    const geometry = new THREE.CylinderGeometry(radius, radius, length, segments || 16, 1, false);
    const mesh = new THREE.Mesh(geometry, material);

    mesh.position.copy(start).add(end).multiplyScalar(0.5);

    const quaternion = new THREE.Quaternion();
    quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction.clone().normalize());

    mesh.quaternion.copy(quaternion);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    mesh.frustumCulled = false;

    group.add(mesh);
}

function addReferenceGrid(group, radius, height) {
    const size = Math.max(radius * 3.2, height * 1.4, 20);
    const grid = new THREE.GridHelper(size, 20, 0x94a3b8, 0xd1d5db);

    grid.position.y = -0.02;
    grid.material.opacity = 0.48;
    grid.material.transparent = true;
    grid.frustumCulled = false;

    group.add(grid);
}

function addVerticalReference(group, radius, height) {
    const geometry = new THREE.BufferGeometry().setFromPoints([
        new THREE.Vector3(radius * 1.35, 0, 0),
        new THREE.Vector3(radius * 1.35, height, 0)
    ]);

    const line = new THREE.Line(
        geometry,
        new THREE.LineBasicMaterial({
            color: 0xef4444,
            transparent: true,
            opacity: 0.7
        })
    );

    line.frustumCulled = false;
    group.add(line);
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
            viewer.renderer.domElement.style.cursor = viewer.isDragging ? "grabbing" : "grab";
            return;
        }

        const data = valid.object.userData;
        viewer.renderer.domElement.style.cursor = "pointer";

        overlay.innerHTML = `
            <div style="font-size:11px;text-transform:uppercase;letter-spacing:1px;color:#64748b;margin-bottom:4px;">
                Detalle de pieza
            </div>
            <strong style="font-size:15px;color:#0f172a;">${data.tipo}</strong>
            <div style="margin-top:8px;display:grid;grid-template-columns:auto auto;gap:4px 12px;color:#334155;">
                <span>Material:</span><strong>${data.material || "—"}</strong>
                <span>Altura:</span><strong>${data.altura || "—"}</strong>
                <span>Espesor:</span><strong>${data.espesor || "—"}</strong>
                <span>Diámetro:</span><strong>${data.diametro || "—"}</strong>
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

        if (canvas.hasPointerCapture(e.pointerId)) {
            canvas.releasePointerCapture(e.pointerId);
        }

        canvas.style.cursor = "grab";
    });

    canvas.addEventListener("pointerleave", () => {
        viewer.isDragging = false;
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
        viewer.inputPitch = Math.max(-Math.PI / 2 + 0.1, Math.min(Math.PI / 2 - 0.1, viewer.inputPitch));
    });

    canvas.addEventListener("wheel", e => {
        e.preventDefault();

        const factor = e.deltaY > 0 ? 1.1 : 0.9;
        viewer.inputDistance = Math.max(15, Math.min(420, viewer.inputDistance * factor));
    }, { passive: false });
}

function resize(viewer) {
    const rect = viewer.shell.getBoundingClientRect();

    const width = Math.max(320, rect.width || 320);
    const height = Math.max(720, rect.height || 720);

    viewer.camera.aspect = width / height;
    viewer.camera.updateProjectionMatrix();
    viewer.renderer.setSize(width, height, false);
}

function animate(viewer) {
    viewer.animationId = requestAnimationFrame(() => animate(viewer));

    if (!viewer.renderer || !viewer.scene || !viewer.camera) return;

    viewer.yaw += (viewer.inputYaw - viewer.yaw) * 0.12;
    viewer.pitch += (viewer.inputPitch - viewer.pitch) * 0.12;
    viewer.distance += (viewer.inputDistance - viewer.distance) * 0.15;

    updateCamera(viewer);
    viewer.renderer.render(viewer.scene, viewer.camera);
}

function fitCamera(viewer) {
    const height = viewer.modelHeight || 40;
    const outerRadius = viewer.modelOuterRadius || viewer.modelRadius || 20;
    const maxSize = Math.max(height, outerRadius * 2, 1);

    viewer.inputDistance = maxSize * VISUAL_CONFIG.cameraPadding;
    viewer.inputPitch = 0.24;
    viewer.inputYaw = 0.74;

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

    viewer.camera.near = 0.05;
    viewer.camera.far = Math.max(1000, viewer.distance * 10);
    viewer.camera.updateProjectionMatrix();
}

function addScaleBadge(shell, metersPerUnit, tank) {
    const roof = normalizarTecho(tank.techo);
    const escalera = tank.escalera?.tipo ? tank.escalera.tipo : "—";
    const numeroEscaleras = Number(tank.escalera?.numeroEscaleras) || 0;

    const badge = document.createElement("div");

    badge.innerHTML = `
        <div style="font-size:11px;text-transform:uppercase;letter-spacing:1px;color:#94a3b8;margin-bottom:6px;">
            Modelo técnico
        </div>
        <strong style="font-size:14px;color:#fff;">Tanque 3D</strong><br>
        <span>Escala: 1 u. 3D = ${metersPerUnit.toFixed(2)} m</span><br>
        <span>Techo: ${roof.label}</span><br>
        <span>Escalera: ${escalera}${numeroEscaleras > 0 ? ` (${numeroEscaleras})` : ""}</span>
    `;

    badge.style.position = "absolute";
    badge.style.right = "22px";
    badge.style.bottom = "100px";
    badge.style.zIndex = "5";
    badge.style.padding = "14px 16px";
    badge.style.borderRadius = "18px";
    badge.style.background = "rgba(15,23,42,0.90)";
    badge.style.border = "1px solid rgba(255,255,255,0.08)";
    badge.style.boxShadow = "0 18px 40px rgba(15,23,42,0.22)";
    badge.style.color = "#e5e7eb";
    badge.style.font = "12px 'Segoe UI', Arial";
    badge.style.lineHeight = "1.55";
    badge.style.backdropFilter = "blur(10px)";

    shell.appendChild(badge);
}

function addTechnicalControls(shell, container) {
    const panel = document.createElement("div");

    panel.style.position = "absolute";
    panel.style.left = "20px";
    panel.style.bottom = "100px";
    panel.style.zIndex = "9";
    panel.style.width = "196px";
    panel.style.padding = "15px";
    panel.style.borderRadius = "18px";
    panel.style.background = "rgba(15,23,42,0.90)";
    panel.style.border = "1px solid rgba(255,255,255,0.08)";
    panel.style.boxShadow = "0 18px 40px rgba(15,23,42,0.22)";
    panel.style.backdropFilter = "blur(10px)";
    panel.style.color = "white";
    panel.style.font = "13px 'Segoe UI', Arial";

    panel.innerHTML = `
        <div style="font-weight:700;font-size:14px;margin-bottom:12px;color:#fff;">Vista técnica</div>
        ${createTechnicalCheckbox("Techo", "showRoof")}
        ${createTechnicalCheckbox("Barandilla", "showGuardrail")}
        ${createTechnicalCheckbox("Conexiones", "showConnections")}
        ${createTechnicalCheckbox("Escalera", "showLadder")}
        ${createTechnicalCheckbox("Agua", "showWater")}
        ${createTechnicalCheckbox("Referencias", "showReferences")}
    `;

    panel.querySelectorAll("input").forEach(input => {
        input.addEventListener("change", () => {
            technicalViewState[input.dataset.key] = input.checked;

            const viewer = viewers.get(container);
            if (!viewer) return;

            rebuildTank(viewer);
        });
    });

    shell.appendChild(panel);
}

function createTechnicalCheckbox(label, key) {
    return `
        <label style="display:flex;gap:8px;margin-top:8px;align-items:center;cursor:pointer;user-select:none;">
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
    overlay.style.top = "20px";
    overlay.style.transform = "translateX(-50%)";
    overlay.style.padding = "12px 16px";
    overlay.style.borderRadius = "16px";
    overlay.style.background = "rgba(255,255,255,0.96)";
    overlay.style.border = "1px solid rgba(15,23,42,0.08)";
    overlay.style.color = "#111827";
    overlay.style.font = "13px 'Segoe UI', Arial";
    overlay.style.zIndex = "20";
    overlay.style.pointerEvents = "none";
    overlay.style.display = "none";
    overlay.style.boxShadow = "0 18px 45px rgba(15,23,42,0.18)";

    shell.appendChild(overlay);
}

function addDownloadPngButton(shell, renderer, tank) {
    const button = document.createElement("button");

    button.type = "button";
    button.textContent = "Descargar PNG";
    button.style.position = "absolute";
    button.style.right = "20px";
    button.style.top = "20px";
    button.style.zIndex = "8";
    button.style.border = "0";
    button.style.borderRadius = "14px";
    button.style.padding = "10px 15px";
    button.style.background = "#ffffff";
    button.style.color = "#111827";
    button.style.font = "700 13px 'Segoe UI', Arial";
    button.style.cursor = "pointer";
    button.style.boxShadow = "0 10px 24px rgba(15,23,42,0.12)";

    button.addEventListener("click", () => {
        renderer.render(renderer.__tank3dScene, renderer.__tank3dCamera);

        const link = document.createElement("a");
        link.download = `tank-3d-${tank?.id || "modelo"}-${new Date().toISOString().slice(0, 10)}.png`;
        link.href = renderer.domElement.toDataURL("image/png");
        link.click();
    });

    shell.appendChild(button);
}

function addMouseHelpPanel(shell) {
    const panel = document.createElement("div");

    panel.innerHTML = `Rotar: clic izquierdo · Zoom: rueda · Datos: cursor`;
    panel.style.position = "absolute";
    panel.style.left = "50%";
    panel.style.bottom = "20px";
    panel.style.transform = "translateX(-50%)";
    panel.style.zIndex = "12";
    panel.style.padding = "10px 18px";
    panel.style.borderRadius = "18px";
    panel.style.background = "rgba(15,23,42,0.90)";
    panel.style.border = "1px solid rgba(255,255,255,0.08)";
    panel.style.color = "#ffffff";
    panel.style.font = "12px 'Segoe UI', Arial";
    panel.style.pointerEvents = "none";
    panel.style.boxShadow = "0 18px 40px rgba(15,23,42,0.22)";
    panel.style.backdropFilter = "blur(10px)";

    shell.appendChild(panel);
}

function colorForMaterial(name) {
    const normalized = String(name || "").toUpperCase();

    if (normalized.includes("HSLA")) return 0x71717a;
    if (normalized.includes("S355")) return 0x52525b;
    if (normalized.includes("S275")) return 0x71717a;
    if (normalized.includes("S235")) return 0xa1a1aa;
    if (normalized.includes("GLASS") || normalized.includes("VITR")) return 0x164e63;

    return 0x737373;
}

function calcularRadioNucleoTecho(radius, numeroVigas, vigasTechoConico, scale) {
    const manualDiameter =
        Number(vigasTechoConico?.diametroNucleoCentralManual) ||
        Number(vigasTechoConico?.diametroNucleoTechoConicoManual) ||
        Number(vigasTechoConico?.diametroNucleoCentral) ||
        Number(vigasTechoConico?.diametroNucleo) ||
        0;

    if (manualDiameter > 0 && scale > 0) {
        return Math.max(manualDiameter * scale / 2, radius * 0.06, 0.35);
    }

    const factorUsuario = Number(vigasTechoConico?.factorNucleo3D) || 0;
    const factorPorTamano = radius < 6 ? 0.2 : radius < 12 ? 0.17 : 0.145;
    const factorPorVigas = Math.min(0.1, Math.max(0, numeroVigas) * 0.0025);
    const factorFinal = factorUsuario > 0 ? factorUsuario : factorPorTamano + factorPorVigas;

    return Math.max(radius * factorFinal, radius * 0.08, 0.42);
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

    return rawHeight > 50 ? rawHeight * scale / 1000 : rawHeight * scale;
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

function formatTechnicalValue(value, suffix) {
    const n = Number(value);
    if (!Number.isFinite(n) || n <= 0) return "—";
    return `${n} ${suffix}`;
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

function showError(container, message) {
    container.innerHTML = `
        <div style="
            padding:20px;
            border-radius:16px;
            background:#fef2f2;
            color:#991b1b;
            border:1px solid #fecaca;
            font-family:'Segoe UI', Arial, sans-serif;
            font-weight:600;
            font-size:14px;
            box-shadow:0 10px 30px rgba(0,0,0,0.05);">
            ${message}
        </div>
    `;
}

window.tank3d = {
    renderTank3D
};