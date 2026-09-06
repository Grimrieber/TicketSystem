# TicketSystem

A multi-tenant support desk in **VB.NET** — companies, their clients, projects, tickets,
threaded messaging, and a knowledge base that links back to the tickets that produced it.

> **Architecture showcase, not a distribution.** The database schema and the reusable
> business logic are here to read. The ASP.NET page layer, deployment scripts and
> migration tooling are not, so this does not build as-is. That is deliberate.

## Domain

Thirteen tables. The shape worth noting is that it is genuinely multi-tenant: a
`Company` owns `Clients`, `Projects` and `Tickets`, and every query is scoped by
company rather than filtered in the UI.

| Area | Tables |
|---|---|
| Tenancy | `Companies`, `Clients`, `Collaborators` |
| Work | `Projects`, `Tickets`, `Status`, `Priority` |
| Communication | `Messages`, `ChatMessages` |
| Knowledge base | `KbEntries`, `KbTicketLinks`, `KbAudit` |
| Integration | `APIRequests` |

`KbTicketLinks` is the join that makes the knowledge base useful rather than decorative:
an article records which tickets it came from, so a recurring problem accumulates
evidence instead of being answered from scratch each time.

## The GUID migration

`sql/MigrateToGUIDs.sql`, `MigrateToGUIDs_Part2.sql` and
`FixMigration_AddForeignKeyGUIDs.sql` convert integer primary keys to GUIDs across a
live schema, in sequenced parts.

Splitting it was the point. Converting keys and remediating foreign keys in one
transaction against a populated database is a long lock and an all-or-nothing failure.
Run in stages, each part is separately verifiable, and a failure leaves a state you can
reason about rather than a half-migrated schema you have to restore from backup.

## What is here

**Included** — the full schema and migration scripts, plus the reusable logic:
authentication (BCrypt), data access, notification, audit, messaging and file handling
helpers, and the domain models.

**Not included** — the ASP.NET WebForms pages, SignalR hubs, deployment scripts and the
migration project from the previous system.

## Configuration

No credentials are in this repository. Connection strings load from
`connectionStrings.config`, which is gitignored; `connectionStrings.config.example`
shows the shape. SMTP settings come from `appSettings` the same way — see the top of
`src/Helpers/NotificationHelper.vb`.

## Stack

VB.NET · ASP.NET WebForms · SQL Server · BCrypt · SignalR

---

© 2026 Stephan Rieber. All rights reserved. Published for portfolio review.
No license is granted to use, copy, modify, or distribute this code.
