const canvas = document.getElementById("viewport");
const ctx = canvas.getContext("2d");

const metricCount = document.getElementById("metric-count");
const metricLargest = document.getElementById("metric-largest");
const metricFace = document.getElementById("metric-face");
const metricSpin = document.getElementById("metric-spin");
const resetButton = document.getElementById("reset-button");
const scatterButton = document.getElementById("scatter-button");

const CONFIG = {
  halfSize: 1,
  wallInset: 0.07,
  baseRadius: 0.135,
  gravityStrength: 8.5,
  surfaceDrag: 6.2,
  staticSlipThreshold: 1.5,
  kineticSlipThreshold: 0.72,
  settleSpeedThreshold: 0.11,
  edgeBounce: 0.22,
  edgeSlideDamping: 0.9,
  attractionStrength: 5.2,
  faceSwitchThreshold: 0.18,
  faceSwitchBias: 2.4,
  edgeTransferMargin: 0.04,
  transitionDuration: 0.13,
  mergeFactor: 0.92,
  followSharpness: 7.2,
  supportGravitySharpness: 4.6,
  angularVelocityLimit: 4.8,
  angularAccelerationLimit: 14,
  angularInertiaScale: 0.06,
  maxSurfaceAcceleration: 5.4,
  interiorOffset: 0.028,
  maxDt: 1 / 30,
  cameraDistance: 4.8,
  projectionScale: 0.92,
};

const WORLD_GRAVITY = vec3(0, -CONFIG.gravityStrength, 0);
const FACE_AXES = ["x", "y", "z"];
const CUBE_VERTICES = [
  vec3(-1, -1, -1),
  vec3(1, -1, -1),
  vec3(1, 1, -1),
  vec3(-1, 1, -1),
  vec3(-1, -1, 1),
  vec3(1, -1, 1),
  vec3(1, 1, 1),
  vec3(-1, 1, 1),
].map((point) => scale(point, CONFIG.halfSize));

const CUBE_FACES = [
  { id: "x-", axis: "x", side: -1, normal: vec3(-1, 0, 0), indices: [0, 4, 7, 3] },
  { id: "x+", axis: "x", side: 1, normal: vec3(1, 0, 0), indices: [1, 2, 6, 5] },
  { id: "y-", axis: "y", side: -1, normal: vec3(0, -1, 0), indices: [0, 1, 5, 4] },
  { id: "y+", axis: "y", side: 1, normal: vec3(0, 1, 0), indices: [3, 7, 6, 2] },
  { id: "z-", axis: "z", side: -1, normal: vec3(0, 0, -1), indices: [0, 3, 2, 1] },
  { id: "z+", axis: "z", side: 1, normal: vec3(0, 0, 1), indices: [4, 5, 6, 7] },
];

const INITIAL_ROTATION = quatNormalize(
  quatMul(
    quatFromAxisAngle(vec3(0, 1, 0), 0.62),
    quatFromAxisAngle(vec3(1, 0, 0), -0.38),
  ),
);

const state = {
  viewportWidth: 1,
  viewportHeight: 1,
  rotation: INITIAL_ROTATION,
  targetRotation: INITIAL_ROTATION,
  previousAngularVelocity: vec3(),
  angularVelocity: vec3(),
  angularAcceleration: vec3(),
  supportGravityLocal: quatRotateVec(quatConjugate(INITIAL_ROTATION), WORLD_GRAVITY),
  droplets: [],
  pointer: {
    active: false,
    id: null,
    arcballVector: null,
  },
  stats: {
    dominantFace: { axis: "y", side: -1 },
    dropletCount: 0,
    largestShare: 0,
    angularSpeed: 0,
  },
};

function vec3(x = 0, y = 0, z = 0) {
  return { x, y, z };
}

function add(a, b) {
  return vec3(a.x + b.x, a.y + b.y, a.z + b.z);
}

function sub(a, b) {
  return vec3(a.x - b.x, a.y - b.y, a.z - b.z);
}

function scale(v, s) {
  return vec3(v.x * s, v.y * s, v.z * s);
}

function dot(a, b) {
  return a.x * b.x + a.y * b.y + a.z * b.z;
}

function cross(a, b) {
  return vec3(
    a.y * b.z - a.z * b.y,
    a.z * b.x - a.x * b.z,
    a.x * b.y - a.y * b.x,
  );
}

function lengthOf(v) {
  return Math.hypot(v.x, v.y, v.z);
}

function normalize(v) {
  const len = lengthOf(v);
  if (len < 1e-8) {
    return vec3(0, 0, 0);
  }
  return scale(v, 1 / len);
}

function clampVectorMagnitude(v, maxLength) {
  const len = lengthOf(v);
  if (len <= maxLength || len < 1e-8) {
    return v;
  }
  return scale(v, maxLength / len);
}

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}

