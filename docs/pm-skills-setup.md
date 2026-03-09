# PM Skills Setup Guide

## What PM Skills Are

PM Skills is a collection of product management skills for Claude Code created by [Dean Peters](https://github.com/deanpeters/Product-Manager-Skills). They give Claude Code structured capabilities for PRD creation, user story writing, roadmap planning, backlog management, and other PM workflows.

## Installation

Clone the repository to a local directory:

```bash
git clone https://github.com/deanpeters/Product-Manager-Skills.git ~/pm-skills
```

That's it. The skills are plain markdown files that Claude Code reads on demand.

## How to Invoke in Claude Code

Reference the skill paths in your Claude Code session. For example, if you cloned to `~/pm-skills`:

```
Use the PRD writer skill at ~/pm-skills/skills/prd-writer/
```

You can also add the skill paths to your Claude Code configuration so they are discoverable automatically.

## Recommended Skills

| # | Skill | Description |
|---|-------|-------------|
| 1 | **backlog-groomer** | Prioritize and refine product backlog items |
| 2 | **competitive-analyst** | Analyze competitor products and market positioning |
| 3 | **feature-prioritizer** | Score and rank features using frameworks like RICE/MoSCoW |
| 4 | **market-researcher** | Conduct market research and identify trends |
| 5 | **metrics-definer** | Define KPIs and success metrics for features |
| 6 | **prd-writer** | Write comprehensive product requirements documents |
| 7 | **release-planner** | Plan release schedules and milestones |
| 8 | **roadmap-builder** | Create product roadmaps with timeline and dependencies |
| 9 | **sprint-planner** | Plan sprint goals, capacity, and task breakdown |
| 10 | **stakeholder-communicator** | Draft stakeholder updates and status reports |
| 11 | **technical-spec-writer** | Write technical specifications from PRDs |
| 12 | **user-persona-creator** | Build data-driven user personas |
| 13 | **user-story-writer** | Write user stories with acceptance criteria |
| 14 | **ux-researcher** | Plan and analyze user research studies |

## License Note

PM Skills is licensed under **CC BY-NC-SA 4.0** (Creative Commons Attribution-NonCommercial-ShareAlike). This means it is a personal development tool for non-commercial use only. Review the full license in the PM Skills repository before using it in any commercial context.

## Important

PM Skills are **optional** and installed locally by each contributor. They are **not** bundled with Real Estate Star and are not required to contribute to the project.
