(() => {
  // Only non-sensitive theme/sidebar preferences are persisted. Remove obsolete
  // application state on account transitions without touching other applications.
  if (location.pathname === '/login') {
    for (const storage of [localStorage, sessionStorage]) {
      for (const key of Object.keys(storage)) {
        if (key.startsWith('onezero-') && !['onezero-theme', 'onezero-sidebar-collapsed'].includes(key)) storage.removeItem(key);
      }
    }
  }
  if (!('serviceWorker' in navigator)) return;
  let requestedUpdate = false;
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (requestedUpdate) location.reload();
  });
  navigator.serviceWorker.register('/service-worker.js').then(registration => {
    const showUpdate = () => {
      if (!registration.waiting || document.getElementById('pwa-update')) return;
      const banner = document.createElement('div');
      banner.id = 'pwa-update';
      banner.className = 'entity-message';
      banner.setAttribute('role', 'status');
      banner.textContent = 'An update is ready. Save your work before reloading. ';
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = 'Reload to update';
      button.addEventListener('click', () => {
        requestedUpdate = true;
        registration.waiting.postMessage({ type: 'ACTIVATE_UPDATE' });
      });
      banner.append(button);
      document.body.prepend(banner);
    };
    showUpdate();
    registration.addEventListener('updatefound', () => {
      const installing = registration.installing;
      installing?.addEventListener('statechange', () => {
        if (installing.state === 'installed' && navigator.serviceWorker.controller) showUpdate();
      });
    });
  }).catch(() => { /* Online operation remains available; registration can retry on reload. */ });
})();