function lerp(a, b, t) {
  return a + (b - a) * t;
}

function mixVec3(a, b, t) {
  return vec3(
    lerp(a.x, b.x, t),
    lerp(a.y, b.y, t),
    lerp(a.z, b.z, t),
  );
}

function smoothstep01(value) {
  const t = clamp(value, 0, 1);
  return t * t * (3 - 2 * t);
}

function smoothSharpness(sharpness, dt) {
  return 1 - Math.exp(-sharpness * dt);
}

function quat(x = 0, y = 0, z = 0, w = 1) {
  return { x, y, z, w };
}

function quatIdentity() {
  return quat(0, 0, 0, 1);
}

function quatNormalize(q) {
  const len = Math.hypot(q.x, q.y, q.z, q.w);
  if (len < 1e-8) {
    return quatIdentity();
  }
  return quat(q.x / len, q.y / len, q.z / len, q.w / len);
}

function quatConjugate(q) {
  return quat(-q.x, -q.y, -q.z, q.w);
}

function quatMul(a, b) {
  return quat(
    a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
    a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
    a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
    a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z,
  );
}

function quatFromAxisAngle(axis, angle) {
  const safeAxis = normalize(axis);
  const halfAngle = angle * 0.5;
  const s = Math.sin(halfAngle);
  return quat(safeAxis.x * s, safeAxis.y * s, safeAxis.z * s, Math.cos(halfAngle));
}

function quatRotateVec(q, v) {
  const u = vec3(q.x, q.y, q.z);
  const uv = cross(u, v);
  const uuv = cross(u, uv);
  return add(v, add(scale(uv, 2 * q.w), scale(uuv, 2)));
}

function quatSlerp(a, b, t) {
  let q1 = quatNormalize(a);
  let q2 = quatNormalize(b);
  let cosine = q1.x * q2.x + q1.y * q2.y + q1.z * q2.z + q1.w * q2.w;

  if (cosine < 0) {
    cosine = -cosine;
    q2 = quat(-q2.x, -q2.y, -q2.z, -q2.w);
  }

  if (cosine > 0.9995) {
    return quatNormalize(quat(
      lerp(q1.x, q2.x, t),
      lerp(q1.y, q2.y, t),
      lerp(q1.z, q2.z, t),
      lerp(q1.w, q2.w, t),
    ));
  }

  const theta = Math.acos(clamp(cosine, -1, 1));
  const sinTheta = Math.sin(theta);
  const aScale = Math.sin((1 - t) * theta) / sinTheta;
  const bScale = Math.sin(t * theta) / sinTheta;

  return quat(
    q1.x * aScale + q2.x * bScale,
    q1.y * aScale + q2.y * bScale,
    q1.z * aScale + q2.z * bScale,
    q1.w * aScale + q2.w * bScale,
  );
}

function quatFromUnitVectors(from, to) {
  const d = clamp(dot(from, to), -1, 1);
  if (d < -0.999999) {
    const axis = Math.abs(from.x) < 0.8 ? normalize(cross(from, vec3(1, 0, 0))) : normalize(cross(from, vec3(0, 1, 0)));
    return quatFromAxisAngle(axis, Math.PI);
  }

  const c = cross(from, to);
  return quatNormalize(quat(c.x, c.y, c.z, 1 + d));
}

function quatToAxisAngle(q) {
  const safe = quatNormalize(q);
  const angle = 2 * Math.acos(clamp(safe.w, -1, 1));
  const s = Math.sqrt(Math.max(0, 1 - safe.w * safe.w));
  if (s < 1e-6 || angle < 1e-6) {
    return { axis: vec3(0, 0, 0), angle: 0 };
  }
  return { axis: vec3(safe.x / s, safe.y / s, safe.z / s), angle };
}

function mapPointerToArcball(clientX, clientY) {
  const rect = canvas.getBoundingClientRect();
  const nx = ((clientX - rect.left) / rect.width) * 2 - 1;
  const ny = 1 - ((clientY - rect.top) / rect.height) * 2;
  const radius = 0.92;
  const x = nx / radius;
  const y = ny / radius;
  const lengthSquared = x * x + y * y;

  if (lengthSquared > 1) {
    const invLength = 1 / Math.sqrt(lengthSquared);
    return vec3(x * invLength, y * invLength, 0);
  }

  return vec3(x, y, Math.sqrt(1 - lengthSquared));
}

function getFaceBasis(face) {
  switch (face.axis) {
    case "x":
      return {
        normal: vec3(face.side, 0, 0),
        u: vec3(0, 1, 0),
        v: vec3(0, 0, 1),
      };
    case "y":
      return {
        normal: vec3(0, face.side, 0),
        u: vec3(1, 0, 0),
        v: vec3(0, 0, 1),
      };
    default:
      return {
        normal: vec3(0, 0, face.side),
        u: vec3(1, 0, 0),
        v: vec3(0, 1, 0),
      };
  }
}

