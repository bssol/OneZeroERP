# OneZero ERP UI Design System

## Purpose

The UI is a Blazor Web App delivered as a Progressive Web App. The design system provides a consistent shell for every ERP module while keeping module-specific screens inside their own module boundaries.

## Experience principles

- Make control state visible: draft, pending, posted, locked, unauthorized, and unavailable states should never be implied only by color.
- Keep high-frequency work dense but calm: clear hierarchy, predictable spacing, and strong table/card behavior.
- Prefer progressive disclosure: show the important decision first, then expose details and drill-down.
- Preserve traceability: module pages should provide breadcrumbs, source context, recent activity, and clear return paths.
- Keyboard and assistive technology support are first-class requirements.

## Shell architecture

The shared shell owns responsive sidebar/module-registry navigation, top header, breadcrumbs, notifications, profile menu, logout, theme selection, application dashboard, and loading/empty/error/unauthorized/not-found states. Modules contribute navigation metadata and page components through application contracts; they do not duplicate sidebar links or rewrite the shell.

## Layout and responsive behavior

Desktop uses a persistent 250px navigation rail and a flexible content region. Tablet narrows the rail and collapses dashboard metrics to two columns. Mobile turns the rail into an off-canvas panel, simplifies profile/theme controls, converts cards to one column, and reduces content padding.

Use semantic landmarks (`aside`, `nav`, `header`, `main`), visible focus rings, labels for controls, minimum touch targets, and responsive tables that become cards or horizontal scroll regions when necessary. Do not rely on hover to reveal an action.

## Tokens and themes

Components consume semantic CSS variables rather than hardcoded colors: `--primary`, `--primary-strong`, `--page`, `--surface`, `--surface-alt`, `--text`, `--muted`, `--border`, `--shadow`, `--success`, `--warning`, and `--danger`.

Available themes:

1. Professional Blue / Indigo — default, focused, and confident.
2. Neutral Slate — restrained and information-dense.
3. Emerald / Teal — fresh and operational.

The selected theme is stored in browser local storage through the theme service. No sensitive identity or financial data is stored there. Dark-mode-compatible token expansion can be added without changing component markup.

## Reusable component families

- `MetricCard`: label, value, comparison, trend, and semantic tone.
- `ModuleCard`: module identity, description, implemented/planned status, and route.
- Panel/card, action list, activity list, and state components.
- Future ERP components: data table, filter bar, date range, money display, account tree, journal-line editor, confirmation dialog, audit timeline, and export action.

Future components must expose typed parameters, use semantic markup, keep validation messages adjacent to fields, and avoid embedding module business rules in visual components.

## PWA policy

The PWA may cache the application shell and safe static assets. It must not cache access/refresh tokens, financial records, or audit payloads. Authoritative actions—posting, approval, locking, reconciliation finalization, and permission-sensitive mutations—require a live server transaction. Offline drafts, if approved, must be clearly local and unposted, resynchronized with idempotency and conflict handling, and never presented as posted.

## Quality checks

- Test at desktop, tablet, and mobile breakpoints.
- Verify keyboard-only navigation, visible focus, accessible names, contrast, semantic headings, and error announcements.
- Verify theme persistence and logout/account-switch clearing of sensitive client state.
- Verify every module route has loading, empty, error, and unauthorized behavior.
