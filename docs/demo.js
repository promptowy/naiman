// Live example: one moving scene, drawn at 30 frames a second on the left and 60 on the right.
// It shows what a doubled frame rate looks like. It is a simulation of frame rate, not a recording of a game.
(() => {
  const canvas = document.getElementById('live');
  if (!canvas) return;
  const ctx = canvas.getContext('2d');
  const still = matchMedia('(prefers-reduced-motion: reduce)').matches;
  const toggle = document.getElementById('live-toggle');
  const speedButtons = [...document.querySelectorAll('[data-speed]')];
  const C = { bg: '#0c0d0f', line: '#2c3035', ink: '#f5f2eb', dim: '#6b7074', action: '#ff5a1f', dusk: '#ff2d78', noon: '#ffb627' };
  let split = 0.5, playing = !still, speed = 1, clock = 0, last = 0, w = 0, h = 0, dragging = false, seen = false;

  function size() {
    const ratio = Math.min(2, window.devicePixelRatio || 1);
    w = canvas.clientWidth; h = canvas.clientHeight;
    canvas.width = Math.round(w * ratio); canvas.height = Math.round(h * ratio);
    ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
  }

  function scene(t, x0, x1) {
    ctx.save();
    ctx.beginPath(); ctx.rect(x0, 0, x1 - x0, h); ctx.clip();
    ctx.fillStyle = C.bg; ctx.fillRect(x0, 0, x1 - x0, h);
    const ground = h * 0.66, r = Math.min(h * 0.46, w * 0.2), cx = w * 0.5;
    // the sun: same mark as in the app, bands sliding down to the horizon
    const sky = ctx.createLinearGradient(0, ground - r, 0, ground);
    sky.addColorStop(0, C.noon); sky.addColorStop(0.55, C.action); sky.addColorStop(1, C.dusk);
    ctx.save();
    ctx.beginPath(); ctx.rect(cx - r, ground - r, r * 2, r); ctx.clip();
    ctx.fillStyle = sky; ctx.beginPath(); ctx.arc(cx, ground, r, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = C.bg;
    for (let k = 0; k < 7; k++) {
      const q = ((k + t * 0.35) % 7) / 7, thick = r * (0.012 + 0.07 * q);
      ctx.fillRect(cx - r, ground - r * 0.64 * Math.pow(1 - q, 1.5) - thick, r * 2, thick);
    }
    ctx.restore();
    ctx.fillStyle = C.line; ctx.fillRect(x0, ground, x1 - x0, 1);
    // far posts, slow
    const far = w * 0.11, farShift = (t * w * 0.16) % far;
    ctx.fillStyle = C.dim;
    for (let x = -far; x < w + far; x += far) ctx.fillRect(x - farShift, ground - h * 0.1, Math.max(2, w * 0.004), h * 0.1);
    // road markings and near posts, fast: this is where 30 frames visibly steps
    const near = w * 0.2, nearShift = (t * w * 0.62) % near;
    for (let x = -near; x < w + near * 2; x += near) {
      const px = x - nearShift;
      ctx.fillStyle = C.ink; ctx.fillRect(px, ground - h * 0.34, Math.max(4, w * 0.012), h * 0.34 + h * 0.2);
      ctx.fillStyle = C.action; ctx.fillRect(px + near * 0.42, ground + h * 0.2, near * 0.26, h * 0.045);
    }
    ctx.restore();
  }

  function draw() {
    const mid = Math.round(w * split);
    scene(Math.floor(clock * 30) / 30, 0, mid);
    scene(Math.floor(clock * 60) / 60, mid, w);
    ctx.fillStyle = C.ink; ctx.fillRect(mid - 1, 0, 2, h);
    ctx.fillStyle = C.ink; ctx.fillRect(mid - 9, h * 0.5 - 22, 18, 44);
    ctx.fillStyle = C.bg; ctx.fillRect(mid - 3, h * 0.5 - 10, 2, 20); ctx.fillRect(mid + 1, h * 0.5 - 10, 2, 20);
  }

  function frame(now) {
    if (!last) last = now;
    const dt = Math.min(0.1, (now - last) / 1000);
    last = now;
    if (playing && seen) clock += dt * speed;
    draw();
    requestAnimationFrame(frame);
  }

  function setSplit(event) {
    const box = canvas.getBoundingClientRect();
    split = Math.max(0.08, Math.min(0.92, (event.clientX - box.left) / box.width));
  }
  canvas.addEventListener('pointerdown', event => { dragging = true; canvas.setPointerCapture(event.pointerId); setSplit(event); });
  canvas.addEventListener('pointermove', event => { if (dragging) setSplit(event); });
  canvas.addEventListener('pointerup', () => { dragging = false; });
  canvas.addEventListener('keydown', event => {
    if (event.key === 'ArrowLeft') split = Math.max(0.08, split - 0.05);
    if (event.key === 'ArrowRight') split = Math.min(0.92, split + 0.05);
  });
  function label() {
    if (!toggle) return;
    toggle.textContent = playing ? toggle.dataset.pause : toggle.dataset.play;
    toggle.setAttribute('aria-pressed', String(!playing));
  }
  if (toggle) toggle.addEventListener('click', () => { playing = !playing; label(); });
  speedButtons.forEach(button => button.addEventListener('click', () => {
    speed = Number(button.dataset.speed);
    speedButtons.forEach(other => other.setAttribute('aria-current', String(other === button)));
  }));
  speedButtons.forEach(button => button.addEventListener('keydown', event => {
    if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); button.click(); }
  }));
  new IntersectionObserver(entries => { seen = entries[0].isIntersecting; }, { threshold: 0.15 }).observe(canvas);
  window.addEventListener('resize', size);
  size(); label();
  requestAnimationFrame(frame);
})();

// Sections ease in once as they enter the viewport.
(() => {
  if (matchMedia('(prefers-reduced-motion: reduce)').matches || !('IntersectionObserver' in window)) return;
  const items = [...document.querySelectorAll('.stage, .split, .card, .measure, .edge li, .versus, .facts > div')];
  document.documentElement.classList.add('motion');
  const watch = new IntersectionObserver(entries => entries.forEach(entry => {
    if (entry.isIntersecting) { entry.target.classList.add('in'); watch.unobserve(entry.target); }
  }), { rootMargin: '0px 0px -8% 0px' });
  items.forEach(item => watch.observe(item));
})();
