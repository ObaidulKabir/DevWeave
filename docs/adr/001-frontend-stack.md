# ADR-001: Owner Studio frontend stack

Status: Accepted  
Date: 2026-11-11  
Deciders: Engineering (implementation readiness program)

## Context

`DEVW-ARCH-001` recommends Next.js + TypeScript for Owner Studio, Client Portal and Admin. The repository already ships a React 19 + Vite + TypeScript + Tailwind Owner Studio at `web/studio`. Migrating before Phase B control-plane readiness would delay the coordination gate without improving protocol correctness.

## Decision

Retain **React 19 + Vite + TypeScript + Tailwind** for Owner Studio through Phase C of the control-plane readiness program.

Revisit Next.js only when a second Experience-plane app (Client Portal / Admin) needs SSR, shared routing conventions, or an approved BFF boundary.

## Consequences

- Studio continues to call versioned REST under `/tenants/...` via Vite proxy.
- No Next.js migration work blocks Phase A/B API readiness.
- A future ADR may supersede this if Experience-plane multi-app packaging requires Next.js.
