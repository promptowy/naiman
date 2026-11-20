(() => {
  const box = document.getElementById('usage-stats');
  if (!box) return;
  const locale = document.documentElement.lang;
  const number = new Intl.NumberFormat(locale);
  const unavailable = locale.startsWith('pl') ? 'brak danych' : 'unavailable';
  const api = 'https://promptowy.com/naiman-stats.php?public=1';
  const published = (location.origin === 'https://promptowy.com' && /^\/(en\/)?naiman\/?$/.test(location.pathname)) ||
    (location.origin === 'https://promptowy.github.io' && /^\/naiman\/(index\.html|pl\.html)?$/.test(location.pathname));

  function show(key, value, checked) {
    const el = box.querySelector('[data-stat=' + key + ']');
    el.textContent = Number.isSafeInteger(value) && value >= 0 ? number.format(value) : unavailable;
    if (checked) el.title = new Date(checked * 1000).toLocaleString(locale);
  }

  async function githubDownloads() {
    let total = 0;
    let found = false;
    for (let page = 1; page <= 30; page++) {
      const response = await fetch('https://api.github.com/repos/promptowy/naiman/releases?per_page=100&page=' + page,
        { credentials: 'omit', signal: AbortSignal.timeout(8000) });
      if (!response.ok) throw new Error('GitHub unavailable');
      const releases = await response.json();
      for (const release of releases) {
        for (const asset of release.assets) {
          if (asset.name !== 'NAIMAN.exe' || asset.state !== 'uploaded') continue;
          if (!Number.isSafeInteger(asset.download_count) || asset.download_count < 0) throw new Error('Invalid count');
          found = true;
          total += asset.download_count;
        }
      }
      if (releases.length < 100 && found) return total;
      if (!releases.length) break;
    }
    throw new Error('Incomplete release list');
  }

  async function load() {
    if (document.visibilityState !== 'visible') return;
    document.removeEventListener('visibilitychange', load);
    let snapshot = {};
    try {
      const response = await fetch(api, { method: published ? 'POST' : 'GET',
        body: published ? 'pageview' : undefined, credentials: 'omit', cache: 'no-store',
        signal: AbortSignal.timeout(10000) });
      if (response.ok) snapshot = await response.json();
    } catch { /* Downloads can still be read from GitHub. */ }
    show('views', snapshot.views);
    show('downloads', snapshot.downloads, snapshot.checked);
    if (!Number.isSafeInteger(snapshot.downloads) || !snapshot.checked || Date.now() / 1000 - snapshot.checked > 600) {
      try { show('downloads', await githubDownloads(), Date.now() / 1000); }
      catch { /* Keep the last server reading. */ }
    }
  }
  document.addEventListener('visibilitychange', load);
  load();
})();
