# ADR: Crib Visual for Stable 2.0

## Context

The crib asset exists on this branch tip `e4d172b` and includes an animated visual path for baby crib presentation. That work is not yet proven across placement, save/load behavior, or multi-baby shelter scenarios. Stable 2.0 needs childcare behavior to remain predictable, testable, and low-risk.

## Options Considered

### Option A: Ship the crib visual in stable 2.0

This would include the animated crib visual in the stable release. It gives the player-facing feature the most complete presentation, but it accepts unresolved risk in placement, persistence, and multi-baby behavior.

### Option B: Ship a reduced crib visual in stable 2.0

This would include a trimmed or partially disabled version of the visual. It lowers scope but still keeps the release dependent on unproven visual lifecycle behavior.

### Option C: Defer the crib visual until after stable 2.0

This ships childcare without the crib visual and leaves the crib-baby-mesh-animation work on its branch for a post-2.0 minor release.

## Decision

Choose Option C for stable 2.0.

## Rationale

Option C is the smallest stable design. Childcare can ship without coupling stable 2.0 to an animated object path whose placement, save/load, and multi-baby behavior have not cleared the section 10.3 gate list. Keeping the visual work isolated preserves the branch work without expanding the release risk.

## Consequences

Stable 2.0 ships childcare behavior without the crib visual. Players will not see the animated crib presentation in this release. The crib-baby-mesh-animation branch remains the home for that work until it is ready for a post-2.0 minor release.

## Revisit Criteria

Revisit this decision when the crib visual has demonstrated reliable placement, save/load persistence, and multi-baby behavior, and when the section 10.3 gate list is complete.