function getFaceCoordinate(face) {
  return face.side * (CONFIG.halfSize - CONFIG.wallInset);
}

function facePoint(face, u, v) {
  const basis = getFaceBasis(face);
  return add(scale(basis.normal, getFaceCoordinate(face)), add(scale(basis.u, u), scale(basis.v, v)));
}

function getComponent(point, axis) {
  return point[axis];
}

function setComponent(point, axis, value) {
  point[axis] = value;
}

function limitForRadius(radius) {
  return Math.max(0.08, CONFIG.halfSize - CONFIG.wallInset - radius * 0.82);
}

function dominantFace(acceleration, fallback) {
  let axis = fallback ? fallback.axis : "y";
  let side = fallback ? fallback.side : -1;
  let bestMagnitude = fallback ? Math.abs(acceleration[axis]) : -1;

  for (const candidate of FACE_AXES) {
    const magnitude = Math.abs(acceleration[candidate]);
    const candidateSide = acceleration[candidate] >= 0 ? 1 : -1;
    const sameFace = fallback && candidate === axis && candidateSide === side;
    const requiredGain = sameFace ? 0 : CONFIG.faceSwitchBias;

    if (magnitude > bestMagnitude + requiredGain) {
      bestMagnitude = magnitude;
      axis = candidate;
      side = candidateSide;
    }
  }

  return { axis, side };
}

function faceEquals(a, b) {
  return a.axis === b.axis && a.side === b.side;
}

function faceLabel(face) {
  return `${face.axis.toUpperCase()}${face.side > 0 ? "+" : "-"}`;
}

function computeDropRadius(drop) {
  return CONFIG.baseRadius * Math.cbrt(drop.volume);
}

function computeInitialDrops() {
  const droplets = [];
  const columns = 4;
  const rows = 3;
  const startFace = { axis: "y", side: -1 };
  const spacing = 0.42;

  for (let index = 0; index < 12; index += 1) {
    const column = index % columns;
    const row = Math.floor(index / columns);
    const u = (column - (columns - 1) * 0.5) * spacing + randomRange(-0.08, 0.08);
    const v = (row - (rows - 1) * 0.5) * spacing + randomRange(-0.08, 0.08);

    droplets.push({
      id: `drop-${index}`,
      face: { ...startFace },
      u,
      v,
      du: randomRange(-0.18, 0.18),
      dv: randomRange(-0.18, 0.18),
      volume: randomRange(0.68, 1.18),
      transition: null,
    });
  }

  return droplets;
}

function resetDrops() {
  state.droplets = computeInitialDrops();
  state.supportGravityLocal = quatRotateVec(quatConjugate(state.rotation), WORLD_GRAVITY);
}

function scatterDrops() {
  const gravityLocal = quatRotateVec(quatConjugate(state.rotation), WORLD_GRAVITY);
  const supportFace = dominantFace(gravityLocal, { axis: "y", side: -1 });

  state.droplets.forEach((drop, index) => {
    const radius = computeDropRadius(drop);
    const limit = limitForRadius(radius);
    drop.face = { ...supportFace };
    drop.u = randomRange(-limit * 0.88, limit * 0.88);
    drop.v = randomRange(-limit * 0.88, limit * 0.88);
    drop.du = randomRange(-1.6, 1.6) + (index % 2 === 0 ? 0.7 : -0.7);
    drop.dv = randomRange(-1.2, 1.2);
    drop.transition = null;
  });
}

function randomRange(min, max) {
  return min + Math.random() * (max - min);
}

function computeDropVelocity3D(drop) {
  const basis = getFaceBasis(drop.face);
  return add(scale(basis.u, drop.du), scale(basis.v, drop.dv));
}

function computePinningThreshold(drop, baseThreshold) {
  const sizeFactor = Math.max(0.8, Math.cbrt(drop.volume));
  return baseThreshold / sizeFactor;
}

function getMotionBasisFromVelocity(face, velocity) {
  const basis = getFaceBasis(face);
  const speed = lengthOf(velocity);
  const tangent = speed > 0.02 ? normalize(velocity) : basis.u;
  const bitangent = normalize(cross(basis.normal, tangent));

  return {
    basis,
    tangent,
    bitangent,
  };
}

function updateSupportGravity(gravityLocal, dt) {
  const blend = smoothSharpness(CONFIG.supportGravitySharpness, dt);
  const blended = mixVec3(state.supportGravityLocal, gravityLocal, blend);
  const magnitude = lengthOf(blended);
  state.supportGravityLocal = magnitude > 1e-5 ? scale(blended, CONFIG.gravityStrength / magnitude) : gravityLocal;
}

