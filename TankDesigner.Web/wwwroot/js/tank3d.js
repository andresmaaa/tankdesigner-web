const viewers = new WeakMap();

function renderTank3D(container, tank) {
    if (!container || !tank || !Array.isArray(tank.anillos)) {
        return;
    }

    if (!window.THREE) {
        console.error("Three.js no está cargado.");
        return;
    }

    if (!THREE.OrbitControls) {
        console.error("OrbitControls no está cargado.");
        return;
    }

    let viewer = viewers.get(container);

    if (!viewer) {
        viewer = createViewer(container);
        viewers.set(container, viewer);
    }

    clearScene(viewer);
    buildTank(viewer, tank);
    fitCamera(viewer, tank);
    resize(viewer);

    viewer.renderer.render(viewer.scene, viewer.camera);
}

function createViewer(container) {
    container.innerHTML = "";

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0xf8fafc);

    const camera = new THREE.PerspectiveCamera(45, 1, 0.01, 100000);

    const renderer = new THREE.WebGLRenderer({
        antialias: true,
        alpha: true
    });

    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    container.appendChild(renderer.domElement);

    const controls = new THREE.OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.08;
    controls.screenSpacePanning = false;
    controls.minDistance = 1;
    controls.maxDistance = 100000;

    const ambient = new THREE.AmbientLight(0xffffff, 0.72);
    scene.add(ambient);

    const key = new THREE.DirectionalLight(0xffffff, 1.1);
    key.position.set(12, 18, 10);
    key.castShadow = true;
    scene.add(key);

    const fill = new THREE.DirectionalLight(0xffffff, 0.45);
    fill.position.set(-10, 8, -14);
    scene.add(fill);

    const group = new THREE.Group();
    scene.add(group);

    const viewer = {
        container,
        scene,
        camera,
        renderer,
        controls,
        group
    };

    const resizeObserver = new ResizeObserver(() => resize(viewer));
    resizeObserver.observe(container);
    viewer.resizeObserver = resizeObserver;

    resize(viewer);
    animate(viewer);

    return viewer;
}

function resize(viewer) {
    const rect = viewer.container.getBoundingClientRect();

    const width = Math.max(320, rect.width || 320);
    const height = Math.max(520, rect.height || 520);

    viewer.camera.aspect = width / height;
    viewer.camera.updateProjectionMatrix();
    viewer.renderer.setSize(width, height, false);
}

function animate(viewer) {
    requestAnimationFrame(() => animate(viewer));

    viewer.controls.update();
    viewer.renderer.render(viewer.scene, viewer.camera);
}

function clearScene(viewer) {
    while (viewer.group.children.length > 0) {
        const child = viewer.group.children.pop();
        disposeObject(child);
    }
}

function disposeObject(object) {
    if (!object) {
        return;
    }

    if (typeof object.traverse === "function") {
        object.traverse((child) => disposeGeometryAndMaterial(child));
    } else {
        disposeGeometryAndMaterial(object);
    }
}

function disposeGeometryAndMaterial(child) {
    if (child.geometry) {
        child.geometry.dispose();
    }

    if (child.material) {
        if (Array.isArray(child.material)) {
            child.material.forEach((material) => material.dispose());
        } else {
            child.material.dispose();
        }
    }
}

function buildTank(viewer, tank) {
    const scale = 0.01; 

    const diameter = (Number(tank.diametro) || 1) * scale;
    const radius = diameter / 2;

    const rings = tank.anillos
        .filter(r => Number(r.altura) > 0);

    if (rings.length === 0) {
        return;
    }

    let currentY = 0;

    rings.forEach((ring, index) => {
        const height = Number(ring.altura);
        const materialName = ring.material || tank.materialPrincipal || "material";
        const color = colorForMaterial(materialName);

        const shellGeometry = new THREE.CylinderGeometry(
            radius,
            radius,
            height,
            160,
            1,
            true
        );

        const shellMaterial = new THREE.MeshStandardMaterial({
            color,
            metalness: 0.72,
            roughness: 0.28,
            transparent: true,
            opacity: ring.valido === false ? 0.55 : 0.9,
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
                opacity: 0.18
            })
        );

        edges.position.copy(shell.position);
        viewer.group.add(edges);

        addRingSeam(viewer.group, radius, currentY, 0x1e293b);
        addRingSeam(viewer.group, radius, currentY + height, 0x1e293b);

        addRingLabel(viewer.group, radius, currentY + height / 2, index + 1, materialName, ring.espesor);

        currentY += height;
    });

    addBottomDisc(viewer.group, radius);
    addTopOpening(viewer.group, radius, currentY);
    addReferenceGrid(viewer.group, radius, currentY);

    viewer.group.position.y = -currentY / 2;
}

