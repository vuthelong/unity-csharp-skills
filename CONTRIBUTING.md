# Contributing

## Layout

```
plugins/<category>/
  .claude-plugin/plugin.json
  skills/<skill-name>/
    SKILL.md
    references/*.md   (optional, loaded on demand)
    scripts/*         (optional)
```

Skill names are unique across the whole repo, kebab-case, and match the folder name.

## SKILL.md rules

1. Frontmatter is YAML with these keys only:

   ```yaml
   ---
   name: skill-name
   description: "<What it does>. Use when <situations>. Triggers: \"<phrase>\", \"<phrase>\". Not for <X> (<skill>), <Y> (<skill>)."
   license: MIT   # or exactly: Unity Companion License
   metadata:
     category: <category>
     sources: "<origin repo>/<path>[, <origin repo>/<path>]"
     unity: "6000.0+"
   ---
   ```

2. Description: third person, at most ~600 chars, user-phrased triggers, ends with one "Not for ... (skill)." sentence naming real skills. Boundaries are two-way: the named skill points back. Keep API inventories in the body.
3. Body under ~500 lines. Move long tables, API listings and recipes to `references/` and link them from SKILL.md with a one-line "read when" trigger.
4. Imperative voice. Lead with the workflow, then rules and pitfalls. No marketing copy.
5. Target Unity 6 (6000.x). Flag APIs removed or obsoleted across 6000.x minors, e.g. `Object.GetInstanceID()` becomes CS0619 on 6000.5+ (use `GetEntityId()`), `FindObjectOfType` becomes `FindFirstObjectByType` / `FindAnyObjectByType`.
6. No dependency on a specific MCP server's tool names unless the skill is explicitly about that server.
7. Cross-link related skills by name (for example "see `ui-uitk`") instead of duplicating content. End every SKILL.md with `## References` (if it has `references/`) then `## Related skills` (`- skill-name: when`).

## Licensing

- Content derived from Unity-Technologies/skills keeps `license: Unity Companion License` and stays in Unity-dependent use.
- Content derived from Unity-Open-MCP, a5c-ai/babysitter, DietrichGebert/ponytail, scenario-labs/skills or patch-gg/pixelfx is MIT; keep the source in `metadata.sources`.
- A skill that merges several sources takes the most restrictive license among them.
- Original content is MIT.
