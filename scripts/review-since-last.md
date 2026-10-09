# Review all since the last review

Review every change since the previous saved review, including code, tests, scripts, configuration, documentation, and OpenSpec artifacts. Do not implement fixes or provision resources.

Read applicable root and scoped AGENTS.override.md / AGENTS.md instructions. Read active OpenSpec proposal.md, specs/**/spec.md, design.md, and tasks.md alongside any completed baseline. Keep diagnostic/local evidence distinct from production, cloud, and physical Android acceptance.

Run `node scripts/review-checkpoint.cjs compare` from the repository root. The last checkpoint is `.local/review/last-review.json`; saved original files are under `.local/review/checkpoints/<id>/files/`. Compare contents, not timestamps. Include affected callers and deleted files. Prefer a user-supplied Git base or snapshot when available. Do not initialize Git or invent a prior baseline. Without a checkpoint, state that an exact incremental comparison is unavailable; ask for the base or explicitly label a current-workspace audit. Never claim an issue is newly introduced without evidence.

Flag all discrete actionable bugs the author would fix. Exclude intentional changes, pre-existing bugs in incremental reviews, speculative effects, and trivial style. Deduplicate by changed location and remedy. Use an imperative title beginning [P0], [P1], [P2], or [P3], one brief paragraph describing the triggering scenario, impact, and remedy, and the smallest useful changed line range. For findings materially supported by repository-specific instructions, verify and cite the smallest supporting line range in the applicable instruction file.

Run relevant documented checks when possible; use isolated ignored backend output if a running server holds existing binaries. Do not stop user services, install dependencies, access credentials, or claim unexecuted checks passed. Automated media events do not prove audible seeking or physical Android background playback. Report executed checks and limitations separately from defects.

Return the review harness's required output format. Otherwise report prioritized findings, an overall correctness verdict, validation, and limitations. No findings is a valid outcome; unavailable validation is not a pass.

Save the report, executed checks, limitations, and unresolved findings in ignored `.local/review/latest-review.md`. Only after completing the scoped review, inspect source for accidentally embedded secrets and run `node scripts/review-checkpoint.cjs capture` to save the next baseline. A checkpoint means reviewed, not accepted or defect-free. Do not advance the checkpoint after an incomplete review. Preserve unresolved findings for future follow-up without relabeling unchanged defects as newly introduced.
