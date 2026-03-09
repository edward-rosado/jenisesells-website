# Project Rules

## Multi-Tenant Rules

- **Never hardcode agent data.** All agent-specific values (name, phone, email, brokerage, colors, state, etc.) must be read from `config/agents/{agent-id}.json`.
- When writing or modifying skills, use `{agent.*}` variable syntax to reference config fields.
- Test skills work with the reference tenant (`jenise-buckalew`) but ensure they are fully generic.

## Skill Conventions

- Each skill lives in `skills/{skill-name}/SKILL.md`.
- Skills must include an `## Agent Config` section explaining which config fields they use.
- State-specific data goes in `skills/{skill-name}/templates/{STATE}/`, not in the main SKILL.md.
- Skills should support graceful degradation when optional config fields are absent.

## Commit Conventions

- Use conventional commits: `feat:`, `fix:`, `docs:`, `chore:`, `test:`, `refactor:`, `perf:`, `ci:`
- Keep commits atomic — one logical change per commit.
- Write descriptive commit messages that explain *why*, not just *what*.

## Pull Request Process

- Create feature branches from `main`.
- Link PRs to GitHub Issues when applicable.
- Include a test plan in PR descriptions.
- All skills must pass the "no hardcoded agent data" check before merging.

## Adding a New Agent

1. Copy `config/agents/jenise-buckalew.json` as a template.
2. Update all fields for the new agent.
3. Validate against `config/agent.schema.json`.
4. Test each skill with the new agent profile.

## Adding a New State (Contracts)

1. Create `skills/contracts/templates/{STATE}/README.md`.
2. Document the state's standard form, sections, and required fields.
3. Add the state to the "Supported States" table in `skills/contracts/SKILL.md`.
