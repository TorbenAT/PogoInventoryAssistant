# Decision tag planner dry-run

The deterministic `DecisionTagPlanner` creates a run-local, current-carousel
tag plan only. It never calls `TagWorkflowService`, an Android executor, or a
swipe operation.

Mapping:

- `Keep` -> `AI-Indexed`
- `Review`, `Delete`, `Partial`, `Unknown`, or missing critical identity -> `AI-Review`

Task-K run2 dry-run source: `local-data/validation/reid-pilot-2x50/task-k/run2/recommendations.csv`.
The source contains 50 decisions: 1 KEEP and 49 REVIEW, with no DELETE rows.
The single KEEP has unknown species identity and is therefore safely downgraded
to `AI-Review`. Result: 0 `AI-Indexed`, 50 `AI-Review`, 0 DELETE tags, and 0
plans eligible for phone execution from this evidence-only report. No plan
requires cross-run identity or exact CP; every executable plan requires the
current run, ordinal, stable fingerprint, and Details/Appraisal state.
