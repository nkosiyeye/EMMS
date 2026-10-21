# Dashboard Redesign (Home/Index)

Goal: replace the one-size-fits-all dashboard with a **role-aware** home page. Every user lands on
"what needs my attention today" for their role and facility, then the numbers that matter to them.

## What's wrong with the current dashboard

- Same 4 tiles + 5 bar charts for every role. A Biomed, a Data Collector and a Pharmacist see the same thing.
- Data Collectors land on charts for features they can't access (sidebar hides movement/jobs for them).
- Pharmacist / CMS Officer get nothing about cylinders on the home page.
- `GetDashboardCounts` fetches the top 5 notifications but the page never shows them.
- Tiles aren't clickable (old clickable version is commented out).
- The facility dropdown only affects the "Assets By Service Point" chart, and non-admins see every facility in it.
- `DecommissionedByFacilityCounts` actually holds non-functional + decommissioned counts (name is misleading).
- "Service Due" shows a signed day count (`-5 day(s)`); should read "due in 5 days" / "12 days overdue".
- Every chart call loads the full asset view model in memory (`GetAssetIndexViewModel`) and groups in C#.
- Five large bar charts stacked vertically, most of which are reference data rather than actions.

## New layout (all roles)

```
┌──────────────────────────────────────────────────────────────┐

│ [Quick action] [Quick action] [Quick action]                 │
├──────────────────────────────────────────────────────────────┤
│ KPI │ KPI │ KPI │ KPI      ← clickable, link to filtered list │
├────────────────────────────────────┬─────────────────────────┤
│ Needs your attention (action list) │ Notifications (top 5)   │
│                                    │                         │
├────────────────────────────────────┴─────────────────────────┤
│ 1–2 role-specific charts / tables                            │
└──────────────────────────────────────────────────────────────┘
```

- Header: greeting, user's facility and role, today's date.
- Quick actions: 2–4 buttons for the most common tasks of that role.
- KPI tiles: compact, icon + number + short label + trend/subtext, each a link to the filtered page.
- "Needs your attention": a ranked list of concrete items (overdue service, unassigned requests, approvals...).
- Notifications panel: the top 5 we already fetch.
- Each section loads independently with its own skeleton loader (keep the shimmer approach).

## Content per role

### Administrator (system-wide, with facility filter that applies to the whole page)
- KPIs: total assets · % functional (availability) · open work requests · assets overdue for service · users awaiting activation
- Attention: inactive user registrations to approve, assets overdue service (oldest first), work requests open > 14 days, movements in transit too long
- Charts: functional status (donut), non-functional by facility (top 10, horizontal bar), facility comparison table (assets / % functional / open WRs / overdue service)
- Technician workload snapshot (top 5 from `WorkloadViewModel`) with link to full Workload page
- Recent status changes feed (`AssetStatusChange`)

### Facility Manager (own facility)
- KPIs: facility assets · % functional · open work requests · overdue service
- Attention: work requests with no job yet, jobs completed awaiting closure, incoming movements to receive, overdue service
- Charts: functional status donut, assets by service point
- Technician workload snapshot for their facility
- Quick actions: Raise work request, Receive asset, Technician workload

### Biomed (personal work queue)
- KPIs: my open jobs · my jobs in progress · completed by me this month · service due this week
- Attention: my assigned jobs (oldest first), unassigned work requests at my facility, preventive maintenance due/overdue
- Chart: my jobs by status, or completed jobs per week (last 8 weeks)
- Parts used by me this month (`JobPart`)
- Quick actions: Create job, Work requests, Move asset

### General User
- KPIs: my open requests · my requests completed this month · non-functional assets at my facility · assets awaiting my receipt
- Attention: status of my work requests (open / in progress / completed), movements to receive
- Quick actions: Report a fault (work request), Receive asset, Asset search

### Data Collector
- KPIs: assets I registered today · this week · total · records with missing data
- Attention: my assets with gaps (unknown functional status, no location/deployment, missing serial where `RequiresSerial`)
- Recent registrations table (last 10)
- Quick actions: Register asset, Asset list

