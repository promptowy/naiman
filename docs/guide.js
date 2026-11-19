(() => {
  const routes = [...document.querySelectorAll('[data-path]')];
  if (!routes.length) return;
  const links = [...document.querySelectorAll('[data-route]')];
  function select(key, scroll) {
    if (!routes.some(route => route.id === key)) key = 'xbox';
    routes.forEach(route => { route.hidden = route.id !== key; });
    links.forEach(link => link.setAttribute('aria-current', String(link.dataset.route === key)));
    if (scroll) document.getElementById('setup').scrollIntoView({block:'start'});
  }
  function fromHash() { const key = location.hash.slice(1); if (key==='xbox'||key==='ps5') select(key,true); }
  links.forEach(link => link.addEventListener('click',e => {
    if(e.ctrlKey||e.metaKey||e.shiftKey||e.altKey)return;
    e.preventDefault();
    const key=link.dataset.route;
    if(location.hash!=='#'+key)history.pushState(null,'','#'+key);
    select(key,true);
  }));
  routes.forEach(route => {
    const steps=[...route.querySelectorAll('details')];
    steps.forEach((item,i) => {
      const button=item.querySelector('[data-next]');
      if(button)button.addEventListener('click',()=>{
        steps.forEach((s,n)=>{s.open=n===i+1;});
        const next=steps[i+1].querySelector('summary');next.focus();next.scrollIntoView({block:'nearest'});
      });
    });
  });
  window.addEventListener('popstate',()=>select(location.hash.slice(1),true));
  window.addEventListener('hashchange',fromHash);
  select(location.hash.slice(1),false);
  if(location.hash==='#xbox'||location.hash==='#ps5')requestAnimationFrame(fromHash);
})();
