# Revora Repository Instructions

## Canonical UI system

Before creating, changing, reviewing, or testing any user interface anywhere in
this repository, read `src/IdentityService/DESIGN-bmw-m.md` in full.

- Treat its YAML front matter as the canonical source for colors, typography,
  spacing, radii, components, breakpoints, and motion.
- Extend the canonical design file before introducing a visual token that it
  does not define.
- Regenerate every derived token artifact after changing the source and run the
  relevant token freshness check.
- Do not add raw colors, spacing values, radii, shadows, gradients, or motion
  values in UI implementation files when a canonical token exists.
- Do not introduce BMW logos, BMW-owned fonts, automotive photography, or other
  imagery unless the repository contains an explicitly licensed asset for that
  use. Use the Revora identity and the documented Inter substitute.
- Preserve accessible focus states, keyboard operation, reduced-motion support,
  labels, contrast, and minimum touch targets in every UI change.

For the Identity UI, `src/IdentityService.Ui/app/tokens.css` is generated from
the canonical design file. Never edit it directly.
