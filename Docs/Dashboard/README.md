# Dashboard Redesign (Home/Index)

Goal: a calm, role-aware home page. It answers two questions for the signed-in user —
**how is my equipment doing** and **what is waiting on me** — and nothing else.

## Design rules

- **Don't repeat the chrome.** The top bar already shows the facility and the notification bell, and the
  sidebar is the navigation. So: no page header, no notifications panel, no quick-action buttons.
- **One structure for every role**, top to bottom:
  1. Up to **4 numbers** — icon, label, number, one line of context. The whole tile opens the list it counts.
  2. For admins and facility managers only: **2 bar charts** (Chart.js, horizontal, one series each, value at the bar tip).
     Equipment status uses the status colours (green / red / amber / grey); out-of-service-by-location uses one blue.
  3. The role's **work table** — its real queue, in the app's standard table style
     (`fixed-table table-striped table-bordered table-hover`, dark header, DataTables, icon buttons in the Action column).
- **Colour means "act on this".** Values are neutral by default; amber/red only when something needs attention.
  Healthy numbers are not painted green.
- **One source of truth per number.** A tile and the table under it count the same thing
  (e.g. "Overdue for service: 38" = the red rows in "Due for service").
- **Admins** get a single "Showing: [facility]" filter that rescopes the whole page, and a one-line banner
  only when user accounts are waiting for activation.

```
┌───────────────────────────────────────────────────────── Showing: [All ▾] ┐  (admin only)
│ 5 user accounts are waiting for activation.                       Review │  (only when > 0)
├──────────────┬──────────────┬──────────────┬─────────────────────────────┤
│ Equipment    │ Functional   │ Open work    │ Overdue for service         │
│ 123          │ 89%          │ 10           │ 38                          │
│              │ 110 of 123   │ 2 without job│                             │
├──────────────┴──────────────┼──────────────┴─────────────────────────────┤
│ Equipment status            │ Out of service by facility / service point │  (admin, manager)
│ Functional ████████████ 110 │ Mbabane GH ███ 1                           │
├─────────────────────────────┴────────────────────────────────────────────┤
│ Due for service  38                                                      │
│ Equipment        Location            Service due           [Request service]
└──────────────────────────────────────────────────────────────────────────┘
```

## Content per role

| Role | Numbers | Breakdowns | Work table(s) |
|---|---|---|---|
| Administrator | equipment in service · % functional · open requests (without a job) · overdue for service | equipment status · out of service by facility (by service point when filtered) | due for service |
| Facility Manager | same, own facility | equipment status · out of service by service point | due for service |
| Biomed | my jobs in progress · completed this month · requests without a job · overdue for service | — | my jobs in progress · due for service |
| General User | my open requests · ready for me to close · assets to receive · non-functional at my facility | — | my open requests (Close / Open) |
| Data Collector | registered today · this week · total · records to complete | — | records to complete |
| Pharmacist | waiting for my approval · approved / rejected this month | — | cylinder requests waiting for approval |
| CMS Officer | approved not ordered · orders to dispatch · dispatched not received · plant down this month | — | orders to process (Create order / Dispatch) |

"Due for service" = serviceable equipment overdue or due within 30 days, most overdue first; rows with an open
request show "Requested" instead of the "Request service" button.

## Technical approach

- `DashboardService.BuildAsync(user, facilityId)` builds `HomeDashboardViewModel`
  (`Notices`, `Stats`, `Breakdowns`, `Tables`), switching on `UserType`.
- Rendered on the server in one request (no per-widget AJAX, no loaders). Partials: `_StatTile`, `_Breakdown`, `_WorkTable`.
  Chart data is embedded in each canvas (`data-chart`) and drawn by the script in `Index.cshtml`.
- Scope enforced on the server: only admins may pass `facilityId`; everyone else is pinned to their own facility / own work.
- Counts and grouping run in SQL (`vw_AssetCurrentLocation`, `vw_AssetInTransit`), not in memory.
- Styles: the "Dashboard" section at the end of `wwwroot/css/style.css`.
- Pure rules unit-tested in `EMMS.Tests/DashboardRulesTests.cs`.

## Tasks

### Phase 1 — Foundation
- [x] 1. `HomeDashboardViewModel`
- [x] 2. `DashboardService` with `BuildAsync(User)` switching on `UserType`
- [x] 3. Register the service; `HomeController.Index` uses it
- [x] 4. Partials: `_StatTile`, `_Breakdown`, `_WorkTable` (replaced the first header/attention/notifications design)
- [x] 5. Dashboard CSS in `style.css`
- [x] 6. Service-due wording ("in N days" / "N days overdue"), most overdue first

### Phase 2 — Role dashboards
- [x] 7–13. First version for every role (see table above) — verified against the dev DB; needs a visual review in the browser

### Phase 3 — Data & performance
- [x] 14. Dashboard counts/grouping in SQL (new service; the old `DashboardViewModel` is no longer used by the page)
- [x] 16. Facility filter admin-only; scoping enforced server-side
- [ ] 17. Old JSON endpoints `GetDashboardCounts`, `GetAssetsDueServiceJson`, `GetDashboardChartData` are no longer called — remove in task 21

### Phase 4 — Polish & tests
- [x] 18. Tiles link to their list; "Overdue for service" jumps to the Due for service table (`#due-for-service`)
- [x] 18a. Chart bars open the asset list with its header filters preset from the query string
      (`?Status=…&Facility=…&ServicePoint=…`, site.js `emmsColumnFilters`). Out-of-service bars use the new
      `outofservice` status filter (non-functional or under maintenance). No link on "Unknown" (mostly assets with no
      location, which the list cannot filter) or "No service point". Charts show "+N more …" when cut at 8 rows.
- [x] 18b. Row buttons open the exact item: `?focus=<id>` scrolls to and highlights `[data-focus-id]` (switching tab and
      DataTables page), `&act=<name>` clicks that row's `[data-focus-act]` button (site.js `emmsFocusRow`).
      Used for: my requests → JobManagement (Close dialog when ready), pharmacist → Cylinder request,
      CMS → Create order form / order row.
- [ ] 19. Mobile layout check in the browser (tiles 2-up, tables scroll)
- [x] 20. Unit tests for scoping, tones and wording
- [ ] 21. Remove dead code after task 22: old endpoints above, `DashboardViewModel`, `IndexModel`, commented-out old `Index` in `HomeController`.
      Also fix or drop `AssetManagementRepository.GetAssetsDueService` (its `||` filter matches every asset with a service date).

### Phase 5 — Move current dashboard to Reports
- [x] 22. Five downloadable Excel reports on the Reports page, one per old dashboard chart, grouped exactly as the chart was:
      Functional Status · Procurement Status · Movement Reason · Non-Functional Assets by Facility · Assets by Service Point.
      - Each workbook: a **Summary** sheet (group, count, % of total, facility, generated date) + **one sheet per group**
        listing its assets (frozen header, auto-filter).
      - Same inclusion rules as the old charts (e.g. functional status / movement reason = assets with a location;
        non-functional by facility = non-functional or decommissioned).
      - Admins pick a facility (or all) in the download dialog — for every report, including All Assets and Due for Service.
        Everyone else is pinned to their own facility on the server.
      - No date range on these (they are a snapshot of current status); the two existing reports keep theirs.
      - Code: `Service/AssetSummaryReportService.cs`, `ReportsController.GenerateReport`, `Views/Reports/Index.cshtml`;
        tests in `EMMS.Tests/AssetSummaryReportTests.cs`.