function computeEffectiveAcceleration(position, velocity, supportGravityLocal) {
  const alpha = scale(state.angularAcceleration, CONFIG.angularInertiaScale);
  const tangential = scale(cross(alpha, position), -1);
  return clampVectorMagnitude(add(supportGravityLocal, tangential), CONFIG.maxSurfaceAcceleration);
}

function canSwitchFace(drop, position, velocity, nextFace) {
  const radius = computeDropRadius(drop);
  const limit = limitForRadius(radius);
  const edgeThreshold = limit - CONFIG.edgeTransferMargin;
  const edgeCoordinate = getComponent(position, nextFace.axis) * nextFace.side;
  const approachVelocity = dot(velocity, getFaceBasis(nextFace).normal);

  return edgeCoordinate >= edgeThreshold && approachVelocity > -0.02;
}

function getSharedEdgeDirection(faceA, faceB) {
  const direction = cross(getFaceBasis(faceA).normal, getFaceBasis(faceB).normal);
  const length = lengthOf(direction);
  if (length < 1e-6) {
    return null;
  }
  return scale(direction, 1 / length);
}

function computeTransferCoordinates(previousFace, nextFace, position, limit) {
  const nextBasis = getFaceBasis(nextFace);
  const edgeDirection = getSharedEdgeDirection(previousFace, nextFace);
  let nextU = clamp(dot(position, nextBasis.u), -limit, limit);
  let nextV = clamp(dot(position, nextBasis.v), -limit, limit);

  if (!edgeDirection) {
    return { nextU, nextV };
  }

  const inset = Math.min(CONFIG.edgeTransferMargin * 1.25, limit * 0.2);
  const edgeAlongU = Math.abs(dot(edgeDirection, nextBasis.u));
  const edgeAlongV = Math.abs(dot(edgeDirection, nextBasis.v));

  if (edgeAlongU > edgeAlongV) {
    const edgeNormalSign = Math.sign(dot(position, nextBasis.v)) || 1;
    nextV = edgeNormalSign * (limit - inset);
  } else {
    const edgeNormalSign = Math.sign(dot(position, nextBasis.u)) || 1;
    nextU = edgeNormalSign * (limit - inset);
  }

  return { nextU, nextV };
}

function computeTransferVelocity(previousFace, nextFace, velocity) {
  const nextBasis = getFaceBasis(nextFace);
  const edgeDirection = getSharedEdgeDirection(previousFace, nextFace);

  if (!edgeDirection) {
    return {
      nextDu: dot(velocity, nextBasis.u) * 0.35,
      nextDv: dot(velocity, nextBasis.v) * 0.35,
    };
  }

  const alongEdgeSpeed = dot(velocity, edgeDirection);
  const carryU = dot(velocity, nextBasis.u) * 0.12;
  const carryV = dot(velocity, nextBasis.v) * 0.12;

  return {
    nextDu: alongEdgeSpeed * dot(edgeDirection, nextBasis.u) + carryU,
    nextDv: alongEdgeSpeed * dot(edgeDirection, nextBasis.v) + carryV,
  };
}

function buildTransitionState(previousFace, previousU, previousV, previousVelocity, nextFace, nextU, nextV, nextVelocity) {
  const previousMotion = getMotionBasisFromVelocity(previousFace, previousVelocity);
  const nextMotion = getMotionBasisFromVelocity(nextFace, nextVelocity);

  return {
    elapsed: 0,
    duration: CONFIG.transitionDuration,
    fromCenterLocal: sub(facePoint(previousFace, previousU, previousV), scale(previousMotion.basis.normal, CONFIG.interiorOffset)),
    toCenterLocal: sub(facePoint(nextFace, nextU, nextV), scale(nextMotion.basis.normal, CONFIG.interiorOffset)),
    fromNormalLocal: previousMotion.basis.normal,
    toNormalLocal: nextMotion.basis.normal,
    fromTangentLocal: previousMotion.tangent,
    toTangentLocal: nextMotion.tangent,
    fromBitangentLocal: previousMotion.bitangent,
    toBitangentLocal: nextMotion.bitangent,
  };
}

function switchDropFace(drop, nextFace, position, velocity) {
  const previousFace = { ...drop.face };
  const previousU = drop.u;
  const previousV = drop.v;
  const previousVelocity = velocity;
  const radius = computeDropRadius(drop);
  const limit = limitForRadius(radius);
  const transferCoordinates = computeTransferCoordinates(previousFace, nextFace, position, limit);
  const transferVelocity = computeTransferVelocity(previousFace, nextFace, velocity);
  const nextVelocity = add(scale(getFaceBasis(nextFace).u, transferVelocity.nextDu), scale(getFaceBasis(nextFace).v, transferVelocity.nextDv));

  drop.face = { ...nextFace };
  drop.u = transferCoordinates.nextU;
  drop.v = transferCoordinates.nextV;
  drop.du = transferVelocity.nextDu;
  drop.dv = transferVelocity.nextDv;
  drop.transition = buildTransitionState(previousFace, previousU, previousV, previousVelocity, drop.face, drop.u, drop.v, nextVelocity);
}

