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

## Known limits

- "Defined at" ordering uses the item's creation time, not the approval time.
- A tenant-level decision is stored in the project that proposed it and binds every project in the tenant.
- Changing a related decision does not yet mark linked decisions for re-review.
- Studio has no topic view yet.