function addRingSeam(group, radius, y, color) {
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

    const points = curve.getPoints(180).map(p => new THREE.Vector3(p.x, y, p.y));
    const geometry = new THREE.BufferGeometry().setFromPoints(points);

    const material = new THREE.LineBasicMaterial({
        color,
        transparent: true,
        opacity: 0.65
    });

    group.add(new THREE.LineLoop(geometry, material));
}

function addBottomDisc(group, radius) {
    const geometry = new THREE.CircleGeometry(radius, 160);

    const material = new THREE.MeshStandardMaterial({
        color: 0xe2e8f0,
        metalness: 0.45,
        roughness: 0.38,
        side: THREE.DoubleSide
    });

    const disc = new THREE.Mesh(geometry, material);
    disc.rotation.x = -Math.PI / 2;
    disc.position.y = 0;
    disc.receiveShadow = true;

    group.add(disc);
}

function addTopOpening(group, radius, height) {
    const geometry = new THREE.TorusGeometry(radius, radius * 0.012, 12, 180);

    const material = new THREE.MeshStandardMaterial({
        color: 0x0f172a,
        metalness: 0.55,
        roughness: 0.32
    });

    const torus = new THREE.Mesh(geometry, material);
    torus.rotation.x = Math.PI / 2;
    torus.position.y = height;

    group.add(torus);
}

function addReferenceGrid(group, radius, height) {
    const size = Math.max(radius * 3.2, height * 1.4, 10);

    const grid = new THREE.GridHelper(
        size,
        18,
        0x94a3b8,
        0xcbd5e1
    );

    grid.position.y = -0.015;
    group.add(grid);
}

function addRingLabel(group, radius, y, index, materialName, thickness) {
    const text = `A${index} · ${materialName}${thickness ? ` · ${thickness} mm` : ""}`;
    const canvas = document.createElement("canvas");

    canvas.width = 512;
    canvas.height = 128;

    const ctx = canvas.getContext("2d");
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    ctx.fillStyle = "rgba(15,23,42,0.86)";
    roundRect(ctx, 12, 22, 488, 84, 18);
    ctx.fill();

    ctx.fillStyle = "#ffffff";
    ctx.font = "700 34px Arial";
    ctx.fillText(text, 34, 76);

    const texture = new THREE.CanvasTexture(canvas);
    const material = new THREE.SpriteMaterial({
        map: texture,
        transparent: true
    });

    const sprite = new THREE.Sprite(material);
    sprite.position.set(radius * 1.22, y, 0);
    sprite.scale.set(radius * 0.52, radius * 0.13, 1);

    group.add(sprite);
}

function roundRect(ctx, x, y, width, height, radius) {
    ctx.beginPath();
    ctx.moveTo(x + radius, y);
    ctx.lineTo(x + width - radius, y);
    ctx.quadraticCurveTo(x + width, y, x + width, y + radius);
    ctx.lineTo(x + width, y + height - radius);
    ctx.quadraticCurveTo(x + width, y + height, x + width - radius, y + height);
    ctx.lineTo(x + radius, y + height);
    ctx.quadraticCurveTo(x, y + height, x, y + height - radius);
    ctx.lineTo(x, y + radius);
    ctx.quadraticCurveTo(x, y, x + radius, y);
    ctx.closePath();
}

function fitCamera(viewer, tank) {
    const height = Number(tank.alturaTotal) || totalHeightFromRings(tank.anillos);
    const radius = (Number(tank.diametro) || 1) / 2;
    const maxSize = Math.max(height, radius * 2, 1);

    viewer.camera.position.set(
        radius * 2.4,
        height * 0.45,
        maxSize * 1.55
    );

    viewer.camera.near = Math.max(0.01, maxSize / 1000);
    viewer.camera.far = maxSize * 100;
    viewer.camera.updateProjectionMatrix();

    viewer.controls.target.set(0, 0, 0);
    viewer.controls.update();
}

function totalHeightFromRings(rings) {
    if (!Array.isArray(rings)) {
        return 1;
    }

    return rings.reduce(
        (sum, ring) => sum + Math.max(0, Number(ring.altura) || 0),
        0
    );
}

function colorForMaterial(name) {
    const normalized = String(name || "").toUpperCase();

    if (normalized.includes("HSLA")) return 0x2563eb;
    if (normalized.includes("S355")) return 0x0f766e;
    if (normalized.includes("S275")) return 0x7c3aed;
    if (normalized.includes("S235")) return 0x64748b;
    if (normalized.includes("GLASS") || normalized.includes("VITR")) return 0x0891b2;

    let hash = 0;

    for (let i = 0; i < normalized.length; i++) {
        hash = normalized.charCodeAt(i) + ((hash << 5) - hash);
    }

    const palette = [
        0x2563eb,
        0x0f766e,
        0x7c3aed,
        0x9333ea,
        0x0369a1,
        0x475569
    ];

    return palette[Math.abs(hash) % palette.length];
}

window.tank3d = {
    renderTank3D: renderTank3D
};