function updateDropTransition(drop, dt) {
  if (!drop.transition) {
    return;
  }

  drop.transition.elapsed += dt;
  if (drop.transition.elapsed >= drop.transition.duration) {
    drop.transition = null;
  }
}

function applySurfaceAttraction(dt) {
  for (let i = 0; i < state.droplets.length; i += 1) {
    const a = state.droplets[i];
    for (let j = i + 1; j < state.droplets.length; j += 1) {
      const b = state.droplets[j];
      if (a.transition || b.transition || !faceEquals(a.face, b.face)) {
        continue;
      }

      const dx = b.u - a.u;
      const dy = b.v - a.v;
      const distance = Math.hypot(dx, dy);
      const radiusSum = computeDropRadius(a) + computeDropRadius(b);
      const attractionRadius = radiusSum * 2.6;

      if (distance < 1e-5 || distance > attractionRadius) {
        continue;
      }

      const pull = (1 - distance / attractionRadius) * CONFIG.attractionStrength * dt;
      const nx = dx / distance;
      const ny = dy / distance;
      const totalVolume = a.volume + b.volume;
      const aShare = b.volume / totalVolume;
      const bShare = a.volume / totalVolume;

      a.du += nx * pull * aShare;
      a.dv += ny * pull * aShare;
      b.du -= nx * pull * bShare;
      b.dv -= ny * pull * bShare;
    }
  }
}

function updateDrops(dt) {
  const gravityLocal = quatRotateVec(quatConjugate(state.rotation), WORLD_GRAVITY);
  updateSupportGravity(gravityLocal, dt);

  applySurfaceAttraction(dt);

  for (const drop of state.droplets) {
    let position = facePoint(drop.face, drop.u, drop.v);
    let velocity = computeDropVelocity3D(drop);
    let acceleration = computeEffectiveAcceleration(position, velocity, state.supportGravityLocal);
    const nextFace = dominantFace(state.supportGravityLocal, drop.face);

    if (!faceEquals(drop.face, nextFace) && canSwitchFace(drop, position, velocity, nextFace)) {
      switchDropFace(drop, nextFace, position, velocity);
      position = facePoint(drop.face, drop.u, drop.v);
      velocity = computeDropVelocity3D(drop);
      acceleration = computeEffectiveAcceleration(position, velocity, state.supportGravityLocal);
    }

    const basis = getFaceBasis(drop.face);
    const surfaceAx = dot(acceleration, basis.u);
    const surfaceAy = dot(acceleration, basis.v);
    const surfaceAcceleration = Math.hypot(surfaceAx, surfaceAy);
    const surfaceSpeed = Math.hypot(drop.du, drop.dv);
    const startThreshold = computePinningThreshold(drop, CONFIG.staticSlipThreshold);
    const stopThreshold = computePinningThreshold(drop, CONFIG.kineticSlipThreshold);

    if (surfaceSpeed < CONFIG.settleSpeedThreshold && surfaceAcceleration < startThreshold) {
      drop.du = 0;
      drop.dv = 0;
    } else {
      drop.du = (drop.du + surfaceAx * dt) * Math.exp(-CONFIG.surfaceDrag * dt);
      drop.dv = (drop.dv + surfaceAy * dt) * Math.exp(-CONFIG.surfaceDrag * dt);

      if (Math.hypot(drop.du, drop.dv) < CONFIG.settleSpeedThreshold && surfaceAcceleration < stopThreshold) {
        drop.du = 0;
        drop.dv = 0;
      }
    }

    drop.u += drop.du * dt;
    drop.v += drop.dv * dt;

    const radius = computeDropRadius(drop);
    const limit = limitForRadius(radius);

    if (drop.u < -limit) {
      drop.u = -limit;
      if (drop.du < 0) {
        drop.du *= -CONFIG.edgeBounce;
        drop.dv *= CONFIG.edgeSlideDamping;
      }
    } else if (drop.u > limit) {
      drop.u = limit;
      if (drop.du > 0) {
        drop.du *= -CONFIG.edgeBounce;
        drop.dv *= CONFIG.edgeSlideDamping;
      }
    }

    if (drop.v < -limit) {
      drop.v = -limit;
      if (drop.dv < 0) {
        drop.dv *= -CONFIG.edgeBounce;
        drop.du *= CONFIG.edgeSlideDamping;
      }
    } else if (drop.v > limit) {
      drop.v = limit;
      if (drop.dv > 0) {
        drop.dv *= -CONFIG.edgeBounce;
        drop.du *= CONFIG.edgeSlideDamping;
      }
    }

    updateDropTransition(drop, dt);
  }

  mergeDrops();
  updateStats();
}

