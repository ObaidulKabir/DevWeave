# Machine-readable specification decisions

Agents must follow the same decisions. Every decision is structured data validated against a topic
definition; prose (`rationale`) is only a note.

## Model

- **Topic** (`SpecTopicCatalog`, repo-versioned): a named decision area with the legal `choices`, each
  with typed `parameters` (string / integer / boolean, optional closed value set), allowed constraint
  rules, and cross-topic compatibility `rules` (e.g. `db.access=ef-core` requires `backend.language=csharp`).
- **Decision**: a knowledge item of type `spec.decision` (schema 1.0). It reuses revisions, human
  review and audit. Shape:

```json
{
  "topic": "db.engine",
  "scope": { "level": "project" },
  "decision": { "choice": "postgresql", "params": { "majorVersion": 17 } },
  "constraints": [],
  "locked": false,
  "rationale": "Matches the hosting plan."
}
```

## Precedence (`SpecResolver`)

1. Scope: project overrides tenant overrides platform, unless the wider decision is `locked`.
2. Within one scope level the earliest-defined approved decision wins; a later one with different
   content is reported as a conflict.
3. Platform decisions are predefined in code (`SpecTopicCatalog.PredefinedDecisions`) and cannot be proposed.
4. Only a human-approved, current revision binds. Proposals (including from agents) do not.

## Surfaces

| Surface | Purpose |
| --- | --- |
| `GET /tenants/{t}/spec/topics` | Topic definitions |
| `GET /tenants/{t}/projects/{p}/spec/effective` | One binding decision per topic, undecided topics, incompatibilities |
| `GET /tenants/{t}/projects/{p}/spec/topics/{topic}/resolve` | Binding decision, origin, revision ids, conflicts |
| `POST /tenants/{t}/projects/{p}/spec/decisions` | Propose a decision (422 with `issues` if it breaks the topic definition) |
| MCP `resolve_spec`, `get_effective_spec`, `list_spec_topics`, `propose_spec_decision` | Same, for agents |

## Layered catalog: built-in packs plus extensions

Topics, options and profiles come from **packs**: versioned JSON data files embedded in
`DevWeave.Domain/Specifications/Packs/`. `core` is always on; `delivery` (branching, commits, PR size, CI,
release) is optional and switched on by an overlay.

Each tenant or project can extend the catalog with an **overlay**: a `spec.overlay` knowledge item that a
person approves. An overlay can enable packs, add namespaced topics, options and profiles, hide or
deprecate options, and set defaults. Rules enforced by `SpecCatalogBuilder`:

- New topics and profiles must start with the overlay's namespace (`acme.`); built-in roots are reserved.
- A topic whose platform decision is `locked` cannot be changed by any overlay.
- The platform's current choice for a topic cannot be hidden; deprecated choices cannot be chosen for new decisions.
- An overlay that fails validation is skipped whole and reported under `problems`; it never half-applies.
- Narrower layers win: project overlays apply after tenant overlays.

Extra surfaces: `GET .../spec/catalog`, `.../spec/topics`, `.../spec/topics/{topic}/options` (ranked, with
compatibility reasons), `.../spec/open-topics`, `POST .../spec/overlays`. MCP: `get_spec_catalog`,
`list_spec_options`, `next_open_topics`, `propose_spec_overlay`.

## Profiles, bundles and package upgrades

- A **profile** is a named set of decisions (for example `dotnet-postgres-react`). `POST .../spec/bundles`
  (MCP `propose_spec_profile`) sends a profile, optionally with edited lines, as one `spec.bundle` item. Every
  line is validated against its topic and locked topics are refused. **One approval** makes all lines binding.
- Several versions of a pack may ship. The newest applies unless an approved overlay **pins** an older one
  (`pinPacks`). `GET .../spec/upgrades` (MCP `get_spec_pack_upgrades`) returns the diff for each pack that is
  held back: added and removed topics, added, removed and deprecated options, changed defaults and predefined
  decisions, profile changes, and the current decisions the move would affect. Accepting an upgrade is an
  overlay that pins the new version, approved by a person.
- Studio: the **Catalog** page turns on packs, reviews and applies profiles line by line, shows upgrade diffs,
  and adds options or topics.

## Known limits

- "Defined at" ordering uses the item's creation time, not the approval time.
- A tenant-level decision is stored in the project that proposed it and binds every project in the tenant.
- Changing a related decision does not yet mark linked decisions for re-review.
- Only one version of each built-in pack ships today, so the upgrade view is empty until a newer pack version is released. The diff logic is covered by tests with synthetic packs.
- Custom options added from Studio carry no parameters; richer definitions need the API.
