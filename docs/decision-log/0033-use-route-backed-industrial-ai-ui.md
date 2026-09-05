# 0033 - Use route-backed Industrial AI UI

Date: 2026-09-05

Decision: standardize the Angular frontend on an Industrial AI Cockpit visual system backed by semantic CSS variables and real Router URLs.

Why: the MVP UI was visually consistent but used many ad hoc values, state-only navigation, font glyph icons, and a mobile layout without primary navigation. Route-backed workspaces restore deep links and browser navigation, while shared tokens and accessible interaction rules improve quality without adding a second styling framework.

Scope: Chat remains the home page. Desktop keeps a persistent sidebar; mobile uses a compact top bar plus role-aware bottom navigation. Dialogs must manage focus and all structural icons use the shared SVG sprite.
