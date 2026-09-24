window.oneZeroTheme = {
  set: function (theme) {
    document.documentElement.dataset.theme = theme;
    localStorage.setItem('onezero-theme', theme);
  },
  load: function () {
    const theme = localStorage.getItem('onezero-theme') || 'professional';
    document.documentElement.dataset.theme = theme;
    return theme;
  }
};
window.oneZeroTheme.load();

window.oneZeroPassword = {
  toggle: function (inputId, button) {
    const input = document.getElementById(inputId);
    if (!input) return;
    const visible = input.type === 'text';
    input.type = visible ? 'password' : 'text';
    button.setAttribute('aria-pressed', String(!visible));
    button.setAttribute('aria-label', visible ? 'Show password' : 'Hide password');
    button.setAttribute('title', visible ? 'Show password' : 'Hide password');
  }
};

window.oneZeroShell = {
  getCollapsed: function (fallback) {
    const stored = localStorage.getItem('onezero-sidebar-collapsed');
    return stored === null ? fallback : stored === 'true';
  },
  setCollapsed: function (collapsed) {
    localStorage.setItem('onezero-sidebar-collapsed', String(collapsed));
  }
};