### Pharmacist
- KPIs: cylinder requests awaiting approval · approved this month · rejected this month
- Attention: pending requests to approve/reject (inline links)
- Link to Cylinder Dashboard

### CMS Officer
- KPIs: approved requests awaiting order · orders to dispatch · dispatched not received · plant-down orders
- Attention: requests to turn into orders, orders ready to dispatch
- Link to Cylinder Dashboard

## Technical approach

- New `DashboardService` that builds a role-specific `HomeDashboardViewModel`
  (header info, quick actions, KPI tiles, attention items) from `CurrentUser`.
- One partial / ViewComponent per widget (`ViewComponents/` already exists), so `Index.cshtml`
  becomes a role switch that composes widgets.
- Per-widget JSON endpoints (or ViewComponent AJAX) so slow widgets don't block the page.
- Scope every query by role + facility on the server; never trust a facility id from non-admins.
- Push counts/grouping into SQL (EF `GroupBy`/`CountAsync`) instead of loading all assets.
- Unit tests for the role/scoping rules (same style as `WorkRequestRulesTests`).

## Tasks

### Phase 1 — Foundation
- [ ] 1. Create `HomeDashboardViewModel` (Header, QuickActions, KpiTiles, AttentionItems, Notifications)
- [ ] 2. Create `DashboardService` with a `BuildAsync(User)` entry point that switches on `UserType`
- [ ] 3. Register the service in `Program.cs`; slim down `HomeController.Index` to use it
- [ ] 4. Add reusable widget partials: `_DashHeader`, `_KpiTile`, `_AttentionList`, `_NotificationsPanel`, `_ChartCard`
- [ ] 5. New dashboard CSS (tile, attention list, section skeletons) in `site.css`; keep existing status badges
- [ ] 6. Fix service-due wording ("due in N days" / "N days overdue") and sort overdue first

### Phase 2 — Role dashboards
- [ ] 7. Biomed dashboard (my jobs, unassigned requests, PM due, parts used)
- [ ] 8. Facility Manager dashboard (facility KPIs, attention list, workload snapshot, service point chart)
- [ ] 9. Administrator dashboard (system KPIs, page-wide facility filter, facility comparison, user approvals)
- [ ] 10. General User dashboard (my requests, receive asset, report fault)
- [ ] 11. Data Collector dashboard (my registrations, data-quality gaps)
- [ ] 12. Pharmacist dashboard (pending cylinder approvals)
- [ ] 13. CMS Officer dashboard (orders to create / dispatch / receive)

### Phase 3 — Data & performance
- [ ] 14. Replace in-memory grouping in `DashboardViewModel` with SQL-side aggregate queries
- [ ] 15. Rename `DecommissionedByFacilityCounts` → `NonFunctionalByFacilityCounts`
- [ ] 16. Restrict facility filter to Admins; enforce facility scoping server-side on all dashboard endpoints
- [ ] 17. Add `[RequireLogin]` (or confirm `BaseController` covers it) on all dashboard JSON endpoints

### Phase 4 — Polish & tests
- [ ] 18. Make every KPI tile link to the matching filtered list page
- [ ] 19. Mobile layout check (tiles 2-up, attention list full width)
- [ ] 20. Unit tests for role scoping and attention-item rules
- [ ] 21. Remove dead code (commented-out tiles / old `Index` in `HomeController`, unused `IndexModel` if no longer used)

### Phase 5 — Move current dashboard to Reports
- [ ] 22. Turn the current dashboard content into a downloadable **"Asset Summary" report** in `ReportsController`
      (new `reportType` in `GenerateReport`, Excel via ClosedXML like the existing reports), one sheet each:
      - Summary: total assets, open requests, completed jobs, pending jobs
      - Functional Status counts
      - Procurement Status counts
      - Movement Reason counts
      - Non-Functional Assets by Facility
      - Assets by Service Point (respects facility filter)
      - Equipment Due for Service (tag, name, procurement status, location, functional status, service due, days overdue)
      - Scoped by role/facility the same way the dashboard is today; date range from the report form where it applies
      - Add the option to `Views/Reports/Index.cshtml`; do this **before** removing the old widgets from Home/Index
