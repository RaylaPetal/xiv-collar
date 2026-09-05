# Relay Protocol Fixtures

Language-neutral definition of the Cloudflare relay wire protocol, shared by the
Worker (TypeScript) and the plugin (C#) so neither implementation is the source
of truth for the other.

- `schemas/` - JSON Schema (2020-12) for every envelope type the relay accepts.
  `envelope.schema.json` is the top-level discriminated union; unknown `type`
  values and gameplay-command shapes are rejected because they match none of
  the `oneOf` branches and `additionalProperties` is `false` throughout.
- `fixtures/valid/` - one example per envelope type.
- `fixtures/invalid/` - examples that must fail validation: an unknown `type`,
  and a gameplay-command-shaped payload (restraint/gesture/outfit/etc.), which
  proves the protocol has no envelope capable of carrying gameplay commands.
- `constants.json` - canonical JSON, signing, hashing, and crypto parameters
  shared by both runtimes (see `docs/threat-model.md` for rationale).
- `vectors/` - cross-runtime canonical-JSON and crypto test vectors consumed by
  both the Worker test suite and the plugin test suite so the two
  implementations cannot silently disagree on encoding or algorithm choice.
- `docs/threat-model.md` - retained metadata, deletion windows, and the
  explicit command-transport exclusion.

Nothing in this directory talks to Cloudflare or contains secrets; it is pure
data plus documentation that both runtimes validate against in their own test
suites.
