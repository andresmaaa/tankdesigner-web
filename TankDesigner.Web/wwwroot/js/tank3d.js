const viewers = new WeakMap();

const technicalViewState = {
    showRoof: true,
    showGuardrail: false,
    showConnections: true,
    showLadder: true,
    showReferences: false,
    showWater: true
};

// ======================================================
// CONFIGURACIÓN VISUAL GLOBAL PROFESIONAL
// ======================================================

const VISUAL_CONFIG = {
    shellSegments: 96,
    curveSegments: 128,
    shadowMapSize: 2048,

    materials: {
        steel: {
            metalness: 0.82,
            roughness: 0.26,
            envMapIntensity: 1.15
        },

        galvanized: {
            metalness: 0.88,
            roughness: 0.22,
            envMapIntensity: 1.3
        },

        darkSteel: {
            metalness: 0.9,
            roughness: 0.18,
            envMapIntensity: 1.4
        }
    }
};

// ======================================================
// RENDER PRINCIPAL
// ======================================================

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
        showError(container, "No hay anillos válidos.");
        return;
    }

    const rings = tank.anillos.filter(r => Number(r.altura) > 0);

    const realDiameter = Number(tank.diametro) || 1;
    const realHeight =
        Number(tank.alturaTotal) ||
        rings.reduce((s, r) => s + Number(r.altura || 0), 0);

    const maxRealSize = Math.max(realDiameter, realHeight, 1);

    const targetModelSize = 42;
    const scale = targetModelSize / maxRealSize;

    const metersPerUnit = 1 / scale;

    const viewer = createViewer(
        container,
        scale,
        metersPerUnit,
        tank,
        dotNetRef
    );

    viewers.set(container, viewer);

    buildTank(viewer, tank, rings, scale);

    fitCamera(viewer);

    viewer.renderer.render(viewer.scene, viewer.camera);
}

// ======================================================
// DISPOSE
// ======================================================

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

        if (obj.geometry) {
            obj.geometry.dispose();
        }

        if (obj.material) {

            if (Array.isArray(obj.material)) {
                obj.material.forEach(m => m.dispose());
            }
            else {
                obj.material.dispose();
            }
        }
    });

    oldViewer.renderer.dispose();

    if (
        oldViewer.renderer.domElement &&
        oldViewer.renderer.domElement.parentNode
    ) {
        oldViewer.renderer.domElement.parentNode.removeChild(
            oldViewer.renderer.domElement
        );
    }

    viewers.delete(container);
}

// ======================================================
// CREATE VIEWER PROFESIONAL
// ======================================================