function mergeDrops() {
  for (let i = state.droplets.length - 1; i >= 0; i -= 1) {
    for (let j = i - 1; j >= 0; j -= 1) {
      const a = state.droplets[i];
      const b = state.droplets[j];
      if (a.transition || b.transition || !faceEquals(a.face, b.face)) {
        continue;
      }

      const distance = Math.hypot(a.u - b.u, a.v - b.v);
      const mergeDistance = (computeDropRadius(a) + computeDropRadius(b)) * CONFIG.mergeFactor;
      if (distance > mergeDistance) {
        continue;
      }

      const totalVolume = a.volume + b.volume;
      const merged = {
        id: `${b.id}+${a.id}`,
        face: { ...a.face },
        u: (a.u * a.volume + b.u * b.volume) / totalVolume,
        v: (a.v * a.volume + b.v * b.volume) / totalVolume,
        du: (a.du * a.volume + b.du * b.volume) / totalVolume,
        dv: (a.dv * a.volume + b.dv * b.volume) / totalVolume,
        volume: totalVolume,
      };

      state.droplets.splice(i, 1);
      state.droplets.splice(j, 1, merged);
      break;
    }
  }
}

function updateRotation(dt) {
  const previousRotation = state.rotation;
  state.rotation = quatSlerp(state.rotation, state.targetRotation, smoothSharpness(CONFIG.followSharpness, dt));

  const delta = quatMul(state.rotation, quatConjugate(previousRotation));
  const rotationDelta = quatToAxisAngle(delta);
  let deltaAngle = rotationDelta.angle;
  if (deltaAngle > Math.PI) {
    deltaAngle -= Math.PI * 2;
  }

  const omegaWorld = rotationDelta.angle > 0 ? scale(rotationDelta.axis, deltaAngle / Math.max(dt, 1e-5)) : vec3();
  state.angularVelocity = clampVectorMagnitude(quatRotateVec(quatConjugate(state.rotation), omegaWorld), CONFIG.angularVelocityLimit);
  state.angularAcceleration = clampVectorMagnitude(scale(sub(state.angularVelocity, state.previousAngularVelocity), 1 / Math.max(dt, 1e-5)), CONFIG.angularAccelerationLimit);
  state.previousAngularVelocity = state.angularVelocity;
}

function updateStats() {
  state.stats.dropletCount = state.droplets.length;
  if (state.droplets.length === 0) {
    state.stats.largestShare = 0;
    state.stats.dominantFace = { axis: "y", side: -1 };
    state.stats.angularSpeed = lengthOf(state.angularVelocity);
    metricCount.textContent = "0";
    metricLargest.textContent = "0%";
    metricFace.textContent = "Y-";
    metricSpin.textContent = state.stats.angularSpeed.toFixed(2);
    return;
  }

  const totalVolume = state.droplets.reduce((sum, drop) => sum + drop.volume, 0);
  const largest = state.droplets.reduce((best, drop) => (drop.volume > best.volume ? drop : best), state.droplets[0]);

  state.stats.largestShare = largest ? (largest.volume / totalVolume) * 100 : 0;
  state.stats.dominantFace = largest ? largest.face : { axis: "y", side: -1 };
  state.stats.angularSpeed = lengthOf(state.angularVelocity);

  metricCount.textContent = `${state.stats.dropletCount}`;
  metricLargest.textContent = `${state.stats.largestShare.toFixed(0)}%`;
  metricFace.textContent = faceLabel(state.stats.dominantFace);
  metricSpin.textContent = state.stats.angularSpeed.toFixed(2);
}

function resizeCanvas() {
  const rect = canvas.getBoundingClientRect();
  const dpr = window.devicePixelRatio || 1;
  canvas.width = Math.max(1, Math.floor(rect.width * dpr));
  canvas.height = Math.max(1, Math.floor(rect.height * dpr));
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  state.viewportWidth = rect.width;
  state.viewportHeight = rect.height;
}

function project(worldPoint) {
  const depth = CONFIG.cameraDistance - worldPoint.z;
  const perspective = (Math.min(state.viewportWidth, state.viewportHeight) * CONFIG.projectionScale) / depth;
  return {
    x: state.viewportWidth * 0.5 + worldPoint.x * perspective,
    y: state.viewportHeight * 0.54 - worldPoint.y * perspective,
    depth,
    scale: perspective,
  };
}

function drawBackground() {
  const width = state.viewportWidth;
  const height = state.viewportHeight;
  const gradient = ctx.createRadialGradient(width * 0.5, height * 0.3, 30, width * 0.5, height * 0.5, width * 0.65);
  gradient.addColorStop(0, "rgba(164, 228, 255, 0.10)");
  gradient.addColorStop(0.45, "rgba(37, 101, 128, 0.06)");
  gradient.addColorStop(1, "rgba(4, 13, 17, 0)");
  ctx.fillStyle = gradient;
  ctx.fillRect(0, 0, width, height);
}

