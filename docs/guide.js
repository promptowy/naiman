// Shows one console path at a time. Without this script all three paths stay visible, one after another.
(() => {
  const paths = [...document.querySelectorAll('[data-path]')];
  if (!paths.length) return;
  const links = [...document.querySelectorAll('[data-route]')];
  const keys = paths.map(path => path.id);
  let remembered = null;
  try { remembered = localStorage.getItem('naiman-route'); } catch (e) { /* private mode */ }

  function show(key, scroll) {
    if (!keys.includes(key)) key = keys.includes(remembered) ? remembered : keys[0];
    paths.forEach(path => { path.hidden = path.id !== key; path.toggleAttribute('data-on', path.id === key); });
    links.forEach(link => link.setAttribute('aria-current', String(link.dataset.route === key)));
    try { localStorage.setItem('naiman-route', key); } catch (e) { /* private mode */ }
    remembered = key;
    if (scroll) document.getElementById('kroki').scrollIntoView({ block: 'start' });
  }
  links.forEach(link => link.addEventListener('click', event => {
    if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    const key = link.dataset.route;
    if (location.hash !== '#' + key) history.pushState(null, '', '#' + key);
    show(key, !link.closest('.tabs') || window.scrollY < document.getElementById('kroki').offsetTop - 200);
  }));
  const fromHash = scroll => show(location.hash.slice(1), scroll && keys.includes(location.hash.slice(1)));
  window.addEventListener('popstate', () => fromHash(true));
  window.addEventListener('hashchange', () => fromHash(true));
  fromHash(false);
  if (keys.includes(location.hash.slice(1))) requestAnimationFrame(() => fromHash(true));
})();