function createViewer(container, scale, metersPerUnit, tank, dotNetRef) {

    const shell = document.createElement("div");

    shell.style.position = "relative";
    shell.style.width = "100%";
    shell.style.height = "720px";
    shell.style.borderRadius = "24px";
    shell.style.overflow = "hidden";

    shell.style.background = `
        radial-gradient(
            circle at top,
            #f8fafc 0%,
            #edf2f7 40%,
            #dbe4ee 100%
        )
    `;

    shell.style.boxShadow = `
        inset 0 1px 0 rgba(255,255,255,0.7),
        0 10px 40px rgba(15,23,42,0.08)
    `;

    container.appendChild(shell);

    // SCENE

    const scene = new THREE.Scene();

    // CAMERA

    const camera = new THREE.PerspectiveCamera(
        30,
        1,
        0.1,
        10000
    );

    // RENDERER

    const renderer = new THREE.WebGLRenderer({
        antialias: true,
        alpha: true,
        preserveDrawingBuffer: true,
        powerPreference: "high-performance"
    });

    renderer.setPixelRatio(
        Math.min(window.devicePixelRatio || 1, 2)
    );

    renderer.shadowMap.enabled = true;

    renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    renderer.outputColorSpace = THREE.SRGBColorSpace;

    renderer.toneMapping = THREE.ACESFilmicToneMapping;

    renderer.toneMappingExposure = 1.05;

    shell.appendChild(renderer.domElement);

    // GROUP

    const group = new THREE.Group();

    scene.add(group);

    // ILUMINACIÓN PROFESIONAL

    const ambient = new THREE.AmbientLight(
        0xffffff,
        0.55
    );

    scene.add(ambient);

    const keyLight = new THREE.DirectionalLight(
        0xffffff,
        1.65
    );

    keyLight.position.set(40, 55, 40);

    keyLight.castShadow = true;

    keyLight.shadow.mapSize.width =
        VISUAL_CONFIG.shadowMapSize;

    keyLight.shadow.mapSize.height =
        VISUAL_CONFIG.shadowMapSize;

    keyLight.shadow.bias = -0.0004;

    keyLight.shadow.radius = 2.5;

    scene.add(keyLight);

    const fillLight = new THREE.DirectionalLight(
        0xdbeafe,
        0.8
    );

    fillLight.position.set(-45, 15, -20);

    scene.add(fillLight);

    const rimLight = new THREE.DirectionalLight(
        0xffffff,
        1.5
    );

    rimLight.position.set(-25, 40, -60);

    scene.add(rimLight);

    const topLight = new THREE.DirectionalLight(
        0xffffff,
        0.45
    );

    topLight.position.set(0, 90, 0);

    scene.add(topLight);

    // ENTORNO PBR

    const pmrem = new THREE.PMREMGenerator(renderer);

    const envScene = new THREE.Scene();

    envScene.background = new THREE.Color(0xf1f5f9);

    const envLight = new THREE.Mesh(
        new THREE.SphereGeometry(30, 16, 16),
        new THREE.MeshBasicMaterial({
            color: 0xffffff
        })
    );

    envLight.position.set(0, 60, 0);

    envScene.add(envLight);

    scene.environment =
        pmrem.fromScene(envScene).texture;

    // GROUND SHADOW

    const shadowPlane = new THREE.Mesh(
        new THREE.PlaneGeometry(250, 250),
        new THREE.ShadowMaterial({
            opacity: 0.12
        })
    );

    shadowPlane.rotation.x = -Math.PI / 2;

    shadowPlane.position.y = -0.02;

    shadowPlane.receiveShadow = true;

    scene.add(shadowPlane);

    // UI

    addScaleBadge(shell, metersPerUnit, tank);

    addRoofControls(shell, container, tank, dotNetRef);

    addTechnicalControls(shell, container, tank, dotNetRef);

    addTechnicalInfoOverlay(shell);

    addDownloadPngButton(shell, renderer);

    addMouseHelpPanel(shell);

    // VIEWER

    const viewer = {
        container,
        shell,
        scene,
        camera,
        renderer,
        group,

        inputYaw: 0.72,
        inputPitch: 0.24,
        inputDistance: 72,

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

    const resizeObserver =
        new ResizeObserver(() => resize(viewer));

    resizeObserver.observe(container);

    viewer.resizeObserver = resizeObserver;

    animate(viewer);

    return viewer;
}
// BUILD TANK PROFESIONAL

function buildTank(viewer, tank, rings, scale) {

    const diameter =
        (Number(tank.diametro) || 1) * scale;

    const radius = diameter / 2;

    let currentY = 0;

    // STARTER RING

    const starterHeight =
        getStarterRingHeight(tank, scale);

    const starterTotalHeight =
        addStarterRing(
            viewer.group,
            radius,
            starterHeight,
            tank
        );

    currentY += starterTotalHeight;

    // ANILLOS

    rings.forEach((ring, index) => {

        const height =
            Number(ring.altura) * scale;

        const materialName =
            ring.material ||
            tank.materialPrincipal ||
            "material";

        const baseColor =
            colorForMaterial(materialName);

        const variation =
            index % 2 === 0 ? 0.97 : 1.03;

        const shellMaterial =
            createSteelMaterial(
                adjustColor(baseColor, variation)
            );

        const shellGeometry =
            new THREE.CylinderGeometry(
                radius,
                radius,
                height,
                VISUAL_CONFIG.shellSegments,
                1,
                true
            );

        const shell = new THREE.Mesh(
            shellGeometry,
            shellMaterial
        );

        shell.position.y =
            currentY + height / 2;

        shell.castShadow = true;
        shell.receiveShadow = true;

        shell.userData = {
            tipo: `Anillo ${index + 1}`,
            material: materialName,
            altura: formatTechnicalValue(
                ring.altura,
                "m"
            ),
            espesor: formatTechnicalValue(
                ring.espesor,
                "mm"
            ),
            diametro: formatTechnicalValue(
                tank.diametro,
                "m"
            )
        };

        viewer.group.add(shell);

        // LÍNEAS TÉCNICAS

        const edges = new THREE.LineSegments(
            new THREE.EdgesGeometry(
                shellGeometry,
                22
            ),

            new THREE.LineBasicMaterial({
                color: 0x000000,
                transparent: true,
                opacity: 0.08
            })
        );

        edges.position.copy(shell.position);

        viewer.group.add(edges);

        // UNIÓN ENTRE ANILLOS

        addRingSeam(
            viewer.group,
            radius,
            currentY
        );

        addRingSeam(
            viewer.group,
            radius,
            currentY + height
        );

        currentY += height;
    });

    // BASE

    addBottomDisc(
        viewer.group,
        radius * 0.985
    );

    // AGUA

    if (technicalViewState.showWater) {

        addWaterLevelIfAvailable(
            viewer.group,
            radius,
            currentY,
            tank,
            scale
        );
    }

    // RIGIDIZADOR SUPERIOR
    // 

    addTopStiffener(
        viewer.group,
        radius,
        currentY
    );

    // TECHO

    if (technicalViewState.showRoof) {

        addRoof(
            viewer.group,
            radius,
            currentY,
            tank.techo,
            tank.vigasTechoConico,
            scale
        );
    }

    // BARANDILLA

    if (technicalViewState.showGuardrail) {

        addRoofGuardrail(
            viewer.group,
            radius,
            currentY,
            tank.techo
        );
    }

    // CONEXIONES

    if (technicalViewState.showConnections) {

        addTankConnections(
            viewer.group,
            radius,
            currentY
        );
    }

    // MANHOLE

    addManhole(
        viewer.group,
        radius,
        currentY
    );

    // VENT

    addRoofVent(
        viewer.group,
        radius,
        currentY,
        tank.techo
    );

    // REFERENCIAS

    if (technicalViewState.showReferences) {

        addReferenceGrid(
            viewer.group,
            radius,
            currentY
        );

        addVerticalReference(
            viewer.group,
            radius,
            currentY
        );
    }

    // ESCALERA

    if (technicalViewState.showLadder) {

        addLadder(
            viewer.group,
            radius,
            currentY,
            tank.escalera,
            scale
        );
    }

    // CENTRADO

    viewer.group.position.y =
        -currentY / 2;

    viewer.modelRadius = radius;

    viewer.modelHeight = currentY;
}

// MATERIAL METÁLICO PROFESIONAL

function createSteelMaterial(color) {

    return new THREE.MeshStandardMaterial({

        color,

        metalness:
            VISUAL_CONFIG.materials.steel.metalness,

        roughness:
            VISUAL_CONFIG.materials.steel.roughness,

        envMapIntensity:
            VISUAL_CONFIG.materials.steel.envMapIntensity,

        side: THREE.DoubleSide
    });
}

// AJUSTE DE COLOR

function adjustColor(hex, factor) {

    const color = new THREE.Color(hex);

    color.r *= factor;
    color.g *= factor;
    color.b *= factor;

    return color;
}


// STARTER RING PROFESIONAL

function addStarterRing(
    group,
    radius,
    height,
    tank
) {

    const material =
        new THREE.MeshStandardMaterial({

            color: 0x57534e,

            metalness: 0.9,

            roughness: 0.32,

            envMapIntensity: 1.4,

            side: THREE.DoubleSide
        });

    const geometry =
        new THREE.CylinderGeometry(
            radius * 1.012,
            radius * 1.012,
            height,
            VISUAL_CONFIG.shellSegments,
            1,
            true
        );

    const starter =
        new THREE.Mesh(
            geometry,
            material
        );

    starter.position.y = height / 2;

    starter.castShadow = true;
    starter.receiveShadow = true;

    starter.userData = {
        tipo: "Starter Ring",
        material: "Acero estructural",
        altura: `${getStarterRingHeightMm(tank)} mm`,
        espesor: "—",
        diametro: "—"
    };

    group.add(starter);

    // REFUERZO VISUAL

    addRingSeam(
        group,
        radius * 1.012,
        0
    );

    addRingSeam(
        group,
        radius * 1.012,
        height
    );

    return height;
}

// MANHOLE INDUSTRIAL

function addManhole(group, radius, height) {

    const angle = Math.PI * 1.82;

    const y = height * 0.28;

    const radial =
        new THREE.Vector3(
            Math.cos(angle),
            0,
            Math.sin(angle)
        );

    const plateRadius =
        Math.max(radius * 0.10, 0.42);

    const plateThickness =
        Math.max(radius * 0.012, 0.045);

    const flangeRadius =
        plateRadius * 1.12;

    const material =
        new THREE.MeshStandardMaterial({

            color: 0x94a3b8,

            metalness: 0.88,

            roughness: 0.2
        });

    const center =
        radial.clone().multiplyScalar(
            radius + plateThickness * 0.4
        );

    center.y = y;

    const quaternion =
        new THREE.Quaternion();

    quaternion.setFromUnitVectors(
        new THREE.Vector3(0, 1, 0),
        radial.clone().normalize()
    );

    // ARO

    const flange =
        new THREE.Mesh(

            new THREE.CylinderGeometry(
                flangeRadius,
                flangeRadius,
                plateThickness * 1.2,
                48
            ),

            material
        );

    flange.position.copy(center);

    flange.quaternion.copy(quaternion);

    flange.castShadow = true;

    group.add(flange);

    // TAPA

    const plate =
        new THREE.Mesh(

            new THREE.CylinderGeometry(
                plateRadius,
                plateRadius,
                plateThickness,
                48
            ),

            new THREE.MeshStandardMaterial({

                color: 0xcbd5e1,

                metalness: 0.92,

                roughness: 0.16
            })
        );

    plate.position.copy(
        center.clone().add(
            radial.clone().multiplyScalar(
                plateThickness * 0.5
            )
        )
    );

    plate.quaternion.copy(quaternion);

    plate.castShadow = true;

    group.add(plate);

    // TORNILLERÍA

    const tangent =
        new THREE.Vector3(
            -Math.sin(angle),
            0,
            Math.cos(angle)
        );

    const vertical =
        new THREE.Vector3(0, 1, 0);

    const boltCount = 12;

    for (let i = 0; i < boltCount; i++) {

        const a =
            (Math.PI * 2 * i) / boltCount;

        const pos =
            center.clone()
                .add(
                    tangent.clone().multiplyScalar(
                        Math.cos(a) *
                        flangeRadius *
                        0.72
                    )
                )
                .add(
                    vertical.clone().multiplyScalar(
                        Math.sin(a) *
                        flangeRadius *
                        0.72
                    )
                );

        const bolt =
            new THREE.Mesh(

                new THREE.CylinderGeometry(
                    0.018,
                    0.018,
                    0.04,
                    8
                ),

                new THREE.MeshStandardMaterial({

                    color: 0x111827,

                    metalness: 0.9,

                    roughness: 0.16
                })
            );

        bolt.position.copy(pos);

        bolt.quaternion.copy(quaternion);

        group.add(bolt);
    }
    }
    function addTankConnections(group, radius, height) {
        addNozzle(group, radius, height, {
            angle: Math.PI * 1.18,
            y: height * 0.13,
            size: 0.70,
            label: "Drenaje"
        });

        addNozzle(group, radius, height, {
            angle: Math.PI * 1.32,
            y: height * 0.36,
            size: 0.90,
            label: "Salida"
        });

        addNozzle(group, radius, height, {
            angle: Math.PI * 1.46,
            y: height * 0.68,
            size: 0.78,
            label: "Entrada"
        });

        addNozzle(group, radius, height, {
            angle: Math.PI * 1.62,
            y: height * 0.84,
            size: 0.52,
            label: "Rebosadero"
        });
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
            metalness: 0.82,
            roughness: 0.2,
            envMapIntensity: 1.1
        });

        const materialFlange = new THREE.MeshStandardMaterial({
            color: 0x475569,
            metalness: 0.9,
            roughness: 0.16,
            envMapIntensity: 1.2
        });

        const materialBolt = new THREE.MeshStandardMaterial({
            color: 0x111827,
            metalness: 0.86,
            roughness: 0.18
        });

        const base = radial.clone().multiplyScalar(radius * 1.006);
        base.y = y;

        const end = radial.clone().multiplyScalar(radius + nozzleLength);
        end.y = y;

        addCylinderBetween(group, base, end, nozzleRadius, materialNozzle, 32);

        const flangeCenter = radial.clone().multiplyScalar(radius + nozzleLength + flangeThickness * 0.2);
        flangeCenter.y = y;

        const flange = new THREE.Mesh(
            new THREE.CylinderGeometry(flangeRadius, flangeRadius, flangeThickness, 48),
            materialFlange
        );

        flange.position.copy(flangeCenter);

        const quaternion = new THREE.Quaternion();
        quaternion.setFromUnitVectors(
            new THREE.Vector3(0, 1, 0),
            radial.clone().normalize()
        );

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

        const boltCount = 8;

        for (let i = 0; i < boltCount; i++) {
            const a = (Math.PI * 2 * i) / boltCount;

            const boltPos = flangeCenter.clone()
                .add(tangent.clone().multiplyScalar(Math.cos(a) * flangeRadius * 0.72))
                .add(vertical.clone().multiplyScalar(Math.sin(a) * flangeRadius * 0.72))
                .add(radial.clone().multiplyScalar(flangeThickness * 0.75));

            const bolt = new THREE.Mesh(
                new THREE.CylinderGeometry(boltRadius, boltRadius, flangeThickness * 1.15, 8),
                materialBolt
            );

            bolt.position.copy(boltPos);
            bolt.quaternion.copy(quaternion);
            bolt.castShadow = true;
            bolt.receiveShadow = true;

            group.add(bolt);
        }
    }

    function addRoofGuardrail(group, radius, height, roofRaw) {
        const roof = normalizarTecho(roofRaw);
        if (roof.type === "none") return;

        const material = new THREE.MeshStandardMaterial({
            color: 0xe5e7eb,
            metalness: 0.92,
            roughness: 0.15,
            envMapIntensity: 1.2,
            side: THREE.DoubleSide
        });

        const postRadius = Math.max(radius * 0.0038, 0.020);
        const railRadius = Math.max(radius * 0.0048, 0.024);
        const railHeight = Math.max(radius * 0.085, 0.72);
        const lowerRailHeight = railHeight * 0.55;

        const railRadiusPosition = radius * 1.04;
        const postCount = Math.max(28, Math.min(64, Math.floor(radius * 5)));

        for (let i = 0; i < postCount; i++) {
            const angle = (Math.PI * 2 * i) / postCount;
            const x = Math.cos(angle) * railRadiusPosition;
            const z = Math.sin(angle) * railRadiusPosition;

            const bottom = new THREE.Vector3(x, height, z);
            const top = new THREE.Vector3(x, height + railHeight, z);

            addCylinderBetween(group, bottom, top, postRadius, material, 10);
        }

        addCircularRail(group, railRadiusPosition, height + railHeight, railRadius, material);
        addCircularRail(group, railRadiusPosition, height + lowerRailHeight, railRadius * 0.82, material);
    }

    function addCircularRail(group, radius, y, tubeRadius, material) {
        const segments = 128;
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
            addCylinderBetween(group, points[i], points[i + 1], tubeRadius, material, 10);
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

    function addOpenTop(group, radius, height) {
        const geometry = new THREE.TorusGeometry(
            radius,
            Math.max(radius * 0.014, 0.035),
            16,
            160
        );

        const material = new THREE.MeshStandardMaterial({
            color: 0x0f172a,
            metalness: 0.82,
            roughness: 0.2
        });

        const torus = new THREE.Mesh(geometry, material);
        torus.rotation.x = Math.PI / 2;
        torus.position.y = height;
        torus.castShadow = true;
        torus.receiveShadow = true;

        group.add(torus);
    }

    function addFlatRoof(group, radius, height) {
        const baseMaterial = new THREE.MeshStandardMaterial({
            color: 0xdbe3ec,
            metalness: 0.68,
            roughness: 0.28,
            side: THREE.DoubleSide,
            envMapIntensity: 1.1
        });

        const roof = new THREE.Mesh(
            new THREE.CircleGeometry(radius * 0.985, 160),
            baseMaterial
        );

        roof.rotation.x = -Math.PI / 2;
        roof.position.y = height + radius * 0.012;
        roof.castShadow = true;
        roof.receiveShadow = true;

        group.add(roof);

        const ribMaterial = new THREE.MeshStandardMaterial({
            color: 0x94a3b8,
            metalness: 0.8,
            roughness: 0.2
        });

        const sheetCount = Math.max(10, Math.min(22, Math.floor(radius * 1.8)));
        const sheetWidth = (radius * 2) / sheetCount;
        const ribHeight = Math.max(radius * 0.012, 0.035);
        const ribWidth = Math.max(radius * 0.010, 0.030);

        for (let i = 0; i < sheetCount; i++) {
            const x = -radius + sheetWidth * (i + 0.5);
            const halfLength = Math.sqrt(Math.max(0, radius * radius - x * x));

            if (halfLength <= 0.1) continue;

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
        const alturaConoReal =
            vigasTechoConico && Number(vigasTechoConico.alturaCono) > 0
                ? Number(vigasTechoConico.alturaCono)
                : 0;

        const roofHeight =
            alturaConoReal > 0
                ? alturaConoReal * scale
                : Math.max(radius * 0.16, 1.2);

        const geometry = new THREE.ConeGeometry(
            radius * 1.01,
            roofHeight,
            160,
            1,
            false
        );

        const material = new THREE.MeshStandardMaterial({
            color: 0xe5e7eb,
            metalness: 0.72,
            roughness: 0.28,
            transparent: true,
            opacity: 0.92,
            side: THREE.DoubleSide,
            envMapIntensity: 1.1
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

        const numeroVigas =
            vigasTechoConico && vigasTechoConico.aplica === true
                ? Number(vigasTechoConico.numeroVigas) || 0
                : 0;

        addOpenTop(group, radius, height);
        addConeRoofPanels(group, radius, height, roofHeight);
        addConeRoofRafters(group, radius, height, roofHeight, vigasTechoConico, scale);
        addConeRoofCenterHub(group, height + roofHeight, radius, numeroVigas, vigasTechoConico, scale);
    }

    function addConeRoofPanels(group, radius, baseHeight, roofHeight) {
        const material = new THREE.LineBasicMaterial({
            color: 0x64748b,
            transparent: true,
            opacity: 0.30
        });

        [0.33, 0.66].forEach(f => {
            const ringRadius = radius * f;
            const y = baseHeight + roofHeight * (1 - f);

            const curve = new THREE.EllipseCurve(
                0,
                0,
                ringRadius,
                ringRadius,
                0,
                Math.PI * 2,
                false,
                0
            );

            const points = curve
                .getPoints(160)
                .map(p => new THREE.Vector3(p.x, y, p.y));

            const geometry = new THREE.BufferGeometry().setFromPoints(points);

            group.add(new THREE.LineLoop(geometry, material));
        });
    }

    function addConeRoofRafters(group, radius, baseHeight, roofHeight, vigasTechoConico, scale) {
        if (!vigasTechoConico || vigasTechoConico.aplica !== true) return;

        const numeroVigas = Math.max(0, Number(vigasTechoConico.numeroVigas) || 0);
        if (numeroVigas <= 0) return;

        const beamMaterial = new THREE.MeshStandardMaterial({
            color: 0x991b1b,
            metalness: 0.88,
            roughness: 0.2,
            envMapIntensity: 1.1
        });

        const hubRadius = calcularRadioNucleoTecho(radius, numeroVigas, vigasTechoConico, scale);
        const startRadius = hubRadius * 1.05;
        const endRadius = radius * 0.965;
        const beamRadius = Math.max(radius * 0.0068, 0.035);
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

        const geometry = new THREE.CylinderGeometry(hubRadius, hubRadius, hubHeight, 96);

        const material = new THREE.MeshStandardMaterial({
            color: 0x5f0f0f,
            metalness: 0.9,
            roughness: 0.16
        });

        const hub = new THREE.Mesh(geometry, material);
        hub.position.y = y + hubHeight / 2;
        hub.castShadow = true;
        hub.receiveShadow = true;

        group.add(hub);
    }

    function addDomeRoof(group, radius, height) {
        const domeHeight = Math.max(radius * 0.42, 1.35);

        const geometry = new THREE.SphereGeometry(
            radius * 1.015,
            160,
            40,
            0,
            Math.PI * 2,
            0,
            Math.PI / 2
        );

        const material = new THREE.MeshStandardMaterial({
            color: 0xe5e7eb,
            metalness: 0.82,
            roughness: 0.22,
            transparent: true,
            opacity: 0.92,
            side: THREE.DoubleSide,
            envMapIntensity: 1.1
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
            metalness: 0.9,
            roughness: 0.16
        });

        const skirtHeight = Math.max(radius * 0.09, 0.38);
        const skirtThickness = Math.max(radius * 0.016, 0.05);

        const skirt = new THREE.Mesh(
            new THREE.CylinderGeometry(radius * 1.01, radius * 1.01, skirtHeight, 160, 1, true),
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
            metalness: 0.9,
            roughness: 0.14
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

            addCylinderBetween(group, start, end, ribRadius, material, 12);
        }

        [0.35, 0.62, 0.84].forEach(f => {
            const ringRadius = radius * f;
            const y = height + domeHeight * (1 - f * 0.72);

            addCircularRail(group, ringRadius, y, ribRadius * 0.85, material);
        });
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
            color: 0x0ea5e9,
            transparent: true,
            opacity: 0.28,
            metalness: 0.06,
            roughness: 0.08,
            side: THREE.DoubleSide,
            envMapIntensity: 0.8
        });

        const disc = new THREE.Mesh(
            new THREE.CircleGeometry(radius * 0.96, 160),
            material
        );

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
            color: 0x38bdf8,
            transparent: true,
            opacity: 0.85
        });

        const curve = new THREE.EllipseCurve(
            0,
            0,
            radius * 0.965,
            radius * 0.965,
            0,
            Math.PI * 2,
            false,
            0
        );

        const points = curve
            .getPoints(160)
            .map(p => new THREE.Vector3(p.x, y + 0.01, p.y));

        const geometry = new THREE.BufferGeometry().setFromPoints(points);
        group.add(new THREE.LineLoop(geometry, lineMaterial));
    }

    function addTopStiffener(group, radius, height) {
        const geometry = new THREE.TorusGeometry(
            radius * 1.015,
            Math.max(radius * 0.018, 0.045),
            16,
            160
        );

        const material = new THREE.MeshStandardMaterial({
            color: 0x1e293b,
            metalness: 0.9,
            roughness: 0.2,
            envMapIntensity: 1.1
        });

        const stiffener = new THREE.Mesh(geometry, material);
        stiffener.rotation.x = Math.PI / 2;
        stiffener.position.y = height;
        stiffener.castShadow = true;
        stiffener.receiveShadow = true;

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
        const curve = new THREE.EllipseCurve(
            0,
            0,
            radius * 1.006,
            radius * 1.006,
            0,
            Math.PI * 2,
            false,
            0
        );

        const points = curve
            .getPoints(160)
            .map(p => new THREE.Vector3(p.x, y, p.y));

        const geometry = new THREE.BufferGeometry().setFromPoints(points);

        group.add(new THREE.LineLoop(
            geometry,
            new THREE.LineBasicMaterial({
                color: 0x000000,
                transparent: true,
                opacity: 0.26
            })
        ));
    }

    function addBottomDisc(group, radius) {
        const material = new THREE.MeshStandardMaterial({
            color: 0x334155,
            metalness: 0.12,
            roughness: 0.88,
            side: THREE.DoubleSide
        });

        const disc = new THREE.Mesh(
            new THREE.CircleGeometry(radius * 1.15, 160),
            material
        );

        disc.rotation.x = -Math.PI / 2;
        disc.position.y = -0.015;
        disc.receiveShadow = true;

        group.add(disc);
    }

    function addReferenceGrid(group, radius, height) {
        const size = Math.max(radius * 3.2, height * 1.4, 20);
        const grid = new THREE.GridHelper(size, 20, 0x94a3b8, 0xd1d5db);

        grid.position.y = -0.02;

        if (grid.material) {
            grid.material.opacity = 0.48;
            grid.material.transparent = true;
        }

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
            metalness: 0.82,
            roughness: 0.2,
            envMapIntensity: 1.1
        });

        const cageMaterial = new THREE.MeshStandardMaterial({
            color: 0xcbd5e1,
            metalness: 0.88,
            roughness: 0.16,
            transparent: true,
            opacity: 0.62,
            envMapIntensity: 1.1
        });

        const platformMaterial = new THREE.MeshStandardMaterial({
            color: 0x64748b,
            metalness: 0.82,
            roughness: 0.2,
            envMapIntensity: 1.1
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

        addCylinderBetween(group, leftRailBottom, leftRailTop, railRadius, ladderMaterial, 16);
        addCylinderBetween(group, rightRailBottom, rightRailTop, railRadius, ladderMaterial, 16);

        const modelRungSpacing = scale && scale > 0 ? 0.30 * scale : 0.26;
        const rungSpacing = Math.max(Math.min(modelRungSpacing, 0.38), 0.16);
        const rungCount = Math.max(14, Math.floor((topY - bottomY) / rungSpacing));

        for (let i = 1; i < rungCount; i++) {
            const y = bottomY + ((topY - bottomY) * i) / rungCount;

            const left = centerBase.clone().add(tangent.clone().multiplyScalar(-railHalfWidth));
            left.y = y;

            const right = centerBase.clone().add(tangent.clone().multiplyScalar(railHalfWidth));
            right.y = y;

            addCylinderBetween(group, left, right, rungRadius, ladderMaterial, 12);
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

        addVerticalLadderTopPlatform(
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

        addLadderTankBrackets(group, radius, height, radial, tangent, centerBase, cageMaterial);
    }

    function addVerticalLadderIntermediatePlatform(
        group,
        radius,
        height,
        radial,
        tangent,
        platformMaterial,
        railMaterial,
        centerBase,
        railHalfWidth
    ) {
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

        addCylinderBetween(group, leftBottom, leftOuter, supportRadius, railMaterial, 12);
        addCylinderBetween(group, rightBottom, rightOuter, supportRadius, railMaterial, 12);
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
        const segments = 24;

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

    function addLadderTankBrackets(group, radius, height, radial, tangent, centerBase, material) {
        const bracketRadius = Math.max(radius * 0.0035, 0.018);
        const bracketCount = Math.max(6, Math.floor(height / Math.max(radius * 0.18, 1.0)));

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
            color: 0xff7a18,
            metalness: 0.82,
            roughness: 0.2,
            envMapIntensity: 1.1
        });

        const railMaterial = new THREE.MeshStandardMaterial({
            color: 0xe2e8f0,
            metalness: 1.0,
            roughness: 0.12,
            envMapIntensity: 1.2
        });

        const supportMaterial = new THREE.MeshStandardMaterial({
            color: 0x334155,
            metalness: 0.9,
            roughness: 0.2,
            envMapIntensity: 1.1
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
                addCylinderBetween(group, outerBase, outerTop, postRadius, railMaterial, 12);
                addCylinderBetween(group, innerBase, innerTop, postRadius * 0.85, railMaterial, 12);
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

                addCylinderBetween(group, wallPoint, stairPoint, postRadius * 0.75, supportMaterial, 10);
            }
        }

        connectPath(group, outerRail, railRadius, railMaterial, 12);
        connectPath(group, innerRail, railRadius, railMaterial, 12);
        connectPath(group, outerMidRail, railRadius * 0.8, railMaterial, 10);
        connectPath(group, lowerStringer, stringerRadius, supportMaterial, 12);
        connectPath(group, innerStringer, stringerRadius * 0.85, railMaterial, 12);

        const finalAngle = -Math.PI / 2 + angleOffset + turns * Math.PI * 2;
        addHelicalTopPlatform(group, radius, height, finalAngle, supportMaterial, railMaterial);
    }

    function connectPath(group, points, radius, material, segments) {
        for (let i = 0; i < points.length - 1; i++) {
            addCylinderBetween(group, points[i], points[i + 1], radius, material, segments);
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
            addCylinderBetween(group, bottoms[i], tops[i], railRadius, railMaterial, 12);
        }

        addCylinderBetween(group, tops[0], tops[1], railRadius, railMaterial, 12);
        addCylinderBetween(group, tops[0], tops[2], railRadius, railMaterial, 12);
        addCylinderBetween(group, tops[1], tops[3], railRadius, railMaterial, 12);
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

        const nozzleLength = Math.max(radius * 0.06 * size, 0.18);
        const nozzleRadius = Math.max(radius * 0.030 * size, 0.12);

        const flangeRadius = nozzleRadius * 1.45;
        const flangeThickness = Math.max(nozzleRadius * 0.24, 0.04);
        const boltRadius = Math.max(nozzleRadius * 0.06, 0.015);

        const materialNozzle = new THREE.MeshStandardMaterial({
            color: 0xb6beca,
            metalness: 0.8,
            roughness: 0.2,
            envMapIntensity: 1.1
        });

        const materialFlange = new THREE.MeshStandardMaterial({
            color: 0x475569,
            metalness: 0.9,
            roughness: 0.15,
            envMapIntensity: 1.1
        });

        const materialBolt = new THREE.MeshStandardMaterial({
            color: 0x111827,
            metalness: 0.8,
            roughness: 0.2,
            envMapIntensity: 1
        });

        const base = radial.clone().multiplyScalar(radius * 1.005);
        base.y = y;

        const end = radial.clone().multiplyScalar(radius + nozzleLength);
        end.y = y;

        addCylinderBetween(group, base, end, nozzleRadius, materialNozzle, 32);

        const flangeCenter = radial.clone().multiplyScalar(radius + nozzleLength + flangeThickness * 0.18);
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

        const boltCount = 8;

        for (let i = 0; i < boltCount; i++) {
            const a = (Math.PI * 2 * i) / boltCount;

            const boltPos = flangeCenter.clone()
                .add(tangent.clone().multiplyScalar(Math.cos(a) * flangeRadius * 0.70))
                .add(vertical.clone().multiplyScalar(Math.sin(a) * flangeRadius * 0.70));

            const bolt = new THREE.Mesh(
                new THREE.CylinderGeometry(boltRadius, boltRadius, flangeThickness * 1.2, 8),
                materialBolt
            );

            bolt.position.copy(boltPos);
            bolt.quaternion.copy(quaternion);
            bolt.castShadow = true;
            bolt.receiveShadow = true;

            group.add(bolt);
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
                viewer.renderer.domElement.style.cursor = viewer.isDragging ? "grabbing" : "grab";
                return;
            }

            const data = valid.object.userData;
            viewer.renderer.domElement.style.cursor = "pointer";

            overlay.innerHTML = `
            <div style="font-size:11px;text-transform:uppercase;letter-spacing:1px;color:#6b7280;margin-bottom:4px;">
                Detalle de pieza
            </div>
            <strong style="font-size:15px;color:#111827;">${data.tipo}</strong><br>
            <div style="margin-top:8px;display:grid;grid-template-columns:auto auto;gap:4px 12px;color:#374151;">
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
            viewer.inputPitch = Math.max(
                -Math.PI / 2 + 0.1,
                Math.min(Math.PI / 2 - 0.1, viewer.inputPitch)
            );
        });

        canvas.addEventListener("wheel", e => {
            e.preventDefault();

            const factor = e.deltaY > 0 ? 1.1 : 0.9;
            viewer.inputDistance = Math.max(15, Math.min(300, viewer.inputDistance * factor));
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
        const radius = viewer.modelRadius || 20;
        const maxSize = Math.max(height, radius * 2, 1);

        viewer.inputDistance = maxSize * 1.9;
        viewer.inputPitch = 0.25;
        viewer.inputYaw = 0.72;

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

        viewer.camera.near = Math.max(0.1, viewer.distance * 0.08);
        viewer.camera.far = Math.max(1000, viewer.distance * 5);
        viewer.camera.updateProjectionMatrix();
    }

    function colorForMaterial(name) {
        const normalized = String(name || "").toUpperCase();

        if (normalized.includes("HSLA")) return 0x71717a;
        if (normalized.includes("S355")) return 0x52525b;
        if (normalized.includes("S275")) return 0x71717a;
        if (normalized.includes("S235")) return 0xa1a1aa;
        if (normalized.includes("GLASS") || normalized.includes("VITR")) return 0x164e63;

        return 0x71717a;
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
        renderTank3D: renderTank3D
    };