function renderCube() {
  const worldVertices = CUBE_VERTICES.map((vertex) => quatRotateVec(state.rotation, vertex));
  const projectedVertices = worldVertices.map(project);
  const cameraPosition = vec3(0, 0, CONFIG.cameraDistance);

  const faces = CUBE_FACES.map((face) => {
    const points = face.indices.map((index) => projectedVertices[index]);
    const center = scale(face.indices.reduce((sum, index) => add(sum, worldVertices[index]), vec3()), 1 / face.indices.length);
    const normal = quatRotateVec(state.rotation, face.normal);
    const toCamera = normalize(sub(cameraPosition, center));
    const facing = dot(normal, toCamera) > 0;
    return {
      ...face,
      center,
      normal,
      facing,
      points,
      drawDepth: center.z,
    };
  }).sort((a, b) => a.drawDepth - b.drawDepth);

  for (const face of faces) {
    drawFace(face, false);
  }

  renderDrops();

  for (const face of faces) {
    drawFace(face, true);
  }
}

function drawFace(face, frontPass) {
  const alpha = frontPass ? (face.facing ? 0.12 : 0.05) : (face.facing ? 0.04 : 0.07);
  const strokeAlpha = frontPass ? (face.facing ? 0.82 : 0.18) : (face.facing ? 0.18 : 0.3);

  ctx.beginPath();
  ctx.moveTo(face.points[0].x, face.points[0].y);
  for (let index = 1; index < face.points.length; index += 1) {
    ctx.lineTo(face.points[index].x, face.points[index].y);
  }
  ctx.closePath();

  ctx.fillStyle = `rgba(186, 233, 255, ${alpha})`;
  ctx.fill();

  ctx.strokeStyle = `rgba(206, 244, 255, ${strokeAlpha})`;
  ctx.lineWidth = frontPass ? (face.facing ? 1.45 : 0.9) : 0.8;
  ctx.stroke();
}

function renderDrops() {
  const cameraPosition = vec3(0, 0, CONFIG.cameraDistance);
  const drawableDrops = state.droplets.map((drop) => buildDropVisual(drop, cameraPosition)).sort((a, b) => a.depth - b.depth);

  for (const visual of drawableDrops) {
    drawDrop(visual);
  }
}

function resolveDropRenderPose(drop) {
  const basis = getFaceBasis(drop.face);
  const speed = Math.hypot(drop.du, drop.dv);
  const baseRadius = computeDropRadius(drop);
  const stretch = clamp(1 + speed * 0.085, 1, 1.38);
  const rxLocal = baseRadius * stretch;
  const ryLocal = baseRadius / Math.sqrt(stretch);

  if (drop.transition) {
    const transition = drop.transition;
    const t = smoothstep01(transition.elapsed / transition.duration);
    return {
      centerLocal: mixVec3(transition.fromCenterLocal, transition.toCenterLocal, t),
      tangentLocal: normalize(mixVec3(transition.fromTangentLocal, transition.toTangentLocal, t)),
      bitangentLocal: normalize(mixVec3(transition.fromBitangentLocal, transition.toBitangentLocal, t)),
      normalLocal: normalize(mixVec3(transition.fromNormalLocal, transition.toNormalLocal, t)),
      rxLocal,
      ryLocal,
    };
  }

  const localCenter = sub(facePoint(drop.face, drop.u, drop.v), scale(basis.normal, CONFIG.interiorOffset));
  const tangent = speed > 0.02 ? normalize(add(scale(basis.u, drop.du), scale(basis.v, drop.dv))) : basis.u;
  const bitangent = normalize(cross(basis.normal, tangent));

  return {
    centerLocal: localCenter,
    tangentLocal: tangent,
    bitangentLocal: bitangent,
    normalLocal: basis.normal,
    rxLocal,
    ryLocal,
  };
}

function buildDropVisual(drop, cameraPosition) {
  const pose = resolveDropRenderPose(drop);
  const centerWorld = quatRotateVec(state.rotation, pose.centerLocal);
  const tangentWorld = quatRotateVec(state.rotation, add(pose.centerLocal, scale(pose.tangentLocal, pose.rxLocal)));
  const bitangentWorld = quatRotateVec(state.rotation, add(pose.centerLocal, scale(pose.bitangentLocal, pose.ryLocal)));
  const screenCenter = project(centerWorld);
  const screenTangent = project(tangentWorld);
  const screenBitangent = project(bitangentWorld);
  const faceNormalWorld = quatRotateVec(state.rotation, pose.normalLocal);
  const toCamera = normalize(sub(cameraPosition, centerWorld));
  const faceVisibility = clamp(dot(faceNormalWorld, toCamera) * 0.5 + 0.55, 0.34, 1);

  return {
    centerX: screenCenter.x,
    centerY: screenCenter.y,
    radiusX: Math.max(2, Math.hypot(screenTangent.x - screenCenter.x, screenTangent.y - screenCenter.y)),
    radiusY: Math.max(2, Math.hypot(screenBitangent.x - screenCenter.x, screenBitangent.y - screenCenter.y)),
    angle: Math.atan2(screenTangent.y - screenCenter.y, screenTangent.x - screenCenter.x),
    depth: centerWorld.z,
    alpha: faceVisibility,
  };
}

function drawDrop(drop) {
  ctx.save();
  ctx.translate(drop.centerX, drop.centerY);
  ctx.rotate(drop.angle);

  ctx.beginPath();
  ctx.ellipse(0, 0, drop.radiusX * 1.06, drop.radiusY * 1.12, 0, 0, Math.PI * 2);
  ctx.fillStyle = `rgba(4, 16, 22, ${0.14 * drop.alpha})`;
  ctx.fill();

  const gradient = ctx.createRadialGradient(-drop.radiusX * 0.32, -drop.radiusY * 0.4, drop.radiusX * 0.08, 0, 0, Math.max(drop.radiusX, drop.radiusY));
  gradient.addColorStop(0, `rgba(255, 255, 255, ${0.95 * drop.alpha})`);
  gradient.addColorStop(0.22, `rgba(179, 246, 255, ${0.9 * drop.alpha})`);
  gradient.addColorStop(0.68, `rgba(77, 184, 221, ${0.72 * drop.alpha})`);
  gradient.addColorStop(1, `rgba(23, 78, 111, ${0.16 * drop.alpha})`);

  ctx.shadowColor = `rgba(126, 231, 255, ${0.34 * drop.alpha})`;
  ctx.shadowBlur = 18;
  ctx.beginPath();
  ctx.ellipse(0, 0, drop.radiusX, drop.radiusY, 0, 0, Math.PI * 2);
  ctx.fillStyle = gradient;
  ctx.fill();

  ctx.shadowBlur = 0;
  ctx.beginPath();
  ctx.ellipse(-drop.radiusX * 0.12, -drop.radiusY * 0.16, drop.radiusX * 0.36, drop.radiusY * 0.3, -0.2, 0, Math.PI * 2);
  ctx.fillStyle = `rgba(255, 255, 255, ${0.28 * drop.alpha})`;
  ctx.fill();

  ctx.strokeStyle = `rgba(221, 249, 255, ${0.42 * drop.alpha})`;
  ctx.lineWidth = 1.1;
  ctx.beginPath();
  ctx.ellipse(0, 0, drop.radiusX, drop.radiusY, 0, 0, Math.PI * 2);
  ctx.stroke();

  ctx.restore();
}

function renderFrame() {
  ctx.clearRect(0, 0, state.viewportWidth, state.viewportHeight);
  drawBackground();
  renderCube();
}

function update(dt) {
  updateRotation(dt);
  updateDrops(dt);
}

function animationFrame(now) {
  if (!animationFrame.lastTime) {
    animationFrame.lastTime = now;
  }

  const dt = Math.min(CONFIG.maxDt, (now - animationFrame.lastTime) / 1000);
  animationFrame.lastTime = now;

  update(dt);
  renderFrame();
  requestAnimationFrame(animationFrame);
}

function onPointerDown(event) {
  state.pointer.active = true;
  state.pointer.id = event.pointerId;
  state.pointer.arcballVector = mapPointerToArcball(event.clientX, event.clientY);
  canvas.classList.add("dragging");
  canvas.setPointerCapture(event.pointerId);
}

function onPointerMove(event) {
  if (!state.pointer.active || event.pointerId !== state.pointer.id) {
    return;
  }

  const nextVector = mapPointerToArcball(event.clientX, event.clientY);
  const deltaRotation = quatFromUnitVectors(state.pointer.arcballVector, nextVector);
  state.targetRotation = quatNormalize(quatMul(deltaRotation, state.targetRotation));
  state.pointer.arcballVector = nextVector;
}

function onPointerUp(event) {
  if (event.pointerId !== state.pointer.id) {
    return;
  }

  state.pointer.active = false;
  state.pointer.id = null;
  state.pointer.arcballVector = null;
  canvas.classList.remove("dragging");
}

function addEventListeners() {
  window.addEventListener("resize", resizeCanvas);
  canvas.addEventListener("pointerdown", onPointerDown);
  canvas.addEventListener("pointermove", onPointerMove);
  canvas.addEventListener("pointerup", onPointerUp);
  canvas.addEventListener("pointercancel", onPointerUp);
  resetButton.addEventListener("click", resetDrops);
  scatterButton.addEventListener("click", scatterDrops);
}

function init() {
  resizeCanvas();
  addEventListeners();
  resetDrops();
  updateStats();
  requestAnimationFrame(animationFrame);
}

init();











